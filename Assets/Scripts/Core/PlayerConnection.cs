// File: Scripts/Core/PlayerConnection.cs

using UnityEngine;
using System.Collections.Generic;

// --- Interfaces (Ensure these are defined in separate files) ---
// using Scripts/Core/IEntity.cs
// using Scripts/Gameplay/Entities/IDamageable.cs
// using Scripts/Gameplay/Entities/IHealable.cs
// using Scripts/Data/DamageType.cs (enum)

/// <summary>
/// Represents a connected player, holding lobby data, runtime game state,
/// and implementing core entity behaviors like taking damage and receiving healing.
/// </summary>
public class PlayerConnection : IEntity, IDamageable, IHealable, IActionBudget
{
    // --- Core Connection Fields ---
    public string NetworkId { get; }
    public string ReconnectToken { get; set; }
    public float LastActivityTime { get; set; }

    // --- Lobby Data ---
    public LobbyPlayerData LobbyData { get; set; }

    // --- Consolidated PlayerGameData Fields ---
    public PlayerClassDefinition ClassDefinition { get; set; }
    public int Level { get; set; } = 1;
    public int Experience { get; set; } = 0;

    // --- Action Budget Fields ---
    // Base action counts (from ClassDefinition)
    public int BaseMainActions { get; private set; } = 1; // Default
    public int BaseBonusActions { get; private set; } = 1; // Default

    // Current turn's action counts (can be modified by effects)
    public int MainActions { get; private set; } = 1;
    public int BonusActions { get; private set; } = 1;

    // Remaining actions for the current turn
    public int MainActionsRemaining { get; private set; } = 1;
    public int BonusActionsRemaining { get; private set; } = 1;
    // --- END Action Budget Fields ---

    // --- Flattened Runtime Stats (formerly in PlayerGameData.RuntimeStats) ---
    public int CurrentHealth { get; set; }
    public int MaxHealth { get; set; }
    public int Attack { get; set; }
    public int Defense { get; set; }
    public int Magic { get; set; }

    // --- Abilities and Inventory are managed by respective services ---
    // Keeping a reference for initialization/stats if needed, but services manage lists
    public List<AbilityDefinition> UnlockedAbilities { get; set; } = new List<AbilityDefinition>();
    // Inventory is managed by InventoryService, no direct field needed here

    // --- Reference Counting for Item-Granted Abilities ---
    // Key: AbilityDefinition, Value: Number of equipped items granting this ability
    private Dictionary<AbilityDefinition, int> _itemGrantedAbilityCounts = new Dictionary<AbilityDefinition, int>();
    // --- End Reference Counting ---


    // --- Constructor ---
    public PlayerConnection(string networkId)
    {
        NetworkId = networkId;
        // Stats will be initialized via InitializeStats() after ClassDefinition and Level are set
        UnlockedAbilities = new List<AbilityDefinition>();
        // LobbyData should be assigned after creation
    }

    // --- Method to Initialize Stats based on Class and Level ---
    /// <summary>
    /// Initializes the player's stats based on their class definition and level.
    /// Should be called after ClassDefinition and Level are set.
    /// </summary>
    public void InitializeStats()
    {
        if (ClassDefinition == null)
        {
            Debug.LogError("PlayerConnection.InitializeStats: ClassDefinition is null.");
            // Set defaults or handle error state
            MaxHealth = 100;
            CurrentHealth = MaxHealth;
            Attack = 10;
            Defense = 5;
            Magic = 10;
            // ---  Default Action Budgets ---
            BaseMainActions = 1;
            BaseBonusActions = 1;
            ResetActionBudgetForNewTurn();
            // --- END  ---
            return;
        }

        // Example calculations - adjust as needed based on your PlayerClassDefinition fields
        // Ensure growth curves (healthGrowth, attackGrowth etc.) exist in PlayerClassDefinition
        MaxHealth = Mathf.FloorToInt(ClassDefinition.baseHealth * (ClassDefinition.healthGrowth?.Evaluate(Level) ?? 1.0f));
        CurrentHealth = MaxHealth; // Start at full health
        Attack = Mathf.FloorToInt(ClassDefinition.baseAttack * (ClassDefinition.attackGrowth?.Evaluate(Level) ?? 1.0f));
        Defense = Mathf.FloorToInt(ClassDefinition.baseDefense * (ClassDefinition.defenseGrowth?.Evaluate(Level) ?? 1.0f));
        Magic = Mathf.FloorToInt(ClassDefinition.baseMagic * (ClassDefinition.magicGrowth?.Evaluate(Level) ?? 1.0f));

        // --- Initialize Base Action Counts ---
        BaseMainActions = ClassDefinition.baseMainActions;
        BaseBonusActions = ClassDefinition.baseBonusActions;
        // Reset current budget to base values
        ResetActionBudgetForNewTurn();
        // --- END ---


        Debug.Log($"Initialized stats for {LobbyData?.Name ?? "Unknown Player"} (Level {Level} {ClassDefinition.className}): " +
                  $"HP {CurrentHealth}/{MaxHealth}, ATK {Attack}, DEF {Defense}, MAG {Magic}, MainActions {BaseMainActions}, BonusActions {BaseBonusActions}");

        // sending player name, class, level and xp etc
        SendStatsUpdateToClient();
    }


