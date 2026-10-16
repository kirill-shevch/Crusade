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
    private BattleState battleState;
    private bool battleStarted = false;
    private readonly ICombatRandom combatRandom = new SeededCombatRandom(System.Environment.TickCount);
    [SerializeField] private float speedMultiplier = 1f;

    void Start()
    {
        InitializeSquads();
        fightButton.onClick.AddListener(StartBattle);
    }

    void Update()
    {
        if (!battleStarted || battleFinished)
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
        if (battleFinished) return;
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

            attackerData.attackCooldown = CombatResolver.Interval(attackerData.attacksPerSecond);
        }
        else
        {
            attackerData.attackCooldown -= Time.deltaTime;
        }
    }

    void InitializeSquads()
    {
        string playerSquadJson = JsonUtility.ToJson(GameManager.Instance.PlayerSquad);
        string enemySquadJson = JsonUtility.ToJson(GameManager.Instance.EnemySquad);
        battleState = new BattleState(GameContent.Units,
            JsonUtility.FromJson<UnitArrayWrapper>(playerSquadJson)?.units,
            JsonUtility.FromJson<UnitArrayWrapper>(enemySquadJson)?.units);
        playerSquadWrapper = battleState.Player;
        enemySquadWrapper = battleState.Enemy;
        ApplyBuffs(playerSquadWrapper.units);
        // Existing buffs raise starting HP. This is also the maximum for this battle.
        foreach (var unit in playerSquadWrapper.units) unit.maxHealth = unit.health;
        PlaceUnits(playerSquadWrapper.units, playerSquadPanel.transform, playerUnits);
        PlaceAbilities(playerSquadWrapper.units, abilityPanel.transform, playerUnits);

        PlaceUnits(enemySquadWrapper.units, enemySquadPanel.transform, enemyUnits);
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
                    rectTransform.sizeDelta = new Vector2(225, 225); // 1.5 times bigger (was 150)
    
                    // Set up button colors for disabled state
                    ColorBlock colors = button.colors;
                    colors.normalColor = Color.white;
                    colors.highlightedColor = new Color(0.9f, 0.9f, 0.9f);
                    colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
                    colors.disabledColor = new Color(0.1f, 0.1f, 0.1f, 1f); // Even darker when disabled
                    colors.selectedColor = Color.white;
                    button.colors = colors;
    
                    // Set parent
                    abilityButton.transform.SetParent(panel.transform, false);
    
                    // Position buttons (example positioning)
                    int buttonCount = instantiatedButtons.Count;
                    float totalWidth = buttonCount * 240; // Adjusted spacing for larger buttons
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
        if (battleFinished) return;
        StartCoroutine(ShieldBashCoroutine(caster, casterGameObject, abilityButton));
    }

    private IEnumerator ShieldBashCoroutine(Unit caster, GameObject casterGameObject, Button abilityButton)
    {
        // Disable the button and darken it
        abilityButton.interactable = false;
        abilityButton.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.3f, 1f);

        // Wait for 0.5 seconds
        yield return new WaitForSeconds(0.5f);

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

        // Deal 25 damage, push back and slow
        foreach (var target in targets)
        {
            Unit targetData = target.GetComponent<UnitBehavior>().unit;
            var hit = ApplyDamage(caster, target, 25);
            if (battleFinished) yield break;
            if (hit.evaded || targetData.health == 0 || caster.health == 0) continue;

            Debug.Log($"{caster.unit} used Shieldbash on {targetData.unit}, dealing {hit.hpLost} damage.");
            
            // Push back by teleporting
            Vector2 pushDirection = (target.transform.position - casterGameObject.transform.position).normalized;
            float pushDistance = 3f;
            target.transform.position += (Vector3)(pushDirection * pushDistance);

            // Apply slow effect
            float originalSpeed = targetData.moveSpeed;
            targetData.moveSpeed /= 2f; // Slow by half
            StartCoroutine(RevertSlow(targetData, originalSpeed, 0.5f));


        }
    }

    // Rapidfire Ability
    private void RapidFire(Unit caster, GameObject casterGameObject, Button abilityButton)
    {
        if (battleFinished) return;
        StartCoroutine(RapidFireCoroutine(caster, casterGameObject, abilityButton));
    }

    private IEnumerator RapidFireCoroutine(Unit caster, GameObject casterGameObject, Button abilityButton)
    {
        Debug.Log("Rapidfire");
        // Disable the button and darken it
        abilityButton.interactable = false;
        abilityButton.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.3f, 1f);

        // Wait for 0.5 seconds
        yield return new WaitForSeconds(0.5f);

        // Show EffectImage on caster
        StartCoroutine(ShowEffectImage(casterGameObject, "rapidfireeffect", 3f));
        
        // Increase attack speed
        UnitBehavior behavior = casterGameObject.GetComponent<UnitBehavior>();
        float originalRate = behavior.unit.attacksPerSecond;
        behavior.unit.attacksPerSecond = CombatStats.RateWithLegacyReduction(originalRate, 0.9f);

        // Revert after 3 seconds
        StartCoroutine(RevertRapidFire(behavior, originalRate, 3f));
    }

    private IEnumerator RevertRapidFire(UnitBehavior behavior, float originalRate, float duration)
    {
        yield return new WaitForSeconds(duration);
        behavior.unit.attacksPerSecond = originalRate;
    }

    // Thunderstorm Ability
    private void Thunderstorm(Unit caster, Button abilityButton)
    {
        if (battleFinished) return;
        StartCoroutine(ThunderstormCoroutine(caster, abilityButton));
    }

    private IEnumerator ThunderstormCoroutine(Unit caster, Button abilityButton)
    {
        // Disable the button and darken it
        abilityButton.interactable = false;
        abilityButton.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.3f, 1f);

        // Wait for 0.5 seconds
        yield return new WaitForSeconds(0.5f);

        // Find all enemy units
        List<GameObject> enemies = enemyUnits.Values.Where(u => u.activeSelf).ToList();

        foreach (var enemy in enemies)
        {
            // Show EffectImage on each enemy
            StartCoroutine(ShowEffectImage(enemy, "thunderstormeffect", 1f));

            // Deal 45 damage
            Unit targetData = enemy.GetComponent<UnitBehavior>().unit;
            var hit = ApplyDamage(caster, enemy, 45);
            if (battleFinished) yield break;
            if (hit.evaded || targetData.health == 0 || caster.health == 0) continue;

            // Apply slow effect
            float originalSpeed = targetData.moveSpeed;
            targetData.moveSpeed /= 2f; // Slow by half
            StartCoroutine(RevertSlow(targetData, originalSpeed, 0.5f));

            Debug.Log($"{caster.unit} used Thunderstorm on {targetData.unit}, dealing {hit.hpLost} damage.");


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
        effect.transform.localPosition = Vector3.zero;
        effect.transform.localScale = new Vector3(20f, 20f, 20f); // Twice as big (was 10f)
        renderer.color = new Color(1f, 1f, 1f, 0.75f); // Less transparent (was 0.5f)

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
        var game = GameManager.Instance;
        var state = game.State;
        for (int i = 0; i < squad.Length; i++)
            squad[i] = CombatStats.Build(squad[i], GameContent.Units, game.SelectedCharacter, state.buffs.ToArray(), state.permanent.ToArray());
    }
    void StartBattle()
    {
        if (battleFinished) return;
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
        float speed = 10f;
        Vector3 startPosition = projectile.transform.position;
        Vector3 targetPosition = targetObject.transform.position;

        while ((targetPosition - projectile.transform.position).sqrMagnitude > 0.1f)
        {
            projectile.transform.position = Vector3.MoveTowards(projectile.transform.position, targetPosition, speed * Time.deltaTime);
            yield return null;
        }

        // Ensure the projectile reaches the target
        projectile.transform.position = targetPosition;

        if (battleFinished || targetData.health <= 0 || attackerData.health <= 0) { Destroy(projectile); yield break; }
        double rawDamage = attackerData.minimumAttackDamage + combatRandom.Next01() *
            (attackerData.maximumAttackDamage - attackerData.minimumAttackDamage);
        var hit = ApplyDamage(attackerData, targetObject, rawDamage);
        if (battleFinished || hit.evaded || hit.killed) { Destroy(projectile); yield break; }
        // Calculate impact direction and apply small knockback
        Vector2 impactDirection = (targetObject.transform.position - startPosition).normalized;
        float knockbackDistance = 0.2f; // Small knockback distance
        Vector3 knockbackPosition = targetObject.transform.position + (Vector3)(impactDirection * knockbackDistance);
        
        // Quick knockback movement
        float knockbackDuration = 0.1f;
        float elapsedTime = 0;
        Vector3 originalPosition = targetObject.transform.position;
        
        while (elapsedTime < knockbackDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / knockbackDuration;
            targetObject.transform.position = Vector3.Lerp(originalPosition, knockbackPosition, t);
            yield return null;
        }

        // Destroy projectile after it reaches the target
        Destroy(projectile);
    }

    // Helper method to revert slow effect
    private IEnumerator RevertSlow(Unit unit, float originalSpeed, float duration)
    {
        yield return new WaitForSeconds(duration);
        unit.moveSpeed = originalSpeed;
    }

    private CombatResult ApplyDamage(Unit attacker, GameObject targetObject, double rawDamage)
    {
        if (battleFinished || !targetObject.activeSelf) return default;
        Unit target = targetObject.GetComponent<UnitBehavior>().unit;
        var result = CombatResolver.Damage(attacker, target, rawDamage, combatRandom);
        unitHPTexts[targetObject].text = target.health.ToString();
        foreach (var pair in unitHPTexts)
            if (pair.Key != null && pair.Key.GetComponent<UnitBehavior>()?.unit == attacker)
                pair.Value.text = attacker.health.ToString();
        if (result.killed)
        {
            targetObject.SetActive(false);
            unitHPTexts[targetObject].text = string.Empty;
            OnUnitDeath(targetObject);
        }
        return result;
    }
    private bool battleFinished;
    public void OnUnitDeath(GameObject unitObject)
    {
        if (battleFinished) return;
        if (playerUnits.ContainsValue(unitObject))
        {
            Debug.Log("playerUnits death");
            if (playerUnits.Values.All(u => !u.activeSelf))
            {
                battleFinished = true; battleStarted = false; StopAllCoroutines();
                GameManager.Instance.ResetGame();
                loseButton.gameObject.SetActive(true);
            }
        }
        else if (enemyUnits.ContainsValue(unitObject))
        {
            Debug.Log("enemy units death");
            if (enemyUnits.Values.All(u => !u.activeSelf))
            {
                battleFinished = true; battleStarted = false; StopAllCoroutines();
                GameManager.Instance.WinBattle();
                winButton.gameObject.SetActive(true);
            }
        }
    }

    public void OnWinButtonClicked()
    {
        if (GameManager.Instance.WinBattle()) SceneManager.LoadScene(GameManager.Instance.ResumeScene);
    }
    public void OnLoseButtonClicked()
    {
        if (GameManager.Instance.ResetGame()) SceneManager.LoadScene("MainMenuScene");
    }
}
