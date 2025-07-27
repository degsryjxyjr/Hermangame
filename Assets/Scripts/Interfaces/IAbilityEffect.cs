// File: Scripts/Gameplay/Abilities/IAbilityEffect.cs (Updated)
using System.Collections.Generic;
using UnityEngine; // Add if using UnityEngine types

/// <summary>
/// Interface for scripts that implement the specific logic of an ability effect.
/// </summary>
public interface IAbilityEffect
{
    /// <summary>
    /// Executes the ability effect.
    /// </summary>
    /// <param name="caster">The player/entity casting the ability.</param>
    /// <param name="targets">The list of resolved targets for the ability (implementing IDamageable).</param>
    /// <param name="abilityDefinition">The definition providing parameters.</param>
    /// <returns>True if the effect was executed successfully, false otherwise.</returns>
    bool Execute(IEntity caster, List<IDamageable> targets, AbilityDefinition abilityDefinition);
    // Consider if targets should be a more generic IEntity list in the future for non-damage/heal effects
}