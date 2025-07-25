// File: Scripts/Gameplay/Abilities/Effects/GenericHealEffect.cs
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Implements a heal effect based on the ability's baseEffectValue.
/// Expects baseEffectValue to be negative (e.g., -50 heals for 50 HP).
/// </summary>
public class GenericHealEffect : MonoBehaviour, IAbilityEffect
{
    public bool Execute(PlayerConnection caster, List<PlayerConnection> targets, AbilityDefinition abilityDefinition)
    {
        if (abilityDefinition == null)
        {
            Debug.LogWarning("GenericHealEffect: AbilityDefinition is null.");
            return false;
        }

        if (targets == null || targets.Count == 0)
        {
            Debug.Log($"GenericHealEffect: No targets specified for ability {abilityDefinition.abilityName}.");
            // Technically executed successfully (no targets to affect), but no effect.
            return true;
        }

        // baseEffectValue is expected to be negative for heals.
        // We use Abs to get the positive heal amount.
        int healAmount = Mathf.Abs(abilityDefinition.baseEffectValue);

        if (healAmount <= 0)
        {
            Debug.Log($"GenericHealEffect: Ability {abilityDefinition.abilityName} has no heal value ({abilityDefinition.baseEffectValue}).");
            return true; // Technically executed, just no effect.
        }

        bool success = false;
        List<string> healedTargetNames = new List<string>();

        foreach (var target in targets)
        {
            if (target == null || !target.IsAlive()) // Check if target is valid and alive
            {
                Debug.Log($"GenericHealEffect: Skipping null or dead target for ability {abilityDefinition.abilityName}.");
                continue;
            }

            int healthBefore = target.CurrentHealth;
            target.CurrentHealth = Mathf.Clamp(
                target.CurrentHealth + healAmount,
                0,
                target.MaxHealth
            );
            int actualHeal = target.CurrentHealth - healthBefore;

            if (actualHeal > 0)
            {
                Debug.Log($"GenericHealEffect: Healed {target.LobbyData?.Name ?? "Unknown Target"} for {actualHeal}. New HP: {target.CurrentHealth}/{target.MaxHealth}");
                healedTargetNames.Add(target.LobbyData?.Name ?? "Unknown Target");
                success = true; // At least one target was healed

                // Send stats update to the healed target's client
                GameServer.Instance.SendToPlayer(target.NetworkId, new
                {
                    type = "stats_update",
                    currentHealth = target.CurrentHealth,
                    maxHealth = target.MaxHealth
                });
            }
            else
            {
                 Debug.Log($"GenericHealEffect: Heal of {healAmount} had no effect on {target.LobbyData?.Name ?? "Unknown Target"} (already at full HP?).");
            }
        }

        // Send effect message to the caster's client
        if (success)
        {
            string targetNames = string.Join(", ", healedTargetNames);
            string message = healedTargetNames.Count > 1 ?
                $"You healed {targetNames} for {healAmount} HP each." :
                $"You healed {targetNames} for {healAmount} HP.";
                
            GameServer.Instance.SendToPlayer(caster.NetworkId, new
            {
                type = "ability_effect", // Or a more specific type like "heal_effect"
                message = message
            });
        }
        else
        {
             // Optional: Send a message if no targets were healed
             // GameServer.Instance.SendToPlayer(caster.NetworkId, new
             // {
             //     type = "ability_effect",
             //     message = "The heal had no effect."
             // });
        }


        // Note: CombatService/ExecuteAbility is responsible for triggering animations/VFX
        // and deducting mana if needed, based on abilityDefinition properties.

        return success || (healAmount > 0); // Return true if intended to heal, even if no targets were affected
    }
}