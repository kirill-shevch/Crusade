using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;
using System.Linq;

public class RewardsManager : MonoBehaviour
{
    public TextMeshProUGUI nodeText;
    public TextMeshProUGUI rewardsText;
    public Button okayButton;

    private readonly string[] mergePairs = { "Militia-Soldier", "Soldier-Defender", "Scolar-Wizard", "Wizard-Sorcerer", "Hunter-Bower" };
    private Dictionary<string, string> mergeMap;

    void Start()
    {
        InitializeMergeMap();
        HandleRewards();
        okayButton.onClick.AddListener(OnOkayButtonClicked);
    }

    void InitializeMergeMap()
    {
        mergeMap = new Dictionary<string, string>();
        foreach (string pair in mergePairs)
        {
            string[] units = pair.Split('-');
            mergeMap[units[0]] = units[1];
        }
    }

    void HandleRewards()
    {
        string nodeType = PlayerPrefs.GetString("CurrentNodeType");
        
        // Load current map config to get node text
        TextAsset json = Resources.Load<TextAsset>("Configs/Map");
        MapConfig allMaps = JsonUtility.FromJson<MapConfig>(json.text);
        Map currentMap = System.Array.Find(allMaps.maps, map => map.name == GameManager.Instance.CurrentMap);
        Node currentNode = System.Array.Find(currentMap.nodes, node => node.id == GameManager.Instance.CurrentNodeId);
        
        // Set node description text
        nodeText.text = currentNode.rewardsText;
        string rewardsInfo = "";

        switch (nodeType)
        {
            case "barracks":
                rewardsInfo = HandleBarracksRewards();
                break;
            case "treasure":
                rewardsInfo = HandleTreasureRewards();
                break;
            case "end":
                rewardsInfo = HandleEndNodeRewards(currentMap.finalReward);
                break;
        }

        rewardsText.text = rewardsInfo;
    }

    string HandleBarracksRewards()
    {
        string[] possibleUnits = { "Militia", "Hunter", "Scolar" };
        string newUnit = possibleUnits[Random.Range(0, possibleUnits.Length)];
        AddOrMergeUnit(newUnit);
        return $"You have received a new unit: {newUnit}";
    }

    void AddOrMergeUnit(string newUnit)
    {
        // Get current squad
        UnitArrayWrapper squadWrapper = GameManager.Instance.PlayerSquad;
        List<Unit> squad = squadWrapper?.units != null ? 
            new List<Unit>(squadWrapper.units) : new List<Unit>();

        // Find first available placement (1-6)
        var occupiedPlacements = squad.Select(u => u.placement).ToList();
        int newPlacement = Enumerable.Range(1, 6)
            .FirstOrDefault(i => !occupiedPlacements.Contains(i));

        // Add unit to first available spot
        if (newPlacement != 0)
        {
            squad.Add(new Unit { unit = newUnit, placement = newPlacement });
        }

        // Perform cascade merging
        bool merged;
        do
        {
            merged = false;
            foreach (var entry in mergeMap)
            {
                var unitsToMerge = squad
                    .Where(u => u.unit == entry.Key)
                    .OrderBy(u => u.placement)
                    .ToList();

                if (unitsToMerge.Count >= 2)
                {
                    // Remove the two units being merged
                    squad.Remove(unitsToMerge[0]);
                    squad.Remove(unitsToMerge[1]);

                    // Add the merged unit in the lowest placement of the two merged units
                    int mergedPlacement = unitsToMerge[0].placement;
                    squad.Add(new Unit { unit = entry.Value, placement = mergedPlacement });

                    merged = true;
                    break;
                }
            }
        } while (merged);

        // Save updated squad
        GameManager.Instance.SetPlayerSquad(new UnitArrayWrapper { units = squad.ToArray() });
        GameManager.Instance.SaveGameState();
    }

    string HandleTreasureRewards()
    {
        string[] possibleBuffs = { "Armor", "Damage" };
        string newBuff = possibleBuffs[Random.Range(0, possibleBuffs.Length)];
        string buffs = PlayerPrefs.GetString("PlayerBuffs", "");
        buffs += newBuff + ",";
        PlayerPrefs.SetString("PlayerBuffs", buffs);
        PlayerPrefs.Save();
        return $"You have received a new buff: {newBuff}";
    }

    string HandleEndNodeRewards(string finalReward)
    {
        // Save the permanent buff
        string permanentBuffs = PlayerPrefs.GetString("PermanentBuffs", "");
        permanentBuffs += finalReward + ",";
        PlayerPrefs.SetString("PermanentBuffs", permanentBuffs);
        PlayerPrefs.Save();
        
        return "Congratulations! You have completed the map!";
    }

    void OnOkayButtonClicked()
    {
        string nodeType = PlayerPrefs.GetString("CurrentNodeType");
        
        if (nodeType == "end")
        {
            // Load map config to check if there are next maps
            TextAsset json = Resources.Load<TextAsset>("Configs/Map");
            MapConfig allMaps = JsonUtility.FromJson<MapConfig>(json.text);
            Map currentMap = System.Array.Find(allMaps.maps, map => map.name == GameManager.Instance.CurrentMap);
            
            if (currentMap.nextMaps != null && currentMap.nextMaps.Length > 0)
            {
                // If there are next maps available, go to the choose map scene
                SceneManager.LoadScene("ChooseNextMapScene");
            }
            else
            {
                GameManager.Instance.ResetGame();
                SceneManager.LoadScene("MainMenuScene");
            }
        }
        else
        {
            // For non-end nodes, return to map
            SceneController.Instance.LoadMap();
        }
    }
}