    // --- Implementation of IEntity ---
    public string GetEntityName()
    {
        return this.LobbyData?.Name ?? "Unknown Player";
    }

    public Transform GetPosition()
    {
        // As discussed, this might return null or a default for now,
        // or be implemented meaningfully in specific scene contexts.
        Debug.LogWarning("GetPosition called on PlayerConnection, position tracking might be scene-specific.");
        return null; // Or return a default Transform
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
                // Example: Placeholder for magic resistance
                // damageTaken = Mathf.Max(1, damageTaken - Mathf.FloorToInt(this.MagicResistance / 10f));
                break;
            // Add cases for other DamageTypes as needed
            default:
                // Default to physical calculation
                damageTaken = Mathf.Max(1, damageTaken - Mathf.FloorToInt(this.Defense / 10f));
                break;
        }
        // --- End Damage Type Calculations ---

        // Apply the calculated damage to current health
        this.CurrentHealth -= damageTaken;
        this.CurrentHealth = Mathf.Max(0, this.CurrentHealth); // Clamp to 0

        Debug.Log($"{this.LobbyData.Name} took {damageTaken} {type} damage. HP: {this.CurrentHealth}/{this.MaxHealth}");

        // --- Send Updates to Client ---
        GameServer.Instance.SendToPlayer(this.NetworkId, new
        {
            type = "stats_update",
            currentHealth = this.CurrentHealth,
            maxHealth = this.MaxHealth
            // Add other relevant stats if they change
        });

        // --- Check for Death ---
        if (!IsAlive())
        {
            Debug.Log($"{this.LobbyData.Name} has been defeated!");
            // TODO: Handle player death (notify CombatService, trigger events, etc.)
            // CombatService.Instance?.HandlePlayerDefeat(this.NetworkId);
        }

