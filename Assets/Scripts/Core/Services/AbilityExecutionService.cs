// File: Scripts/Core/Services/AbilityExecutionService.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq; // For potential targeting logic
using System;
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
    /// Executes an ability, handling targeting, effects, costs, context checks, and action consumption.
    /// This is the central hub for ability logic.
    /// </summary>
    /// <param name="caster">The IEntity (player or enemy) casting the ability.</param>
    /// <param name="targets">The list of resolved targets for the ability.</param>
    /// <param name="abilityDefinition">The ability to execute.</param>
    /// <param name="context">The context in which the ability is being used.</param>
    /// <returns>True if executed successfully, false otherwise.</returns>
    public bool ExecuteAbility(IEntity caster, List<IDamageable> targets, AbilityDefinition abilityDefinition, AbilityContext context)
    {

        // --- Validation ---
        if (caster == null)
        {
            Debug.LogError("AbilityExecutionService: Cannot execute ability, caster is null.");
            return false;
        }
        if (targets == null || targets.Count == 0)
        {
            Debug.LogWarning($"AbilityExecutionService: Cannot execute ability '{abilityDefinition?.abilityName ?? "Unknown"}', targets list is null or empty.");
            return false;
        }
        if (abilityDefinition == null)
        {
            Debug.LogError("AbilityExecutionService: Cannot execute ability, abilityDefinition is null.");
            return false;
        }

        string casterName = caster.GetEntityName() ?? "Unknown Entity";
        Debug.Log($"AbilityExecutionService: Executing ability '{abilityDefinition.abilityName}' from caster '{casterName}' in context '{context}'.");


        // --- Context Validation ---
        // Check if the ability can be used in the given context
        bool contextValid = false;
        switch (context)
        {
            case AbilityContext.OutOfCombat:
                contextValid = abilityDefinition.usableOutOfCombat;
                break;
            case AbilityContext.InCombat:
                contextValid = abilityDefinition.usableInCombat;
                break;
            case AbilityContext.Always:
                contextValid = true; // Always usable, regardless of context
                break;
        }

        if (!contextValid)
        {
            Debug.LogWarning($"AbilityExecutionService: Ability '{abilityDefinition.abilityName}' cannot be used in context '{context}'.");
            return false; // Fail if context is invalid
        }
        // --- End Context Validation ---


        // --- Action Cost Validation (MODIFIED) ---
        // Only check and consume action points if the ability is used IN combat.
        int actionCost = abilityDefinition.actionCost;
        bool needsActionCost = context == AbilityContext.InCombat; 
        IActionBudget actionEntity = caster as IActionBudget;

        if (needsActionCost && actionEntity != null)
        {
            if (actionEntity.ActionsRemaining < actionCost)
            {
                Debug.LogWarning($"AbilityExecutionService: {casterName} does not have enough actions ({actionEntity.ActionsRemaining}) to cast {abilityDefinition.abilityName} (cost: {actionCost}).");
                return false;
            }
            // Note: We don't consume the action here yet. Do it after successful execution.
            // Action consumption logic also needs to be conditional (see below).
        }
        else if (needsActionCost && actionEntity == null) // NEW: Warn if cost needed but not supported
        {
            Debug.LogWarning($"AbilityExecutionService: Context is InCombat, action cost is {actionCost}, but caster '{casterName}' does not implement IActionBudget. Execution might be unintended.");
            // Depending on design, you might want to fail here strictly.
            // For now, let's proceed if the context check passed, assuming non-IActionBudget entities are exempt.
        }
        // If needsActionCost is false (OutOfCombat/Always), skip the check entirely.
        // --- End Action Cost Validation ---

        // --- Perform Ability Effects ---
        // This part calls the shared logic or handles list targets
        bool executedSuccessfully = ExecuteAbilityEffect(caster, targets, abilityDefinition);

        if (executedSuccessfully)
        {
            Debug.Log($"Ability {abilityDefinition.abilityName} executed successfully by {casterName} on {targets.Count} target(s) (context: {context}).");
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
    private bool ExecuteAbilityEffect(IEntity caster, List<IDamageable> targets, AbilityDefinition abilityDefinition)
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
        string casterName = caster.GetEntityName() ?? "Unknown Entity";
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
                 Debug.Log($"Triggering animation '{abilityDefinition.animationTrigger}' for caster {casterName}.");
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
    public bool ExecuteAbilityFromItem(IEntity  caster, IDamageable target, AbilityDefinition abilityDefinition, AbilityContext context)
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