// File: Scripts/Gameplay/Entities/EnemyEntity.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq; // For finding abilities/items if needed

/// <summary>
/// Represents an active enemy instance in the game world, implementing core interfaces.
/// </summary>
public class EnemyEntity : MonoBehaviour, IEntity, IDamageable, IHealable // Add IAbilityEffect target if needed
{
    [Header("Configuration")]
    [SerializeField] private EnemyDefinition _enemyDefinition;
    [SerializeField] private int _level = 1; // Instance level

    [Header("Runtime State")]
    // These could potentially be in a separate RuntimeStats class like PlayerConnection, but simpler here for now.
    public int CurrentHealth { get; private set; }
    public int MaxHealth { get; private set; }
    public int Attack { get; private set; }
    public int Defense { get; private set; }
    public int Magic { get; private set; }

    // Reference to the list of abilities this enemy instance can use
    public List<AbilityDefinition> UnlockedAbilities { get; private set; } = new List<AbilityDefinition>();

    // Reference to the list of items/equipment this enemy instance has
    // For simplicity, let's assume a basic inventory or just starting equipment applied to stats
    // A full inventory system for enemies is complex. For now, equipment modifies stats.
    // You could have a simplified EnemyInventory if needed later.

    private void Awake()
    {
        if (_enemyDefinition != null)
        {
            InitializeFromDefinition(_level);
        }
        else
        {
            Debug.LogError("EnemyEntity is missing its EnemyDefinition!", this);
            // Set default stats or disable?
            CurrentHealth = 1;
            MaxHealth = 1;
        }
    }

    /// <summary>
    /// Initializes the enemy's stats and abilities based on its definition and level.
    /// </summary>
    private void InitializeFromDefinition(int level)
    {
        // Calculate stats based on definition and level
        MaxHealth = Mathf.FloorToInt(_enemyDefinition.baseHealth * _enemyDefinition.healthGrowth.Evaluate(level));
        CurrentHealth = MaxHealth; // Start at full health
        Attack = Mathf.FloorToInt(_enemyDefinition.baseAttack * _enemyDefinition.attackGrowth.Evaluate(level));
        Defense = Mathf.FloorToInt(_enemyDefinition.baseDefense * _enemyDefinition.defenseGrowth.Evaluate(level));
        Magic = Mathf.FloorToInt(_enemyDefinition.baseMagic * _enemyDefinition.magicGrowth.Evaluate(level));

        // Copy innate abilities from the definition
        UnlockedAbilities = new List<AbilityDefinition>(_enemyDefinition.innateAbilities);

        // Apply starting equipment bonuses (if any)
        // This is a simplified version. A full equipment system would be more involved.
        foreach (var equipItem in _enemyDefinition.startingEquipment)
        {
            if (equipItem != null && equipItem.itemType == ItemDefinition.ItemType.Equipment)
            {
                // Directly apply stat bonuses from starting equipment
                Attack += equipItem.attackModifier;
                Defense += equipItem.defenseModifier;
                Magic += equipItem.magicModifier;
                // MaxHealth += equipItem.healthModifier; // If you use health modifier on equipment
                Debug.Log($"Applied starting equipment {equipItem.displayName} bonuses to {_enemyDefinition.displayName}");
            }
        }

        Debug.Log($"Initialized Enemy {_enemyDefinition.displayName} (Level {level}): " +
                  $"HP {CurrentHealth}/{MaxHealth}, ATK {Attack}, DEF {Defense}, MAG {Magic}");
    }

    // --- Implementation of IEntity ---
    public string GetEntityName()
    {
        return _enemyDefinition?.displayName ?? "Unknown Enemy";
    }

    public Transform GetPosition()
    {
        // Return the transform of this GameObject in the scene
        return this.transform;
    }
    // --- End Implementation of IEntity ---


