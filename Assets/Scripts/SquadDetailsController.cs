using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System.Linq;

public class SquadDetailsController : MonoBehaviour
{
    public GameObject cell1;
    public GameObject cell2;
    public GameObject cell3;
    public GameObject cell4;
    public GameObject cell5;
    public GameObject cell6;

    public Button left;
    public Button right;
    public Button top;
    public Button bottom;

    public TextMeshProUGUI unitDetailsText;

    private Dictionary<int, GameObject> cellDict = new Dictionary<int, GameObject>();
    private Dictionary<GameObject, Unit> unitDict = new Dictionary<GameObject, Unit>();
    private GameObject selectedUnitObject;
    private Unit selectedUnit;
    private bool isPlayerSquad;

    Sprite GetUnitSprite(string unitName)
    {
        return Resources.Load<Sprite>($"Images/{unitName}");
    }

    // Method to load the Map scene
    public void LoadMap()
    {
        SceneManager.LoadScene("MapScene");
    }

    void FulfillUnitDetails(Unit[] squad)
    {
        var fresh = GameContent.Units.CreateFreshSquad(squad);
        System.Array.Copy(fresh, squad, fresh.Length);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Initialize the cell dictionary
        cellDict.Add(1, cell1);
        cellDict.Add(2, cell2);
        cellDict.Add(3, cell3);
        cellDict.Add(4, cell4);
        cellDict.Add(5, cell5);
        cellDict.Add(6, cell6);

        // Load the squad details
        LoadSquadDetails();

        // Attach empty methods to buttons
        left.onClick.AddListener(OnLeftButtonClicked);
        right.onClick.AddListener(OnRightButtonClicked);
        top.onClick.AddListener(OnTopButtonClicked);
        bottom.onClick.AddListener(OnBottomButtonClicked);

        // Hide the buttons initially
        left.gameObject.SetActive(false);
        right.gameObject.SetActive(false);
        top.gameObject.SetActive(false);
        bottom.gameObject.SetActive(false);
    }

    void LoadSquadDetails()
    {
        string squadJson = JsonUtility.ToJson(GameManager.Instance.DetailsSquad);
        UnitArrayWrapper squadWrapper = JsonUtility.FromJson<UnitArrayWrapper>(squadJson);

        FulfillUnitDetails(squadWrapper.units);

        isPlayerSquad = GameManager.Instance.DetailsIsPlayer;

        foreach (Unit unit in squadWrapper.units)
        {
            int cellIndex = unit.placement;
            if (cellDict.ContainsKey(cellIndex))
            {
                GameObject unitObject = new GameObject(unit.unit);
                unitObject.AddComponent<Image>().sprite = GetUnitSprite(unit.unit);
                unitObject.transform.SetParent(cellDict[cellIndex].transform, false);
                unitObject.transform.localScale = new Vector3(2, 2, 1);
                unitObject.AddComponent<Button>().onClick.AddListener(() => OnUnitClicked(unitObject, unit));

                unitDict[unitObject] = unit;
            }
        }
    }

