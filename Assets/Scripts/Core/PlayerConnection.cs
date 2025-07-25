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
public class PlayerConnection : IEntity, IDamageable, IHealable
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
            return;
        }

        // Example calculations - adjust as needed based on your PlayerClassDefinition fields
        // Ensure growth curves (healthGrowth, attackGrowth etc.) exist in PlayerClassDefinition
        MaxHealth = Mathf.FloorToInt(ClassDefinition.baseHealth * (ClassDefinition.healthGrowth?.Evaluate(Level) ?? 1.0f));
        CurrentHealth = MaxHealth; // Start at full health
        Attack = Mathf.FloorToInt(ClassDefinition.baseAttack * (ClassDefinition.attackGrowth?.Evaluate(Level) ?? 1.0f));
        Defense = Mathf.FloorToInt(ClassDefinition.baseDefense * (ClassDefinition.defenseGrowth?.Evaluate(Level) ?? 1.0f));
        Magic = Mathf.FloorToInt(ClassDefinition.baseMagic * (ClassDefinition.magicGrowth?.Evaluate(Level) ?? 1.0f));

        Debug.Log($"Initialized stats for {LobbyData?.Name ?? "Unknown Player"} (Level {Level} {ClassDefinition.className}): " +
                  $"HP {CurrentHealth}/{MaxHealth}, ATK {Attack}, DEF {Defense}, MAG {Magic}");

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
    public void OnEquipItem(ItemDefinition equippedItem)
    {
        if (equippedItem == null)
        {
            Debug.LogWarning("Attempted to equip a null item.");
            return;
        }

        // 1. Try to get a specific IEquipmentEffect script
        IEquipmentEffect customEffect = equippedItem.GetEquipmentEffect();
        if (customEffect != null)
        {
            // 2a. If found, delegate the application to the custom effect script
            customEffect.ApplyEffect(this, equippedItem);
            Debug.Log($"Applied custom equipment effect '{customEffect.GetType().Name}' from {equippedItem.displayName}.");
        }
        else
        {
            // 2b. If no custom effect, apply basic stat bonuses directly (fallback)
            // This replicates the logic that was previously in InventoryService or could be its own simple effect script.
            ApplyBasicStatBonuses(equippedItem);
            Debug.Log($"Applied basic stat bonuses from {equippedItem.displayName} (no custom effect script).");
        }

        // Note: The specific effect script (BasicStatBonusEffect or custom ones) is responsible
        // for calling SendStatsUpdateToClient() if needed.
        // If applying multiple types of effects, you might call it once at the end here instead.
        // For now, let's assume the effect script handles it.
        // SendStatsUpdateToClient(); 
    }

        /// <summary>
        /// Removes the effects of an unequipped item from the player.
        /// </summary>
        /// <param name="unequippedItem">The ItemDefinition of the item being unequipped.</param>
        public void OnUnequipItem(ItemDefinition unequippedItem)
        {
            if (unequippedItem == null)
            {
                Debug.LogWarning("Attempted to unequip a null item.");
                return;
            }

            // 1. Try to get the specific IEquipmentEffect script
            IEquipmentEffect customEffect = unequippedItem.GetEquipmentEffect();
            if (customEffect != null)
            {
                // 2a. If found, delegate the removal to the custom effect script
                // **** THIS IS THE CRITICAL CALL ****
                customEffect.RemoveEffect(this, unequippedItem);
                Debug.Log($"Removed custom equipment effect '{customEffect.GetType().Name}' from {unequippedItem.displayName}.");
            }
            else
            {
                // 2b. If no custom effect, remove basic stat bonuses directly (fallback)
                RemoveBasicStatBonuses(unequippedItem);
                Debug.Log($"Removed basic stat bonuses from {unequippedItem.displayName} (no custom effect script).");
            }
            // Note: The specific effect script handles client updates.
            SendStatsUpdateToClient(); 
        }

        // --- Helper methods for direct stat modification (used as fallback) ---
        // These can also be the logic inside a default/standard IEquipmentEffect script like BasicStatBonusEffect.

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

