using UnityEngine;

[System.Serializable]
public class Node
{
    public int id;
    public string type;
}

[System.Serializable]
public class UnitArrayWrapper
{
    public Unit[] units;
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
    public Node[] nodes;
    public Edge[] edges;
}
