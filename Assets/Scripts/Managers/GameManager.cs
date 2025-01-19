using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    
    // Game state
    public UnitArrayWrapper PlayerSquad { get; private set; }
    public List<int> VisitedNodes { get; private set; } = new List<int>();
    public List<string> VisitedEdges { get; private set; } = new List<string>();
    public string SelectedCharacter { get; private set; }
    public int CurrentNodeId { get; private set; }
    public string CurrentMap { get; private set; }
    
    void Awake()
    {
        Debug.Log("GameManager Awake");
        if (Instance == null)
        {
            Debug.Log("GameManager Awake");
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadGameState();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    void LoadGameState()
    {
        // Load from PlayerPrefs
        SelectedCharacter = PlayerPrefs.GetString("SelectedCharacter", "Knight");
        CurrentNodeId = PlayerPrefs.GetInt("CharacterNode", 1);
        
        string playerSquadJson = PlayerPrefs.GetString("PlayerSquad", "");
        if (!string.IsNullOrEmpty(playerSquadJson))
        {
            PlayerSquad = JsonUtility.FromJson<UnitArrayWrapper>(playerSquadJson);
        }
        
        // Load visited nodes and edges
        string visitedNodesString = PlayerPrefs.GetString("VisitedNodes", "");
        if (!string.IsNullOrEmpty(visitedNodesString))
        {
            VisitedNodes = new List<int>(visitedNodesString.Split(',').Select(int.Parse));
        }
        
        string visitedEdgesString = PlayerPrefs.GetString("VisitedEdges", "");
        if (!string.IsNullOrEmpty(visitedEdgesString))
        {
            VisitedEdges = new List<string>(visitedEdgesString.Split(','));
        }
        
        // Load current map, default to "FieldMap"
        CurrentMap = PlayerPrefs.GetString("currentMap", "FieldMap");
    }
    
    public void SaveGameState()
    {
        PlayerPrefs.SetString("SelectedCharacter", SelectedCharacter);
        PlayerPrefs.SetInt("CharacterNode", CurrentNodeId);
        
        if (PlayerSquad != null)
        {
            PlayerPrefs.SetString("PlayerSquad", JsonUtility.ToJson(PlayerSquad));
        }
        
        PlayerPrefs.SetString("VisitedNodes", string.Join(",", VisitedNodes));
        PlayerPrefs.SetString("VisitedEdges", string.Join(",", VisitedEdges));
        PlayerPrefs.SetString("currentMap", CurrentMap);
        PlayerPrefs.Save();
    }
    
    public void SetPlayerSquad(UnitArrayWrapper squad)
    {
        PlayerSquad = squad;
        SaveGameState();
    }
    
    public void SetCurrentNode(int nodeId)
    {
        CurrentNodeId = nodeId;
        if (!VisitedNodes.Contains(nodeId))
        {
            VisitedNodes.Add(nodeId);
        }
        SaveGameState();
    }
    
    public void AddVisitedEdge(string edgeKey)
    {
        if (!VisitedEdges.Contains(edgeKey))
        {
            VisitedEdges.Add(edgeKey);
            SaveGameState();
        }
    }
    
    public void SetSelectedCharacter(string character)
    {
        SelectedCharacter = character;
        SaveGameState();
    }
    
    public void SetCurrentMap(string mapName)
    {
        CurrentMap = mapName;
        SaveGameState();
    }
    
    public void ResetGame()
    {
        PlayerPrefs.DeleteAll();
        VisitedNodes.Clear();
        VisitedEdges.Clear();
        PlayerSquad = null;
        SelectedCharacter = "";
        CurrentNodeId = 1;
        CurrentMap = "FieldMap";
    }
} 