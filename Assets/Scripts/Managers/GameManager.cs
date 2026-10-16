using UnityEngine;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public static GameManager Ensure()
    {
        if (Instance == null) new GameObject("GameManager").AddComponent<GameManager>();
        return Instance;
    }
    private RunSave state = new RunSave();
    private RunSaveStore store;
    private bool loadBlocked;
    public string LastError { get; private set; }
    public RunSave State => state.Copy();
    public UnitArrayWrapper PlayerSquad => new UnitArrayWrapper { units = State.squad };
    public List<int> VisitedNodes => new List<int>(state.nodes);
    public List<string> VisitedEdges => new List<string>(state.edges);
    public string SelectedCharacter => state.character;
    public int CurrentNodeId => state.node;
    public string CurrentMap => state.map;
    public string Buffs => string.Join(",", state.buffs);
    public string PermanentBuffs => string.Join(",", state.permanent);
    public bool HasRun => !loadBlocked && state.active;
    public UnitArrayWrapper DetailsSquad;
    public bool DetailsIsPlayer;
    public MapConfig Maps => JsonUtility.FromJson<MapConfig>(Resources.Load<TextAsset>("Configs/Map").text);
    public Map Map => Maps.maps.Single(m => m.name == state.map);
    public UnitArrayWrapper EnemySquad => new UnitArrayWrapper { units = Map.edges.Single(e => RunSave.EdgeKey(e.from,e.to) == RunSave.EdgeKey(state.battleFrom,state.battleTo)).squad };
    public string ResumeScene => !HasRun ? "MainMenuScene" : state.reward != null ? "RewardsScene" : state.mapCompleted ? "ChooseNextMapScene" : "MapScene";
    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this; DontDestroyOnLoad(gameObject);
        store = new RunSaveStore(Path.Combine(Application.persistentDataPath, "run.json"));
        store.Validate = s => RunRules.Validate(s, GameContent.Units, Maps);
        try
        {
            var loaded = store.Load();
            if (loaded == null)
            {
                loaded = LegacyRunMigration.Import(PlayerPrefs.GetString, PlayerPrefs.GetInt, GameContent.Units, Maps);
                Directory.CreateDirectory(Path.GetDirectoryName(store.Path));
                if (loaded.active) File.WriteAllText(store.Path + ".legacy.json", JsonUtility.ToJson(loaded,true));
                store.Save(loaded);
            }
            state = loaded; LastError = store.Warning;
        }
        catch (Exception ex) { loadBlocked = true; LastError = "Save unavailable: " + ex.Message; }
    }
    void OnDestroy() { if (Instance == this) Instance = null; }
    void OnGUI()
    {
        if (!string.IsNullOrEmpty(LastError)) GUI.Box(new Rect(10,10,Math.Min(Screen.width-20,900),70), LastError + "\nSave files are preserved. Retry after fixing storage, or restart after recovery.");
    }
    public bool Change(Action<RunSave> edit)
    {
        if (loadBlocked) return false;
        try
        {
            var candidate = state.Copy(); edit(candidate); store.Save(candidate); state = candidate; LastError = null; return true;
        }
        catch (Exception ex) { LastError = "Not saved: " + ex.Message; return false; }
    }
    public bool BeginRun(string character) => Change(s =>
    {
        var fresh = new RunSave { active = true, runId = Guid.NewGuid().ToString("N"), character = character,
            squad = new[] { GameContent.Units.CreateLegacyInstance(character,1) }, nodes = new List<int>{1} };
        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(fresh),s);
        s.reward = null;
    });
    public bool SaveGameState() => Change(s => { });
    public bool SetPlayerSquad(UnitArrayWrapper squad) => Change(s => s.squad = GameContent.Units.CreateFreshSquad(squad.units));
    public bool SetCurrentNode(int id) => Change(s =>
    {
        if (!Map.edges.Any(e => RunSave.EdgeKey(e.from,e.to) == RunSave.EdgeKey(s.node,id))) throw new InvalidOperationException("Nodes are not connected");
        s.battleFrom = s.battleTo = 0;
        RunRules.Arrive(s,Map,id,n => UnityEngine.Random.Range(0,n));
    });
    public bool StartBattle(int target) => Change(s => { s.battleFrom = s.node; s.battleTo = target; });
    public bool WinBattle() => Change(s => RunRules.Win(s,Map,n => UnityEngine.Random.Range(0,n)));
    public bool ClaimReward() => Change(s => RunRules.Claim(s,GameContent.Units));
    public bool DismissReward() => Change(s =>
    {
        if (s.reward == null) return;
        if (!s.reward.claimed) s.reward.declined = true;
        s.mapCompleted = s.reward.type == "end"; s.reward = null;
        if (s.mapCompleted && Map.nextMaps.Length == 0) s.active = false;
    });
    public bool NextMap(string name) => Change(s => RunRules.NextMap(s,name,Maps,GameContent.Units));
    public bool ResetGame() => Change(s => { JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(new RunSave()),s); s.reward = null; });
}
