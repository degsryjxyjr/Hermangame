using UnityEngine;

// Assets/Scripts/Gameplay/Enemy.cs

public class Enemy : Entity
{
    public EnemyData data; // ScriptableObject from Data/Entities/Enemy/

    void Start()
    {
        Initialize(data);
    }

    public void Initialize(EnemyData enemyData)
    {
        data = enemyData;
        spriteRenderer.sprite = data.sprite;
        maxHealth = data.health;
        currentHealth = maxHealth;
        attackPower = data.damage;
    }
    // Enemy.cs additions
    public void Attack(PlayerCharacter target)
    {
        Debug.Log($"{name} bites {target.name}!");
        target.TakeDamage(attackPower);
    }
}
