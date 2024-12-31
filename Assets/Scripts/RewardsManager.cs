using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class RewardsManager : MonoBehaviour
{
    public TextMeshProUGUI rewardsText;
    public Button okayButton;

    void Start()
    {
        string nodeType = PlayerPrefs.GetString("CurrentNodeType");
        string rewardsInfo = "";

        if (nodeType == "barracks")
        {
            rewardsInfo = HandleBarracksRewards();
        }
        else if (nodeType == "treasure")
        {
            rewardsInfo = HandleTreasureRewards();
        }
        else if (nodeType == "end")
        {
            rewardsInfo = "Congratulations! You have completed the game!";
        }

        rewardsText.text = rewardsInfo;

        okayButton.onClick.AddListener(OnOkayButtonClicked);
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
        // Retrieve the player's squad from PlayerPrefs
        string playerSquadJson = PlayerPrefs.GetString("PlayerSquad", "{}");
        UnitArrayWrapper playerSquadWrapper = JsonUtility.FromJson<UnitArrayWrapper>(playerSquadJson);
        List<Unit> squad = new List<Unit>(playerSquadWrapper.units);

        // Ensure we don't exceed the max size of the squad
        if (squad.Count >= 6)
        {
            Debug.Log("Squad is already at maximum capacity.");
            return;
        }

        // Add the new unit or merge it with existing units
        bool added = false;
        for (int i = 0; i < squad.Count; i++)
        {
            if (squad[i] == null)
            {
                squad[i] = new Unit { unit = newUnit, placement = i + 1 };
                added = true;
                break;
            }
        }

        if (!added)
        {
            squad.Add(new Unit { unit = newUnit, placement = squad.Count + 1 });
        }

        // Merge units if necessary
        MergeUnits(squad);

        // Save the updated squad to PlayerPrefs
        playerSquadWrapper.units = squad.ToArray();
        PlayerPrefs.SetString("PlayerSquad", JsonUtility.ToJson(playerSquadWrapper));
        PlayerPrefs.Save();
    }

    void MergeUnits(List<Unit> squad)
    {
        string[] mergePairs = { "Militia-Soldier", "Soldier-Defender", "Scolar-Wizard", "Wizard-Sorcerer", "Hunter-Bower" };
        Dictionary<string, string> mergeMap = new Dictionary<string, string>();

        foreach (string pair in mergePairs)
        {
            string[] units = pair.Split('-');
            mergeMap[units[0]] = units[1];
        }

        bool merged;
        do
        {
            merged = false;
            foreach (var entry in mergeMap)
            {
                var unitsToMerge = squad.FindAll(u => u.unit == entry.Key);
                if (unitsToMerge.Count >= 2)
                {
                    // Remove the units being merged
                    squad.Remove(unitsToMerge[0]);
                    squad.Remove(unitsToMerge[1]);

                    // Add the higher unit
                    squad.Add(new Unit { unit = entry.Value, placement = squad.Count + 1 });
                    merged = true;
                    break;
                }
            }
        } while (merged);
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

    void OnOkayButtonClicked()
    {
        string nodeType = PlayerPrefs.GetString("CurrentNodeType");

        if (nodeType == "end")
        {
            PlayerPrefs.DeleteAll();
            SceneManager.LoadScene("MainMenuScene");
        }
        else
        {
            SceneManager.LoadScene("MapScene");
        }
    }
}
