// File: Scripts/Gameplay/Entities/EnemyEntity.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq; // For finding abilities/items if needed
using System;
/// <summary>
/// Represents an active enemy instance in the game world, implementing core interfaces.
/// </summary>
public class EnemyEntity : MonoBehaviour, IEntity, IDamageable, IHealable, IActionBudget // Add IAbilityEffect target if needed
{
    [Header("Configuration")]
    [SerializeField] public EnemyDefinition _enemyDefinition;
    [SerializeField] private int _level = 1; // Instance level
    

    [Header("Runtime State")]
    // These could potentially be in a separate RuntimeStats class like PlayerConnection, but simpler here for now.
    public int CurrentHealth { get; private set; }
    public int MaxHealth { get; private set; }
    public int Attack { get; private set; }
    public int Defense { get; private set; }
    public int Magic { get; private set; }


    // --- Action Budget Field ---
    // Base action count (from ClassDefinition)
    public int BaseActions { get; private set; } = 1; // Default

    // Current turn's action counts (can be modified by effects)
    public int TotalActions { get; private set; } = 1;

    // Remaining actions for the current turn
    public int ActionsRemaining { get; private set; } = 1;
    // --- END Action Budget Fields ---

    
    // Reference to the list of abilities this enemy instance can use
    public List<AbilityDefinition> UnlockedAbilities { get; private set; } = new List<AbilityDefinition>();

    // Reference to the list of items/equipment this enemy instance has
    // For simplicity, let's assume a basic inventory or just starting equipment applied to stats
    // A full inventory system for enemies is complex. For now, equipment modifies stats.
    // You could have a simplified EnemyInventory if needed later.

    // --- Reference to EncounterManager ---
    // This allows the enemy to notify the manager of its death.
    // The EncounterManager can set this when spawning the enemy.
    [HideInInspector] public EncounterManager EncounterManager { get; set; } = null;


