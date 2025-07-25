// Scripts/Gameplay/Abilities/IAbilityEffect.cs
using System.Collections.Generic;

public interface IAbilityEffect
{
    bool Execute(PlayerConnection caster, List<PlayerConnection> targets, AbilityDefinition abilityDefinition);
    // Consider if targets should be a more generic IInteractable or IEntity list in the future
}