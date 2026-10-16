using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

[Serializable]
public sealed class UnitDefinitionRecord
{
    public string id, unit, displayName, faction, role, description, projectile, ability;
    public int level, health;
    public float armor, moveSpeed, minimumAttackDamage, maximumAttackDamage, attackSpeed, attackRange;
    public float attacksPerSecond, evasion, critChance, lifeSteal, armorPenetration;
}

[Serializable]
public sealed class UnitDefinitionFile { public UnitDefinitionRecord[] units; }

// Immutable snapshot: changing a deserialized input record cannot change live content.
public sealed class UnitDefinition
{
    public string Id { get; }
    public string LegacyKey { get; }
    public string DisplayName { get; }
    public string Faction { get; }
    public string Role { get; }
    public string Description { get; }
    public string Projectile { get; }
    public string Ability { get; }
    public int Level { get; }
    public int MaxHealth { get; }
    public float Armor { get; }
    public float MoveSpeed { get; }
    public float MinimumAttackDamage { get; }
    public float MaximumAttackDamage { get; }
    public float AttacksPerSecond { get; }
    public float Evasion { get; }
    public float CritChance { get; }
    public float LifeSteal { get; }
    public float ArmorPenetration { get; }
    public float AttackRange { get; }

    public UnitDefinition(UnitDefinitionRecord r)
    {
        if (r == null) throw new ArgumentNullException(nameof(r));
        Id = r.id; LegacyKey = r.unit; DisplayName = r.displayName;
        Faction = r.faction; Role = r.role; Description = r.description;
        Projectile = r.projectile; Ability = r.ability; Level = r.level;
        MaxHealth = r.health; Armor = r.armor; MoveSpeed = r.moveSpeed;
        MinimumAttackDamage = r.minimumAttackDamage; MaximumAttackDamage = r.maximumAttackDamage;
        AttacksPerSecond = r.attacksPerSecond > 0 ? r.attacksPerSecond : CombatResolver.LegacyRate(r.attackSpeed);
        Evasion = r.evasion; CritChance = r.critChance; LifeSteal = r.lifeSteal; ArmorPenetration = r.armorPenetration;
        AttackRange = r.attackRange;
    }

    public Unit CreateInstance(int placement, string instanceId = null)
    {
        if (placement < 1 || placement > 6) throw new ArgumentOutOfRangeException(nameof(placement));
        return new Unit {
            definitionId = Id, instanceId = string.IsNullOrWhiteSpace(instanceId) ? Guid.NewGuid().ToString("N") : instanceId,
            unit = LegacyKey, placement = placement, maxHealth = MaxHealth, health = MaxHealth,
            armor = Armor, moveSpeed = MoveSpeed, minimumAttackDamage = MinimumAttackDamage,
            maximumAttackDamage = MaximumAttackDamage, attacksPerSecond = AttacksPerSecond,
            evasion = Evasion, critChance = CritChance, lifeSteal = LifeSteal, armorPenetration = ArmorPenetration,
            attackRange = AttackRange, level = Level, description = Description,
            projectile = Projectile, ability = Ability, attackCooldown = 0
        };
    }
}

public sealed class UnitCatalog
{
    private readonly Dictionary<string, UnitDefinition> byId = new Dictionary<string, UnitDefinition>(StringComparer.Ordinal);
    private readonly Dictionary<string, UnitDefinition> byLegacy = new Dictionary<string, UnitDefinition>(StringComparer.Ordinal);
    public ReadOnlyCollection<UnitDefinition> Definitions { get; }

    public UnitCatalog(UnitDefinitionRecord[] records)
    {
        var errors = ContentValidator.ValidateUnits(records);
        if (errors.Count > 0) throw new ArgumentException(string.Join("\n", errors));
        var list = new List<UnitDefinition>();
        foreach (var record in records)
        {
            var definition = new UnitDefinition(record);
            byId.Add(definition.Id, definition); byLegacy.Add(definition.LegacyKey, definition);
            list.Add(definition);
        }
        Definitions = list.AsReadOnly();
    }

    public static UnitCatalog FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Configs/Units: empty JSON.");
        return new UnitCatalog(JsonUtility.FromJson<UnitDefinitionFile>(json)?.units);
    }

    public UnitDefinition Resolve(UnitInstance source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        // Never silently fall back from an unknown explicit ID to a legacy name.
        var key = string.IsNullOrWhiteSpace(source.definitionId) ? source.unit : source.definitionId;
        var index = string.IsNullOrWhiteSpace(source.definitionId) ? byLegacy : byId;
        if (key == null || !index.TryGetValue(key, out var definition))
            throw new ArgumentException($"Configs/Units: unknown unit reference '{key}'.");
        return definition;
    }

    public Unit[] CreateFreshSquad(Unit[] source)
    {
        var errors = ContentValidator.ValidateSquad(source, this, "Squad");
        if (errors.Count > 0) throw new ArgumentException(string.Join("\n", errors));
        var result = new Unit[source.Length];
        for (int i = 0; i < source.Length; i++)
            result[i] = Resolve(source[i]).CreateInstance(source[i].placement, source[i].instanceId);
        return result;
    }

    public Unit CreateLegacyInstance(string legacyKey, int placement)
    {
        return Resolve(new Unit { unit = legacyKey }).CreateInstance(placement);
    }
}

public static class GameContent
{
    private static UnitCatalog units;
    public static UnitCatalog Units => units ?? (units = UnitCatalog.FromJson(Read("Configs/Units")));
    public static string Read(string path)
    {
        var asset = Resources.Load<TextAsset>(path);
        if (asset == null) throw new InvalidOperationException($"Missing resource: {path}");
        return asset.text;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { units = null; }
}

public sealed class BattleState
{
    public UnitArrayWrapper Player { get; }
    public UnitArrayWrapper Enemy { get; }
    public BattleState(UnitCatalog catalog, Unit[] player, Unit[] enemy)
    {
        if (catalog == null) throw new ArgumentNullException(nameof(catalog));
        Player = new UnitArrayWrapper { units = catalog.CreateFreshSquad(player) };
        Enemy = new UnitArrayWrapper { units = catalog.CreateFreshSquad(enemy) };
    }
}
