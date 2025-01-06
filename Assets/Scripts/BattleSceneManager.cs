using UnityEngine;
using TMPro;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Linq;

public class BattleSceneManager : MonoBehaviour
{
    public GameObject playerSquadPanel;
    public GameObject enemySquadPanel;
    public Button fightButton;
    public Button winButton;
    public Button loseButton;
    public GameObject unitPrefab; // Prefab to represent units in the battle

    private Dictionary<int, GameObject> playerUnits = new Dictionary<int, GameObject>();
    private Dictionary<int, GameObject> enemyUnits = new Dictionary<int, GameObject>();
    private Dictionary<GameObject, TextMeshProUGUI> unitHPTexts = new Dictionary<GameObject, TextMeshProUGUI>();
    private UnitArrayWrapper playerSquadWrapper;
    private UnitArrayWrapper enemySquadWrapper;
    private bool battleStarted = false;

    void Start()
    {
        InitializeSquads();
        fightButton.onClick.AddListener(StartBattle);
    }

    void Update()
    {
        if (!battleStarted)
        {
            return;
        }
        // Player units' actions
        foreach (var playerUnit in playerUnits.Values)
        {
            if (playerUnit.activeSelf)
            {
                ExecuteUnitActions(playerUnit, enemyUnits);
            }
        }

        // Enemy units' actions
        foreach (var enemyUnit in enemyUnits.Values)
        {
            if (enemyUnit.activeSelf)
            {
                ExecuteUnitActions(enemyUnit, playerUnits);
            }
        }
    }

    void ExecuteUnitActions(GameObject unitObject, Dictionary<int, GameObject> potentialTargets)
    {
        Unit unitData = unitObject.GetComponent<UnitBehavior>().unit;

        if (unitData != null)
        {
            GameObject targetObject = GetClosestTarget(unitObject, potentialTargets);
            if (targetObject != null)
            {
                Unit targetData = targetObject.GetComponent<UnitBehavior>().unit;

                float distanceToTarget = Vector2.Distance(unitObject.transform.position, targetObject.transform.position);
                if (distanceToTarget <= unitData.attackRange)
                {
                    Attack(unitObject, targetObject, unitData, targetData);
                }
                else
                {
                    MoveTowardsTarget(unitObject, targetObject, unitData);
                }
            }
        }
    }

    void MoveTowardsTarget(GameObject unitObject, GameObject targetObject, Unit unitData)
    {
        Vector3 direction = (targetObject.transform.position - unitObject.transform.position).normalized;
        unitObject.transform.position += direction * unitData.moveSpeed * Time.deltaTime;
    }

    void Attack(GameObject attackerObject, GameObject targetObject, Unit attackerData, Unit targetData)
    {
        if (attackerData.attackCooldown <= 0f)
        {
            float damage = Random.Range(attackerData.minimumAttackDamage, attackerData.maximumAttackDamage) - targetData.armor;
            var oldHealth = targetData.health;
            targetData.health -= (int)Mathf.Max(1, damage);

            // Update target's health text
            Debug.Log($"{attackerData.unit} attacks! Changing health of {targetData.unit} from {oldHealth} to {targetData.health}.");
            unitHPTexts[targetObject].text = targetData.health.ToString();
            if (targetData.health <= 0)
            {
                targetObject.SetActive(false);
                Destroy(unitHPTexts[targetObject].gameObject);
                OnUnitDeath(targetObject);
            }

            attackerData.attackCooldown = attackerData.attackSpeed;
        }
        else
        {
            attackerData.attackCooldown -= Time.deltaTime;
        }
    }

