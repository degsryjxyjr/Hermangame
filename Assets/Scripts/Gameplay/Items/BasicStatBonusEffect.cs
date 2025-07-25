// File: Scripts/Gameplay/Items/Effects/BasicStatBonusEffect.cs
using UnityEngine;

/// <summary>
/// A basic IEquipmentEffect implementation that applies the standard stat modifiers
/// defined in the ItemDefinition (attackModifier, defenseModifier, etc.).
/// </summary>
public class BasicStatBonusEffect : MonoBehaviour, IEquipmentEffect
{
    public void ApplyEffect(PlayerConnection wearer, ItemDefinition itemDefinition)
    {
        if (wearer == null || itemDefinition == null)
        {
            Debug.LogWarning("BasicStatBonusEffect.ApplyEffect: Null wearer or itemDefinition.");
            return;
        }

        // Apply stat modifiers from the ItemDefinition
        wearer.Attack += itemDefinition.attackModifier;
        wearer.Defense += itemDefinition.defenseModifier;
        wearer.Magic += itemDefinition.magicModifier;
        // wearer.MaxHealth += itemDefinition.healthModifier; // Uncomment if MaxHealth modifier is used
        
        Debug.Log($"BasicStatBonusEffect: Applied bonuses from {itemDefinition.displayName} to {wearer.LobbyData?.Name ?? "Unknown Player"}. " +
                  $"ATK+{itemDefinition.attackModifier}, DEF+{itemDefinition.defenseModifier}, MAG+{itemDefinition.magicModifier}");
                  
        // Send updated stats to client
        wearer.SendStatsUpdateToClient(); // Assuming this method exists on PlayerConnection
    }

    public void RemoveEffect(PlayerConnection wearer, ItemDefinition itemDefinition)
    {
        if (wearer == null || itemDefinition == null)
        {
            Debug.LogWarning("BasicStatBonusEffect.RemoveEffect: Null wearer or itemDefinition.");
            return;
        }

        // Remove stat modifiers (subtract them)
        wearer.Attack -= itemDefinition.attackModifier;
        wearer.Defense -= itemDefinition.defenseModifier;
        wearer.Magic -= itemDefinition.magicModifier;
        // wearer.MaxHealth -= itemDefinition.healthModifier; // Uncomment if MaxHealth modifier is used

        // Ensure stats don't go below zero (optional, depends on game design)
        wearer.Attack = Mathf.Max(0, wearer.Attack);
        wearer.Defense = Mathf.Max(0, wearer.Defense);
        wearer.Magic = Mathf.Max(0, wearer.Magic);
        // wearer.MaxHealth = Mathf.Max(1, wearer.MaxHealth); // Ensure a minimum MaxHealth if needed
        // wearer.CurrentHealth = Mathf.Min(wearer.CurrentHealth, wearer.MaxHealth); // Clamp current HP

        Debug.Log($"BasicStatBonusEffect: Removed bonuses from {itemDefinition.displayName} from {wearer.LobbyData?.Name ?? "Unknown Player"}. " +
                  $"ATK-{itemDefinition.attackModifier}, DEF-{itemDefinition.defenseModifier}, MAG-{itemDefinition.magicModifier}");

        // Send updated stats to client
        wearer.SendStatsUpdateToClient(); // Assuming this method exists on PlayerConnection
    }
}