    // --- Implementation of IDamageable ---
    public int TakeDamage(int amount, DamageType type = DamageType.Physical)
    {
        if (amount <= 0 || !IsAlive())
        {
            return 0;
        }

        int damageTaken = amount;

        // --- Damage Type Specific Calculations ---
        switch (type)
        {
            case DamageType.Physical:
                // Example: Apply defense mitigation
                damageTaken = Mathf.Max(1, damageTaken - Mathf.FloorToInt(this.Defense / 10f));
                break;
            case DamageType.Magic:
                // Example: Apply magic resistance
                damageTaken = Mathf.Max(1, damageTaken - Mathf.FloorToInt(this.Magic / 10f)); // Using Magic as resistance for now
                break;
            // Add cases for other DamageTypes as needed
            default:
                damageTaken = Mathf.Max(1, damageTaken - Mathf.FloorToInt(this.Defense / 10f));
                break;
        }
        // --- End Damage Type Calculations ---

        // Apply the calculated damage to current health
        this.CurrentHealth -= damageTaken;
        this.CurrentHealth = Mathf.Max(0, this.CurrentHealth); // Clamp to 0

        Debug.Log($"{this.GetEntityName()} took {damageTaken} {type} damage. HP: {this.CurrentHealth}/{this.MaxHealth}");

        // --- Check for Death ---
        if (!IsAlive())
        {
            Debug.Log($"{this.GetEntityName()} has been defeated!");
            // TODO: Handle enemy death
            // - Trigger OnDeath event
            // - Handle loot drops (CombatService or EncounterManager?)
            // - Inform CombatService that this enemy is no longer active
            // - Play death animation/VFX
            // - Destroy this GameObject or deactivate it
            OnDeath();
        }
        // Note: EnemyEntity typically won't send stats updates to a client like PlayerConnection does,
        // unless you have a spectator/debug view. CombatService might broadcast enemy HP changes.

        return damageTaken; // Return the actual damage dealt
    }

    public bool IsAlive()
    {
        return this.CurrentHealth > 0;
    }

    /// <summary>
    /// Handles the logic for when the enemy dies.
    /// </summary>
    private void OnDeath()
    {
        // 1. Determine loot drops
        List<ItemDefinition> droppedItems = new List<ItemDefinition>();
        // a. Add guaranteed loot
        foreach (var lootItem in _enemyDefinition.guaranteedLoot)
        {
            if (lootItem.item != null)
            {
                int quantity = Random.Range(lootItem.minQuantity, lootItem.maxQuantity + 1);
                for (int i = 0; i < quantity; i++)
                {
                    droppedItems.Add(lootItem.item);
                }
            }
        }
        // b. Roll for random loot
        foreach (var lootItem in _enemyDefinition.randomLoot)
        {
            if (lootItem.item != null && Random.value <= lootItem.dropChance)
            {
                int quantity = Random.Range(lootItem.minQuantity, lootItem.maxQuantity + 1);
                for (int i = 0; i < quantity; i++)
                {
                    droppedItems.Add(lootItem.item);
                }
            }
        }

        // 2. Notify CombatService or EncounterManager about death and loot
        // The system managing the encounter should handle distributing loot to players.
        // You might have an event or direct call.
        // Example: EncounterManager.Instance?.OnEnemyKilled(this, droppedItems);

        // 3. Trigger visual/audio feedback (animation, VFX, sound)
        // This might involve an animation controller or VFX spawner attached to this GameObject.
        // GetComponent<Animator>()?.SetTrigger("Die");
        // Instantiate(deathVFXPrefab, transform.position, Quaternion.identity);

        // 4. Remove the enemy from the game world
        // For prototype, just destroy. Later, you might pool or deactivate.
        Destroy(this.gameObject);
    }
    // --- End Implementation of IDamageable ---


    // --- Implementation of IHealable ---
    public int ReceiveHealing(int amount)
    {
        if (amount <= 0 || !IsAlive()) return 0;

        int healReceived = amount;
        int healthBefore = this.CurrentHealth;

        this.CurrentHealth = Mathf.Clamp(
            this.CurrentHealth + healReceived,
            0,
            this.MaxHealth
        );

        int actualHeal = this.CurrentHealth - healthBefore;
        Debug.Log($"{this.GetEntityName()} received {actualHeal} healing. HP: {this.CurrentHealth}/{this.MaxHealth}");

        // Note: Enemy healing typically doesn't need to notify clients unless observed.
        // If needed, EncounterManager could broadcast this.

        return actualHeal;
    }
    // --- End Implementation of IHealable ---


    // --- Helper Methods for Combat/AI ---

    /// <summary>
    /// Gets a random ability from the enemy's unlocked list that is usable in combat.
    /// This is a simple example for AI decision making.
    /// </summary>
    /// <returns>A random usable ability, or null if none are available/usable.</returns>
    public AbilityDefinition GetRandomUsableCombatAbility()
    {
        if (UnlockedAbilities == null || UnlockedAbilities.Count == 0)
        {
            return null;
        }

        // Filter for abilities usable in combat
        var usableAbilities = UnlockedAbilities.Where(a => a != null && a.usableInCombat).ToList();

        if (usableAbilities.Count == 0)
        {
            return null;
        }

        // Return a random one
        return usableAbilities[Random.Range(0, usableAbilities.Count)];
    }

    // Add more AI/Combat related methods as needed
    // e.g., GetTargetPreference(), CalculateThreatLevel(), etc.
    // These would depend on your specific AI implementation.
}