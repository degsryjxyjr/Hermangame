// File: Scripts/Core/Services/CombatService.cs (Refactored)
using UnityEngine;
using System.Collections.Generic;
using System.Linq; // For potential targeting logic

public class CombatService : MonoBehaviour
{
    public static CombatService Instance { get; private set; }

    // --- Basic Combat State  ---
    private bool _isInCombat = false;
    public bool IsInCombat => _isInCombat;
    // - End Basic Combat State -

    // --- Reference to EncounterManager ---
    // This will be instantiated or found in the combat scene.
    private EncounterManager _encounterManager;


    public EncounterManager GetEncounterManager() => _encounterManager;

    // --- End Reference ---

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"Duplicate CombatService instance detected! Destroying {gameObject.name}");
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // Don't DontDestroyOnLoad if it's scene-specific. Otherwise, do.
        // For this example, let's assume it's scene-specific and managed by GameStateManager.
        // DontDestroyOnLoad(gameObject); 
        Debug.Log("CombatService initialized and set as singleton");
    }
    // --- Called by GameStateManager or when Combat Scene loads ---
    /// <summary>
    /// Initializes the combat service for a new encounter.
    /// Finds or creates the EncounterManager and starts the encounter.
    /// </summary>
    public void InitializeForEncounter(List<PlayerConnection> players, List<EnemyDefinition> enemiesToSpawn)
    {
        Debug.Log("CombatService: Initializing for new encounter.");

        // 1. Find or create EncounterManager
        // Option A: Assume one exists in the scene (e.g., on a dedicated GameObject)
        _encounterManager = FindFirstObjectByType<EncounterManager>();
        // Option B: Create one dynamically
        if (_encounterManager == null)
        {
            Debug.LogWarning("CombatService: Could not find EncounterManager so creating one!");
             GameObject emGO = new GameObject("EncounterManager");
             _encounterManager = emGO.AddComponent<EncounterManager>();
        }

        if (_encounterManager == null)
        {
            
            // TODO: Handle error, maybe transition back to Map state?
            return;
        }

        // 2. Subscribe to EncounterManager events
        _encounterManager.OnEncounterStarted += OnEncounterStarted_Internal;
        _encounterManager.OnEncounterEnded += OnEncounterEnded_Internal;
        _encounterManager.OnTurnStarted += OnTurnStarted_Internal;
        _encounterManager.OnPlayerAdded += OnPlayerAdded_Internal;
        _encounterManager.OnEnemyAdded += OnEnemyAdded_Internal;
        _encounterManager.OnEnemyDefeated += OnEnemyDefeated_Internal;

        // 3. Tell EncounterManager to start the encounter
        // TODO: Get actual enemy definitions based on the encounter details
        //List<EnemyDefinition> enemiesToSpawn = new List<EnemyDefinition>(); // Placeholder
        // Example: enemiesToSpawn.Add(Resources.Load<EnemyDefinition>("Enemies/Goblin"));
        _encounterManager.StartEncounter(players, enemiesToSpawn /*, other details */);

        // The EncounterManager will now spawn enemies, set up turn order, and notify us via events.
    }


    // --- EncounterManager Event Handlers with Server Messages ---

    private void OnEncounterStarted_Internal()
    {
        Debug.Log("CombatService: Received OnEncounterStarted from EncounterManager.");
        
        if (_encounterManager == null)
        {
            Debug.LogError("CombatService: OnEncounterStarted_Internal called but _encounterManager is null!");
            return;
        }

        // --- Prepare Initial Encounter State Data ---
        var encounterData = new Dictionary<string, object>
        {
            ["type"] = "encounter_start",
            ["status"] = "active",
            ["message"] = "Combat encounter has begun!"
        };

        // Add Player Data
        var playersData = new List<object>();
        foreach (var player in _encounterManager.GetActivePlayers())
        {
            if (player != null)
            {
                // Determine the icon for the player (from their class definition)
                string playerIconPath = "images/players/default-player.png"; // Default fallback
                if (player.ClassDefinition != null && player.ClassDefinition.classIcon != null)
                {
                    // Use the sprite's name or asset name. 
                    playerIconPath = $"images/players/{player.ClassDefinition.classIcon.name}.png";
                }


                playersData.Add(new
                {
                    id = player.NetworkId, // Use NetworkId for identification
                    name = player.LobbyData?.Name ?? "Unknown Player",
                    currentHealth = player.CurrentHealth,
                    maxHealth = player.MaxHealth,
                    actions = player.ActionsRemaining,
                    attack = player.Attack,
                    defense = player.Defense,
                    magic = player.Magic,
                    isAlive = player.IsAlive(),
                    iconPath = playerIconPath

                    // Add other relevant player data for the combat view
                });
            }
        }
        encounterData["players"] = playersData;

        // Add Enemy Data
        var enemiesData = new List<object>();
        foreach (var enemy in _encounterManager.GetActiveEnemies())
        {
            if (enemy != null)
            {
                // Determine the icon for the enemy (from their definition)
                string enemyIconPath = "images/enemies/default-enemy.png"; // Default fallback
                if (enemy._enemyDefinition != null && enemy._enemyDefinition.icon != null)
                {
                    // Similar logic as for player icon.
                    // Assuming organization like "images/enemies/{icon_name}.png"
                    enemyIconPath = $"images/enemies/{enemy._enemyDefinition.icon.name}.png";
                    // --- OR --- if you store a direct path:
                    // enemyIconPath = enemy.EnemyDefinition.iconPath;
                }


                // Get the base definition name, or fallback
                string enemyName = enemy.GetEntityName() ?? "Unknown Enemy";

                enemiesData.Add(new
                {
                    // Use InstanceID or a unique ID assigned by EncounterManager for client targeting
                    id = enemy.GetInstanceID().ToString(),
                    name = enemyName,
                    currentHealth = enemy.CurrentHealth,
                    maxHealth = enemy.MaxHealth,
                    actions = enemy.ActionsRemaining,
                    attack = enemy.Attack,
                    defense = enemy.Defense,
                    magic = enemy.Magic,
                    isAlive = enemy.IsAlive(),
                    iconPath = enemyIconPath
                    // Add other relevant enemy data (icon path if needed for client UI?)
                    // icon = enemy.EnemyDefinition?.icon != null ? $"images/enemies/{enemy.EnemyDefinition.icon.name}" : "images/enemies/default-enemy.jpg"
                });
            }
        }
        encounterData["enemies"] = enemiesData;

        // Add Initial Turn Order (simplified list of IDs for now)
        var turnOrderData = new List<object>();
        foreach (var entity in _encounterManager.GetTurnOrder())
        {
            if (entity is PlayerConnection player)
            {
                turnOrderData.Add(new { type = "player", id = player.NetworkId });
            }
            else if (entity is EnemyEntity enemy)
            {
                turnOrderData.Add(new { type = "enemy", id = enemy.GetInstanceID().ToString() });
            }
        }
        encounterData["turnOrder"] = turnOrderData;

        // --- Send Initial Encounter State to ALL Players in the Encounter ---
        foreach (var player in _encounterManager.GetActivePlayers())
        {
            if (player != null && player.NetworkId != null)
            {
                GameServer.Instance.SendToPlayer(player.NetworkId, encounterData);
                Debug.Log($"CombatService: Sent 'encounter_start' message to player {player.LobbyData?.Name ?? player.NetworkId}");
            }
        }
    }

    private void OnEncounterEnded_Internal()
    {
        Debug.Log("CombatService: Received OnEncounterEnded from EncounterManager.");

        if (_encounterManager == null)
        {
            Debug.LogError("CombatService: OnEncounterEnded_Internal called but _encounterManager is null!");
            return;
        }

        EndCombat();
    }

    private void OnTurnStarted_Internal(object turnEntity)
    {
        Debug.Log($"CombatService: Received OnTurnStarted for {GetEntityNameSafe(turnEntity)}.");

        if (_encounterManager == null || turnEntity == null)
        {
            Debug.LogError("CombatService: OnTurnStarted_Internal called but _encounterManager or turnEntity is null!");
            return;
        }

        string entityId = null;
        string entityType = null;
        string entityName = null;

        // Determine the ID and type of the entity whose turn it is
        if (turnEntity is PlayerConnection player)
        {
            entityId = player.NetworkId;
            entityType = "player";
            entityName = GetEntityNameSafe(turnEntity);
            SendTurnStartData(entityId, entityType, entityName);
        }
        else if (turnEntity is EnemyEntity enemy)
        {
            entityId = enemy.GetInstanceID().ToString(); // Or a unique ID from EncounterManager
            entityType = "enemy";
            entityName = GetEntityNameSafe(turnEntity);
            // Sending turn start to players here. Only then actually starting the AI turn
            SendTurnStartData(entityId, entityType, entityName);

            // This is where the enemy's AI decision-making process is triggered.
            TriggerEnemyAI(enemy);
        }
        else
        {
            Debug.LogError($"CombatService: Unknown turn entity type: {turnEntity.GetType()}");
        }
    }


    private void SendTurnStartData(string entityId, string entityType, string entityName)
    {
        if (string.IsNullOrEmpty(entityId))
        {
            Debug.LogError("CombatService: Could not determine entity ID for turn start message.");
            return;
        }
        // --- Prepare Turn Start Data ---
        var turnData = new Dictionary<string, object>
        {
            ["type"] = "turn_start",
            ["entity"] = new
            {
                id = entityId,
                type = entityType,
                name = entityName
            },
            ["message"] = $"{entityName}'s turn begins!"
        };
        // --- End Prepare Data ---

        // --- Send Turn Start Message to ALL Players in the Encounter ---
        // Everyone needs to know whose turn it is to update their UI.
        foreach (var p in _encounterManager.GetActivePlayers())
        {
            if (p != null)
            {
                GameServer.Instance.SendToPlayer(p.NetworkId, turnData);
                Debug.Log($"CombatService: Sent 'turn_start' message for {entityType} {entityName} to player {p.LobbyData?.Name ?? p.NetworkId}");
            }
        }
    }


    private void OnPlayerAdded_Internal(PlayerConnection player)
    {
        // This event fires when a player is added to the encounter (usually at start)
        // You might send a specific message if players can join mid-encounter, 
        // but typically the full state is sent on 'encounter_start'.
        Debug.Log($"CombatService: Player {player?.LobbyData?.Name ?? "Unknown"} added to encounter.");
        // Optional: Send a message to other players about the new participant
        // foreach(var otherPlayer in _encounterManager.GetActivePlayers().Where(p => p != player))
        // {
        //     GameServer.Instance.SendToPlayer(otherPlayer.NetworkId, new { type = "player_joined_encounter", playerId = player.NetworkId, playerName = player.LobbyData.Name });
        // }
    }

    private void OnEnemyAdded_Internal(EnemyEntity enemy)
    {
        // This event fires when an enemy is added/spawned.
        // Notify clients about the new enemy.
        Debug.Log($"CombatService: Enemy {enemy?.GetEntityName() ?? "Unknown"} added to encounter.");

        // --- Prepare Enemy Spawn Data ---
        var spawnData = new Dictionary<string, object>
        {
            ["type"] = "enemy_spawned",
            ["enemy"] = new
            {
                id = enemy.GetInstanceID().ToString(),
                name = enemy.GetEntityName() ?? "Unknown Enemy",
                currentHealth = enemy.CurrentHealth,
                maxHealth = enemy.MaxHealth,
                // Add other initial enemy data if needed by client
            },
            ["message"] = $"{enemy.GetEntityName() ?? "An enemy"} has appeared!"
        };
        // --- End Prepare Data ---

        // --- Send Enemy Spawn Message to ALL Players ---
        foreach (var player in _encounterManager.GetActivePlayers())
        {
            if (player != null)
            {
                GameServer.Instance.SendToPlayer(player.NetworkId, spawnData);
                Debug.Log($"CombatService: Sent 'enemy_spawned' message for {enemy.GetEntityName()} to player {player.LobbyData?.Name ?? player.NetworkId}");
            }
        }
    }

    private void OnEnemyDefeated_Internal(EnemyEntity enemy)
    {
        Debug.Log($"CombatService: Received OnEnemyDefeated for {enemy?.GetEntityName() ?? "Unknown Enemy"}.");
        
        if (enemy == null || _encounterManager == null)
        {
            Debug.LogError("CombatService: OnEnemyDefeated_Internal called with null enemy or _encounterManager!");
            return;
        }

        // --- Prepare Enemy Defeated Data ---
        var defeatData = new Dictionary<string, object>
        {
            ["type"] = "enemy_defeated",
            ["enemyId"] = enemy.GetInstanceID().ToString(), // ID used by client to identify which enemy UI element to remove/update
            ["message"] = $"{enemy.GetEntityName() ?? "The enemy"} has been defeated!"
            // TODO: Include loot drops if decided here, or send separately
            // ["loot"] = new List<object> { ... }
        };
        // --- End Prepare Data ---

        // --- Send Enemy Defeated Message to ALL Players ---
        foreach (var player in _encounterManager.GetActivePlayers())
        {
            if (player != null)
            {
                GameServer.Instance.SendToPlayer(player.NetworkId, defeatData);
                Debug.Log($"CombatService: Sent 'enemy_defeated' message for {enemy.GetEntityName()} to player {player.LobbyData?.Name ?? player.NetworkId}");
            }
        }
    }

    // --- Helper Method for Logging Entity Names ---
    private string GetEntityNameSafe(object entity)
    {
        if (entity is IEntity iEntity) return iEntity.GetEntityName();
        if (entity != null) return entity.ToString();
        return "Null";
    }
    // --- End Helper Method ---

    // --- End  EncounterManager Event Handlers ---





    // --- Enemy AI Trigger ---
    private void TriggerEnemyAI(EnemyEntity enemy)
    {
        if (enemy == null) return;

        Debug.Log($"CombatService: Triggering AI for enemy {enemy.GetEntityName()}.");

        // 1. Ask the enemy to decide its action (simplified)
        // In a real system, this might involve a more complex AI decision tree.
        AbilityDefinition chosenAbility = enemy.GetRandomUsableCombatAbility();
        IDamageable chosenTarget = null; // Simplified target selection

        if (chosenAbility != null)
        {
            // 2. Select a target (simplified: random player)
            var alivePlayers = _encounterManager.GetActivePlayers().FindAll(p => p.IsAlive());
            if (alivePlayers.Count > 0)
            {
                chosenTarget = alivePlayers[Random.Range(0, alivePlayers.Count)];
            }
        }

        if (chosenAbility != null && chosenTarget != null)
        {
            // 3. Execute the ability using AbilityExecutionService
            List<IDamageable> targets = new List<IDamageable> { chosenTarget };

            Debug.Log($"CombatService: Enemy {enemy.GetEntityName()} chose to use {chosenAbility.abilityName} on THIS IS NOT IMPLEMENTED.");

            bool success = AbilityExecutionService.Instance.ExecuteAbility(enemy, targets, chosenAbility, AbilityExecutionService.AbilityContext.InCombat);
            if (success)
            {
                // Handle success (broadcast new target stats to all clients and advance turn)
                SendCombatEntityUpdate();
                _encounterManager.AdvanceTurn();
            }
        }
        else
        {
            Debug.Log($"CombatService: Enemy {enemy.GetEntityName()} has no valid action. Ending turn.");
            // If no action, just advance the turn.
            _encounterManager.AdvanceTurn();
        }
    }
    // --- End Enemy AI Trigger ---

    // Helper function to broadcast encounter entity updates(player heals, enemy takes damage etc)
    public void SendCombatEntityUpdate(bool sendDataFromAllEntities = true)
    {



        if (sendDataFromAllEntities)
        {
            // --- Prepare Data ---
            var combatEntityData = new Dictionary<string, object>
            {
                ["type"] = "combat_entities_update",
                ["message"] = "Update to all entities stats!"
            };
            // --- End Prepare Data ---

            // Add Player Data
            var playersData = new List<object>();
            foreach (var player in _encounterManager.GetActivePlayers())
            {
                if (player != null)
                {
                    // Determine the icon for the player (from their class definition)
                    string playerIconPath = "images/players/default-player.png"; // Default fallback
                    if (player.ClassDefinition != null && player.ClassDefinition.classIcon != null)
                    {
                        // Use the sprite's name or asset name. 
                        playerIconPath = $"images/players/{player.ClassDefinition.classIcon.name}.png";
                    }

                    playersData.Add(new
                    {
                        id = player.NetworkId, // Use NetworkId for identification
                        name = player.LobbyData?.Name ?? "Unknown Player",
                        currentHealth = player.CurrentHealth,
                        maxHealth = player.MaxHealth,
                        actions = player.ActionsRemaining,
                        attack = player.Attack,
                        defense = player.Defense,
                        magic = player.Magic,
                        isAlive = player.IsAlive(),
                        iconPath = playerIconPath
                        // Add other relevant player data for the combat view
                    });
                }
            }
            combatEntityData["players"] = playersData;

            // Add Enemy Data
            var enemiesData = new List<object>();
            foreach (var enemy in _encounterManager.GetActiveEnemies())
            {
                if (enemy != null)
                {



                    // Determine the icon for the enemy (from their definition)
                    string enemyIconPath = "images/enemies/default-enemy.png"; // Default fallback
                    if (enemy._enemyDefinition != null && enemy._enemyDefinition.icon != null)
                    {
                        // Similar logic as for player icon.
                        enemyIconPath = $"images/enemies/{enemy._enemyDefinition.icon.name}.png";
                    }
                    // Get the base definition name, or fallback
                    string enemyName = enemy.GetEntityName() ?? "Unknown Enemy";

                    enemiesData.Add(new
                    {
                        // Use InstanceID or a unique ID assigned by EncounterManager for client targeting
                        id = enemy.GetInstanceID().ToString(),
                        name = enemyName,
                        currentHealth = enemy.CurrentHealth,
                        maxHealth = enemy.MaxHealth,
                        actions = enemy.ActionsRemaining,
                        attack = enemy.Attack,
                        defense = enemy.Defense,
                        magic = enemy.Magic,
                        isAlive = enemy.IsAlive(),
                        iconPath = enemyIconPath
                        // Add other relevant enemy data (icon path if needed for client UI?)
                        // icon = enemy.EnemyDefinition?.icon != null ? $"images/enemies/{enemy.EnemyDefinition.icon.name}" : "images/enemies/default-enemy.jpg"
                    });
                }
            }
            combatEntityData["enemies"] = enemiesData;
            
            // --- Send Entity Update Message to ALL Players in the Encounter ---
            GameServer.Instance.Broadcast(combatEntityData);
            Debug.Log($"CombatService: Broadcast 'combat_entities_update' message");
        }
        else
        {
            // --- Prepare Data ---
            var combatEntityData = new Dictionary<string, object>
            {
                ["type"] = "combat_entity_update",
                ["message"] = "Update to entity's stats!"
            };
            // --- Send Entity Update Message to ALL Players in the Encounter ---
            foreach (var player in _encounterManager.GetActivePlayers())
            {
                if (player != null && player.NetworkId != null)
                {
                    GameServer.Instance.SendToPlayer(player.NetworkId, combatEntityData);
                    Debug.Log($"CombatService: Sent 'combat_entity_update' message to player {player.LobbyData?.Name ?? player.NetworkId}");
                }
            }
        }

    }



    // End helper function




    // --- Player Action Processing ---
    /// <summary>
    /// Processes a player's action during their turn.
    /// </summary>
    public void ProcessPlayerCombatAction(string playerId, Dictionary<string, object> actionData)
    {
        // 1. Validate state and turn
        if (_encounterManager == null || _encounterManager.CurrentState != EncounterManager.EncounterState.Active)
        {
            Debug.LogWarning($"CombatService: Cannot process action, encounter not active.");
            // TODO: Send error message to client
            return;
        }

        var currentPlayer = PlayerManager.Instance.GetPlayer(playerId);
        if (currentPlayer == null)
        {
            Debug.LogWarning($"CombatService: Player {playerId} not found in PlayerManager.");
            // TODO: Send error
            return;
        }

        if (_encounterManager.CurrentTurnEntity != currentPlayer)
        {
            Debug.LogWarning($"CombatService: Player {playerId} tried to act, but it's not their turn ({GetEntityNameSafe(_encounterManager.CurrentTurnEntity)}).");
            // TODO: Send error message to client (e.g., "Not your turn")
            return;
        }

        // 2. Parse action type
        if (!actionData.TryGetValue("type", out var typeObj))
        {
            Debug.LogWarning($"CombatService: Action data missing 'type' field for player {playerId}.");
            // TODO: Send error message to client
            return;
        }

        string actionType = typeObj.ToString();

        Debug.Log($"CombatService: Received combat action from player {playerId}. Action Type: {actionType}");

        switch (actionType)
        {
            case "use_ability":
                ProcessUseAbilityAction(playerId, currentPlayer, actionData);
                break;

            case "use_item":
                ProcessUseItemAction(playerId, actionData);
                break;

            case "end_turn":
                Debug.Log($"CombatService: Received end_turn msg from player {playerId}. Advancing turn.");
                _encounterManager.AdvanceTurn();
                break;

            default:
                Debug.LogWarning($"CombatService: Unknown combat action type: '{actionType}' from player {playerId}.");
                // TODO: Send error message to client (e.g., "Unknown action type")
                break;
        }

        // --- Check if Current Entity's Turn Should End ---
        // This check happens AFTER processing the action (successful or not, if slot was consumed)
        if (_encounterManager.AreCurrentEntityActionsExhausted())
        {
            Debug.Log($"CombatService: Entity {_encounterManager.GetCurrentTurnEntityName()} has used all actions. Advancing turn.");
            _encounterManager.AdvanceTurn();
        }
        else
        {
            // Optionally, send a message to the client confirming the action
            // and informing them how many actions they have left.
            // This would require defining a new message type or adding action data to existing messages.
            Debug.Log($"CombatService: Player {playerId} action processed. Actions remaining: {_encounterManager.GetActionsRemaining()}.");
            // TODO: Potentially send an update to the client about remaining actions.
        }
        // --- END Check Turn End ---
    }
    // --- End Player Action Processing ---


    // --- Helper Methods for Individual Actions ---

    /// <summary>
    /// Handles the 'use_ability' action type.
    /// </summary>
    private void ProcessUseAbilityAction(string playerId, PlayerConnection player, Dictionary<string, object> actionData)
    {
        // 1. Extract required data
        if (!actionData.TryGetValue("abilityId", out var abilityIdObj))
        {
            Debug.LogWarning($"CombatService: 'use_ability' action missing 'abilityId' for player {playerId}.");
            // TODO: Send error to client
            return;
        }

        if (!actionData.TryGetValue("target", out var targetObj))
        {
            Debug.LogWarning($"CombatService: 'use_ability' action missing 'target' for player {playerId}.");
            // TODO: Send error to client
            return;
        }

        string abilityId = abilityIdObj.ToString();
        string targetSpecifier = targetObj.ToString();

        // 2. Fetch Ability Definition
        AbilityDefinition abilityDef = AbilityExecutionService.Instance.GetAbilityDefenitionById(abilityId); // Use your actual lookup method
        if (abilityDef == null)
        {
            Debug.LogError($"CombatService: AbilityDefinition not found for ID: '{abilityId}' from player {playerId}.");
            // TODO: Send error to client (e.g., "Ability not found")
            // Decide if this consumes an action slot. Let's assume it doesn't if it fails to resolve.
            return;
        }

        // 3. Determine Action Cost
        int actionCost = abilityDef.actionCost;
        // Optional: Add validation for action cost
        if (actionCost <= 0)
        {
            Debug.LogWarning($"CombatService: Ability '{abilityId}' has invalid action cost ({actionCost}). Using cost of 1.");
            actionCost = 1; // Default or error handling
        }

        // 4. Check Action Availability using EncounterManager (with specific cost)
        if (!_encounterManager.IsActionAvailable(actionCost))
        {
            Debug.LogWarning($"CombatService: Player {playerId} tried to use ability '{abilityId}' (cost: {actionCost}), but only has {_encounterManager.GetActionsRemaining()} actions available.");
            // TODO: Send specific error to client (e.g., "Not enough actions")
            // Do not process the action further if not enough actions
            return;
        }

        // 5. Resolve Target using EncounterManager
        IDamageable resolvedTarget = _encounterManager.ResolveTarget(targetSpecifier, player);
        if (resolvedTarget == null)
        {
            Debug.LogWarning($"CombatService: Failed to resolve target '{targetSpecifier}' for ability {abilityId} from player {playerId}.");
            // TODO: Send error to client (e.g., "Invalid target")
            // Decide if this consumes an action slot. Let's assume it doesn't if it fails to resolve.
            // If it *should* consume on failed resolve, call _encounterManager.RecordActionUsed(actionCost) here.
            return;
        }

        // 6. Delegate to AbilityExecutionService
        List<IDamageable> targetList = new List<IDamageable> { resolvedTarget };
        bool success = AbilityExecutionService.Instance.ExecuteAbility(
            caster: player,
            targets: targetList,
            abilityDefinition: abilityDef,
            context: AbilityExecutionService.AbilityContext.InCombat
            // Note: isFromItem is false here as this is a direct ability use, not item use.
        );

        // 7. Handle result, Record Action Usage and broadcast entity updates for the targetted entitites
        if (success)
        {

            Debug.Log($"CombatService: Ability {abilityId} executed successfully for player {playerId}.");
            // broadcast entity update
            SendCombatEntityUpdate();

            // Record the action usage based on the actual ability cost.
            _encounterManager.RecordActionUsed(actionCost);
            // TODO: Potentially send success confirmation/update to client(s)
        }
        else
        {
            Debug.Log($"CombatService: Ability {abilityId} execution failed for player {playerId}.");
            // TODO: Send specific error message to client (e.g., "Ability execution failed")

            // Decide if a failed action still consumes the slot. Often it does.
            // Let's assume it does for simplicity, consuming the actions.
            _encounterManager.RecordActionUsed(actionCost);
            // TODO: Potentially send update to clients about the failed attempt and action count changes.
        }
    }

    /// <summary>
    /// Handles the 'use_item' action type.
    /// </summary>
    private void ProcessUseItemAction(string playerId, Dictionary<string, object> actionData)
    {
        // Handle item use during combat
        // This should route back to InventoryService.
        if (!actionData.TryGetValue("itemId", out var itemIdObj))
        {
            Debug.LogWarning($"CombatService: 'use_item' action missing 'itemId' for player {playerId}.");
            // TODO: Send error to client
            return;
        }

        string itemId = itemIdObj.ToString();

        // The InventoryService should handle the logic, including checking action costs
        // if the item use consumes actions. It might call AbilityExecutionService
        // with InCombat context if needed.
        bool itemUseInitiated = InventoryService.Instance.UseItem(playerId, itemId);

        if (itemUseInitiated)
        {
            // broadcast entity update
            SendCombatEntityUpdate();

            // Record the action usage (FOR NOW JUST THE DEFAULT COST OF 1!!!! TODO FIX THIS TO USE THE ACTUAL ACTIONS).
            _encounterManager.RecordActionUsed();

            Debug.Log($"CombatService: Item use initiated for player {playerId}, item ID {itemId}.");

        }
        else
        {
            Debug.LogWarning($"CombatService: Item use failed for player {playerId}, item ID {itemId}.");
            // The reason for failure should be logged/handled by InventoryService.
            // Decide if a failed item use (e.g. item not found, conditions not met)
            // should consume an action. Let's assume it does NOT consume an action if it fails to start.
        }

        // OLD Logic (just consumed 1 action regardless):
        // _encounterManager.RecordActionUsed(); // This is now handled internally by UseItem if needed.
    }
    // --- End Helper Methods ---

    // --- Message Handling ---

    public void HandleMessage(PlayerConnection player, Dictionary<string, object> msg)
    {
        // --- CORRECTED: Use player.NetworkId instead of player.SessionId ---
        string playerId = player.NetworkId;
        string currentTurnEnemyName = _encounterManager.GetCurrentTurnEntityName();
        // Route combat-specific messages
        if (_isInCombat && playerId == currentTurnEnemyName)
        {
            // It's this player's turn, process their combat action
            ProcessPlayerCombatAction(playerId, msg);
        }
        else if (_isInCombat)
        {
            // It's combat, but not this player's turn
            Debug.Log($"Received combat message from {playerId}, but it's not their turn. Turn is {currentTurnEnemyName}'s.");
            // TODO: Send error message to client
        }
        else
        {
            // Not in combat, ignore or handle differently?
            Debug.Log($"Received combat message from {playerId}, but not in combat.");
            Debug.Log($"IsIncombat: {_isInCombat}. Current turn entity name {currentTurnEnemyName}");
            // TODO: Send error message to client or ignore
        }
    }
    // --- End Message Handling ---



    // --- Combat Flow Management ---

    /// <summary>
    /// Starts a combat encounter. (Placeholder logic, needs EncounterManager)
    /// </summary>
    public void StartCombat(List<PlayerConnection> players /*, Encounter details */)
    {
        _isInCombat = true;
        // TODO: Initialize EncounterManager with players and enemies
        // TODO: Determine initial turn order
        // TODO: Set _currentTurnPlayerId
        // TODO: Send combat start message to all players
        Debug.Log("Combat started!");
    }

    /// <summary>
    /// Ends the current combat encounter. (Placeholder logic)
    /// </summary>
    public void EndCombat()
    {
        // TODO: distribute rewards, etc.


        // Determine the outcome (simplified)
        string result = "unknown";
        string message = "The encounter has concluded.";
        if (_encounterManager.CurrentState == EncounterManager.EncounterState.Victory)
        {
            result = "victory";
            message = "Victory! You have defeated the enemies!";
        }
        else if (_encounterManager.CurrentState == EncounterManager.EncounterState.Defeat)
        {
            result = "defeat";
            message = "Defeat! You have been overwhelmed...";
        }

        // --- Prepare Encounter End Data ---
        var endData = new Dictionary<string, object>
        {
            ["type"] = "encounter_end",
            ["result"] = result,
            ["message"] = message
            // TODO: Include rewards, experience gained, loot drops, etc.
            // ["rewards"] = new { xp = 100, items = new List<object> { ... } }
        };
        // --- End Prepare Data ---

        // --- Send Encounter End Message to ALL Players in the Encounter ---
        foreach (var player in _encounterManager.GetActivePlayers())
        {
            if (player != null && player.NetworkId != null)
            {
                GameServer.Instance.SendToPlayer(player.NetworkId, endData);
                Debug.Log($"CombatService: Sent 'encounter_end' message (result: {result}) to player {player.LobbyData?.Name ?? player.NetworkId}");
            }
        }

        _isInCombat = false;


        // Clean up EncounterManager state
        _encounterManager.CleanUpEncounter();

        Debug.Log("Combat ended!");
        // Change state to map
        GameStateManager.Instance.ChangeState(GameStateManager.GameState.Map);
    }

    

    private void EndTurn()
    {
        // Logic to determine next player, update state, send turn info to clients
        // TODO: Integrate with EncounterManager for turn order and enemy turns
        // _currentTurnPlayerId = GetNextPlayerInTurnOrder(); // Implement this
        // Send message to all players about whose turn it is now
        // BroadcastCombatState(); // Example
        Debug.Log($"Turn ended. Next turn logic needs implementation.");
    }



    // --- Lifecycle and State Handling ---
    public void OnPlayerDisconnected(PlayerConnection player)
    {
        // Handle player disconnect during combat
        Debug.Log($"CombatService: Player {player?.NetworkId ?? "Unknown"} disconnected during combat.");
        // TODO: Decide on logic (forfeit player's turns, AI control, end encounter if all players leave?)
        // The EncounterManager's lists will still contain the PlayerConnection object.
        // If the player reconnects, their object reference is the same, so state is mostly preserved.
    }

    public void OnPlayerReconnected(PlayerConnection player)
    {
        // Handle player reconnect during combat
        Debug.Log($"CombatService: Player {player?.NetworkId ?? "Unknown"} reconnected during combat.");
        // TODO: Send current encounter state to the reconnected player
        // This involves serializing the state from EncounterManager and sending it.
    }
    // Called when leaving the combat scene or ending the game session
    private void OnDestroy()
    {
        // Unsubscribe from events to prevent errors if the service is destroyed
        if (_encounterManager != null)
        {
            _encounterManager.OnEncounterStarted -= OnEncounterStarted_Internal;
            _encounterManager.OnEncounterEnded -= OnEncounterEnded_Internal;
            _encounterManager.OnTurnStarted -= OnTurnStarted_Internal;
            _encounterManager.OnPlayerAdded -= OnPlayerAdded_Internal;
            _encounterManager.OnEnemyAdded -= OnEnemyAdded_Internal;
            _encounterManager.OnEnemyDefeated -= OnEnemyDefeated_Internal;
        }
    }
    // --- End Lifecycle ---
}