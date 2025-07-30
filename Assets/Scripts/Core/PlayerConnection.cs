// File: Scripts/Core/PlayerConnection.cs

using UnityEngine;
using System.Collections.Generic;
using System.Linq;
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

    // --- Action Budget Field ---
    // Base action counts (from ClassDefinition)
    public int BaseActions { get; private set; } = 1; // Default

    // Current turn's action counts (can be modified by effects)
    public int TotalActions { get; set; } = 1;

    // Remaining actions for the current turn
    public int ActionsRemaining { get; private set; } = 1;
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
            TotalActions = 1; // Unified actions
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

        // --- Initialize Base Action Count ---
        TotalActions = ClassDefinition.baseActions;
        // Reset current budget to base values
        ResetActionBudgetForNewTurn();
        // --- END ---


        Debug.Log($"Initialized stats for {LobbyData?.Name ?? "Unknown Player"} (Level {Level} {ClassDefinition.className}): " +
                  $"HP {CurrentHealth}/{MaxHealth}, ATK {Attack}, DEF {Defense}, MAG {Magic}, Actions {TotalActions}");

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
    public int TakeDamage(int amount, AbilityDefinition.DamageType type = AbilityDefinition.DamageType.Physical)
    {
        if (amount <= 0 || !IsAlive())
        {
            return 0;
        }

        int damageTaken = amount;

        // --- Damage Type Specific Calculations ---
        switch (type)
        {
            case AbilityDefinition.DamageType.Physical:
                // Example: Apply defense mitigation
                damageTaken = Mathf.Max(1, damageTaken - Mathf.FloorToInt(this.Defense / 10f));
                break;
            case AbilityDefinition.DamageType.Magic:
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
            // sending statsUpdate so abilities are uptodate on the client side as well. 
            // This fixes a bug where equipment granting abilities isnt communicated to client.
            SendStatsUpdateToClient();
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
            // sending statsUpdate so abilities are uptodate on the client side as well. 
            // This fixes a bug where equipment granting abilities isnt communicated to client.
            SendStatsUpdateToClient();
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
        this.TotalActions += item.actionModifier;
        // this.MaxHealth += item.healthModifier; // Add if used
        Debug.Log($"Applied basic bonuses: +{item.attackModifier} ATK, +{item.defenseModifier} DEF, +{item.magicModifier} MAG, +{item.actionModifier} ACT");
        SendStatsUpdateToClient();
    }

    private void RemoveBasicStatBonuses(ItemDefinition item)
    {
        this.Attack -= item.attackModifier;
        this.Defense -= item.defenseModifier;
        this.Magic -= item.magicModifier;
        this.TotalActions -= item.actionModifier;
        // this.MaxHealth -= item.healthModifier; // Subtract if used
        this.Attack = Mathf.Max(0, this.Attack);
        this.Defense = Mathf.Max(0, this.Defense);
        this.Magic = Mathf.Max(0, this.Magic);
        this.TotalActions = Mathf.Max(0, this.TotalActions);
        // this.MaxHealth = Mathf.Max(1, this.MaxHealth); // Ensure minimum
        // this.CurrentHealth = Mathf.Min(this.CurrentHealth, this.MaxHealth);
        Debug.Log($"Removed basic bonuses: -{item.attackModifier} ATK, -{item.defenseModifier} DEF, -{item.magicModifier} MAG, -{item.actionModifier} ACT");
        SendStatsUpdateToClient();
    }

    /// <summary>
    /// Sends the player's current stats to their client.
    /// </summary>
    public void SendStatsUpdateToClient()
    {

        // --- Prepare Abilities Data ---
        var abilitiesList = new System.Collections.Generic.List<object>();
        foreach (var ability in this.UnlockedAbilities) 
        {
            // Safely add ability data, checking for nulls
            if (ability != null)
            {
                abilitiesList.Add(new {
                    id = ability.name ?? "UnknownAbility", 
                    name = ability.abilityName ?? ability.name ?? "Unnamed Ability", // Fallback chain

                    // supportedTargetTypes is List<AbilityDefinition.TargetType>
                    // Select directly converts each TargetType enum value to its string representation
                    targetTypes = ability.supportedTargetTypes != null ? 
                                  ability.supportedTargetTypes.Select(t => t.ToString()).ToArray() : 
                                  new string[] { "Unknown" }, 

                    actionCost = ability.actionCost >= 0 ? ability.actionCost : 1 // Default cost if invalid
                });
            }
            else
            {
                 // Log or handle null ability in the list if necessary
                 Debug.LogWarning("PlayerConnection.SendStatsUpdateToClient: Found null ability in UnlockedAbilities list for player " + this.NetworkId);
            }
        }

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
            magic = this.Magic,
            // Add other stats as needed
            abilities = abilitiesList
        });
    }
    // --- End Implementation of IEquipmentEffect ---

    // --- IActionBudget Implementation ---

    /// <summary>
    /// Attempts to consume actions with variable cost.
    /// </summary>
    /// <param name="cost">Action points to spend (default 1)</param>
    /// <returns>True if successful, false if insufficient actions</returns>
    public bool ConsumeAction(int cost = 1)
    {
        if (ActionsRemaining >= cost)
        {
            ActionsRemaining -= cost;
            Debug.Log($"{LobbyData?.Name} spent {cost} action(s). Remaining: {ActionsRemaining}/{TotalActions}");
            return true;
        }
        Debug.LogWarning($"{LobbyData?.Name} failed to spend {cost} action(s). Has: {ActionsRemaining}/{TotalActions}");
        return false;
    }

    /// <summary>
    /// Directly modifies available actions (positive or negative).
    /// </summary>
    public void ModifyCurrentActionBudget(int change)
    {
        int newValue = ActionsRemaining + change;
        ActionsRemaining = Mathf.Clamp(newValue, 0, TotalActions);
        Debug.Log($"Player {LobbyData?.Name}'s action budget changed by {change}. Now: {ActionsRemaining}/{TotalActions}");
    }

    /// <summary>
    /// Resets actions to full at turn start.
    /// </summary>
    public void ResetActionBudgetForNewTurn()
    {
        ActionsRemaining = TotalActions;
        Debug.Log($"Player {LobbyData?.Name}'s actions reset to {TotalActions} for new turn");
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

