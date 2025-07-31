// File: Scripts/Data/EnemyDefinition.cs
using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Game/Enemy")]
public class EnemyDefinition : ScriptableObject
{
    [Header("Basic Info")]
    public string enemyId;
    public string displayName;
    public Sprite icon; // For UI representation
    public GameObject modelPrefab; // The visual representation in the game world

    [Header("Base Stats")]
    public int baseHealth;
    public int baseAttack;
    public int baseDefense;
    public int baseMagic; // For magic defense or magic attack if needed


    [Header("Action Budget")]
    public int baseActions = 1;


    [Header("Scaling")]
    // If you want enemies to get stronger with level (for future encounters)
    // You can use AnimationCurves or simple multipliers like player classes
    public AnimationCurve healthGrowth = AnimationCurve.Linear(1, 1, 10, 10); // Example
    public AnimationCurve attackGrowth = AnimationCurve.Linear(1, 1, 10, 10);
    public AnimationCurve defenseGrowth = AnimationCurve.Linear(1, 1, 10, 10);
    public AnimationCurve magicGrowth = AnimationCurve.Linear(1, 1, 10, 10);

    
    // Define potential drops
    [System.Serializable] // Add this attribute to the nested class
    public class LootItem
    {
        public ItemDefinition item;
        [Range(0f, 1f)] public float dropChance = 1.0f;
        public int minQuantity = 1;
        public int maxQuantity = 1;
    }

    // --- : Reward Fields ---
    [Header("Rewards")]
    [Tooltip("Experience points awarded to the party when this enemy is defeated.")]
    public int xpOnDefeat = 0;

    [System.Serializable]
    public class LootDropEntry
    {
        [Tooltip("The item that can be dropped.")]
        public ItemDefinition item;
        [Tooltip("The chance (0.0 to 1.0) that this item will drop.")]
        [Range(0f, 1f)]
        public float dropChance = 0.1f;
        [Tooltip("Minimum amount that can drop (if item is stackable).")]
        public int minAmount = 1;
        [Tooltip("Maximum amount that can drop (if item is stackable).")]
        public int maxAmount = 1;
    }

    [Tooltip("List of potential items this enemy can drop upon defeat.")]
    public List<LootDropEntry> lootTable;
    // --- END NEW ---

    [Header("Abilities")]
    public List<AbilityDefinition> innateAbilities = new List<AbilityDefinition>(); // Abilities the enemy can use

    [Header("Equipment")]
    // If enemies can have predefined equipment
    public List<ItemDefinition> startingEquipment = new List<ItemDefinition>();

    // Add other enemy-specific data like AI behavior type, faction, etc.
    [Header("Behavior")]
    public string aiBehaviorType = "BasicMelee"; // Identifier for AI logic
    // public Faction faction; // If you have a faction system
}