    /// <summary>
    /// Sets the enemy definition and level, then initializes stats
    /// </summary>
    public void SetEnemyDefinition(EnemyDefinition definition, int level = 1)
    {
        _enemyDefinition = definition;
        _level = level;
        InitializeFromDefinition(level);
    }


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
    public void InitializeFromDefinition(int level)
    {
        // Calculate stats based on definition and level
        MaxHealth = Mathf.FloorToInt(_enemyDefinition.baseHealth * _enemyDefinition.healthGrowth.Evaluate(level));
        CurrentHealth = MaxHealth; // Start at full health
        Attack = Mathf.FloorToInt(_enemyDefinition.baseAttack * _enemyDefinition.attackGrowth.Evaluate(level));
        Defense = Mathf.FloorToInt(_enemyDefinition.baseDefense * _enemyDefinition.defenseGrowth.Evaluate(level));
        Magic = Mathf.FloorToInt(_enemyDefinition.baseMagic * _enemyDefinition.magicGrowth.Evaluate(level));

        // --- Initialize Action Count --
        TotalActions = _enemyDefinition?.baseActions ?? 1;
        ResetActionBudgetForNewTurn();

        Debug.Log($"Initialized enemy {_enemyDefinition.displayName} with {TotalActions} actions");
        // --- END Action Count ---


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
                int quantity = UnityEngine.Random.Range(lootItem.minQuantity, lootItem.maxQuantity + 1);
                for (int i = 0; i < quantity; i++)
                {
                    droppedItems.Add(lootItem.item);
                }
            }
        }
        // b. Roll for random loot
        foreach (var lootItem in _enemyDefinition.randomLoot)
        {
            if (lootItem.item != null && UnityEngine.Random.value <= lootItem.dropChance)
            {
                int quantity = UnityEngine.Random.Range(lootItem.minQuantity, lootItem.maxQuantity + 1);
                for (int i = 0; i < quantity; i++)
                {
                    droppedItems.Add(lootItem.item);
                }
            }
        }

        // 2. Notify EncounterManager (NEW)
        // This is the key change. The enemy tells the manager it died.
        if (EncounterManager != null)
        {
            // The EncounterManager will handle removing this enemy from its lists
            // and checking win conditions.
            EncounterManager.OnEnemyDefeatedInternal(this);
        }
        else
        {
            Debug.LogWarning($"Enemy {this.GetEntityName()} died but has no EncounterManager reference. " +
                             "It won't be removed from encounter tracking.");
            // Fallback: Destroy self
            Destroy(this.gameObject);
            return;
        }
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


    // --- NEW: IActionBudget Implementation ---


    /// <summary>
    /// Attempts to consume actions with variable cost.
    /// </summary>
    public bool ConsumeAction(int cost = 1)
    {
        try
        {
            if (cost <= 0)
            {
                throw new ArgumentException($"Invalid action cost {cost} - must be positive");
            }

            if (ActionsRemaining >= cost)
            {
                ActionsRemaining -= cost;
                Debug.Log($"{GetEntityName()} spent {cost} action(s). Remaining: {ActionsRemaining}/{TotalActions}");
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            Debug.LogError($"{GetEntityName()} action consumption error: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Safely modifies action budget with bounds checking.
    /// </summary>
    public void ModifyCurrentActionBudget(int change)
    {
        try
        {
            int newValue = ActionsRemaining + change;

            if (newValue < 0)
            {
                throw new ArgumentException(
                    $"{GetEntityName()} action budget would become negative. Change: {change}, Current: {ActionsRemaining}"
                );
            }

            ActionsRemaining = Mathf.Min(newValue, TotalActions);
            Debug.Log($"{GetEntityName()} actions: {change} → {ActionsRemaining}/{TotalActions}");
        }
        catch (ArgumentException ex)
        {
            Debug.LogError($"Action budget modification failed: {ex.Message}");
            ActionsRemaining = 0; // Fail-safe
            throw;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Unexpected error in {GetEntityName()}: {ex}");
            throw new InvalidOperationException("Action budget modification failed", ex);
        }
    }
    public void ResetActionBudgetForNewTurn()
    {
        ActionsRemaining = TotalActions;
        Debug.Log($"{GetEntityName()} actions reset to {TotalActions} for new turn");
    }
    // --- END NEW: IActionBudget Implementation ---



    // --- Helper Methods for Combat/AI ---


    /// <summary>
    /// Gets a random ability from the enemy's unlocked list that is both usable in combat
    /// and affordable given the current action budget.
    /// </summary>
    /// <returns>A random usable and affordable ability, or null if none are available.</returns>
    public AbilityDefinition GetRandomUsableCombatAbility()
    {
        if (UnlockedAbilities == null || UnlockedAbilities.Count == 0)
        {
            Debug.Log($"{GetEntityName()} has no abilities unlocked");
            return null;
        }

        // Filter abilities by:
        // 1. Must be non-null
        // 2. Must be usable in combat
        // 3. Must have action cost <= remaining actions
        var affordableAbilities = UnlockedAbilities
            .Where(a => a != null 
                        && a.usableInCombat 
                        && a.actionCost <= ActionsRemaining)
            .ToList();

        if (affordableAbilities.Count == 0)
        {
            Debug.LogWarning($"{GetEntityName()} has no affordable abilities " +
                            $"(Remaining Actions: {ActionsRemaining})");
            return null;
        }

        // Select random ability from affordable options
        int randomIndex = UnityEngine.Random.Range(0, affordableAbilities.Count);
        AbilityDefinition chosenAbility = affordableAbilities[randomIndex];
        
        Debug.Log($"{GetEntityName()} selected ability: {chosenAbility.abilityName} " +
                $"(Cost: {chosenAbility.actionCost}, Remaining: {ActionsRemaining})");
        
        return chosenAbility;
    }

    // Add more AI/Combat related methods as needed
    // e.g., GetTargetPreference(), CalculateThreatLevel(), etc.
    // These would depend on your specific AI implementation.
}