// File: Scripts/Core/PlayerConnection.cs

using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;


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

    // --- Consolidated player game data fields ---
    public PlayerClassDefinition ClassDefinition { get; set; }
    public Party CurrentParty { get; set; }

    // --- Action Budget Field ---
    // Base action counts (from ClassDefinition)
    public int BaseActions { get; private set; } = 1; // Default

    // Current turn's action counts (can be modified by effects)
    public int TotalActions { get; set; } = 1;

    // Remaining actions for the current turn
    public int ActionsRemaining { get; private set; } = 1;
    // --- END Action Budget Fields ---

    // --- Flattened Runtime Stats  ---
    public int CurrentHealth { get; set; }
    public int MaxHealth { get; set; }
    public int Attack { get; set; }
    public int Defense { get; set; }
    public int Magic { get; set; }

    // --- Ability Lists ---
    /// <summary>
    /// Abilities granted permanently to the player (e.g., class starting abilities, level-ups, quests).
    /// These are not affected by equipping/unequipping items.
    /// </summary>
    public List<AbilityDefinition> PermanentAbilities { get; private set; } = new List<AbilityDefinition>();

    /// <summary>
    /// Abilities granted temporarily by equipped items.
    /// These are added when an item is equipped and removed when it (or all items granting it) is unequipped.
    /// </summary>
    public List<AbilityDefinition> TransientAbilities { get; private set; } = new List<AbilityDefinition>();


    // --- UnlockedAbilities as a Computed Property ---
    /// <summary>
    /// Gets a combined list of all abilities the player currently has unlocked,
    /// including both permanent and transient abilities.
    /// This property dynamically combines PermanentAbilities and TransientAbilities,
    /// ensuring no duplicates. Use PermanentAbilities or TransientAbilities directly
    /// for adding/removing based on source.
    /// </summary>
    public List<AbilityDefinition> UnlockedAbilities
    {
        get
        {
            // Create a new list starting with permanent abilities
            var allAbilities = new List<AbilityDefinition>(PermanentAbilities);
            
            // Add transient abilities, avoiding duplicates
            foreach (var transientAbility in TransientAbilities)
            {
                // Use ReferenceEquals or a unique ID check if object identity is problematic
                // For ScriptableObjects, direct comparison usually works if it's the same asset instance.
                if (transientAbility != null && !allAbilities.Contains(transientAbility)) 
                {
                    allAbilities.Add(transientAbility);
                }
            }
            return allAbilities;
        }
    }
    // --- END UnlockedAbilities ---



    public int GetActionsRemaining() => ActionsRemaining;
    public int GetTotalActions() => TotalActions;

    // --- Constructor ---
    public PlayerConnection(string networkId)
    {
        NetworkId = networkId;
        LastActivityTime = Time.time;
        ReconnectToken = Guid.NewGuid().ToString();
        // Stats will be initialized via InitializeStats() after ClassDefinition and Level are set

        // Initialize lists
        PermanentAbilities = new List<AbilityDefinition>();
        TransientAbilities = new List<AbilityDefinition>();
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
            Attack = 1;
            Defense = 1;
            Magic = 1;
            // ---  Default Action Budgets ---
            TotalActions = 1; // Unified actions
            ResetActionBudgetForNewTurn();
            // --- END  ---
            return;
        }

        // Example calculations - adjust as needed based on your PlayerClassDefinition fields
        // Ensure growth curves (healthGrowth, attackGrowth etc.) exist in PlayerClassDefinition
        MaxHealth = ClassDefinition.baseHealth;
        CurrentHealth = MaxHealth; // Start at full health
        Attack = ClassDefinition.baseAttack;
        Defense = ClassDefinition.baseDefense;
        Magic = ClassDefinition.baseMagic;

        // --- Initialize Base Action Count ---
        TotalActions = ClassDefinition.baseActions;
        // Reset current budget to base values
        ResetActionBudgetForNewTurn();
        // --- END ---

        // --- Initialize Base Abilities ---
        foreach (AbilityDefinition ability in ClassDefinition.startingAbilities)
        {
            // adding each ability in the list
            AddPermanentAbility(ability);
        }



        Debug.Log($"Initialized stats for {LobbyData?.Name ?? "Unknown Player"} (Level {CurrentParty.Level} {ClassDefinition.className}): " +
                  $"HP {CurrentHealth}/{MaxHealth}, ATK {Attack}, DEF {Defense}, MAG {Magic}, Actions {TotalActions}");

        // sending player name, class, level and xp etc
        SendStatsUpdateToClient();
    }

    // Called on checkLevelUp from Party
    public void LevelUp(int level)
    {
        // double checking the levelUp is valid
        if (CurrentParty.Level != level)
        {
            Debug.Log($"Level Up to lvl.{level} for {LobbyData?.Name ?? "Unknown Player"} failed. Doesnt match party level!");
            return;
        }
        // getting the lvl up data
        LevelUpBonusData lvlUpData = ClassDefinition.GetBonusForLevel(level);
        if (lvlUpData == null)
        {
            Debug.LogError($"PlayerConnection.LevelUp: ClassDefinition.levelUpBonuses[{level}] is null.");
            return;
        }

        MaxHealth += lvlUpData.maxHealthBonus;
        // CurrentHealth = MaxHealth; // Keep currentHeal the same. Might change in the future
        Attack += lvlUpData.attackBonus;
        Defense += lvlUpData.defenseBonus;
        Magic += lvlUpData.magicBonus;

        // --- Initialize Base Action Count ---
        TotalActions += lvlUpData.actionBonus;
        // Reset current budget to base values
        ResetActionBudgetForNewTurn();
        // --- END ---

        // adding abilities
        foreach (AbilityDefinition ability in lvlUpData.newAbilities)
        {
            // adding each ability in the list
            AddPermanentAbility(ability);
        }

        // adding items
        foreach (ItemDefinition item in lvlUpData.grantedItems)
        {
            // addding each item in the grantedItems list
            InventoryService.Instance.AddItem(this.NetworkId, item);
        }

        Debug.Log($"Level Up! Lvl.{level} stats for {LobbyData?.Name ?? "Unknown Player"} (Level {CurrentParty.Level} {ClassDefinition.className}): " +
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


    // --- Ability Management---

    /// <summary>
    /// Adds an ability to the player's permanently unlocked list.
    /// This ability persists regardless of equipment changes.
    /// If the ability is already granted transiently, it is removed from the transient list.
    /// </summary>
    /// <param name="ability">The AbilityDefinition to grant permanently.</param>
    /// <param name="sendUpdate">If true, sends a stats update to the client. Default is true.</param>
    public void AddPermanentAbility(AbilityDefinition ability, bool sendUpdate = true)
    {
        if (ability == null)
        {
            Debug.LogWarning($"PlayerConnection ({LobbyData?.Name}): Attempted to add a null permanent ability.");
            return;
        }

        if (!PermanentAbilities.Contains(ability))
        {
            PermanentAbilities.Add(ability);
            Debug.Log($"PlayerConnection ({LobbyData?.Name}): Permanently granted ability '{ability.abilityName}'.");

            // --- NEW: Handle Edge Case ---
            // If this ability was also granted transiently, remove the transient version
            // as the permanent one now covers it.
            if (TransientAbilities.Contains(ability))
            {
                // Note: We don't call RemoveTransientAbility here because that method
                // has logic to check if OTHER items grant it, which isn't relevant.
                // We are forcibly removing it because it's now permanent.
                if (TransientAbilities.Remove(ability))
                {
                    Debug.Log($"PlayerConnection ({LobbyData?.Name}): Removed transient version of '{ability.abilityName}' as it is now permanently granted.");
                    // Removing it from Transient list might warrant a UI update
                    // if transient abilities are displayed differently or affect action costs differently (unlikely, but possible).
                    // For consistency, we should trigger an update if requested.
                }
            }
            // --- END NEW ---
        }
        else
        {
            Debug.Log($"PlayerConnection ({LobbyData?.Name}): Ability '{ability.abilityName}' is already permanently granted.");
        }

        // Optionally send an update to the client so the new ability appears in the UI
        // This also ensures the client's view of abilities is consistent.
        if (sendUpdate)
        {
            SendStatsUpdateToClient(); // This will now include the updated Permanent/Transient lists
        }
    }

    /// <summary>
    /// Removes an ability from the player's permanently granted list.
    /// Note: This does NOT automatically remove it from the effective UnlockedAbilities list
    /// if it's also granted by a transient source (e.g., an equipped item).
    /// </summary>
    /// <param name="ability">The AbilityDefinition to revoke.</param>
    /// <param name="sendUpdate">If true, sends a stats update to the client. Default is true.</param>
    public void RemovePermanentAbility(AbilityDefinition ability, bool sendUpdate = true)
    {
        if (ability == null)
        {
            Debug.LogWarning($"PlayerConnection ({LobbyData?.Name}): Attempted to remove a null permanent ability.");
            return;
        }

        if (PermanentAbilities.Contains(ability))
        {
            PermanentAbilities.Remove(ability);
            Debug.Log($"PlayerConnection ({LobbyData?.Name}): Permanently revoked ability '{ability.abilityName}'.");

            // Optionally send an update to the client
            // The UnlockedAbilities property will now reflect the change if it wasn't also transient
            if (sendUpdate)
            {
                SendStatsUpdateToClient(); 
            }
        }
        else
        {
             Debug.Log($"PlayerConnection ({LobbyData?.Name}): Ability '{ability.abilityName}' was not found in permanently granted list.");
        }
    }

    /// <summary>
    /// Adds an ability to the player's transient (item-granted) list.
    /// Checks if the ability is already present to avoid duplicates.
    /// </summary>
    /// <param name="ability">The AbilityDefinition to grant temporarily.</param>
    /// <param name="sourceItemName">The name of the item granting the ability (for logging).</param>
    /// <param name="sendUpdate">If true, sends a stats update to the client. Default is true.</param>
    private void AddTransientAbility(AbilityDefinition ability, string sourceItemName, bool sendUpdate = true)
    {
        if (ability == null)
        {
            Debug.LogWarning($"PlayerConnection ({LobbyData?.Name}): Attempted to add a null transient ability from item '{sourceItemName}'.");
            return;
        }
        // checking if the ability is already permanently granted
        if (PermanentAbilities.Contains(ability))
        {
            Debug.Log($"PlayerConnection ({LobbyData?.Name}): Already has the permanent version of ability '{ability.abilityName}' (attempted grant from item '{sourceItemName}').");
            return;
        }
        if (!TransientAbilities.Contains(ability))
        {
            TransientAbilities.Add(ability);
            Debug.Log($"PlayerConnection ({LobbyData?.Name}): Granted transient ability '{ability.abilityName}' from item '{sourceItemName}'.");
            if (sendUpdate)
            {
                SendStatsUpdateToClient();
            }
        }
        else
        {
            Debug.Log($"PlayerConnection ({LobbyData?.Name}): Already has transient ability '{ability.abilityName}' (attempted grant from item '{sourceItemName}').");
        }
    }

    /// <summary>
    /// Removes an ability from the player's transient (item-granted) list.
    /// Checks if another equipped item still grants the ability before removing.
    /// </summary>
    /// <param name="ability">The AbilityDefinition to revoke.</param>
    /// <param name="sourceItemName">The name of the item being unequipped (for logging).</param>
    /// <param name="sendUpdate">If true, sends a stats update to the client. Default is true.</param>
    private void RemoveTransientAbility(AbilityDefinition ability, string sourceItemName, bool sendUpdate = true)
    {
        if (ability == null)
        {
            Debug.LogWarning($"PlayerConnection ({LobbyData?.Name}): Attempted to remove a null transient ability related to item '{sourceItemName}'.");
            return;
        }

        // Check if ANY OTHER equipped item grants this ability
        bool isGrantedByOtherItem = IsAbilityGrantedByOtherEquippedItem(ability, sourceItemName);

        if (!isGrantedByOtherItem)
        {
            // No other item grants it, so remove it from TransientAbilities
            if (TransientAbilities.Remove(ability))
            {
                Debug.Log($"PlayerConnection ({LobbyData?.Name}): Revoked transient ability '{ability.abilityName}' (unequipped '{sourceItemName}'). No other items grant it.");
                if (sendUpdate)
                {
                    SendStatsUpdateToClient();
                }
            }
            else
            {
                Debug.Log($"PlayerConnection ({LobbyData?.Name}): Attempted to revoke transient ability '{ability.abilityName}' (unequipped '{sourceItemName}'), but it was not found in the list.");
            }
        }
        else
        {
            Debug.Log($"PlayerConnection ({LobbyData?.Name}): Transient ability '{ability.abilityName}' NOT revoked (unequipped '{sourceItemName}') because another equipped item grants it.");
        }
    }

    /// <summary>
    /// Helper method to check if an ability is granted by any other currently equipped item.
    /// </summary>
    /// <param name="ability">The ability to check.</param>
    /// <param name="excludingItemName">The name of the item to exclude from the check (typically the one being unequipped).</param>
    /// <returns>True if another equipped item grants the ability, false otherwise.</returns>
    private bool IsAbilityGrantedByOtherEquippedItem(AbilityDefinition ability, string excludingItemName)
    {
        // This requires access to the player's equipped items, likely through InventoryService
        if (InventoryService.Instance == null)
        {
            Debug.LogError("PlayerConnection.IsAbilityGrantedByOtherEquippedItem: InventoryService.Instance is null.");
            return false; // Assume no other item grants it to be safe
        }

        // Get the player's equipped items
        PlayerInventory playerInventory = InventoryService.Instance.GetPlayerInventory(this.NetworkId);

        List<InventorySlot> equippedItemSlots = playerInventory.GetAllEquippedItems();

        List<ItemDefinition> equippedItemDefs = new List<ItemDefinition>();

        foreach (var itemSlot in equippedItemSlots)
        {
            equippedItemDefs.Add(itemSlot.ItemDef);
        }

        if (equippedItemDefs == null || equippedItemDefs.Count == 0)
        {
            return false; // No items equipped, so no other item grants it
        }

        foreach (var equippedItemDef in equippedItemDefs)
        {
            if (equippedItemDef != null && equippedItemDef.displayName != excludingItemName) // Exclude the item being unequipped
            {
                // Check if this equipped item grants the ability
                if (equippedItemDef.linkedAbilities != null && equippedItemDef.linkedAbilities.Contains(ability))
                {
                    return true; // Found another item granting the ability
                }
            }
        }
        return false; // No other equipped item grants this ability
    }

    /// <summary>
    /// Applies the effects of an equipped item to the player.
    /// Specifically handles adding abilities granted by the item.
    /// Assumes item stat effects are handled by IEquipmentEffect scripts.
    /// </summary>
    /// <param name="equippedItem">The ItemDefinition of the item being equipped.</param>
    public void OnEquipItem(ItemDefinition equippedItem)
    {
        if (equippedItem == null)
        {
            Debug.LogWarning("PlayerConnection.OnEquipItem: Attempted to equip a null item.");
            return;
        }

        Debug.Log($"PlayerConnection.OnEquipItem: Equipping item '{equippedItem.displayName}' for player '{LobbyData?.Name}'.");

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
        // Note: Individual effect scripts should handle sending stats_update if needed.

        // --- Handle Abilities Granted by Item ---
        if (equippedItem.linkedAbilities != null && equippedItem.linkedAbilities.Count > 0)
        {
            foreach (var ability in equippedItem.linkedAbilities)
            {
                if (ability != null)
                {
                    // Use the new centralized method
                    AddTransientAbility(ability, equippedItem.displayName, sendUpdate: false); // Don't send update yet
                }
            }
            // Send one stats update after processing all abilities from the item
            SendStatsUpdateToClient();
        }
    }


    /// <summary>
    /// Removes the effects of an unequipped item from the player.
    /// Specifically handles removing abilities granted by the item (if no other item grants them).
    /// Assumes item stat effects are handled by IEquipmentEffect scripts.
    /// </summary>
    /// <param name="unequippedItem">The ItemDefinition of the item being unequipped.</param>
    public void OnUnequipItem(ItemDefinition unequippedItem)
    {
        if (unequippedItem == null)
        {
            Debug.LogWarning("PlayerConnection.OnUnequipItem: Attempted to unequip a null item.");
            return;
        }

        Debug.Log($"PlayerConnection.OnUnequipItem: Unequipping item '{unequippedItem.displayName}' for player '{LobbyData?.Name}'.");

        // 1. Remove Stat Bonuses (via IEquipmentEffect)
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
        // --- Handle Abilities Revoked by Item ---
        if (unequippedItem.linkedAbilities != null && unequippedItem.linkedAbilities.Count > 0)
        {
            foreach (var ability in unequippedItem.linkedAbilities)
            {
                if (ability != null)
                {
                    // Use the new centralized method
                    RemoveTransientAbility(ability, unequippedItem.displayName, sendUpdate: false); // Don't send update yet
                }
            }
            // Send one stats update after processing all abilities from the item
            SendStatsUpdateToClient();
        }
    }


    // --- Communication with Client ---
    /// <summary>
    /// Sends the player's current stats (health, attack, etc.) and abilities to the client.
    /// Updated to send Permanent and Transient abilities separately.
    /// </summary>
    public void SendStatsUpdateToClient()
    {
        if (GameServer.Instance == null)
        {
            Debug.LogError("PlayerConnection.SendStatsUpdateToClient: GameServer.Instance is null.");
            return;
        }

        // Prepare base stats data
        var statsData = new Dictionary<string, object>
        {
            ["type"] = "stats_update",
            ["role"] = this.ClassDefinition?.className ?? "UnknownClass",
            ["level"] = this.CurrentParty?.Level ?? 1,
            ["experience"] = this.CurrentParty?.TotalExperience ?? 0,
            ["currentHealth"] = this.CurrentHealth,
            ["maxHealth"] = this.MaxHealth,
            ["attack"] = this.Attack,
            ["defense"] = this.Defense,
            ["magic"] = this.Magic,
            ["totalActions"] = this.TotalActions,
            ["actionsRemaining"] = this.ActionsRemaining
        };
        
        // list for all abilities
        var abilityData = new List<Dictionary<string, object>>();
        // Prepare Permanent Abilities data
        if (PermanentAbilities != null)
        {
            foreach (var ability in PermanentAbilities)
            {
                if (ability != null)
                {
                    abilityData.Add(new Dictionary<string, object>
                    {
                        ["id"] = ability.name, // Or a unique ID
                        ["name"] = ability.abilityName,
                        ["targetTypes"] = ability.supportedTargetTypes?.Select(t => t.ToString()).ToArray() ?? new string[] { "Unknown" },
                        ["actionCost"] = ability.actionCost >= 0 ? ability.actionCost : 1,
                        ["iconPath"] = ability.icon != null ? $"images/icons/{ability.icon.name}.png" : "images/icons/default-ability.png"
                        // Add other relevant ability properties for the client
                    });
                }
                else
                {
                    Debug.LogWarning("PlayerConnection.SendStatsUpdateToClient: Found null ability in PermanentAbilities list.");
                }
            }
        }

        // Prepare Transient Abilities data
        if (TransientAbilities != null)
        {
            foreach (var ability in TransientAbilities)
            {
                if (ability != null)
                {
                    abilityData.Add(new Dictionary<string, object>
                    {
                        ["id"] = ability.name, // Or a unique ID
                        ["name"] = ability.abilityName,
                        ["targetTypes"] = ability.supportedTargetTypes?.Select(t => t.ToString()).ToArray() ?? new string[] { "Unknown" },
                        ["actionCost"] = ability.actionCost >= 0 ? ability.actionCost : 1,
                        ["iconPath"] = ability.icon != null ? $"images/icons/{ability.icon.name}.png" : "images/icons/default-ability.png"
                        // Add other relevant ability properties for the client
                    });
                }
                else
                {
                    Debug.LogWarning("PlayerConnection.SendStatsUpdateToClient: Found null ability in TransientAbilities list.");
                }
            }
        }

        statsData["abilities"] = abilityData;

        // Send the combined data to the player's client
        GameServer.Instance.SendToPlayer(this.NetworkId, statsData);
        Debug.Log($"PlayerConnection.SendStatsUpdateToClient: Sent stats and abilities update to player {LobbyData?.Name}.");
    }

    
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

