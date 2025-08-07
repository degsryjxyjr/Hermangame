using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Implements a basic damage effect that works with both player and enemy casters.
/// </summary>
public class DamageEffect : MonoBehaviour, IAbilityEffect
{
    public bool Execute(IEntity caster, List<IDamageable> targets, AbilityDefinition abilityDefinition)
    {
        if (abilityDefinition == null)
        {
            Debug.LogWarning("DamageEffect: AbilityDefinition is null.");
            return false;
        }

        if (caster == null)
        {
            Debug.LogWarning("DamageEffect: Caster is null.");
            return false;
        }

        if (targets == null || targets.Count == 0)
        {
            Debug.Log($"DamageEffect: No targets specified for ability {abilityDefinition.abilityName}.");
            return true; // No targets is considered a successful execution
        }

        int baseDamage = abilityDefinition.baseEffectValue;
        if (baseDamage <= 0)
        {
            Debug.Log($"DamageEffect: Ability {abilityDefinition.abilityName} has no damage value.");
            return true;
        }

        bool success = false;
        List<string> damagedTargetNames = new List<string>();
        string casterName = caster.GetEntityName() ?? "Unknown";

        foreach (var target in targets)
        {
            if (target == null || !target.IsAlive())
            {
                Debug.Log("DamageEffect: Skipping null or dead target.");
                continue;
            }

            // Calculate final damage (could be modified by caster stats)
            int finalDamage = CalculateFinalDamage(caster, target, baseDamage, abilityDefinition);
            int actualDamage = target.TakeDamage(finalDamage, abilityDefinition.damageType);

            if (actualDamage > 0)
            {
                string targetName = (target as IEntity)?.GetEntityName() ?? "Unknown";
                Debug.Log($"{casterName} dealt {actualDamage} {abilityDefinition.damageType} damage to {targetName}");
                damagedTargetNames.Add(targetName);
                success = true;

                if (!target.IsAlive())
                {
                    Debug.Log($"DamageEffect: {targetName} has been defeated!");
                }
            }
        }

        // Send combat messages if caster is a player
        if (success && caster is PlayerConnection playerCaster)
        {
            SendCombatMessage(playerCaster, damagedTargetNames, abilityDefinition);
        }

        return success;
    }

    private int CalculateFinalDamage(IEntity caster, IDamageable target, int baseDamage, AbilityDefinition abilityDefinition)
    {
        // Store original damage for debugging
        int originalDamage = baseDamage;
        int finalDamage = baseDamage;

        if (abilityDefinition.attackScaling != 0f)
        {
            // Scale damage with caster's attack stat if available
            if (caster is PlayerConnection player)
            {
                finalDamage += Mathf.FloorToInt(player.Attack * abilityDefinition.attackScaling);
            }
            else if (caster is EnemyEntity enemy)
            {
                finalDamage += Mathf.FloorToInt(enemy.Attack * abilityDefinition.attackScaling);
            }
        }
        if (abilityDefinition.magicScaling != 0f)
        {
            // Scale damage with caster's magic stat if available
            if (caster is PlayerConnection player)
            {
                finalDamage += Mathf.FloorToInt(player.Magic * abilityDefinition.magicScaling);
            }
            else if (caster is EnemyEntity enemy)
            {
                finalDamage += Mathf.FloorToInt(enemy.Magic * abilityDefinition.magicScaling);
            }
        }
        finalDamage = Mathf.Max(1, finalDamage); // Ensure minimum 1 damage
        
        // Debug final calculation
        Debug.Log($"DamageEffect: Final damage calculated: {finalDamage} (Base: {originalDamage} + Modifiers: {finalDamage - originalDamage})");

        return finalDamage;
    }

    private void SendCombatMessage(PlayerConnection caster, List<string> targetNames, AbilityDefinition abilityDefinition)
    {
        string message;
        if (targetNames.Count > 1)
        {
            message = $"Your {abilityDefinition.abilityName} hit {targetNames.Count} targets";
        }
        else
        {
            message = $"Your {abilityDefinition.abilityName} hit {targetNames[0]}";
        }

        GameServer.Instance?.SendToPlayer(caster.NetworkId, new
        {
            type = "combat_message",
            message = message,
            ability = abilityDefinition.abilityName
        });
    }
}