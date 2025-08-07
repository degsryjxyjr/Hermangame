// File: Scripts/Gameplay/Items/Effects/BasicStatBonusEffect.cs
using UnityEngine;

/// <summary>
/// A basic IEquipmentEffect implementation that applies the standard stat modifiers
/// defined in the ItemDefinition (attackModifier, defenseModifier, etc.).
/// </summary>
public class BasicStatBonusEffect : MonoBehaviour, IEquipmentEffect
{
    public void ApplyEffect(IEntity wearer, ItemDefinition itemDefinition)
    {
        if (wearer == null || itemDefinition == null)
        {
            Debug.LogWarning("BasicStatBonusEffect.ApplyEffect: Null wearer or itemDefinition.");
            return;
        }


        // --- NEW: Attempt to cast IEntity to PlayerConnection or EnemyEntity ---
        if (wearer is PlayerConnection playerWearer)
        {
            ApplyToPlayer(playerWearer, itemDefinition);
        }
        else if (wearer is EnemyEntity enemyWearer)
        {
            ApplyToEnemy(enemyWearer, itemDefinition);
        }
        else
        {
            Debug.LogWarning($"BasicStatBonusEffect.ApplyEffect: Unsupported IEntity type: {wearer.GetType().Name}");
        }

    }

    public void RemoveEffect(IEntity wearer, ItemDefinition itemDefinition)
    {
        if (wearer == null || itemDefinition == null)
        {
            Debug.LogWarning("BasicStatBonusEffect.RemoveEffect: Null wearer or itemDefinition.");
            return;
        }


        // --- NEW: Attempt to cast IEntity to PlayerConnection or EnemyEntity ---
        if (wearer is PlayerConnection playerWearer)
        {
            RemoveFromPlayer(playerWearer, itemDefinition);
        }
        else if (wearer is EnemyEntity enemyWearer)
        {
            RemoveFromEnemy(enemyWearer, itemDefinition);
        }
        else
        {
            Debug.LogWarning($"BasicStatBonusEffect.RemoveEffect: Unsupported IEntity type: {wearer.GetType().Name}");
        }

    }
    
    // --- NEW: Private helper methods for PlayerConnection logic ---
    private void ApplyToPlayer(PlayerConnection wearer, ItemDefinition itemDefinition)
    {
        // Apply stat modifiers from the ItemDefinition
        wearer.MaxHealth += itemDefinition.maxHealthModifier; 
        wearer.Attack += itemDefinition.attackModifier;
        wearer.Defense += itemDefinition.defenseModifier;
        wearer.Magic += itemDefinition.magicModifier;
        wearer.TotalActions += itemDefinition.actionModifier; 

        
        Debug.Log($"BasicStatBonusEffect: Applied bonuses from {itemDefinition.displayName} to Player {wearer.LobbyData?.Name ?? "Unknown Player"}. " +
                  $"MaxHP+{itemDefinition.maxHealthModifier}, ATK+{itemDefinition.attackModifier}, DEF+{itemDefinition.defenseModifier}, MAG+{itemDefinition.magicModifier}, ACT+{itemDefinition.actionModifier}");

        // Send updated stats to client (PlayerConnection specific)
        wearer.SendStatsUpdateToClient();
    }

    private void RemoveFromPlayer(PlayerConnection wearer, ItemDefinition itemDefinition)
    {
        // Remove stat modifiers (subtract them)
        wearer.MaxHealth -= itemDefinition.maxHealthModifier; 
        wearer.Attack -= itemDefinition.attackModifier;
        wearer.Defense -= itemDefinition.defenseModifier;
        wearer.Magic -= itemDefinition.magicModifier;
        wearer.TotalActions -= itemDefinition.actionModifier;


        // Ensure stats don't go below zero (optional, depends on game design)
        wearer.Attack = Mathf.Max(0, wearer.Attack);
        wearer.Defense = Mathf.Max(0, wearer.Defense);
        wearer.Magic = Mathf.Max(0, wearer.Magic);
        wearer.TotalActions = Mathf.Max(0, wearer.TotalActions);
        wearer.MaxHealth = Mathf.Max(1, wearer.MaxHealth);
        // wearer.CurrentHealth = Mathf.Min(wearer.CurrentHealth, wearer.MaxHealth); // Clamp current HP

        Debug.Log($"BasicStatBonusEffect: Removed bonuses from {itemDefinition.displayName} from Player {wearer.LobbyData?.Name ?? "Unknown Player"}. " +
                  $"MaxHP-{itemDefinition.maxHealthModifier}, ATK-{itemDefinition.attackModifier}, DEF-{itemDefinition.defenseModifier}, MAG-{itemDefinition.magicModifier}, ACT-{itemDefinition.actionModifier}");

        // Send updated stats to client (PlayerConnection specific)
        wearer.SendStatsUpdateToClient();
    }

    // --- NEW: Private helper methods for EnemyEntity logic ---
    private void ApplyToEnemy(EnemyEntity wearer, ItemDefinition itemDefinition)
    {
        // Apply stat modifiers from the ItemDefinition
        wearer.MaxHealth += itemDefinition.maxHealthModifier; 
        wearer.Attack += itemDefinition.attackModifier;
        wearer.Defense += itemDefinition.defenseModifier;
        wearer.Magic += itemDefinition.magicModifier;
        wearer.TotalActions += itemDefinition.actionModifier; 
        
        Debug.Log($"BasicStatBonusEffect: Applied bonuses from {itemDefinition.displayName} to Enemy {wearer.GetEntityName()}. " +
                  $"MaxHP+{itemDefinition.maxHealthModifier}, ATK+{itemDefinition.attackModifier}, DEF+{itemDefinition.defenseModifier}, MAG+{itemDefinition.magicModifier}, ACT+{itemDefinition.actionModifier}");

        // Note: EnemyEntity might not need to send stats to a client directly,
        // or it might have its own update mechanism if needed for things like UI.
        // For now, we just log the change.
    }

    private void RemoveFromEnemy(EnemyEntity wearer, ItemDefinition itemDefinition)
    {
        // Remove stat modifiers (subtract them)
        wearer.MaxHealth -= itemDefinition.maxHealthModifier; 
        wearer.Attack -= itemDefinition.attackModifier;
        wearer.Defense -= itemDefinition.defenseModifier;
        wearer.Magic -= itemDefinition.magicModifier;
        wearer.TotalActions -= itemDefinition.actionModifier;


        // Ensure stats don't go below zero (optional, depends on game design)
        wearer.Attack = Mathf.Max(0, wearer.Attack);
        wearer.Defense = Mathf.Max(0, wearer.Defense);
        wearer.Magic = Mathf.Max(0, wearer.Magic);
        wearer.TotalActions = Mathf.Max(0, wearer.TotalActions);
        wearer.MaxHealth = Mathf.Max(1, wearer.MaxHealth);


        Debug.Log($"BasicStatBonusEffect: Removed bonuses from {itemDefinition.displayName} from Enemy {wearer.GetEntityName()}. " +
                  $"MaxHP-{itemDefinition.maxHealthModifier}, ATK-{itemDefinition.attackModifier}, DEF-{itemDefinition.defenseModifier}, MAG-{itemDefinition.magicModifier}, ACT-{itemDefinition.actionModifier}");

        // Note: EnemyEntity might not need to send stats to a client directly,
        // or it might have its own update mechanism if needed.
    }


}