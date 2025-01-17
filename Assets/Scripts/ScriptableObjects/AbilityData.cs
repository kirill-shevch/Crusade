using UnityEngine;

[CreateAssetMenu(fileName = "NewAbility", menuName = "Game/Ability Data")]
public class AbilityData : ScriptableObject
{
    public string abilityName;
    public Sprite buttonSprite;
    public Sprite effectSprite;
    public float cooldown;
    public AbilityType abilityType;
    public float effectValue; // Damage/Heal/Shield amount
    public float range;
    
    public enum AbilityType
    {
        Damage,
        Heal,
        Shield,
        Buff
    }
} 