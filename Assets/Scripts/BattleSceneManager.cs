using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Linq;

public class BattleSceneManager : MonoBehaviour
{
    public GameObject playerSquadPanel;
    public GameObject enemySquadPanel;
    public GameObject abilityPanel;
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
    [SerializeField] private float speedMultiplier = 1f;

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
        var rb = unitObject.GetComponent<Rigidbody2D>();
        Vector2 direction = (targetObject.transform.position - unitObject.transform.position).normalized;
        
        // Use Time.deltaTime instead of Time.fixedDeltaTime for consistent timing
        float moveStep = unitData.moveSpeed * speedMultiplier * Time.deltaTime;
        
        // Move using MovePosition for more consistent movement across platforms
        Vector2 newPosition = rb.position + (direction * moveStep);
        rb.MovePosition(newPosition);
        
        // Zero out any residual velocity
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
    }


    void Attack(GameObject attackerObject, GameObject targetObject, Unit attackerData, Unit targetData)
    {
        if (attackerData.attackCooldown <= 0f)
        {
            Rigidbody2D rb = attackerObject.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero; // Clear linear velocity
                rb.angularVelocity = 0f; // Clear angular velocity
            }

            // Spawn the projectile
            GameObject projectile = new GameObject("Projectile");
            projectile.transform.position = attackerObject.transform.position;
            projectile.transform.rotation = Quaternion.LookRotation(Vector3.forward, targetObject.transform.position - attackerObject.transform.position);
            SpriteRenderer spriteRenderer = projectile.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = Resources.Load<Sprite>($"Images/{attackerData.projectile}");
            spriteRenderer.sortingOrder = 1;
            projectile.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);

            StartCoroutine(ProjectileMovement(projectile, targetObject, attackerData, targetData));

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
        PlaceAbilities(playerSquadWrapper.units, abilityPanel.transform, playerUnits);

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
                unit.projectile = detailedUnit.projectile;
                unit.ability = detailedUnit.ability;
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

    void PlaceAbilities(Unit[] squad, Transform panel, Dictionary<int, GameObject> unitDict)
    {
        Debug.Log("Placing abilities");
        TextAsset abilitiesConfig = Resources.Load<TextAsset>("Configs/Abilities");
        AbilityArrayWrapper abilitiesWrapper = JsonUtility.FromJson<AbilityArrayWrapper>(abilitiesConfig.text);
    
        List<RectTransform> instantiatedButtons = new List<RectTransform>();
    
        foreach (Unit unit in squad)
        {
            if (!string.IsNullOrEmpty(unit.ability))
            {
                Ability ability = abilitiesWrapper.abilities.FirstOrDefault(a => a.abilityName == unit.ability);
                if (ability != null)
                {
                    // Create button
                    GameObject abilityButton = new GameObject($"{ability.abilityName}Button");
                    Button button = abilityButton.AddComponent<Button>();
                    button.interactable = false;
                    Image buttonImage = abilityButton.AddComponent<Image>();
                    buttonImage.sprite = Resources.Load<Sprite>($"Images/{ability.buttonImage}");
                    RectTransform rectTransform = abilityButton.GetComponent<RectTransform>();
                    rectTransform.sizeDelta = new Vector2(150, 150); // Adjust size as needed
    
                    // Set parent
                    abilityButton.transform.SetParent(panel.transform, false);
    
                    // Position buttons (example positioning)
                    int buttonCount = instantiatedButtons.Count;
                    float totalWidth = buttonCount * 160; // 100 width + 10 spacing
                    rectTransform.anchoredPosition = new Vector2(0, 450 + totalWidth);
                    instantiatedButtons.Add(rectTransform);
    
                    // Attach Ability Method
                    if (ability.abilityName.ToLower() == "shieldbash")
                    {
                        button.onClick.AddListener(() => ShieldBash(unit, unitDict[unit.placement], button));
                    }
                    else if (ability.abilityName.ToLower() == "rapidfire")
                    {
                        button.onClick.AddListener(() => RapidFire(unit, unitDict[unit.placement], button));
                    }
                    else if (ability.abilityName.ToLower() == "thunderstorm")
                    {
                        button.onClick.AddListener(() => Thunderstorm(unit, button));
                    }
                }
            }
        }
    }

    // Shieldbash Ability
    private void ShieldBash(Unit caster, GameObject casterGameObject, Button abilityButton)
    {
        // Disable the button
        abilityButton.interactable = false;

        // Show EffectImage on caster
        StartCoroutine(ShowEffectImage(casterGameObject, "shieldbasheffect", 1f));

        // Find all units around the caster within a certain radius (e.g., 5 units)
        float radius = 5f;
        List<GameObject> targets = new List<GameObject>();

        foreach (var unit in enemyUnits.Values)
        {
            if (unit != casterGameObject && unit.activeSelf)
            {
                float distance = Vector2.Distance(casterGameObject.transform.position, unit.transform.position);
                if (distance <= radius)
                {
                    targets.Add(unit);
                }
            }
        }

        // Deal 25 damage and push back
        foreach (var target in targets)
        {
            Unit targetData = target.GetComponent<UnitBehavior>().unit;
            float oldHealth = targetData.health;
            targetData.health -= 25;
            unitHPTexts[target].text = targetData.health.ToString();

            Debug.Log($"{caster.unit} used Shieldbash on {targetData.unit}, dealing 25 damage.");
            
            // Push back by teleporting
            Vector2 pushDirection = (target.transform.position - casterGameObject.transform.position).normalized;
            float pushDistance = 3f; // Constant distance to push back
            target.transform.position += (Vector3)(pushDirection * pushDistance);

            if (targetData.health <= 0)
            {
                target.SetActive(false);
                unitHPTexts[target].text = string.Empty;
                OnUnitDeath(target);
            }
        }
    }

    // Rapidfire Ability
    private void RapidFire(Unit caster, GameObject casterGameObject, Button abilityButton)
    {
        Debug.Log("Rapidfire");
        // Disable the button
        abilityButton.interactable = false;

        // Show EffectImage on caster
        StartCoroutine(ShowEffectImage(casterGameObject, "rapidfireeffect", 3f));
        // Increase attack speed
        UnitBehavior behavior = casterGameObject.GetComponent<UnitBehavior>();
        behavior.unit.attackSpeed -= 0.9f;

        // Revert after 3 seconds
        StartCoroutine(RevertRapidFire(behavior, 3f));
    }

    private IEnumerator RevertRapidFire(UnitBehavior behavior, float duration)
    {
        yield return new WaitForSeconds(duration);
        behavior.unit.attackSpeed += 0.9f;
    }

    // Thunderstorm Ability
    private void Thunderstorm(Unit caster, Button abilityButton)
    {
        // Disable the button
        abilityButton.interactable = false;

        // Find all enemy units
        List<GameObject> enemies = enemyUnits.Values.Where(u => u.activeSelf).ToList();

        foreach (var enemy in enemies)
        {
            // Show EffectImage on each enemy
            StartCoroutine(ShowEffectImage(enemy, "thunderstormeffect", 1f));

            // Deal 45 damage
            Unit targetData = enemy.GetComponent<UnitBehavior>().unit;
            float oldHealth = targetData.health;
            targetData.health -= 45;
            unitHPTexts[enemy].text = targetData.health.ToString();

            Debug.Log($"{caster.unit} used Thunderstorm on {targetData.unit}, dealing 45 damage.");

            if (targetData.health <= 0)
            {
                enemy.SetActive(false);
                unitHPTexts[enemy].text = string.Empty;
                OnUnitDeath(enemy);
            }
        }
    }

    // Helper method to show effect images
    private IEnumerator ShowEffectImage(GameObject target, string effectImageName, float duration)
    {
        GameObject effect = new GameObject("EffectImage");
        effect.transform.SetParent(target.transform);
        SpriteRenderer renderer = effect.AddComponent<SpriteRenderer>();
        renderer.sprite = Resources.Load<Sprite>($"Images/{effectImageName}");
        renderer.sortingOrder = 2; // Ensure it's on top
        effect.transform.localPosition = Vector3.zero;// Using localPosition since it's now parented
        effect.transform.localScale = new Vector3(10f, 10f, 10f);
        renderer.color = new Color(1f, 1f, 1f, 0.5f); // Half transparent

        yield return new WaitForSeconds(duration);

        Destroy(effect);
    }

    // Helper to get effect image name from ability name
    private string GetAbilityEffectImage(string abilityName)
    {
        switch (abilityName.ToLower())
        {
            case "rapidfire":
                return "rapidfireeffect";
            // Add other cases if needed
            default:
                return "";
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

        // Make activity panel buttons interactable
        Button[] buttons = abilityPanel.GetComponentsInChildren<Button>();
        foreach (Button button in buttons)
        {
            button.interactable = true;
        }

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

    IEnumerator ProjectileMovement(GameObject projectile, GameObject targetObject, Unit attackerData, Unit targetData)
    {
        float speed = 10f; // Adjust the projectile speed as needed
        Vector3 startPosition = projectile.transform.position;
        Vector3 targetPosition = targetObject.transform.position;

        while ((targetPosition - projectile.transform.position).sqrMagnitude > 0.1f)
        {
            projectile.transform.position = Vector3.MoveTowards(projectile.transform.position, targetPosition, speed * Time.deltaTime);
            yield return null;
        }

        // Ensure the projectile reaches the target
        projectile.transform.position = targetPosition;

        // Deal damage
        float damage = Random.Range(attackerData.minimumAttackDamage, attackerData.maximumAttackDamage) - targetData.armor;
        var oldHealth = targetData.health;
        targetData.health -= (int)Mathf.Max(1, damage);

        // Update target's health text
        Debug.Log($"{attackerData.unit} attacks! Changing health of {targetData.unit} from {oldHealth} to {targetData.health}.");
        unitHPTexts[targetObject].text = targetData.health.ToString();
        if (targetData.health <= 0)
        {
            targetObject.SetActive(false);
            unitHPTexts[targetObject].text = string.Empty;
            OnUnitDeath(targetObject);
        }

        // Destroy projectile after it reaches the target
        Destroy(projectile);
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

        // Load map config and find current map
        var mapConfig = JsonUtility.FromJson<MapConfig>(Resources.Load<TextAsset>("Configs/Map").text);
        var currentMap = mapConfig.maps.First(m => m.name == GameManager.Instance.CurrentMap);

        // Find target node
        var targetNode = currentMap.nodes.First(n => n.id == targetNodeId);

        if (GameManager.Instance.VisitedNodes.Contains(targetNodeId))
        {
            GameManager.Instance.SetCurrentNode(targetNodeId);
            SceneController.Instance.LoadMap();
        }
        else
        {
            GameManager.Instance.SetCurrentNode(targetNodeId);
            SceneManager.LoadScene("RewardsScene");
        }
    }

    public void OnLoseButtonClicked()
    {
        PlayerPrefs.DeleteAll();
        SceneManager.LoadScene("MainMenuScene");
    }
}
