using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public class ContentTests
{
    private static UnitDefinitionRecord Record() => new UnitDefinitionRecord {
        id = "unit.test", unit = "Legacy", displayName = "Test", faction = "Unassigned", role = "Warrior",
        level = 1, health = 100, armor = 10, moveSpeed = 2, minimumAttackDamage = 5,
        maximumAttackDamage = 10, attackSpeed = 0.5f, attackRange = 1, projectile = "test"
    };
    private static UnitCatalog Catalog() => new UnitCatalog(new[] { Record() });

    [Test]
    public void BattleCopiesCannotMutateRosterDefinitionOrOtherBattle()
    {
        var record = Record(); var catalog = new UnitCatalog(new[] { record });
        var roster = new[] { new Unit { unit = "Legacy", placement = 1, health = 19 } };
        var battle = new BattleState(catalog, roster, roster);
        battle.Player.units[0].health = 0;
        battle.Player.units[0].armor = 999;
        record.health = 999;
        Assert.That(roster[0].health, Is.EqualTo(19));
        Assert.That(battle.Enemy.units[0].health, Is.EqualTo(100));
        Assert.That(catalog.Definitions[0].MaxHealth, Is.EqualTo(100));
        var next = new BattleState(catalog, roster, roster);
        Assert.That(next.Player.units[0].health, Is.EqualTo(100));
        Assert.That(next.Player.units[0].armor, Is.EqualTo(10));
        Assert.That(battle.Player.units[0].instanceId, Is.Not.EqualTo(battle.Enemy.units[0].instanceId));
    }

    [Test]
    public void LegacySaveStillLoadsAndNewIdsRoundTrip()
    {
        var legacy = JsonUtility.FromJson<UnitArrayWrapper>("{\"units\":[{\"unit\":\"Legacy\",\"placement\":3,\"health\":17}]}");
        var fresh = Catalog().CreateFreshSquad(legacy.units);
        Assert.That(fresh[0].placement, Is.EqualTo(3));
        Assert.That(fresh[0].definitionId, Is.EqualTo("unit.test"));
        Assert.That(fresh[0].maxHealth, Is.EqualTo(100));
        Assert.That(fresh[0].attacksPerSecond, Is.EqualTo(2f));
        fresh[0].health = 42;
        var saved = JsonUtility.FromJson<UnitArrayWrapper>(JsonUtility.ToJson(new UnitArrayWrapper { units = fresh }));
        Assert.That(saved.units[0].health, Is.EqualTo(42));
        Assert.That(saved.units[0].maxHealth, Is.EqualTo(100));
        Assert.That(saved.units[0].instanceId, Is.EqualTo(fresh[0].instanceId));
        Assert.That(Catalog().CreateFreshSquad(saved.units)[0].instanceId, Is.EqualTo(fresh[0].instanceId));
    }

    [Test]
    public void StableIdSurvivesDisplayNameChangeButUnknownIdDoesNotFallback()
    {
        var r = Record(); r.displayName = "Renamed"; var catalog = new UnitCatalog(new[] { r });
        Assert.That(catalog.Resolve(new Unit { definitionId = "unit.test" }).DisplayName, Is.EqualTo("Renamed"));
        Assert.Throws<ArgumentException>(() => catalog.Resolve(new Unit { definitionId = "missing", unit = "Legacy" }));
    }

    [Test]
    public void RejectsDuplicateIds()
    {
        var errors = ContentValidator.ValidateUnits(new[] { Record(), Record() });
        // Opt-in negative control proves the runner reports a failed NUnit assertion.
        if (Environment.GetCommandLineArgs().Contains("-crusadeProbeFailure")) Assert.That(errors, Is.Empty, "Intentional runner probe");
        Assert.That(errors.Any(e => e.Contains("duplicate ID")), Is.True);
        Assert.Throws<ArgumentException>(() => new UnitCatalog(new[] { Record(), Record() }));
    }

    [TestCase("role")]
    [TestCase("faction")]
    [TestCase("health")]
    [TestCase("speed")]
    [TestCase("damage")]
    [TestCase("nan")]
    public void RejectsInvalidDefinitions(string field)
    {
        var r = Record();
        switch (field) {
            case "role": r.role = "Missing"; break;
            case "faction": r.faction = "Missing"; break;
            case "health": r.health = 0; break;
            case "speed": r.attackSpeed = 0; break;
            case "damage": r.minimumAttackDamage = 20; break;
            case "nan": r.armor = float.NaN; break;
        }
        Assert.Throws<ArgumentException>(() => new UnitCatalog(new[] { r }));
    }

    [TestCase("")]
    [TestCase("{}")]
    [TestCase("{\"units\":[]}")]
    [TestCase("{invalid")]
    public void RejectsMissingOrInvalidCatalog(string json) => Assert.Throws<ArgumentException>(() => UnitCatalog.FromJson(json));

    [Test]
    public void RejectsUnknownUnitAndDuplicatePlacement()
    {
        var errors = ContentValidator.ValidateSquad(new[] {
            new Unit { unit = "Legacy", placement = 1 }, new Unit { unit = "Missing", placement = 1 }
        }, Catalog(), "Fixture");
        Assert.That(errors.Any(e => e.Contains("placement")), Is.True);
        Assert.That(errors.Any(e => e.Contains("unknown unit")), Is.True);
    }

    [Test]
    public void RejectsBrokenMapAbilityAndSpriteReferences()
    {
        var r = Record(); r.ability = "absent";
        var maps = new[] { new Map { name = "Test", nextMaps = new[] { "Missing" }, finalReward = "BullHeart",
            nodes = new[] { new Node { id = 1, type = "barracks" }, new Node { id = 2, type = "end" } },
            edges = new[] { new Edge { from = 1, to = 3, squad = Array.Empty<Unit>() } } } };
        var errors = ContentValidator.Validate(new UnitCatalog(new[] { r }), Array.Empty<Ability>(), maps, _ => false);
        foreach (var expected in new[] { "unknown ability", "unknown next map", "unknown endpoint", "unreachable node 2", "missing sprite" })
            Assert.That(errors.Any(e => e.Contains(expected)), Is.True, expected);
    }

    [Test]
    public void TerminalRewardMarkerCannotBeUsedAsAnImplementedBuff()
    {
        var map = new Map { name = "Test", nextMaps = new[] { "Test" }, finalReward = "InnerFire",
            nodes = new[] { new Node { id = 1, type = "end" } }, edges = Array.Empty<Edge>() };
        var errors = ContentValidator.Validate(Catalog(), Array.Empty<Ability>(), new[] { map }, null);
        Assert.That(errors.Any(e => e.Contains("unknown final reward")), Is.True);
        map.nextMaps = Array.Empty<string>();
        Assert.That(ContentValidator.Validate(Catalog(), Array.Empty<Ability>(), new[] { map }, null), Is.Empty);
    }

    [Test]
    public void ShippedCatalogMapsAbilitiesAndSpritesAreValid()
    {
        var catalog = UnitCatalog.FromJson(GameContent.Read("Configs/Units"));
        var maps = JsonUtility.FromJson<MapConfig>(GameContent.Read("Configs/Map"));
        var abilities = JsonUtility.FromJson<AbilityArrayWrapper>(GameContent.Read("Configs/Abilities"));
        var errors = ContentValidator.Validate(catalog, abilities.abilities, maps.maps, path => Resources.Load<Sprite>(path) != null);
        Assert.That(errors, Is.Empty, string.Join("\n", errors));
        Assert.That(catalog.Definitions.Count, Is.EqualTo(16));
        // Baseline compatibility: all numerical fields in the original JSON remain unchanged.
        var legacy = JsonUtility.FromJson<UnitDefinitionFile>(GameContent.Read("Configs/Units"));
        foreach (var u in legacy.units)
        {

            var instance = catalog.CreateLegacyInstance(u.unit,1);
            Assert.That(instance.health, Is.EqualTo(u.health));
            Assert.That(instance.armor, Is.EqualTo(u.armor));
            Assert.That(instance.attacksPerSecond, Is.EqualTo(1f / u.attackSpeed));
            Assert.That(instance.minimumAttackDamage, Is.EqualTo(u.minimumAttackDamage));
            Assert.That(instance.maximumAttackDamage, Is.EqualTo(u.maximumAttackDamage));
            Assert.That(instance.moveSpeed, Is.EqualTo(u.moveSpeed));
            Assert.That(instance.attackRange, Is.EqualTo(u.attackRange));
        }
    }
}