    void InitializeSquads()
    {
        string playerSquadJson = PlayerPrefs.GetString("PlayerSquad", "{}");
        playerSquadWrapper = JsonUtility.FromJson<UnitArrayWrapper>(playerSquadJson);
        FulfillUnitDetails(playerSquadWrapper.units);
        ApplyBuffs(playerSquadWrapper.units);
        PlaceUnits(playerSquadWrapper.units, playerSquadPanel.transform, playerUnits);

        string enemySquadJson = PlayerPrefs.GetString("EnemySquad", "{}");
        enemySquadWrapper = JsonUtility.FromJson<UnitArrayWrapper>(enemySquadJson);
        FulfillUnitDetails(enemySquadWrapper.units);
        PlaceUnits(enemySquadWrapper.units, enemySquadPanel.transform, enemyUnits);
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

    void PlaceUnits(Unit[] squad, Transform panel, Dictionary<int, GameObject> unitDict)
    {
        foreach (Unit unit in squad)
        {
            GameObject unitObject = Instantiate(unitPrefab, panel);
            Vector3 position = Vector3.zero;

            if (unit.placement >= 1 && unit.placement <= 3)
            {
                // Front line positions
                position = new Vector3(((unit.placement - 1) * 200) - 250, 120, 0); // Example positioning
            }
            else if (unit.placement >= 4 && unit.placement <= 6)
            {
                // Back line positions
                position = new Vector3(((unit.placement - 4) * 200) - 250, -120, 0); // Example positioning
            }

            unitObject.transform.localPosition = position;
            unitObject.transform.rotation = Quaternion.identity; // Ensure units are vertical
            unitObject.GetComponent<Image>().sprite = Resources.Load<Sprite>($"Images/{unit.unit}");

            // Create and position HP text
            GameObject textObject = new GameObject("HealthText");
            textObject.transform.SetParent(unitObject.transform);
            TextMeshProUGUI healthText = textObject.AddComponent<TextMeshProUGUI>();
            healthText.fontSize = 14;
            healthText.color = Color.black;
            healthText.alignment = TextAlignmentOptions.Center;
            healthText.transform.localPosition = new Vector3(0, 110f, 0); // Adjusting vertical padding
            healthText.transform.localScale = Vector3.one * 5; // Adjusting the scale
            healthText.text = unit.health.ToString();
            UnitBehavior unitBehavior = unitObject.AddComponent<UnitBehavior>(); 
            unitBehavior.Initialize(unit);

            unitHPTexts[unitObject] = healthText;
            unitDict[unit.placement] = unitObject;
        }
    }

    void ApplyBuffs(Unit[] squad)
    {
        string buffs = PlayerPrefs.GetString("PlayerBuffs", "");
        List<string> playerBuffs = new List<string>(buffs.Split(new char[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries));
        string selectedCharacter = PlayerPrefs.GetString("SelectedCharacter", "Knight");

        foreach (Unit unit in squad)
        {
            if (unit.unit == selectedCharacter)
            {
                foreach (string buff in playerBuffs)
                {
                    if (buff == "Damage")
                    {
                        unit.minimumAttackDamage += 5;
                        unit.maximumAttackDamage += 5;
                    }
                    else if (buff == "Armor")
                    {
                        unit.armor += 3;
                    }
                }
            }
        }
    }

    void StartBattle()
    {
        fightButton.gameObject.SetActive(false);
        battleStarted = true;
        Image panelImage = playerSquadPanel.GetComponent<Image>(); 
        if (panelImage != null)
        {
            Color color = panelImage.color;
            color.a = 0;
            panelImage.color = color;
        }
        panelImage = enemySquadPanel.GetComponent<Image>(); 
        if (panelImage != null)
        {
            Color color = panelImage.color;
            color.a = 0;
            panelImage.color = color;
        }
    }

    GameObject GetClosestTarget(GameObject attacker, Dictionary<int, GameObject> potentialTargets)
    {
        float closestDistance = float.MaxValue;
        GameObject closestTarget = null;

        foreach (var target in potentialTargets.Values)
        {
            if (target.activeSelf)
            {
                float distance = Vector3.Distance(attacker.transform.position, target.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestTarget = target;
                }
            }
        }
        return closestTarget;
    }

    Unit FindUnit(GameObject unitObject, Dictionary<int, GameObject> units)
    {
        if (!units.ContainsValue(unitObject))
            return null;

        foreach (var unit in playerSquadWrapper.units)
        {
            if (units.ContainsKey(unit.placement) && units[unit.placement] == unitObject)
                return unit;
        }
        foreach (var unit in enemySquadWrapper.units)
        {
            if (units.ContainsKey(unit.placement) && units[unit.placement] == unitObject)
                return unit;
        }
        return null;
    }


    public void OnUnitDeath(GameObject unitObject)
    {
        if (playerUnits.ContainsValue(unitObject))
        {
            Debug.Log("playerUnits death");
            if (playerUnits.Values.All(u => !u.activeSelf))
            {
                loseButton.gameObject.SetActive(true);
            }
        }
        else if (enemyUnits.ContainsValue(unitObject))
        {
            Debug.Log("enemy units death");
            if (enemyUnits.Values.All(u => !u.activeSelf))
            {
                winButton.gameObject.SetActive(true);
            }
        }
    }

    public void OnWinButtonClicked()
    {
        int targetNodeId = PlayerPrefs.GetInt("TargetNode", 1);
        PlayerPrefs.SetInt("CharacterNode", targetNodeId);
        SceneManager.LoadScene("RewardsScene");
    }

    public void OnLoseButtonClicked()
    {
        PlayerPrefs.DeleteAll();
        SceneManager.LoadScene("MainMenuScene");
    }
}
