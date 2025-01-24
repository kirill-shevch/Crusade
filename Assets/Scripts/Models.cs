using UnityEngine;

[System.Serializable]
public class Node
{
    public int id;
    public string type;
    public float x;
    public float y;
    public string rewardsText;
}

[System.Serializable]
public class UnitArrayWrapper
{
    public Unit[] units;
}

[System.Serializable]
public class AbilityArrayWrapper
{
    public Ability[] abilities;
}

[System.Serializable] 
public class Unit 
{ 
    public string unit; 
    public int placement; 
    public int health; 
    public float armor; 
    public float moveSpeed; 
    public float minimumAttackDamage; 
    public float maximumAttackDamage; 
    public float attackSpeed; 
    public float attackRange; 
    public string description; 
    public int level;
    public float attackCooldown = 0f;
    public string projectile;
    public string ability;
}

[System.Serializable]
public class Edge
{
    public int from;
    public int to;
    public Unit[] squad;
}

[System.Serializable]
public class MapConfig
{
    public Map[] maps;
}

[System.Serializable]
public class Map
{
    public string name;
    public string background;
    public string arena;
    public string[] nextMaps;
    public Node[] nodes;
    public Edge[] edges;
    public string finalReward;
}

[System.Serializable]
public class Ability
{
    public string abilityName;
    public string buttonImage;
    public string effectImage;
}