// File: Scripts/Core/Services/AbilityExecutionService.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq; // For potential targeting logic

/// <summary>
/// Central service for executing ability effects, handling targeting, costs, and context checks.
/// Used by CombatService, InventoryService, and potentially direct spell casting.
/// </summary>
public class AbilityExecutionService : MonoBehaviour
{
    public static AbilityExecutionService Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"Duplicate AbilityExecutionService instance detected! Destroying {gameObject.name}");
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Debug.Log("AbilityExecutionService initialized and set as singleton");
    }

    /// <summary>
    /// Context in which an ability is being executed.
    /// </summary>
    public enum AbilityContext
    {
        OutOfCombat, // E.g., using an item, casting a utility spell
        InCombat,    // E.g., using an ability during a combat turn
        Always       // Ability doesn't care about context (rare, but possible)
    }

    /// <summary>
    /// Executes an ability, handling targeting, effects, and context checks.
    /// This is the central hub for ability logic.
    /// </summary>
    /// <param name="caster">The player/entity casting the ability.</param>
    /// <param name="targets">The list of resolved targets for the ability.</param>
    /// <param name="abilityDefinition">The ability to execute.</param>
    /// <param name="context">The context in which the ability is being used.</param>
    /// <returns>True if executed successfully, false otherwise.</returns>
    public bool ExecuteAbility(PlayerConnection caster, List<IDamageable> targets, AbilityDefinition abilityDefinition, AbilityContext context)
    {
        if (abilityDefinition == null)
        {
            Debug.LogWarning("ExecuteAbility called with null AbilityDefinition.");
            return false;
        }

        if (caster == null)
        {
            Debug.LogWarning("ExecuteAbility called with null caster.");
            return false;
        }

        // --- Context Validation ---
        bool canUseInContext = false;
        switch (context)
        {
            case AbilityContext.OutOfCombat:
                canUseInContext = abilityDefinition.usableOutOfCombat;
                break;
            case AbilityContext.InCombat:
                canUseInContext = abilityDefinition.usableInCombat;
                break;
            case AbilityContext.Always:
                canUseInContext = true;
                break;
        }

        if (!canUseInContext)
        {
            Debug.LogWarning($"Ability {abilityDefinition.abilityName} cannot be used in context {context} by {caster?.LobbyData?.Name ?? "Unknown Caster"}.");
            // TODO: Send error message to client if needed (e.g., for OOC spell casting)
            return false;
        }

        // --- Validate Targets against Ability Definition ---
        // Check if the resolved targets are compatible with the ability's supported types.
        // This is a simplified check. A full implementation might be more complex.
        // For now, we assume the caller (CombatService) has resolved valid targets.
        // A more robust check would involve the specific TargetType used for each target.
        // For prototype, we'll proceed if targets are provided (or list is empty for area/self).
        if (targets == null)
        {
             Debug.LogWarning($"ExecuteAbility: Target list is null for ability {abilityDefinition.abilityName}.");
             return false;
        }
        // Further validation could happen inside the IAbilityEffect if needed.

        // --- Perform Ability Effects ---
        // This part calls the shared logic or handles list targets
        bool executedSuccessfully = ExecuteAbilityEffect(caster, targets, abilityDefinition);

        if (executedSuccessfully)
        {
            Debug.Log($"Ability {abilityDefinition.abilityName} executed successfully by {caster.LobbyData.Name} on {targets.Count} target(s) (context: {context}).");
            // TODO: Trigger animations, VFX, send updates to clients (health changes, mana spent, cooldowns started, etc.)
            // Consider what updates are needed based on context (OOC vs InCombat)
            // These are often handled by the caller (CombatService/InventoryService) or the IAbilityEffect itself.
        }

        return executedSuccessfully;
    }

    /// <summary>
    /// Applies the direct effects (heal/damage, stat changes, etc.) of an ability to targets.
    /// This encapsulates the core "what happens" part of an ability.
    /// </summary>
    private bool ExecuteAbilityEffect(PlayerConnection caster, List<IDamageable> targets, AbilityDefinition abilityDefinition)
    {
        // --- Get the IAbilityEffect logic ---
        IAbilityEffect effectLogic = abilityDefinition.GetEffectLogic();
        if (effectLogic == null)
        {
            Debug.LogWarning($"No IAbilityEffect script found for ability {abilityDefinition.abilityName}. EffectLogicSource: {abilityDefinition.effectLogicSource?.name ?? "None"}");
            // Optionally, fallback to simple logic based on baseEffectValue if no script is assigned?
            // For now, treat as failure.
            return false;
        }

        // --- Execute the specific effect logic ---
        // The effect logic handles applying its specific changes (heal, damage, buff, summon, etc.)
        // It receives the caster, the list of targets (which implement IDamageable/IHealable),
        // and the ability definition for parameters.
        bool effectExecuted = effectLogic.Execute(caster, targets, abilityDefinition);

        if (effectExecuted)
        {
            Debug.Log($"Executed ability effect '{effectLogic.GetType().Name}' for ability {abilityDefinition.abilityName}.");
             // Trigger shared effects like animations, VFX if needed here or let effect handle it
             if (abilityDefinition.effectPrefab != null)
             {
                 // TODO: Instantiate VFX at target location(s)
                 Debug.Log($"Instantiating effect prefab for {abilityDefinition.abilityName}.");
             }
             if (!string.IsNullOrEmpty(abilityDefinition.animationTrigger))
             {
                 // TODO: Trigger animation on the caster's entity
                 Debug.Log($"Triggering animation '{abilityDefinition.animationTrigger}' for caster {caster.NetworkId}.");
             }
             // Deduct mana (moved here or handled in ExecuteAbility)
             // ApplyCooldown (conceptual, moved here or handled in ExecuteAbility)
        }
        else
        {
            Debug.LogWarning($"Ability effect '{effectLogic.GetType().Name}' failed for ability {abilityDefinition.abilityName}.");
        }

        return effectExecuted;
    }

    // --- Integration with InventoryService ---
    // This method provides the specific interface for InventoryService to use abilities linked to items.

    /// <summary>
    /// Executes an ability specifically triggered by using an item (consumable).
    /// This is the method InventoryService will call.
    /// </summary>
    /// <param name="caster">The player using the item.</param>
    /// <param name="target">The target of the item's effect (often the caster).</param>
    /// <param name="abilityDefinition">The ability linked to the item.</param>
    /// <returns>True if executed successfully, false otherwise.</returns>
    public bool ExecuteAbilityFromItem(PlayerConnection caster, IDamageable target, AbilityDefinition abilityDefinition, AbilityContext context)
    {
        // The target is usually predetermined (often self) by the item's design.
        List<IDamageable> targets = new List<IDamageable> { target };
        return ExecuteAbility(caster, targets, abilityDefinition, context);
    }

    // --- Placeholder Methods for Future Expansion ---
    // These represent areas that need more development as the game grows.

    private AbilityDefinition GetAbilityDefinitionById(string id)
    {
        // Implement a lookup system. Could be a dictionary loaded at startup,
        // or simply using Resources.Load as shown in HandleMessage.
        // Using Resources.Load directly can be slow, so a cache is better.
        return Resources.Load<AbilityDefinition>($"Abilities/{id}");
    }

    // --- Message Handling for Direct OOC Casting (if implemented) ---
    /*
    public void HandleMessage(PlayerConnection player, Dictionary<string, object> msg)
    {
        string playerId = player.NetworkId;
        if (msg.TryGetValue("action", out var actionObj))
        {
            string action = actionObj.ToString();

            switch (action)
            {
                // --- Out-of-Combat Ability Use ---
                // This could be triggered by a UI button for utility spells, for example.
                case "use_ability_ooc":
                    if (msg.TryGetValue("abilityId", out var abilityIdObj) &&
                        msg.TryGetValue("targetId", out var targetIdObj)) // Target might be needed even OOC
                    {
                        string abilityId = abilityIdObj.ToString();
                        string targetId = targetIdObj.ToString();
                        // TODO: Fetch AbilityDefinition by ID (needs a lookup system)
                        var abilityDef = Resources.Load<AbilityDefinition>($"Abilities/{abilityId}");
                        if (abilityDef != null)
                        {
                            // TODO: Resolve targetId to an IDamageable instance
                            // This would require a system to find entities by ID in the current context (map, etc.)
                            // IDamageable target = ResolveTarget(targetId);
                            // if (target != null)
                            // {
                            //     bool success = ExecuteAbility(player, new List<IDamageable> { target }, abilityDef, AbilityContext.OutOfCombat);
                            //     if (success)
                            //     {
                            //         // TODO: Send success confirmation/update to client(s)
                            //         Debug.Log($"OOC Ability {abilityId} used successfully by {playerId} on {targetId}.");
                            //     }
                            //     else
                            //     {
                            //          // TODO: Send error message to client
                            //          Debug.Log($"Failed to use OOC Ability {abilityId} for player {playerId}.");
                            //     }
                            // }
                            // else
                            // {
                            //      Debug.LogWarning($"Target not found for OOC Ability {abilityId}: {targetId}");
                            // }
                        }
                        else
                        {
                             Debug.LogWarning($"AbilityDefinition not found for ID: {abilityId}");
                             // TODO: Send error message to client
                        }
                    }
                    else
                    {
                         Debug.LogWarning("use_ability_ooc message missing 'abilityId' or 'targetId'");
                         // TODO: Send error message to client
                    }
                    break;

                default:
                    Debug.LogWarning($"Unknown ability execution action: {action}");
                    // TODO: Send error message to client
                    break;
            }
        }
        else
        {
            Debug.LogWarning("Ability execution message missing 'action' field");
            // TODO: Send error message to client
        }
    }
    */
    // --- End Message Handling ---
}