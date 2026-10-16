using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class UnitLifecycleTests
{
    [UnityTest]
    public IEnumerator UnitObjectsUseIndependentBattleStateAndLoadEveryPortrait()
    {
        var catalog = GameContent.Units;
        foreach (var definition in catalog.Definitions)
        {
            var source = new[] { new Unit { definitionId = definition.Id, placement = 1 } };
            var battle = new BattleState(catalog, source, source);
            var go = new GameObject("S02 Test Unit");
            try
            {
                var behavior = go.AddComponent<UnitBehavior>(); behavior.Initialize(battle.Player.units[0]);
                behavior.unit.health = 0;
                yield return null;
                Assert.That(battle.Enemy.units[0].health, Is.EqualTo(definition.MaxHealth));
                Assert.That(Resources.Load<Sprite>("Images/" + behavior.unit.unit), Is.Not.Null, definition.Id);
                Assert.That(Resources.Load<Sprite>("Images/" + behavior.unit.projectile), Is.Not.Null, definition.Id);
                var next = new BattleState(catalog, source, source);
                Assert.That(next.Player.units[0].health, Is.EqualTo(definition.MaxHealth));
                Assert.That(next.Player.units[0].attackCooldown, Is.Zero);
            }
            finally { Object.Destroy(go); }
            yield return null;
        }
    }
}
