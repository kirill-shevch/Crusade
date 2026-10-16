using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

[Serializable] public class RunSave
{
    public int version = 1;
    public bool active;
    public string runId = "", character = "", map = "FieldMap";
    public int node = 1, heroLevel = 1, heroExperience;
    public Unit[] squad = new Unit[0];
    public List<int> nodes = new List<int>();
    public List<string> edges = new List<string>(), buffs = new List<string>(), permanent = new List<string>();
    public int battleFrom, battleTo;
    public List<string> completedBattles = new List<string>();
    public RewardSave reward;
    public bool mapCompleted;
    public static RunSave FromJson(string json)
    {
        var s = JsonUtility.FromJson<RunSave>(json);
        // Unity serializes a null inline class as an empty object.
        if (s != null && s.reward != null && string.IsNullOrEmpty(s.reward.id) &&
            string.IsNullOrEmpty(s.reward.type) && string.IsNullOrEmpty(s.reward.value)) s.reward = null;
        return s;
    }
    public RunSave Copy() => FromJson(JsonUtility.ToJson(this));
    public static string EdgeKey(int a, int b) => Math.Min(a,b) + "-" + Math.Max(a,b);
}
[Serializable] public class RewardSave
{
    public string id, type, value;
    public bool claimed, declined;
}

// The envelope detects partial writes. Replacement keeps the previous valid generation.
public sealed class RunSaveStore
{
    [Serializable] private class Envelope { public int format = 1; public string payload, checksum; }
    public readonly string Path;
    public string Warning { get; private set; }
    public RunSaveStore(string path) { Path = path; }
    private static string Hash(string text)
    {
        using (var sha = SHA256.Create())
            return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(text)));
    }
    private RunSave Read(string path)
    {
        var envelope = JsonUtility.FromJson<Envelope>(File.ReadAllText(path));
        if (envelope == null) throw new InvalidDataException("Empty save");
        if (envelope.format != 1) throw new NotSupportedException("Unsupported save format");
        if (string.IsNullOrEmpty(envelope.payload) || Hash(envelope.payload) != envelope.checksum)
            throw new InvalidDataException("Save checksum mismatch");
        var state = RunSave.FromJson(envelope.payload);
        if (state == null || state.version != 1) throw new NotSupportedException("Unsupported run version");
        Validate(state);
        return state;
    }
    public Action<RunSave> Validate = state => { };
    public RunSave Load()
    {
        if (!File.Exists(Path) && !File.Exists(Path + ".bak")) return null;
        try { return Read(Path); }
        catch (NotSupportedException) { throw; }
        catch (Exception original)
        {
            try
            {
                var recovered = Read(Path + ".bak");
                Warning = "Recovered previous save: " + original.Message;
                return recovered;
            }
            catch (Exception backup) { throw new IOException("Cannot read save or backup. Files preserved.", new AggregateException(original, backup)); }
        }
    }
    public void Save(RunSave state)
    {
        Validate(state);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
        string payload = JsonUtility.ToJson(state);
        byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Envelope { payload = payload, checksum = Hash(payload) }));
        using (var file = new FileStream(Path + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None))
        { file.Write(bytes, 0, bytes.Length); file.Flush(true); }
        if (File.Exists(Path))
        {
            bool valid = false;
            try { Read(Path); valid = true; } catch (NotSupportedException) { throw; } catch { }
            // Never replace a good backup with the damaged primary during recovery.
            if (!valid) File.Copy(Path, Path + ".corrupt-" + Guid.NewGuid().ToString("N"));
            File.Replace(Path + ".tmp", Path, valid ? Path + ".bak" : null);
        }
        else File.Move(Path + ".tmp", Path);
    }
}

