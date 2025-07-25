// File: Scripts/Gameplay/Items/IEquipmentEffect.cs
using System.Collections.Generic;
using UnityEngine; // Add if using UnityEngine types like GameObject

/// <summary>
/// Interface for scripts that implement the passive effects of equipped items.
/// This allows for complex equipment effects beyond simple stat modifiers.
/// </summary>
public interface IEquipmentEffect
{
    /// <summary>
    /// Applies the passive effect of this equipment to the wearer.
    /// </summary>
    /// <param name="wearer">The player wearing the equipment.</param>
    /// <param name="itemDefinition">The definition of the item providing the effect.</param>
    void ApplyEffect(PlayerConnection wearer, ItemDefinition itemDefinition);

    /// <summary>
    /// Removes the passive effect of this equipment from the wearer.
    /// </summary>
    /// <param name="wearer">The player who was wearing the equipment.</param>
    /// <param name="itemDefinition">The definition of the item whose effect is removed.</param>
    void RemoveEffect(PlayerConnection wearer, ItemDefinition itemDefinition);
}