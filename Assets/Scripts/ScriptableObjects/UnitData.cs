using UnityEngine;

[CreateAssetMenu(fileName = "NewUnit", menuName = "Game/Unit Data")]
public class UnitData : ScriptableObject
{
    public string unitName;
    public int baseHealth;
    public float baseArmor;
    public float baseMoveSpeed;
    public float baseMinAttackDamage;
    public float baseMaxAttackDamage;
    public float baseAttackSpeed;
    public float baseAttackRange;
    public string description;
    public Sprite unitSprite;
    public string projectilePrefab;
    public AbilityData[] abilities;
} 