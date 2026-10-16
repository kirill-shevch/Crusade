using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class CombatTests
{
    private sealed class Rolls : ICombatRandom
    {
        readonly Queue<double> values;
        public Rolls(params double[] rolls) { values=new Queue<double>(rolls); }
        public double Next01() => values.Dequeue();
    }
    static Unit Fighter(int hp=200) => new Unit { health=hp,maxHealth=hp,attacksPerSecond=2 };
    [TestCase(0,0,100)]
    [TestCase(100,0,50)]
    [TestCase(100,100,100)]
    [TestCase(100,200,100)]
    [TestCase(300,0,25)]
    public void ArmorAndPenetrationHaveExactExpectedResults(float armor,float penetration,int loss)
    {
        var a=Fighter(); var t=Fighter(); t.armor=armor; a.armorPenetration=penetration;
        var r=CombatResolver.Damage(a,t,100,new Rolls(.5,.5));
        Assert.AreEqual(loss,r.hpLost); Assert.AreEqual(200-loss,t.health);
    }
    [Test] public void CritDoublesPostArmorDamageBeforeRounding()
    {
        var a=Fighter(); var t=Fighter(); a.critChance=100; t.armor=100;
        var r=CombatResolver.Damage(a,t,3.8,new Rolls(0,0));
        Assert.AreEqual(1.9,r.postArmorDamage,1e-8); Assert.AreEqual(3,r.hpLost);
    }
    [Test] public void EvasionStopsCritAndLifesteal()
    {
        var a=Fighter(); a.health=1; a.lifeSteal=100; var t=Fighter(); t.evasion=100;
        var r=CombatResolver.Damage(a,t,100,new Rolls(.999));
        Assert.IsTrue(r.evaded); Assert.AreEqual(200,t.health); Assert.AreEqual(1,a.health);
    }
    [Test] public void ZeroChanceNeverTriggersEvenAtZeroRoll()
    {
        var a=Fighter(); var t=Fighter(); var r=CombatResolver.Damage(a,t,10,new Rolls(0,0));
        Assert.IsFalse(r.evaded); Assert.IsFalse(r.critical); Assert.AreEqual(10,r.hpLost);
    }
    [Test] public void ChanceUsesStrictBoundary()
    {
        var a=Fighter(); var t=Fighter(); t.evasion=50; a.critChance=50;
        var r=CombatResolver.Damage(a,t,10,new Rolls(.5,.499)); Assert.IsFalse(r.evaded); Assert.IsTrue(r.critical);
    }
    [Test] public void LifestealUsesActualHealthLossAndCapsHealing()
    {
        var a=Fighter(); a.health=197; a.lifeSteal=25; var t=Fighter(20);
        var r=CombatResolver.Damage(a,t,100,new Rolls(0,0));
        Assert.AreEqual(20,r.hpLost); Assert.AreEqual(3,r.hpRestored); Assert.AreEqual(200,a.health); Assert.IsTrue(r.killed);
    }
    [Test] public void OneHpVictimCannotGiveOverkillHealing()
    {
        var a=Fighter(); a.health=1; a.lifeSteal=100; var t=Fighter(1);
        var r=CombatResolver.Damage(a,t,999999,new Rolls(0,0)); Assert.AreEqual(1,r.hpRestored); Assert.AreEqual(2,a.health);
    }
    [TestCase(0,0)] [TestCase(.9,0)] [TestCase(1.9,1)]
    public void DamageRoundsDownOnceWithoutMinimum(double raw,int expected)
    {
        var t=Fighter(); Assert.AreEqual(expected,CombatResolver.Damage(Fighter(),t,raw,new Rolls(0,0)).hpLost);
    }
    [Test] public void MultiTargetsGetIndependentEvasionAndCritRolls()
    {
        var a=Fighter(); a.critChance=50; var one=Fighter(100); var two=Fighter(100); one.evasion=two.evasion=50;
        var random=new Rolls(.1,.9,.1);
        Assert.IsTrue(CombatResolver.Damage(a,one,20,random).evaded);
        Assert.AreEqual(40,CombatResolver.Damage(a,two,20,random).hpLost); Assert.AreEqual(100,one.health);
    }
    [Test] public void HealingCritIgnoresArmorEvasionAndLifesteal()
    {
        var h=Fighter(); h.health=1; h.critChance=100; h.lifeSteal=100;
        var t=Fighter(); t.health=10; t.armor=999; t.evasion=100;
        var r=CombatResolver.Heal(h,t,5.9,new Rolls(0)); Assert.AreEqual(11,r.hpRestored); Assert.AreEqual(21,t.health); Assert.AreEqual(1,h.health);
    }
    [Test] public void HealingCapsAndDoesNotResurrect()
    {
        var t=Fighter(); t.health=199;
        Assert.AreEqual(1,CombatResolver.Heal(Fighter(),t,100,new Rolls(0)).hpRestored);
        t.health=0; Assert.AreEqual(0,CombatResolver.Heal(Fighter(),t,100,new Rolls()).hpRestored);
    }
    [Test] public void DeadAttackerAndDeadTargetProduceNoFurtherDamage()
    {
        var a=Fighter(); var t=Fighter(); a.health=0;
        Assert.AreEqual(0,CombatResolver.Damage(a,t,100,new Rolls()).hpLost);
        a.health=200; t.health=0; Assert.IsFalse(CombatResolver.Damage(a,t,100,new Rolls()).killed);
    }
    [TestCase(0)] [TestCase(-1)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
    public void InvalidAttackRatesAreRejected(float rate) { Assert.Throws<ArgumentOutOfRangeException>(()=>CombatResolver.Interval(rate)); }
    [Test] public void LegacyIntervalMigrationPreservesTempo()
    {
        Assert.AreEqual(2,CombatResolver.LegacyRate(.5f)); Assert.AreEqual(.5f,CombatResolver.Interval(2));
        Assert.AreEqual(20,CombatStats.RateWithLegacyReduction(2,.9f),1e-5);
    }
    [Test] public void InvalidDamageDoesNotMutateHealth()
    {
        var a=Fighter(); var t=Fighter();
        Assert.Throws<ArgumentOutOfRangeException>(()=>CombatResolver.Damage(a,t,double.NaN,new Rolls()));
        Assert.Throws<ArgumentOutOfRangeException>(()=>CombatResolver.Damage(a,t,double.PositiveInfinity,new Rolls()));
        Assert.AreEqual(200,t.health);
    }
    [Test] public void IncreasingArmorNeverIncreasesDamage()
    {
        int previous=100;
        for(int armor=0;armor<=10000;armor+=10)
        {
            var t=Fighter(); t.armor=armor;
            int loss=CombatResolver.Damage(Fighter(),t,100,new Rolls(0,0)).hpLost;
            Assert.LessOrEqual(loss,previous); Assert.That(t.health,Is.InRange(0,t.maxHealth)); previous=loss;
        }
    }
    [Test] public void LargeFiniteStatsDoNotOverflowHealth()
    {
        var a=Fighter(); a.critChance=100; a.lifeSteal=100; a.health=1;
        var t=Fighter(); t.armor=float.MaxValue;
        var r=CombatResolver.Damage(a,t,float.MaxValue,new Rolls(0,0));
        Assert.AreEqual(200,r.hpLost); Assert.AreEqual(200,a.health); Assert.AreEqual(0,t.health);
    }
    [Test] public void BuffCalculationIsRepeatableAndDoesNotMutateSource()
    {
        var c=UnitCatalog.FromJson(Resources.Load<TextAsset>("Configs/Units").text); var source=c.CreateLegacyInstance("Knight",1);
        var first=CombatStats.Build(source,c,"Knight",new[]{"Damage","Armor"},new[]{"BullHeart","MagicPotion","OrbOfSpeed"});
        var second=CombatStats.Build(first,c,"Knight",new[]{"Damage","Armor"},new[]{"BullHeart","MagicPotion","OrbOfSpeed"});
        Assert.AreEqual(source.maxHealth+20,first.maxHealth); Assert.AreEqual(source.minimumAttackDamage+8,first.minimumAttackDamage);
        Assert.AreEqual(source.armor+3,first.armor); Assert.AreEqual(JsonUtility.ToJson(first),JsonUtility.ToJson(second));
    }
    [Test] public void ExplicitRateAndNewStatsReachInstances()
    {
        var record=new UnitDefinitionRecord{ id="test",unit="test",displayName="test",role="Warrior",faction="Unassigned",level=1,health=100,attacksPerSecond=3,projectile="test",evasion=20,critChance=30,lifeSteal=40,armorPenetration=50};
        var c=new UnitCatalog(new[]{record}); var u=c.CreateLegacyInstance("test",1);
        Assert.AreEqual(3,u.attacksPerSecond); Assert.AreEqual(20,u.evasion); Assert.AreEqual(30,u.critChance); Assert.AreEqual(40,u.lifeSteal); Assert.AreEqual(50,u.armorPenetration);
        record.evasion=101; Assert.Throws<ArgumentException>(()=>new UnitCatalog(new[]{record}));
    }
}
