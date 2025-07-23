using UnityEngine;

// Assets/Scripts/Gameplay/PlayerCharacter.cs
public class PlayerCharacter : Entity
{
    [Header("Player Data")]
    public PlayerDataOLD data; // ScriptableObject from Data/Entities/Player/
    public int playerLevel;

    // public List<Item> equippedItems = new();

    void Start()
    {
        Initialize(data);
    }

    public void Initialize(PlayerDataOLD playerData)
    {
        data = playerData;
        spriteRenderer.sprite = data.baseSprite;
        maxHealth = data.baseHealth;
        currentHealth = maxHealth;
        attackPower = data.baseAttack;
    }

    public void LevelUp()
    {
        playerLevel++;
        maxHealth += data.healthPerLevel;
        // Add other stat gains
    }
    
    // PlayerCharacter.cs additions
    public void Attack(Enemy target)
    {
        Debug.Log($"{name} attacks {target.name}!");
        target.TakeDamage(attackPower);
    }
}
