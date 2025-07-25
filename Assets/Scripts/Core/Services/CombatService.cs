using UnityEngine;
using System.Collections.Generic;
using System.Linq; // For potential targeting logic

public class CombatService : MonoBehaviour
{
    public static CombatService Instance { get; private set; }

    // --- Basic Combat State (Placeholder) ---
    // In a full implementation, you'd have a more robust state like ActiveEncounter, TurnOrder, etc.
    private bool _isInCombat = false;
    private string _currentTurnPlayerId = null; // Network ID of the player whose turn it is
    // --- End Basic Combat State ---

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"Duplicate CombatService instance detected! Destroying {gameObject.name}");
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Debug.Log("CombatService initialized and set as singleton");
    }

    // --- Ability Execution Core ---

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
    /// This is the central hub for ability logic, usable by InventoryService and combat turns.
    /// </summary>
    /// <param name="casterPlayerId">Network ID of the player/entity casting the ability.</param>
    /// <param name="targetSpecifier">Information about the target. Can be a player ID, "self", "enemy_1", etc., depending on context.</param>
    /// <param name="abilityDefinition">The ability to execute.</param>
    /// <param name="context">The context in which the ability is being used.</param>
    /// <returns>True if executed successfully, false otherwise.</returns>
    public bool ExecuteAbility(string casterPlayerId, string targetSpecifier, AbilityDefinition abilityDefinition, AbilityContext context)
    {
        if (abilityDefinition == null)
        {
            Debug.LogWarning("ExecuteAbility called with null AbilityDefinition.");
            return false;
        }

        var casterPlayer = PlayerManager.Instance.GetPlayer(casterPlayerId);
        if (casterPlayer == null)
        {
            Debug.LogWarning($"Cannot execute ability, caster player not found: {casterPlayerId}");
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
            Debug.LogWarning($"Ability {abilityDefinition.abilityName} cannot be used in context {context}.");
            // TODO: Send error message to client
            return false;
        }

        // --- Target Resolution & Validation ---
        // Determine the resolved target ID and the type of targeting used based on context/specifier.
        string resolvedTargetId = null;
        AbilityDefinition.TargetType resolvedTargetType = AbilityDefinition.TargetType.Self; // Default or derived type

        // --- Simplified Target Resolution based on specifier and context ---
        // This part determines WHO the target is.
        // A full system would be more complex, handling UI selection, AI, etc.
        if (targetSpecifier == "self" || string.IsNullOrEmpty(targetSpecifier))
        {
            resolvedTargetId = casterPlayerId;
            resolvedTargetType = AbilityDefinition.TargetType.Self;
        }
        else
        {
            // Assume targetSpecifier is a direct player ID for SingleAlly/SingleEnemy for now.
            // In a full game, you'd need to determine if targetSpecifier (e.g., "enemy_1")
            // refers to an enemy or ally based on the encounter state.
            resolvedTargetId = targetSpecifier;
            // Placeholder logic: Determine if targetSpecifier ID is an enemy or ally of caster.
            // You need a system to track parties/enemies. For now, assume ally if different ID.
            // This is a CRITICAL PLACEHOLDER that needs proper game state integration.
            if (resolvedTargetId != casterPlayerId)
            {
                 // Placeholder: Assume ally for different ID, enemy logic needs encounter state
                 resolvedTargetType = AbilityDefinition.TargetType.SingleAlly;
                 // TODO: Implement proper IsPlayerAlly(casterId, targetId) or encounter-based lookup
                 // resolvedTargetType = IsPlayerAlly(casterPlayerId, resolvedTargetId) ? AbilityDefinition.TargetType.SingleAlly : AbilityDefinition.TargetType.SingleEnemy;
            }
            else
            {
                resolvedTargetType = AbilityDefinition.TargetType.Self;
            }
        }
        // --- END Simplified Target Resolution ---


        // --- Validate Resolved Target Type against Ability Definition ---
        // This part checks if the type of target we resolved is allowed by the ability.
        bool isTargetTypeSupported = abilityDefinition.supportedTargetTypes.Contains(resolvedTargetType);

        // Handle special cases or combined checks if needed
        // Example: If targeting self, and the ability supports SingleAlly, it might be valid.
        // Adjust these rules based on your game design.
        if (!isTargetTypeSupported)
        {
            // Check if the resolved type is logically covered by a supported type
            if (resolvedTargetType == AbilityDefinition.TargetType.Self &&
                (abilityDefinition.supportedTargetTypes.Contains(AbilityDefinition.TargetType.SingleAlly) ||
                 abilityDefinition.supportedTargetTypes.Contains(AbilityDefinition.TargetType.Area) ||
                 abilityDefinition.supportedTargetTypes.Contains(AbilityDefinition.TargetType.AllAllies)))
            {
                // Self is often a valid target if SingleAlly, Area, or AllAllies is supported.
                // You might want more specific flags (e.g., ability.canTargetCaster).
                isTargetTypeSupported = true;
            }
            else if (resolvedTargetType == AbilityDefinition.TargetType.SingleAlly &&
                     abilityDefinition.supportedTargetTypes.Contains(AbilityDefinition.TargetType.AllAllies))
            {
                // Targeting one ally might be valid if All Allies is supported.
                isTargetTypeSupported = true;
            }
            else if (resolvedTargetType == AbilityDefinition.TargetType.SingleEnemy &&
                     abilityDefinition.supportedTargetTypes.Contains(AbilityDefinition.TargetType.AllEnemies))
            {
                 // Targeting one enemy might be valid if All Enemies is supported.
                 isTargetTypeSupported = true;
            }
            // Add more combined checks as needed.
        }

        if (!isTargetTypeSupported)
        {
            Debug.LogWarning($"Ability {abilityDefinition.abilityName} does not support targeting type {resolvedTargetType} (resolved target: {resolvedTargetId}). Supported types: {string.Join(", ", abilityDefinition.supportedTargetTypes)}");
            // TODO: Send error message to client
            return false;
        }

        // --- Validate Resolved Target ID Exists ---
        // Ensure the resolved target ID actually corresponds to a valid player/entity.
        if (string.IsNullOrEmpty(resolvedTargetId) &&
            resolvedTargetType != AbilityDefinition.TargetType.Area &&
            resolvedTargetType != AbilityDefinition.TargetType.AllEnemies &&
            resolvedTargetType != AbilityDefinition.TargetType.AllAllies)
        {
            Debug.LogWarning($"Could not resolve a valid target ID for ability {abilityDefinition.abilityName} with specifier '{targetSpecifier}' and resolved type {resolvedTargetType}.");
            // TODO: Send error message to client
            return false;
        }

        // --- Perform Ability Effects ---
        // This part calls the shared logic or handles list targets
        bool executedSuccessfully = false;
        if (resolvedTargetType == AbilityDefinition.TargetType.Area ||
            resolvedTargetType == AbilityDefinition.TargetType.AllEnemies ||
            resolvedTargetType == AbilityDefinition.TargetType.AllAllies)
        {
            // Handle multi-target abilities (Placeholder logic)
            // List<string> targetIds = ResolveMultiTarget(resolvedTargetType, ...);
            // foreach(var targetId in targetIds)
            // {
            //      executedSuccessfully |= ApplyAbilityEffects(casterPlayerId, targetId, abilityDefinition);
            // }
            Debug.LogWarning($"Multi-target ability {abilityDefinition.abilityName} execution logic not implemented.");
            executedSuccessfully = false; // Or true if partial success is okay
        }
        else
        {
            // Single target (including Self)
            executedSuccessfully = ApplyAbilityEffects(casterPlayerId, resolvedTargetId, abilityDefinition);
        }

        if (executedSuccessfully)
        {
            Debug.Log($"Ability {abilityDefinition.abilityName} executed successfully by {casterPlayerId} on {resolvedTargetId} (context: {context}).");
            // TODO: Trigger animations, VFX, send updates to clients (health changes, mana spent, cooldowns started, etc.)
            // Consider what updates are needed based on context (OOC vs InCombat)
        }

        return executedSuccessfully;
    }

    /// <summary>
    /// Applies the direct effects (heal/damage, stat changes, etc.) of an ability to a single target.
    /// This encapsulates the core "what happens" part of an ability.
    /// </summary>
    private bool ApplyAbilityEffects(string casterPlayerId, string targetPlayerId, AbilityDefinition abilityDefinition)
    {
        var targetPlayer = PlayerManager.Instance.GetPlayer(targetPlayerId) as PlayerConnection; // Explicit cast if needed
        if (targetPlayer == null)
        {
            Debug.LogWarning($"Cannot apply ability effects, target player not found: {targetPlayerId}");
            return false;
        }

        // --- Apply Core Effect (Heal/Damage) ---
        // --- CHANGED: Use baseEffectValue (assuming you updated AbilityDefinition) ---
        int finalEffectValue = abilityDefinition.baseEffectValue;
        if (finalEffectValue < 0) // Healing
        {
            int healAmount = Mathf.Abs(finalEffectValue);
            int healthBefore = targetPlayer.CurrentHealth;
            targetPlayer.CurrentHealth = Mathf.Clamp(
                targetPlayer.CurrentHealth + healAmount,
                0,
                targetPlayer.MaxHealth
            );
            int actualHeal = targetPlayer.CurrentHealth - healthBefore;

            Debug.Log($"Player {targetPlayerId} healed for {actualHeal}. New HP: {targetPlayer.CurrentHealth}/{targetPlayer.MaxHealth}");

            // --- Send Health Update to Target Client ---
            GameServer.Instance.SendToPlayer(targetPlayer.NetworkId, new
            {
                type = "stats_update",
                currentHealth = targetPlayer.CurrentHealth,
                maxHealth = targetPlayer.MaxHealth
            });

            // --- Send Ability Effect Message ---
            if (casterPlayerId != targetPlayerId)
            {
                GameServer.Instance.SendToPlayer(casterPlayerId, new
                {
                    type = "ability_effect",
                    message = $"You used {abilityDefinition.abilityName} on {targetPlayer.LobbyData?.Name ?? targetPlayerId}."
                });
            }
            else
            {
                GameServer.Instance.SendToPlayer(casterPlayerId, new
                {
                    type = "ability_effect",
                    message = $"You used {abilityDefinition.abilityName}."
                });
            }
        }
        else if (finalEffectValue > 0) // Damage
        {
            // Simple damage application. Add defense, damage types, etc. later.
            int damageAmount = finalEffectValue; // Apply calculations based on type/defense here
            int healthBefore = targetPlayer.CurrentHealth;
            // Placeholder calculation - replace with your damage formula
            int damageTaken = Mathf.Max(1, damageAmount - Mathf.FloorToInt(targetPlayer.Defense / 10f));
            targetPlayer.CurrentHealth = Mathf.Clamp(
                targetPlayer.CurrentHealth - damageTaken,
                0,
                targetPlayer.MaxHealth
            );
            int actualDamage = healthBefore - targetPlayer.CurrentHealth;

            Debug.Log($"Player {targetPlayerId} took {actualDamage} damage. New HP: {targetPlayer.CurrentHealth}/{targetPlayer.MaxHealth}");

            // --- Send Health Update to Target Client ---
            GameServer.Instance.SendToPlayer(targetPlayer.NetworkId, new
            {
                type = "stats_update",
                currentHealth = targetPlayer.CurrentHealth,
                maxHealth = targetPlayer.MaxHealth
            });

            // --- Send Ability Effect Message ---
            GameServer.Instance.SendToPlayer(casterPlayerId, new
            {
                type = "ability_effect",
                message = $"You used {abilityDefinition.abilityName} on {targetPlayer.LobbyData?.Name ?? targetPlayerId}."
            });
        }
        else
        {
            Debug.Log($"Ability {abilityDefinition.abilityName} had no HP effect (effect value was 0).");
            // --- Send Generic Effect Message ---
            GameServer.Instance.SendToPlayer(casterPlayerId, new
            {
                type = "ability_effect",
                message = $"You used {abilityDefinition.abilityName}."
            });
        }

        // --- Deduct Mana (using new property) ---
        var casterPlayer = PlayerManager.Instance.GetPlayer(casterPlayerId) as PlayerConnection;
        if (casterPlayer != null && abilityDefinition.manaCost > 0)
        {
            int manaBefore = casterPlayer.Magic;
            casterPlayer.Magic = Mathf.Max(0, casterPlayer.Magic - abilityDefinition.manaCost);
            int manaSpent = manaBefore - casterPlayer.Magic;
            Debug.Log($"Deducted {manaSpent} mana from {casterPlayerId}. New Mana: {casterPlayer.Magic}");

            // --- Send mana update to caster's client ---
            GameServer.Instance.SendToPlayer(casterPlayer.NetworkId, new
            {
                type = "stats_update",
                magic = casterPlayer.Magic
                // Add other relevant stats if changed
            });
        }

        // --- Apply Cooldown (Conceptual) ---
        // ... (rest of logic)

        return true; // or based on success conditions
    }

    // --- Integration with InventoryService ---
    // This method provides the specific interface for InventoryService to use abilities linked to items.

    /// <summary>
    /// Executes an ability specifically triggered by using an item (consumable).
    /// This is the method InventoryService will call.
    /// </summary>
    public bool ExecuteAbilityFromItem(string casterPlayerId, string targetPlayerId, AbilityDefinition abilityDefinition)
    {
        // When used from an item, it's always considered OutOfCombat context for the ability's rules
        // The target is usually predetermined (often self) by the item's design.
        return ExecuteAbility(casterPlayerId, targetPlayerId, abilityDefinition, AbilityContext.OutOfCombat);
    }

    // --- Combat Turn Integration (Placeholder) ---
    // When you implement full combat turns, the logic for a player's action would call ExecuteAbility.

    /*
    public void ProcessPlayerCombatAction(string playerId, string actionType, string targetSpecifier, string abilityId = null /* , other params * /)
    {
        if (!_isInCombat)
        {
            Debug.LogWarning("Cannot process combat action, not currently in combat.");
            // TODO: Send error
            return;
        }

        if (playerId != _currentTurnPlayerId)
        {
            Debug.LogWarning($"Player {playerId} tried to act, but it's not their turn ({_currentTurnPlayerId}).");
            // TODO: Send error
            return;
        }

        if (actionType == "use_ability" && !string.IsNullOrEmpty(abilityId))
        {
            var abilityDef = GetAbilityDefinitionById(abilityId); // You need a way to fetch this
            if (abilityDef != null)
            {
                bool success = ExecuteAbility(playerId, targetSpecifier, abilityDef, AbilityContext.InCombat);
                if (success)
                {
                    // Handle post-success actions: end turn, apply cooldown, check for victory/defeat, etc.
                    EndTurn(); // Example
                }
                else
                {
                    // Ability failed, maybe allow re-selection or end turn? Depends on rules.
                    Debug.Log($"Ability {abilityId} failed for player {playerId}. Turn might end.");
                    // TODO: Send message to client about failure
                    // EndTurn(); // Example: Turn ends even on failure?
                }
            }
        }
        else if (actionType == "melee_attack")
        {
             // Handle basic attack logic
        }
        else if (actionType == "use_item")
        {
             // Handle using item from inventory during combat (if allowed by item/ability rules)
             // This would likely call InventoryService.UseItem, which in turn might call ExecuteAbilityFromItem
        }
        // ... other action types
    }

    private void EndTurn()
    {
        // Logic to determine next player, update state, send turn info to clients
        _currentTurnPlayerId = GetNextPlayerInTurnOrder(); // Implement this
        // Send message to all players about whose turn it is now
        // BroadcastCombatState(); // Example
        Debug.Log($"Turn ended. Next turn: {_currentTurnPlayerId}");
    }
    */

    // --- Message Handling ---

    public void HandleMessage(PlayerConnection player, Dictionary<string, object> msg)
    {
        // --- CORRECTED: Use player.NetworkId instead of player.SessionId ---
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
                            bool success = ExecuteAbility(playerId, targetId, abilityDef, AbilityContext.OutOfCombat);
                            if (success)
                            {
                                // TODO: Send success confirmation/update to client(s)
                                Debug.Log($"OOC Ability {abilityId} used successfully by {playerId} on {targetId}.");
                            }
                            else
                            {
                                 // TODO: Send error message to client
                                 Debug.Log($"Failed to use OOC Ability {abilityId} for player {playerId}.");
                            }
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

                // --- In-Combat Actions ---
                // These would only be processed if _isInCombat and it's the player's turn.
                case "combat_action":
                     // This would parse the specific combat action (attack, ability, item)
                     // and call the appropriate logic (like ProcessPlayerCombatAction)
                     Debug.Log("Combat action received. Full combat logic needed to process.");
                     // Placeholder:
                     // if (_isInCombat && playerId == _currentTurnPlayerId)
                     // {
                     //      ProcessPlayerCombatAction(playerId, msg);
                     // }
                     // else
                     // {
                     //      // Send error: Not your turn or not in combat
                     // }
                     break;

                default:
                    Debug.LogWarning($"Unknown combat action: {action}");
                    // TODO: Send error message to client
                    break;
            }
        }
        else
        {
            Debug.LogWarning("Combat message missing 'action' field");
            // TODO: Send error message to client
        }
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

    public void StartCombat(List<PlayerConnection> players)
    {
        _isInCombat = true;
        // Initialize encounter state, determine turn order, set _currentTurnPlayerId
        // Send combat start message to all players
        Debug.Log("Combat started!");
    }

    public void EndCombat()
    {
        _isInCombat = false;
        _currentTurnPlayerId = null;
        // Clean up encounter state
        // Send combat end message, distribute rewards, etc.
        Debug.Log("Combat ended!");
    }

    // --- CORRECTED: Use player.NetworkId ---
    public void OnPlayerDisconnected(PlayerConnection player)
    {
        // Handle combat disconnection
        // E.g., if it's their turn, skip it or handle defeat.
        // Remove them from turn order.
        Debug.Log($"Player {player.NetworkId} disconnected during potential combat.");
    }

    // --- CORRECTED: Use player.NetworkId ---
    public void OnPlayerReconnected(PlayerConnection player)
    {
        // Handle combat reconnection
        // E.g., send them the current combat state.
        Debug.Log($"Player {player.NetworkId} reconnected during potential combat.");
    }
}