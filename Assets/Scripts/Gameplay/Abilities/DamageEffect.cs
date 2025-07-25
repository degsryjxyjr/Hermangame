// File: Scripts/Gameplay/Abilities/Effects/DamageEffect.cs
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Implements a basic damage effect based on the ability's baseEffectValue.
/// Expects baseEffectValue to be positive (e.g., 30 deals 30 damage).
/// Applies simple physical defense mitigation.
/// </summary>
public class DamageEffect : MonoBehaviour, IAbilityEffect
{
    public bool Execute(PlayerConnection caster, List<PlayerConnection> targets, AbilityDefinition abilityDefinition)
    {
        if (abilityDefinition == null)
        {
            Debug.LogWarning("DamageEffect: AbilityDefinition is null.");
            return false;
        }

        if (targets == null || targets.Count == 0)
        {
            Debug.Log($"DamageEffect: No targets specified for ability {abilityDefinition.abilityName}.");
            // Technically executed successfully (no targets to affect), but no effect.
            return true;
        }

        // baseEffectValue is expected to be positive for damage.
        int baseDamage = abilityDefinition.baseEffectValue;

        if (baseDamage <= 0)
        {
            Debug.Log($"DamageEffect: Ability {abilityDefinition.abilityName} has no damage value ({abilityDefinition.baseEffectValue}).");
            return true; // Technically executed, just no effect.
        }

        bool success = false;
        List<string> damagedTargetNames = new List<string>();

        foreach (var target in targets)
        {
            if (target == null || !target.IsAlive()) // Check if target is valid and alive
            {
                Debug.Log($"DamageEffect: Skipping null or dead target for ability {abilityDefinition.abilityName}.");
                continue;
            }

            // --- Apply Damage Calculation (Simple Physical) ---
            // Example: Damage = Max(1, BaseDamage - (Defense / 10))
            // This is a placeholder formula. You can make it more complex.
            int damageDealt = Mathf.Max(1, baseDamage - Mathf.FloorToInt(target.Defense / 10f));
            // --- End Damage Calculation ---

            int healthBefore = target.CurrentHealth;
            target.CurrentHealth = Mathf.Clamp(
                target.CurrentHealth - damageDealt,
                0,
                target.MaxHealth
            );
            int actualDamage = healthBefore - target.CurrentHealth; // Should be equal to damageDealt if not clamped

            if (actualDamage > 0)
            {
                Debug.Log($"DamageEffect: Dealt {actualDamage} damage to {target.LobbyData?.Name ?? "Unknown Target"}. New HP: {target.CurrentHealth}/{target.MaxHealth}");
                damagedTargetNames.Add(target.LobbyData?.Name ?? "Unknown Target");
                success = true; // At least one target was damaged

                // Send stats update to the damaged target's client
                GameServer.Instance.SendToPlayer(target.NetworkId, new
                {
                    type = "stats_update",
                    currentHealth = target.CurrentHealth,
                    maxHealth = target.MaxHealth
                });

                // --- Check for Target Death ---
                if (!target.IsAlive())
                {
                    Debug.Log($"DamageEffect: {target.LobbyData?.Name ?? "Unknown Target"} has been defeated!");
                    // TODO: Handle death (trigger OnDeath event, inform combat service, etc.)
                    // Example placeholder for death event
                    // target.OnDeath?.Invoke(); // If you add an event
                    // CombatService.Instance?.HandlePlayerDefeat(target.NetworkId); // Notify combat system
                }
                // --- End Check for Target Death ---
            }
            else
            {
                 // This case should be rare with Max(1, ...), but handle if damage is fully mitigated
                 Debug.Log($"DamageEffect: Damage of {baseDamage} was fully mitigated for {target.LobbyData?.Name ?? "Unknown Target"}.");
            }
        }

        // Send effect message to the caster's client
        if (success)
        {
            string targetNames = string.Join(", ", damagedTargetNames);
            string message = damagedTargetNames.Count > 1 ?
                $"You dealt {baseDamage} damage to {targetNames} (mitigated)." : // Simplified message
                $"You dealt {baseDamage} damage to {targetNames} (mitigated)."; // Simplified message

            GameServer.Instance.SendToPlayer(caster.NetworkId, new
            {
                type = "ability_effect", // Or a more specific type like "damage_effect"
                message = message
            });
        }
        else
        {
             // Optional: Send a message if no targets were damaged
             // GameServer.Instance.SendToPlayer(caster.NetworkId, new
             // {
             //     type = "ability_effect",
             //     message = "Your attack was fully resisted."
             // });
        }

        // Note: CombatService/ExecuteAbility is responsible for triggering animations/VFX
        // and deducting mana if needed, based on abilityDefinition properties.

        return success || (baseDamage > 0); // Return true if intended to damage, even if no targets were affected
    }
}