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
        TextAsset unitConfig = Resources.Load<TextAsset>("Configs/Units");
        UnitArrayWrapper config = JsonUtility.FromJson<UnitArrayWrapper>(unitConfig.text);

        foreach (Unit unit in squad)
        {
            Unit detailedUnit = config.units.FirstOrDefault(u => u.unit == unit.unit);
            if (detailedUnit != null)
            {
                unit.level = detailedUnit.level;
                unit.description = detailedUnit.description;
                unit.health = detailedUnit.health;
                unit.armor = detailedUnit.armor;
                unit.moveSpeed = detailedUnit.moveSpeed;
                unit.minimumAttackDamage = detailedUnit.minimumAttackDamage;
                unit.maximumAttackDamage = detailedUnit.maximumAttackDamage;
                unit.attackSpeed = detailedUnit.attackSpeed;
                unit.attackRange = detailedUnit.attackRange;
            }
        }
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
        string squadJson = PlayerPrefs.GetString("DetailsSquad", "{}");
        UnitArrayWrapper squadWrapper = JsonUtility.FromJson<UnitArrayWrapper>(squadJson);

        FulfillUnitDetails(squadWrapper.units);

        isPlayerSquad = PlayerPrefs.GetInt("isPlayerSquad", 0) == 1;

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
            string selectedCharacter = PlayerPrefs.GetString("SelectedCharacter", "Knight");
            
            // Only show buffs for the hero character
            if (unit.unit == selectedCharacter)
            {
                string buffs = PlayerPrefs.GetString("PlayerBuffs", "");
                List<string> playerBuffs = new List<string>(buffs.Split(new char[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries));
                buffsText = playerBuffs.Count > 0 ? string.Join("\n", playerBuffs.Select(buff => $"- {buff}")) : "No buffs";
            }

            unitDetailsText.text = $"Name: {unit.unit}\nHealth: {unit.health}\nArmor: {unit.armor}\n" +
                                   $"Move Speed: {unit.moveSpeed}\nAttack Damage: {unit.minimumAttackDamage}-{unit.maximumAttackDamage}\n" +
                                   $"Attack Speed: {unit.attackSpeed}\nAttack Range: {unit.attackRange}";
            
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

        // Save the updated squad
        if (isPlayerSquad)
        {
            UnitArrayWrapper playerSquadWrapper = new UnitArrayWrapper { units = unitDict.Values.ToArray() };
            string squadJson = JsonUtility.ToJson(playerSquadWrapper);
            PlayerPrefs.SetString("PlayerSquad", squadJson);
            GameManager.Instance.SetPlayerSquad(playerSquadWrapper);
            GameManager.Instance.SaveGameState();
        }
    }

    void OnLeftButtonClicked()
    {
        int newPos = GetNewPosition(selectedUnit.placement, Vector2.left);
        SwapUnits(selectedUnit.placement, newPos);
        SetMovementButtons(newPos);
    }

    void OnRightButtonClicked()
    {
        int newPos = GetNewPosition(selectedUnit.placement, Vector2.right);
        SwapUnits(selectedUnit.placement, newPos);
        SetMovementButtons(newPos);
    }

    void OnTopButtonClicked()
    {
        int newPos = GetNewPosition(selectedUnit.placement, Vector2.up);
        SwapUnits(selectedUnit.placement, newPos);
        SetMovementButtons(newPos);
    }

    void OnBottomButtonClicked()
    {
        int newPos = GetNewPosition(selectedUnit.placement, Vector2.down);
        SwapUnits(selectedUnit.placement, newPos);
        SetMovementButtons(newPos);
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
