using System;
using System.Collections.Generic;
using System.Linq;

public static class ContentValidator
{
    private static readonly HashSet<string> Roles = new HashSet<string> { "Warrior", "Archer", "Mage", "Rogue", "Priest", "Beast", "Unassigned" };
    private static readonly HashSet<string> Factions = new HashSet<string> { "HumanEmpire", "HumanOutlaws", "Animals", "GreenSkins", "Undead", "MountainGiants", "CultistsOfHell", "Unassigned" };
    private static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
    private static bool NonNegative(float x) => Finite(x) && x >= 0;

    public static List<string> ValidateUnits(UnitDefinitionRecord[] records)
    {
        var errors = new List<string>();
        if (records == null || records.Length == 0) { errors.Add("Configs/Units: units must not be empty."); return errors; }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var legacyKeys = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < records.Length; i++)
        {
            var r = records[i]; var at = $"Configs/Units[{i}] ({r?.id})";
            if (r == null) { errors.Add(at + ": null record."); continue; }
            if (string.IsNullOrWhiteSpace(r.id) || !ids.Add(r.id)) errors.Add(at + ": empty or duplicate ID.");
            if (string.IsNullOrWhiteSpace(r.unit) || !legacyKeys.Add(r.unit)) errors.Add(at + ": empty or duplicate legacy key.");
            if (string.IsNullOrWhiteSpace(r.displayName)) errors.Add(at + ": displayName is required.");
            if (!Roles.Contains(r.role ?? "")) errors.Add(at + ": unknown role.");
            if (!Factions.Contains(r.faction ?? "")) errors.Add(at + ": unknown faction.");
            if (r.level < 1 || r.health <= 0) errors.Add(at + ": level and health must be positive.");
            if (!NonNegative(r.armor) || !NonNegative(r.moveSpeed) || !NonNegative(r.attackRange)) errors.Add(at + ": invalid armor, moveSpeed or attackRange.");
            if (!NonNegative(r.attacksPerSecond) || (r.attacksPerSecond == 0 && (!Finite(r.attackSpeed) || r.attackSpeed <= 0)))
                errors.Add(at + ": attack rate (or legacy interval) must be finite and positive.");
            else { try { CombatResolver.Interval(r.attacksPerSecond > 0 ? r.attacksPerSecond : CombatResolver.LegacyRate(r.attackSpeed)); }
                catch (ArgumentException) { errors.Add(at + ": attack rate cannot be represented safely."); } }
            if (!NonNegative(r.evasion) || r.evasion > 100 || !NonNegative(r.critChance) || r.critChance > 100 ||
                !NonNegative(r.lifeSteal) || r.lifeSteal > 100 || !NonNegative(r.armorPenetration)) errors.Add(at + ": invalid combat percentage or penetration.");
            if (!NonNegative(r.minimumAttackDamage) || !NonNegative(r.maximumAttackDamage) || r.minimumAttackDamage > r.maximumAttackDamage)
                errors.Add(at + ": invalid damage range.");
            if (string.IsNullOrWhiteSpace(r.projectile)) errors.Add(at + ": projectile is required for the current battle implementation.");
        }
        return errors;
    }

    public static List<string> ValidateSquad(Unit[] squad, UnitCatalog catalog, string at)
    {
        var errors = new List<string>();
        if (squad == null) { errors.Add(at + ": missing squad array."); return errors; }
        if (squad.Length > 6) errors.Add(at + ": squad exceeds six slots.");
        var placements = new HashSet<int>(); var instanceIds = new HashSet<string>();
        for (int i = 0; i < squad.Length; i++)
        {
            var u = squad[i]; var item = $"{at}[{i}]";
            if (u == null) { errors.Add(item + ": null unit."); continue; }
            if (u.placement < 1 || u.placement > 6 || !placements.Add(u.placement)) errors.Add(item + ": invalid or duplicate placement.");
            if (!string.IsNullOrWhiteSpace(u.instanceId) && !instanceIds.Add(u.instanceId)) errors.Add(item + ": duplicate instance ID.");
            try { catalog.Resolve(u); } catch (ArgumentException e) { errors.Add(item + ": " + e.Message); }
        }
        return errors;
    }

    public static List<string> Validate(UnitCatalog units, Ability[] abilities, Map[] maps, Func<string, bool> spriteExists)
    {
        if (units == null) throw new ArgumentNullException(nameof(units));
        var errors = new List<string>();
        var abilityIds = new HashSet<string>(StringComparer.Ordinal);
        if (abilities == null) errors.Add("Configs/Abilities: missing abilities array.");
        foreach (var a in abilities ?? Array.Empty<Ability>())
        {
            if (a == null) { errors.Add("Configs/Abilities: null ability."); continue; }
            if (string.IsNullOrWhiteSpace(a.abilityName) || !abilityIds.Add(a.abilityName)) errors.Add("Configs/Abilities: empty or duplicate ability ID.");
            CheckSprite(a.buttonImage, "Configs/Abilities/" + a.abilityName, spriteExists, errors);
            CheckSprite(a.effectImage, "Configs/Abilities/" + a.abilityName, spriteExists, errors);
        }
        foreach (var u in units.Definitions)
        {
            if (!string.IsNullOrEmpty(u.Ability) && !abilityIds.Contains(u.Ability)) errors.Add($"Configs/Units/{u.Id}: unknown ability '{u.Ability}'.");
            CheckSprite(u.LegacyKey, "Configs/Units/" + u.Id, spriteExists, errors);
            CheckSprite(u.Projectile, "Configs/Units/" + u.Id, spriteExists, errors);
        }
        if (maps == null || maps.Length == 0) { errors.Add("Configs/Map: maps must not be empty."); return errors; }
        var mapIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var m in maps)
        {
            if (m == null) { errors.Add("Configs/Map: null map."); continue; }
            if (string.IsNullOrWhiteSpace(m.name) || !mapIds.Add(m.name)) errors.Add("Configs/Map: empty or duplicate map ID.");
        }
        foreach (var m in maps.Where(m => m != null))
        {
            var at = "Configs/Map/" + m.name;
            foreach (var next in m.nextMaps ?? Array.Empty<string>())
                if (next == null || !mapIds.Contains(next)) errors.Add(at + ": unknown next map '" + next + "'.");
            var nodeIds = new HashSet<int>(); var ends = new HashSet<int>();
            if (m.nodes == null || m.nodes.Length == 0) errors.Add(at + ": missing nodes.");
            foreach (var n in m.nodes ?? Array.Empty<Node>())
            {
                if (n == null) { errors.Add(at + ": null node."); continue; }
                if (n.id < 1 || !nodeIds.Add(n.id)) errors.Add(at + ": invalid or duplicate node ID " + n.id);
                if (!Finite(n.x) || !Finite(n.y)) errors.Add(at + ": invalid node coordinates " + n.id);
                if (n.type != "barracks" && n.type != "treasure" && n.type != "end") errors.Add(at + ": unknown node type '" + n.type + "'.");
                if (n.type == "end") ends.Add(n.id);
                CheckSprite(n.type, at + "/node/" + n.id, spriteExists, errors);
            }
            if (!nodeIds.Contains(1)) errors.Add(at + ": start node 1 is missing.");
            if (ends.Count == 0) errors.Add(at + ": no end node.");
            // Legacy terminal marker: InnerFire is immediately discarded when the campaign ends.
            // It is NOT an implemented buff and must not be used before a following map.
            bool terminalMarker = m.finalReward == "InnerFire" && (m.nextMaps == null || m.nextMaps.Length == 0);
            if (!terminalMarker && m.finalReward != "BullHeart" && m.finalReward != "OrbOfSpeed" && m.finalReward != "MagicPotion") errors.Add(at + ": unknown final reward.");
            var edges = new HashSet<string>(); var graph = nodeIds.ToDictionary(id => id, id => new List<int>());
            if (m.edges == null) errors.Add(at + ": missing edges array.");
            foreach (var e in m.edges ?? Array.Empty<Edge>())
            {
                if (e == null) { errors.Add(at + ": null edge."); continue; }
                var edgeAt = $"{at}/edge/{e.from}-{e.to}";
                if (e.from == e.to || !edges.Add(Math.Min(e.from, e.to) + "-" + Math.Max(e.from, e.to))) errors.Add(edgeAt + ": self or duplicate edge.");
                if (!nodeIds.Contains(e.from) || !nodeIds.Contains(e.to)) errors.Add(edgeAt + ": unknown endpoint.");
                else { graph[e.from].Add(e.to); graph[e.to].Add(e.from); }
                errors.AddRange(ValidateSquad(e.squad, units, edgeAt));
            }
            if (graph.ContainsKey(1))
            {
                var visited = new HashSet<int>(); var pending = new Stack<int>(); pending.Push(1);
                while (pending.Count > 0) { int id = pending.Pop(); if (!visited.Add(id)) continue; foreach (int next in graph[id]) pending.Push(next); }
                foreach (int id in nodeIds) if (!visited.Contains(id)) errors.Add(at + ": unreachable node " + id);
            }
        }
        return errors;
    }

    private static void CheckSprite(string name, string at, Func<string, bool> exists, List<string> errors)
    {
        if (exists != null && (string.IsNullOrWhiteSpace(name) || !exists("Images/" + name))) errors.Add(at + ": missing sprite '" + name + "'.");
    }
}
