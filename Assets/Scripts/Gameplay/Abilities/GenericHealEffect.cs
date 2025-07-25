// File: Scripts/Gameplay/Abilities/Effects/GenericHealEffect.cs (Updated for IDamageable/IHealable)
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Implements a heal effect based on the ability's baseEffectValue.
/// Expects baseEffectValue to be negative (e.g., -50 heals for 50 HP).
/// Works on any target implementing IHealable.
/// </summary>
public class GenericHealEffect : MonoBehaviour, IAbilityEffect
{
    // OLD SIGNATURE (causes CS1061):
    // public bool Execute(PlayerManager.PlayerConnection caster, List<PlayerManager.PlayerConnection> targets, AbilityDefinition abilityDefinition)

    // NEW SIGNATURE (correct for refactored system):
    public bool Execute(PlayerConnection caster, List<IDamageable> targets, AbilityDefinition abilityDefinition)
    {
        if (abilityDefinition == null)
        {
            Debug.LogWarning("GenericHealEffect: AbilityDefinition is null.");
            return false;
        }

        if (targets == null || targets.Count == 0)
        {
            Debug.Log($"GenericHealEffect: No targets specified for ability {abilityDefinition.abilityName}.");
            return true; // Technically executed successfully (no targets to affect)
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
            // --- CRITICAL CHANGE: Check for IHealable and IsAlive ---
            // IDamageable is a base interface. We need IHealable for healing.
            // Also check if the target is alive before attempting to heal.
            if (target is IHealable healableTarget && target.IsAlive())
            {
                // --- CALL ReceiveHealing INSTEAD OF ACCESSING .CurrentHealth ---
                int actualHeal = healableTarget.ReceiveHealing(healAmount);

                if (actualHeal > 0)
                {
                    // Get name via IEntity interface (both PlayerConnection and EnemyEntity should implement this)
                    string targetName = (target as IEntity)?.GetEntityName() ?? "Unknown Target";
                    Debug.Log($"GenericHealEffect: Healed {targetName} for {actualHeal} HP.");
                    healedTargetNames.Add(targetName);
                    success = true; // At least one target was healed

                    // --- SEND STATS UPDATE ---
                    // For PlayerConnection, ReceiveHealing should handle sending stats_update.
                    // For EnemyEntity, you might need to broadcast the change if observed.
                    // If you need to send a specific heal message:
                    // GameServer.Instance.SendToPlayer(caster.NetworkId, new { type = "ability_effect", message = $"You healed {targetName} for {actualHeal} HP." });
                }
                else
                {
                    string targetName = (target as IEntity)?.GetEntityName() ?? "Unknown Target";
                    Debug.Log($"GenericHealEffect: Heal of {healAmount} had no effect on {targetName}.");
                }
            }
            else
            {
                string targetName = (target as IEntity)?.GetEntityName() ?? "Unknown/Invalid Target";
                Debug.Log($"GenericHealEffect: Skipping target {targetName} (not IHealable or not alive).");
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
                type = "ability_effect",
                message = message
            });
        }

        return success || (healAmount > 0);
    }
}