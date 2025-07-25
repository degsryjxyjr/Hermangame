// File: Scripts/Gameplay/Abilities/Effects/DamageEffect.cs (Updated for IDamageable)
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Implements a basic damage effect based on the ability's baseEffectValue.
/// Expects baseEffectValue to be positive (e.g., 30 deals 30 damage).
/// Applies simple physical defense mitigation. Works on any IDamageable target.
/// </summary>
public class DamageEffect : MonoBehaviour, IAbilityEffect
{
    // OLD SIGNATURE (causes CS1061):
    // public bool Execute(PlayerManager.PlayerConnection caster, List<PlayerManager.PlayerConnection> targets, AbilityDefinition abilityDefinition)

    // NEW SIGNATURE (correct for refactored system):
    public bool Execute(PlayerConnection caster, List<IDamageable> targets, AbilityDefinition abilityDefinition)
    {
         if (abilityDefinition == null)
         {
             Debug.LogWarning("DamageEffect: AbilityDefinition is null.");
             return false;
         }

         if (targets == null || targets.Count == 0)
         {
             Debug.Log($"DamageEffect: No targets specified for ability {abilityDefinition.abilityName}.");
             return true; // Technically executed successfully (no targets to affect)
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
             // --- CRITICAL CHANGE: Check IsAlive ---
             // Cannot damage a dead target.
             if (target != null && target.IsAlive())
             {
                 // --- DAMAGE CALCULATION (Placeholder) ---
                 // This is a simplified example. You might access target stats differently
                 // or pass them through the ability definition/context.
                 // For now, assume a fixed mitigation or get it creatively.
                 // Let's assume a basic mitigation, but we don't have Defense directly from IDamageable.
                 // We could pass caster/target stats through the context or ability def if needed,
                 // or assume the baseDamage is already the final damage to apply.
                 // For prototype, let's apply base damage directly, or add simple logic if target has a known type.
                 int damageToApply = baseDamage;

                 // Example: If you know the target might have a Defense-like property indirectly,
                 // you'd need a different approach. For now, simple.
                 // You could potentially pass more data through the call chain if needed.

                 // --- CALL TakeDamage INSTEAD OF ACCESSING .CurrentHealth ---
                 // Assume Physical damage type for now, or get from abilityDef if you add that.
                 int actualDamage = target.TakeDamage(damageToApply, DamageType.Physical);

                 if (actualDamage > 0)
                 {
                     // Get name via IEntity interface
                     string targetName = (target as IEntity)?.GetEntityName() ?? "Unknown Target";
                     Debug.Log($"DamageEffect: Dealt {actualDamage} damage to {targetName}.");
                     damagedTargetNames.Add(targetName);
                     success = true; // At least one target was damaged

                     // --- CHECK FOR DEATH (TakeDamage might handle this internally, or we check IsAlive) ---
                     if (!target.IsAlive())
                     {
                         Debug.Log($"DamageEffect: {targetName} has been defeated!");
                         // TODO: Handle death (trigger events, inform EncounterManager/CombatService, loot drops)
                         // This might be handled inside the target's TakeDamage implementation (e.g., EnemyEntity.OnDeath)
                     }

                     // --- SEND STATS UPDATE ---
                     // Similar to heal, the target's TakeDamage implementation should handle sending updates
                     // if it's a PlayerConnection. For enemies, it might be handled differently.
                 }
                 else
                 {
                     string targetName = (target as IEntity)?.GetEntityName() ?? "Unknown Target";
                     Debug.Log($"DamageEffect: Damage of {damageToApply} was fully mitigated for {targetName}.");
                 }
             }
             else
             {
                 // Target is null or already dead
                 Debug.Log($"DamageEffect: Skipping null or dead target.");
             }
         }

         // Send effect message to the caster's client
         if (success)
         {
             string targetNames = string.Join(", ", damagedTargetNames);
             // Simplified message, could be more detailed
             string message = damagedTargetNames.Count > 1 ?
                 $"You attacked {targetNames}." : // Generic message, damage amount might be in stats_update or VFX
                 $"You attacked {targetNames}.";

             GameServer.Instance.SendToPlayer(caster.NetworkId, new
             {
                 type = "ability_effect",
                 message = message
             });
         }

         return success || (baseDamage > 0);
    }
}