    void OnUnitClicked(GameObject unitObject, Unit unit)
    {
        if (selectedUnitObject != null)
        {
            // Reset previous selection
            selectedUnitObject.transform.localScale = new Vector3(2, 2, 1);
        }

        if (selectedUnitObject == unitObject)
        {
            // Unselect the unit
            selectedUnitObject = null;
            selectedUnit = null;
            unitDetailsText.text = "";
            left.gameObject.SetActive(false);
            right.gameObject.SetActive(false);
            top.gameObject.SetActive(false);
            bottom.gameObject.SetActive(false);
        }
        else
        {
            // Select the unit
            selectedUnitObject = unitObject;
            selectedUnit = unit;
            
            // Make selected unit slightly larger
            selectedUnitObject.transform.localScale = new Vector3(2.3f, 2.3f, 1);

            // Show unit details
            string buffsText = "";
            string selectedCharacter = GameManager.Instance.SelectedCharacter;
            
            // Only show buffs for the hero character
            if (unit.unit == selectedCharacter)
            {
                // Get both regular and permanent buffs
                string buffs = GameManager.Instance.Buffs;
                string permanentBuffs = GameManager.Instance.PermanentBuffs;
                
                // Combine both buff lists
                List<string> allBuffs = new List<string>();
                allBuffs.AddRange(buffs.Split(new char[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries));
                allBuffs.AddRange(permanentBuffs.Split(new char[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries));
                
                // Format the buffs text
                buffsText = allBuffs.Count > 0 ? string.Join("\n", allBuffs.Select(buff => $"- {buff}")) : "No buffs";
            }

            if (isPlayerSquad) unit = CombatStats.Build(unit, GameContent.Units, GameManager.Instance.SelectedCharacter, GameManager.Instance.State.buffs.ToArray(), GameManager.Instance.State.permanent.ToArray());
            unitDetailsText.text = $"Name: {GameContent.Units.Resolve(unit).DisplayName}\nHealth: {unit.health}\nArmor: {unit.armor}\n" +
                                   $"Move Speed: {unit.moveSpeed}\nAttack Damage: {unit.minimumAttackDamage}-{unit.maximumAttackDamage}\n" +
                                   $"Attacks/sec: {unit.attacksPerSecond:0.##}\nAttack Range: {unit.attackRange}";
            
            // Add buffs section only for hero
            if (unit.unit == selectedCharacter)
            {
                unitDetailsText.text += $"\n\nBuffs:\n{buffsText}";
            }

            // Show movement buttons if it's a player's squad
            if (isPlayerSquad)
            {
                SetMovementButtons(unit.placement);
            }
        }
    }


    void SetMovementButtons(int placement)
    {
        // Hide all buttons initially
        left.gameObject.SetActive(true);
        right.gameObject.SetActive(true);
        top.gameObject.SetActive(true);
        bottom.gameObject.SetActive(true);

        switch (placement)
        {
            case 1:
                left.gameObject.SetActive(true);
                bottom.gameObject.SetActive(true);
                top.gameObject.SetActive(false);
                right.gameObject.SetActive(false);
                break;
            case 2:
                top.gameObject.SetActive(true);
                bottom.gameObject.SetActive(true);
                left.gameObject.SetActive(true);
                right.gameObject.SetActive(false);
                break;
            case 3:
                top.gameObject.SetActive(true);
                left.gameObject.SetActive(true);
                bottom.gameObject.SetActive(false);
                right.gameObject.SetActive(false);
                break;
            case 4:
                right.gameObject.SetActive(true);
                bottom.gameObject.SetActive(true);
                top.gameObject.SetActive(false);
                left.gameObject.SetActive(false);
                break;
            case 5:
                top.gameObject.SetActive(true);
                bottom.gameObject.SetActive(true);
                right.gameObject.SetActive(true);
                left.gameObject.SetActive(false);
                break;
            case 6:
                top.gameObject.SetActive(true);
                right.gameObject.SetActive(true);
                bottom.gameObject.SetActive(false);
                left.gameObject.SetActive(false);
                break;
        }
    }

    void UpdateUnitPlacements()
    {
        if (isPlayerSquad && !GameManager.Instance.SetPlayerSquad(new UnitArrayWrapper { units = unitDict.Values.ToArray() }))
        {
            var saved = GameManager.Instance.PlayerSquad.units;
            foreach (var unit in unitDict.Values) unit.placement = saved.Single(u => u.instanceId == unit.instanceId).placement;
        }
        foreach (var kvp in unitDict)
        {
            int cellIndex = kvp.Value.placement;
            if (cellDict.ContainsKey(cellIndex))
            {
                GameObject unitObject = kvp.Key;
                unitObject.transform.SetParent(cellDict[cellIndex].transform, false);
                unitObject.transform.localScale = unitObject == selectedUnitObject ? new Vector3(2.3f, 2.3f, 1) : new Vector3(2, 2, 1);
            }
        }


    }

    void OnLeftButtonClicked()
    {
        int newPos = GetNewPosition(selectedUnit.placement, Vector2.left);
        SwapUnits(selectedUnit.placement, newPos);
        SetMovementButtons(selectedUnit.placement);
    }

    void OnRightButtonClicked()
    {
        int newPos = GetNewPosition(selectedUnit.placement, Vector2.right);
        SwapUnits(selectedUnit.placement, newPos);
        SetMovementButtons(selectedUnit.placement);
    }

    void OnTopButtonClicked()
    {
        int newPos = GetNewPosition(selectedUnit.placement, Vector2.up);
        SwapUnits(selectedUnit.placement, newPos);
        SetMovementButtons(selectedUnit.placement);
    }

    void OnBottomButtonClicked()
    {
        int newPos = GetNewPosition(selectedUnit.placement, Vector2.down);
        SwapUnits(selectedUnit.placement, newPos);
        SetMovementButtons(selectedUnit.placement);
    }

    int GetNewPosition(int currentPos, Vector2 direction)
    {
        if (direction == Vector2.right && currentPos > 3) return currentPos - 3;
        if (direction == Vector2.left && currentPos <= 3) return currentPos + 3;
        if (direction == Vector2.up && currentPos != 1 && currentPos != 4) return currentPos - 1;
        if (direction == Vector2.down && currentPos != 3 && currentPos != 6) return currentPos + 1;
        return currentPos;
    }

    void SwapUnits(int pos1, int pos2)
    {
        if (pos1 == pos2 || !cellDict.ContainsKey(pos2)) return;

        Unit tempUnit = unitDict.FirstOrDefault(kvp => kvp.Value.placement == pos2).Value;
        if (tempUnit != null)
        {
            tempUnit.placement = pos1;
        }
        selectedUnit.placement = pos2;

        UpdateUnitPlacements();
    }
}
