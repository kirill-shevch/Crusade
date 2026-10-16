using System;

public interface ICombatRandom { double Next01(); }
public sealed class SeededCombatRandom : ICombatRandom
{
    private readonly Random random;
    public SeededCombatRandom(int seed) { random = new Random(seed); }
    public double Next01() => random.NextDouble();
}
public struct CombatResult
{
    public bool evaded, critical, killed;
    public double rawDamage, postArmorDamage, finalDamage;
    public int hpLost, hpRestored;
}

// HP is integer; all intermediate calculations use double, rounded only at HP application.
public static class CombatResolver
{
    public static double NonNegative(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0) throw new ArgumentOutOfRangeException(nameof(value));
        return value;
    }
    public static float Interval(float attacksPerSecond)
    {
        NonNegative(attacksPerSecond);
        if (attacksPerSecond == 0 || double.IsInfinity(1.0 / attacksPerSecond) || 1.0 / attacksPerSecond > float.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(attacksPerSecond));
        return 1f / attacksPerSecond;
    }
    public static float LegacyRate(float intervalSeconds)
    {
        var result = Interval(intervalSeconds);
        if (float.IsInfinity(result) || result <= 0) throw new ArgumentOutOfRangeException(nameof(intervalSeconds));
        return result;
    }
    static bool Roll(float percent, ICombatRandom random)
    {
        NonNegative(percent);
        if (percent > 100) throw new ArgumentOutOfRangeException(nameof(percent));
        double roll = random.Next01();
        if (double.IsNaN(roll) || roll < 0 || roll >= 1) throw new ArgumentOutOfRangeException(nameof(random));
        return roll < percent / 100.0;
    }
    static int Amount(double value, int limit) => (int)Math.Min(Math.Max(0,limit), Math.Floor(NonNegative(value)));
    static void Health(UnitInstance unit)
    {
        if (unit == null || unit.maxHealth <= 0 || unit.health < 0 || unit.health > unit.maxHealth)
            throw new ArgumentException("Invalid health state");
    }
    public static CombatResult Damage(UnitInstance attacker, UnitInstance target, double rawDamage, ICombatRandom random)
    {
        Health(attacker); Health(target); NonNegative(rawDamage);
        NonNegative(target.armor); NonNegative(attacker.armorPenetration); NonNegative(attacker.lifeSteal);
        if (attacker.lifeSteal > 100) throw new ArgumentOutOfRangeException(nameof(attacker.lifeSteal));
        var result = new CombatResult { rawDamage = rawDamage };
        if (attacker.health == 0 || target.health == 0) return result;
        if (Roll(target.evasion,random)) { result.evaded = true; return result; }
        double armor = Math.Max(0,(double)target.armor-attacker.armorPenetration);
        result.postArmorDamage = rawDamage / (1 + armor / 100);
        result.critical = Roll(attacker.critChance,random);
        result.finalDamage = result.postArmorDamage * (result.critical ? 2 : 1);
        // Validate before mutating either participant.
        result.hpLost = Amount(result.finalDamage,target.health);
        result.hpRestored = Amount(result.hpLost * (attacker.lifeSteal / 100.0),attacker.maxHealth-attacker.health);
        target.health -= result.hpLost;
        attacker.health += result.hpRestored;
        result.killed = target.health == 0;
        return result;
    }
    public static CombatResult Heal(UnitInstance healer, UnitInstance target, double rawHeal, ICombatRandom random)
    {
        Health(healer); Health(target); NonNegative(rawHeal);
        var result = new CombatResult { rawDamage = rawHeal };
        if (healer.health == 0 || target.health == 0) return result; // Healing cannot resurrect.
        result.critical = Roll(healer.critChance,random);
        result.finalDamage = rawHeal * (result.critical ? 2 : 1);
        result.hpRestored = Amount(result.finalDamage,target.maxHealth-target.health);
        target.health += result.hpRestored;
        return result;
    }
}

public static class CombatStats
{
    // Compatibility for prototype effects expressed as reductions of the old interval.
    // 50 ms bounds legacy RapidFire/stacked Orbs that previously produced negative intervals.
    public static float RateWithLegacyReduction(float rate, float seconds) =>
        CombatResolver.LegacyRate(Math.Max(0.05f,CombatResolver.Interval(rate)-seconds));
    public static Unit Build(Unit source, UnitCatalog catalog, string hero, string[] buffs, string[] permanent)
    {
        var unit = catalog.Resolve(source).CreateInstance(source.placement,source.instanceId);
        if (unit.unit != hero) return unit;
        foreach (var buff in buffs)
        {
            if (buff == "Damage") { unit.minimumAttackDamage += 5; unit.maximumAttackDamage += 5; }
            else if (buff == "Armor") unit.armor += 3;
        }
        float reduction = 0;
        foreach (var buff in permanent)
        {
            if (buff == "BullHeart") unit.maxHealth += 15;
            else if (buff == "OrbOfSpeed") reduction += .2f;
            else if (buff == "MagicPotion") { unit.minimumAttackDamage += 3; unit.maximumAttackDamage += 3; unit.maxHealth += 5; }
        }
        if (reduction > 0) unit.attacksPerSecond = RateWithLegacyReduction(unit.attacksPerSecond,reduction);
        unit.health = unit.maxHealth;
        return unit;
    }
}