        return damageTaken;
    }

    public bool IsAlive()
    {
        return this.CurrentHealth > 0;
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
        Debug.Log($"{this.LobbyData.Name} received {actualHeal} healing. HP: {this.CurrentHealth}/{this.MaxHealth}");

        // Send stats update
        GameServer.Instance.SendToPlayer(this.NetworkId, new
        {
            type = "stats_update",
            currentHealth = this.CurrentHealth,
            maxHealth = this.MaxHealth
        });

        return actualHeal;
    }
    // --- End Implementation of IHealable ---

    // --- Implementation of IEquipmentEffect ---

    /// <summary>
    /// Applies the effects of an equipped item to the player.
    /// This method determines the type of effect and applies it.
    /// </summary>
    /// <param name="equippedItem">The ItemDefinition of the item being equipped.</param>
    // Inside PlayerManager.PlayerConnection.cs
    public void OnEquipItem(ItemDefinition equippedItem)
    {
        if (equippedItem == null)
        {
            Debug.LogWarning("Attempted to equip a null item.");
            return;
        }

        // Get the list of IEquipmentEffect scripts
        List<IEquipmentEffect> effects = equippedItem.GetEquipmentEffects();

        if (effects.Count > 0)
        {
            // Apply each effect
            foreach (var effect in effects)
            {
                effect.ApplyEffect(this, equippedItem);
                Debug.Log($"Applied equipment effect '{effect.GetType().Name}' from {equippedItem.displayName}.");
            }
        }
        else
        {
            // Fallback to direct stat modification if no custom effects are found
            ApplyBasicStatBonuses(equippedItem);
            Debug.Log($"Applied basic stat bonuses from {equippedItem.displayName} (no custom effect scripts).");
        }
        // Note: Individual effect scripts should handle sending stats_update if needed.

        // 2. Grant Linked Abilities (with Reference Counting)
        if (equippedItem.linkedAbilities != null)
        {
            foreach (var ability in equippedItem.linkedAbilities)
            {
                if (ability != null)
                {
                    // Initialize count to 0 if not present
                    if (!_itemGrantedAbilityCounts.ContainsKey(ability))
                    {
                        _itemGrantedAbilityCounts[ability] = 0;
                    }

                    // Increment count
                    _itemGrantedAbilityCounts[ability]++;

                    // Only add to UnlockedAbilities if:
                    // 1. This is the first item granting it (count was 0, now 1)
                    // AND 2. It's not already in UnlockedAbilities
                    if (_itemGrantedAbilityCounts[ability] == 1 &&
                        !this.UnlockedAbilities.Contains(ability))
                    {
                        this.UnlockedAbilities.Add(ability);
                        Debug.Log($"Granted ability '{ability.abilityName}' from item '{equippedItem.displayName}' to player '{this.LobbyData.Name}' (RefCount: {_itemGrantedAbilityCounts[ability]})");
                    }
                    else
                    {
                        Debug.Log($"Player '{this.LobbyData.Name}' already knows ability '{ability.abilityName}'. Incremented item-grant RefCount to {_itemGrantedAbilityCounts[ability]} from item '{equippedItem.displayName}'");
                    }
                    // --- End Reference Counting Logic ---
                }
            }
        }
        // --- End Grant Linked Abilities ---
    }

    public void OnUnequipItem(ItemDefinition unequippedItem)
    {
        if (unequippedItem == null)
        {
            Debug.LogWarning("Attempted to unequip a null item.");
            return;
        }


        // 1. Remove Stat Bonuses (via IEquipmentEffect or direct)

        // Get the list of IEquipmentEffect scripts
        List<IEquipmentEffect> effects = unequippedItem.GetEquipmentEffects();
        if (effects.Count > 0)
        {
            // Remove each effect
            foreach (var effect in effects)
            {
                effect.RemoveEffect(this, unequippedItem);
                Debug.Log($"Removed equipment effect '{effect.GetType().Name}' from {unequippedItem.displayName}.");
            }
        }
        else
        {
            // Fallback to removing direct stat modification
            RemoveBasicStatBonuses(unequippedItem);
            Debug.Log($"Removed basic stat bonuses from {unequippedItem.displayName} (no custom effect scripts).");
        }
        // Note: Individual effect scripts should handle sending stats_update if needed.


        // 2. Revoke Linked Abilities (with Reference Counting)
        if (unequippedItem.linkedAbilities != null)
        {
            foreach (var ability in unequippedItem.linkedAbilities)
            {
                if (ability != null && _itemGrantedAbilityCounts.ContainsKey(ability))
                {
                    // Decrement count
                    _itemGrantedAbilityCounts[ability]--;

                    // Only remove from UnlockedAbilities if:
                    // 1. No more items grant this ability (count reached 0)
                    // AND 2. The ability wasn't granted by other means (like class)
                    if (_itemGrantedAbilityCounts[ability] <= 0)
                    {
                        _itemGrantedAbilityCounts.Remove(ability);

                        // Only remove if the ability is marked as item-granted
                        // (This is the key fix - we need to track permanent abilities separately)
                        if (ShouldRemoveAbility(ability))
                        {
                            if (this.UnlockedAbilities.Remove(ability))
                            {
                                Debug.Log($"Revoked ability '{ability.abilityName}' from player '{this.LobbyData.Name}' (was granted by item '{unequippedItem.displayName}'). No other items grant it.");
                            }
                        }
                    }
                    else
                    {
                        Debug.Log($"Decremented item-grant RefCount for ability '{ability.abilityName}' to {_itemGrantedAbilityCounts[ability]} for player '{this.LobbyData.Name}' (unequipped '{unequippedItem.displayName}'). Ability NOT removed.");
                    }
                }
            }
        }
        // --- End Revoke Linked Abilities ---

    }

    // --- Helper methods for direct stat modification (used as fallback) ---
    // These can also be the logic inside a default/standard IEquipmentEffect script like BasicStatBonusEffect.


    private bool ShouldRemoveAbility(AbilityDefinition ability)
    {
        // If the ability is part of the class's permanent abilities, don't remove it
        if (this.ClassDefinition != null &&
            this.ClassDefinition.startingAbilities.Contains(ability))
        {
            return false;
        }

        // Add other checks here if you have other ways to permanently grant abilities

        return true;
    }

    private void ApplyBasicStatBonuses(ItemDefinition item)
    {
        this.Attack += item.attackModifier;
        this.Defense += item.defenseModifier;
        this.Magic += item.magicModifier;
        // this.MaxHealth += item.healthModifier; // Add if used
        Debug.Log($"Applied basic bonuses: +{item.attackModifier} ATK, +{item.defenseModifier} DEF, +{item.magicModifier} MAG");
        SendStatsUpdateToClient();
    }

    private void RemoveBasicStatBonuses(ItemDefinition item)
    {
        this.Attack -= item.attackModifier;
        this.Defense -= item.defenseModifier;
        this.Magic -= item.magicModifier;
        // this.MaxHealth -= item.healthModifier; // Subtract if used
        this.Attack = Mathf.Max(0, this.Attack);
        this.Defense = Mathf.Max(0, this.Defense);
        this.Magic = Mathf.Max(0, this.Magic);
        // this.MaxHealth = Mathf.Max(1, this.MaxHealth); // Ensure minimum
        // this.CurrentHealth = Mathf.Min(this.CurrentHealth, this.MaxHealth);
        Debug.Log($"Removed basic bonuses: -{item.attackModifier} ATK, -{item.defenseModifier} DEF, -{item.magicModifier} MAG");
        SendStatsUpdateToClient();
    }

    /// <summary>
    /// Sends the player's current stats to their client.
    /// </summary>
    public void SendStatsUpdateToClient()
    {
        GameServer.Instance.SendToPlayer(this.NetworkId, new
        {
            type = "stats_update",
            role = this.ClassDefinition.className, //role cause class is used by C
            level = this.Level,
            experience = this.Experience,
            currentHealth = this.CurrentHealth,
            maxHealth = this.MaxHealth,
            attack = this.Attack,
            defense = this.Defense,
            magic = this.Magic
            // Add other stats as needed
        });
    }
    // --- End Implementation of IEquipmentEffect ---

    // --- IActionBudget Implementation ---
    public bool ConsumeMainAction()
    {
        if (MainActionsRemaining > 0)
        {
            MainActionsRemaining--;
            Debug.Log($"Player {LobbyData?.Name} consumed a main action. Remaining: {MainActionsRemaining}/{MainActions}");
            return true;
        }
        Debug.Log($"Player {LobbyData?.Name} tried to consume a main action, but none are left.");
        return false;
    }

    public bool ConsumeBonusAction()
    {
        if (BonusActionsRemaining > 0)
        {
            BonusActionsRemaining--;
            Debug.Log($"Player {LobbyData?.Name} consumed a bonus action. Remaining: {BonusActionsRemaining}/{BonusActions}");
            return true;
        }
        Debug.Log($"Player {LobbyData?.Name} tried to consume a bonus action, but none are left.");
        return false;
    }

    public void ModifyCurrentActionBudget(int mainChange, int bonusChange)
    {
        // Modify the current turn's total available actions
        MainActions = Mathf.Max(0, MainActions + mainChange);
        BonusActions = Mathf.Max(0, BonusActions + bonusChange);

        // Adjust remaining actions, ensuring they don't exceed the new totals
        MainActionsRemaining = Mathf.Min(MainActionsRemaining + mainChange, MainActions);
        BonusActionsRemaining = Mathf.Min(BonusActionsRemaining + bonusChange, BonusActions);

        // Ensure remaining actions don't go below zero
        MainActionsRemaining = Mathf.Max(0, MainActionsRemaining);
        BonusActionsRemaining = Mathf.Max(0, BonusActionsRemaining);

        Debug.Log($"Player {LobbyData?.Name}'s action budget modified. Main: {MainActions} ({MainActionsRemaining} rem), Bonus: {BonusActions} ({BonusActionsRemaining} rem)");
        // TODO: Potentially notify client about action budget change
    }

    public void ResetActionBudgetForNewTurn()
    {
        // Reset to base values at the start of the turn
        MainActions = BaseMainActions;
        BonusActions = BaseBonusActions;
        MainActionsRemaining = MainActions;
        BonusActionsRemaining = BonusActions;
        Debug.Log($"Player {LobbyData?.Name}'s action budget reset for new turn. Main: {MainActions}, Bonus: {BonusActions}");
        // TODO: Potentially notify client about action budget reset
    }
    // --- END: IActionBudget Implementation ---
    

}

// --- Moved Data Classes ---

[System.Serializable]
public class LobbyPlayerData
{
    public string Name;
    public string Role;
    public bool IsReady;
}

public enum DamageType
{
    Physical,
    Magic, // Or more specific types like Fire, Ice, Lightning
    // ... others
}

