using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Heal effect that works with both player and enemy casters and targets
/// </summary>
public class GenericHealEffect : MonoBehaviour, IAbilityEffect
{
    public bool Execute(IEntity caster, List<IDamageable> targets, AbilityDefinition abilityDefinition)
    {
        if (abilityDefinition == null)
        {
            Debug.LogWarning("GenericHealEffect: AbilityDefinition is null.");
            return false;
        }

        if (caster == null)
        {
            Debug.LogWarning("GenericHealEffect: Caster is null.");
            return false;
        }

        if (targets == null || targets.Count == 0)
        {
            Debug.Log($"GenericHealEffect: No targets specified for ability {abilityDefinition.abilityName}.");
            return true; // No targets is considered successful
        }

        int baseHeal = Mathf.Abs(abilityDefinition.baseEffectValue);
        if (baseHeal <= 0)
        {
            Debug.Log($"GenericHealEffect: Ability {abilityDefinition.abilityName} has no heal value.");
            return true;
        }

        bool success = false;
        List<string> healedTargetNames = new List<string>();
        string casterName = caster.GetEntityName() ?? "Unknown";

        foreach (var target in targets)
        {
            if (target == null || !target.IsAlive())
            {
                Debug.Log("GenericHealEffect: Skipping null or dead target.");
                continue;
            }

            if (target is IHealable healableTarget)
            {
                int finalHeal = CalculateFinalHeal(caster, target, baseHeal, abilityDefinition);
                int actualHeal = healableTarget.ReceiveHealing(finalHeal);

                if (actualHeal > 0)
                {
                    string targetName = (target as IEntity)?.GetEntityName() ?? "Unknown";
                    Debug.Log($"{casterName} healed {targetName} for {actualHeal} HP");
                    healedTargetNames.Add(targetName);
                    success = true;
                }
            }
        }

        // Send combat messages if caster is a player
        if (success && caster is PlayerConnection playerCaster)
        {
            SendHealMessage(playerCaster, healedTargetNames, baseHeal, abilityDefinition);
        }

        return success;
    }

    private int CalculateFinalHeal(IEntity caster, IDamageable target, int baseHeal, AbilityDefinition abilityDefinition)
    {
        // Basic heal calculation - can be expanded with stat modifiers
        int finalHeal = baseHeal;

        // Example: Scale healing with caster's magic stat if available
        if (caster is PlayerConnection player)
        {
            finalHeal += Mathf.FloorToInt(player.Magic * abilityDefinition.healingScaling);
        }
        else if (caster is EnemyEntity enemy)
        {
            finalHeal += Mathf.FloorToInt(enemy.Magic * abilityDefinition.healingScaling);
        }

        return Mathf.Max(1, finalHeal); // Ensure minimum 1 heal
    }

    private void SendHealMessage(PlayerConnection caster, List<string> targetNames, int healAmount, AbilityDefinition abilityDefinition)
    {
        string message;
        if (targetNames.Count > 1)
        {
            message = $"Your {abilityDefinition.abilityName} healed {targetNames.Count} targets for {healAmount} HP";
        }
        else
        {
            message = $"Your {abilityDefinition.abilityName} healed {targetNames[0]} for {healAmount} HP";
        }

        GameServer.Instance?.SendToPlayer(caster.NetworkId, new
        {
            type = "combat_message",
            message = message,
            ability = abilityDefinition.abilityName,
            isHeal = true
        });
    }
}