public static class RunRules
{
    public static void Validate(RunSave s, UnitCatalog catalog, MapConfig maps)
    {
        if (s.version != 1) throw new NotSupportedException("Unsupported run version");
        if (!s.active) return;
        var map = maps.maps.Single(m => m.name == s.map);
        if (!map.nodes.Any(n => n.id == s.node) || s.nodes == null || s.edges == null || s.buffs == null || s.permanent == null || s.completedBattles == null)
            throw new InvalidDataException("Invalid map progress");
        catalog.CreateFreshSquad(s.squad);
        if (s.squad.Count(u => u.unit == s.character) != 1 || string.IsNullOrEmpty(s.runId))
            throw new InvalidDataException("Missing hero or run ID");
        if (s.heroLevel < 1 || s.heroExperience < 0 || s.nodes.Any(id => !map.nodes.Any(n => n.id == id)) ||
            s.edges.Any(key => !map.edges.Any(e => RunSave.EdgeKey(e.from,e.to) == key)))
            throw new InvalidDataException("Invalid progress");
        if (s.reward != null && (s.reward.id != s.runId + "/" + s.map + "/" + s.node ||
            s.reward.type != map.nodes.Single(n => n.id == s.node).type)) throw new InvalidDataException("Invalid reward receipt");
        if (s.reward != null && s.reward.type == "barracks") catalog.Resolve(new Unit { unit = s.reward.value });
        if (s.buffs.Any(b => b != "Armor" && b != "Damage") ||
            s.permanent.Any(b => b != "BullHeart" && b != "OrbOfSpeed" && b != "MagicPotion" && b != "InnerFire"))
            throw new InvalidDataException("Unknown saved bonus");
        if (s.reward != null && ((s.reward.type == "treasure" && s.reward.value != "Armor" && s.reward.value != "Damage") ||
            (s.reward.type == "end" && s.reward.value != map.finalReward))) throw new InvalidDataException("Unknown reward");
        if (s.battleTo != 0 && !map.edges.Any(e => RunSave.EdgeKey(e.from,e.to) == RunSave.EdgeKey(s.battleFrom,s.battleTo)))
            throw new InvalidDataException("Invalid pending encounter");
    }
    public static void Arrive(RunSave s, Map map, int target, Func<int,int> random)
    {
        var node = map.nodes.Single(n => n.id == target);
        if (!s.nodes.Contains(target))
        {
            s.nodes.Add(target);
            string value = node.type == "barracks" ? new[] { "Militia", "Hunter", "Scolar" }[random(3)] :
                node.type == "treasure" ? new[] { "Armor", "Damage" }[random(2)] : map.finalReward;
            s.reward = new RewardSave { id = s.runId + "/" + s.map + "/" + target, type = node.type, value = value };
        }
        s.node = target;
    }
    public static void Win(RunSave s, Map map, Func<int,int> random)
    {
        if (s.battleTo == 0) return;
        string key = RunSave.EdgeKey(s.battleFrom,s.battleTo);
        string receipt = s.runId + "/" + s.map + "/" + key;
        if (!s.completedBattles.Contains(receipt)) s.completedBattles.Add(receipt);
        if (!s.edges.Contains(key)) s.edges.Add(key);
        Arrive(s, map, s.battleTo, random);
        s.battleFrom = s.battleTo = 0;
    }
    public static bool Claim(RunSave s, UnitCatalog catalog)
    {
        var r = s.reward;
        if (r == null || r.claimed || r.declined) return true;
        if (r.type == "barracks")
        {
            var trial = s.Copy().squad.ToList();
            int free = Enumerable.Range(1,6).FirstOrDefault(p => trial.All(u => u.placement != p));
            var incoming = catalog.CreateLegacyInstance(r.value, free == 0 ? 1 : free);
            incoming.placement = free == 0 ? 7 : free; // Virtual slot exists only inside this transaction.
            trial.Add(incoming);
            var pairs = new[] { "Militia:Soldier", "Soldier:Defender", "Scolar:Wizard", "Wizard:Sorcerer", "Hunter:Bower" };
            bool merged;
            do
            {
                merged = false;
                foreach (var pair in pairs)
                {
                    var names = pair.Split(':');
                    var units = trial.Where(u => u.unit == names[0] && u.unit != s.character).OrderBy(u => u.placement).Take(2).ToArray();
                    if (units.Length != 2) continue;
                    trial.Remove(units[0]); trial.Remove(units[1]);
                    trial.Add(catalog.CreateLegacyInstance(names[1], units[0].placement));
                    merged = true; break;
                }
            } while (merged);
            if (trial.Count > 6) return false;
            foreach (var unit in trial.Where(u => u.placement == 7))
                unit.placement = Enumerable.Range(1,6).First(p => trial.All(u => u.placement != p));
            s.squad = trial.ToArray();
        }
        else if (r.type == "treasure") s.buffs.Add(r.value);
        else if (r.type == "end") s.permanent.Add(r.value);
        r.claimed = true;
        return true;
    }
    public static void NextMap(RunSave s, string next, MapConfig maps, UnitCatalog catalog)
    {
        var current = maps.maps.Single(m => m.name == s.map);
        if (!s.mapCompleted || !current.nextMaps.Contains(next)) throw new InvalidOperationException("Map is not unlocked");
        maps.maps.Single(m => m.name == next);
        var hero = s.squad.Single(u => u.unit == s.character);
        s.squad = catalog.CreateFreshSquad(new[] { hero });
        s.squad[0].placement = 1;
        s.map = next; s.node = 1; s.nodes = new List<int> { 1 }; s.edges.Clear(); s.buffs.Clear();
        s.reward = null; s.mapCompleted = false; s.battleFrom = s.battleTo = 0;
    }
}
