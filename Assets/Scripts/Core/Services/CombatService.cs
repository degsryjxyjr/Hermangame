// File: Scripts/Core/Services/CombatService.cs (Refactored)
using UnityEngine;
using System.Collections.Generic;
using System.Linq; // For potential targeting logic

public class CombatService : MonoBehaviour
{
    public static CombatService Instance { get; private set; }

    // --- Basic Combat State (To be expanded with EncounterManager) ---
    private bool _isInCombat = false;
    private string _currentTurnPlayerId = null; // Network ID of the player whose turn it is
    // TODO: Integrate with EncounterManager for active players/enemies, turn order
    // --- End Basic Combat State ---

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
                playersData.Add(new
                {
                    id = player.NetworkId, // Use NetworkId for identification
                    name = player.LobbyData?.Name ?? "Unknown Player",
                    currentHealth = player.CurrentHealth,
                    maxHealth = player.MaxHealth,
                    attack = player.Attack,
                    defense = player.Defense,
                    magic = player.Magic,
                    isAlive = player.IsAlive()
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
                // Get the base definition name, or fallback
                string enemyName = enemy.GetEntityName() ?? "Unknown Enemy";
                // If EnemyEntity stores a reference to its definition, you can get more details
                // string enemyName = enemy.EnemyDefinition?.displayName ?? enemy.GetEntityName() ?? "Unknown Enemy";

                enemiesData.Add(new
                {
                    // Use InstanceID or a unique ID assigned by EncounterManager for client targeting
                    id = enemy.GetInstanceID().ToString(), 
                    name = enemyName,
                    currentHealth = enemy.CurrentHealth,
                    maxHealth = enemy.MaxHealth,
                    attack = enemy.Attack,
                    defense = enemy.Defense,
                    magic = enemy.Magic,
                    isAlive = enemy.IsAlive()
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
            if (player != null)
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
            if (player != null)
            {
                GameServer.Instance.SendToPlayer(player.NetworkId, endData);
                Debug.Log($"CombatService: Sent 'encounter_end' message (result: {result}) to player {player.LobbyData?.Name ?? player.NetworkId}");
            }
        }
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

        // Determine the ID and type of the entity whose turn it is
        if (turnEntity is PlayerConnection player)
        {
            entityId = player.NetworkId;
            entityType = "player";
        }
        else if (turnEntity is EnemyEntity enemy)
        {
            entityId = enemy.GetInstanceID().ToString(); // Or a unique ID from EncounterManager
            entityType = "enemy";
        }

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
                name = GetEntityNameSafe(turnEntity)
            },
            ["message"] = $"{GetEntityNameSafe(turnEntity)}'s turn begins!"
        };
        // --- End Prepare Data ---

        // --- Send Turn Start Message to ALL Players in the Encounter ---
        // Everyone needs to know whose turn it is to update their UI.
        foreach (var p in _encounterManager.GetActivePlayers())
        {
            if (p != null)
            {
                GameServer.Instance.SendToPlayer(p.NetworkId, turnData);
                Debug.Log($"CombatService: Sent 'turn_start' message for {entityType} {GetEntityNameSafe(turnEntity)} to player {p.LobbyData?.Name ?? p.NetworkId}");
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

            // Note: EnemyEntity doesn't inherit from PlayerConnection.
            // AbilityExecutionService.ExecuteAbility expects a PlayerConnection caster.
            // We need to decide how to handle non-player casters.
            // Option 1: Modify AbilityExecutionService to accept IEntity or a base class for casters.
            // Option 2: Create a dummy/placeholder PlayerConnection for the enemy (not ideal).
            // Option 3: Handle enemy actions differently (e.g., a separate EnemyAction system).
            // For prototype, let's assume we can cast it somehow or handle it specially.
            // Let's log it for now.

            Debug.Log($"CombatService: Enemy {enemy.GetEntityName()} chose to use {chosenAbility.abilityName} on {chosenTarget.GetEntityName()}.");

            // TODO: Implement actual enemy action execution.
            // This requires resolving the caster issue mentioned above.
            // Pseudo-code:
            // bool success = AbilityExecutionService.Instance.ExecuteAbility(/* enemy as caster */, targets, chosenAbility, AbilityExecutionService.AbilityContext.InCombat);
            // if (success)
            // {
            //     // Handle success (e.g., advance turn)
            //     _encounterManager.AdvanceTurn();
            // }
        }
        else
        {
            Debug.Log($"CombatService: Enemy {enemy.GetEntityName()} has no valid action. Ending turn.");
            // If no action, just advance the turn.
            _encounterManager.AdvanceTurn();
        }
    }
    // --- End Enemy AI Trigger ---

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
            // TODO: Send error
            return;
        }

        var currentPlayer = PlayerManager.Instance.GetPlayer(playerId);
        if (currentPlayer == null || _encounterManager.CurrentTurnEntity != currentPlayer)
        {
            Debug.LogWarning($"CombatService: Player {playerId} tried to act, but it's not their turn ({GetEntityNameSafe(_encounterManager.CurrentTurnEntity)}).");
            // TODO: Send error
            return;
        }
        
        Debug.Log($"CombatService: Received combat action from player {playerId}. Action Data: {JsonUtility.ToJson(actionData)}");

        // 2. Parse action
        if (actionData.TryGetValue("type", out var typeObj))
        {
            string actionType = typeObj.ToString();

            switch (actionType)
            {
                case "use_ability":
                    if (actionData.TryGetValue("abilityId", out var abilityIdObj) &&
                        actionData.TryGetValue("target", out var targetObj))
                    {
                        string abilityId = abilityIdObj.ToString();
                        string targetSpecifier = targetObj.ToString();

                        // 3. Fetch Ability Definition (placeholder)
                        var abilityDef = Resources.Load<AbilityDefinition>($"Abilities/{abilityId}");
                        if (abilityDef == null)
                        {
                            Debug.LogWarning($"CombatService: AbilityDefinition not found: {abilityId}");
                            // TODO: Send error
                            return;
                        }

                        // 4. Resolve Target using EncounterManager
                        IDamageable resolvedTarget = _encounterManager.ResolveTarget(targetSpecifier, currentPlayer);
                        if (resolvedTarget == null)
                        {
                            Debug.LogWarning($"CombatService: Failed to resolve target '{targetSpecifier}' for ability {abilityId}.");
                            // TODO: Send error
                            return;
                        }

                        // 5. Delegate to AbilityExecutionService
                        List<IDamageable> targetList = new List<IDamageable> { resolvedTarget };
                        bool success = AbilityExecutionService.Instance.ExecuteAbility(
                            caster: currentPlayer,
                            targets: targetList,
                            abilityDefinition: abilityDef,
                            context: AbilityExecutionService.AbilityContext.InCombat
                        );

                        // 6. Handle result
                        if (success)
                        {
                            // Advance turn regardless of action success for now.
                            // More complex systems might have reactions/extra actions.
                            _encounterManager.AdvanceTurn();
                        }
                        else
                        {
                            Debug.Log($"CombatService: Ability {abilityId} execution failed for player {playerId}.");
                            // TODO: Send specific error message to client
                            // Do not advance turn automatically on failure? Depends on game rules.
                            // For now, let's advance to keep the game moving.
                            _encounterManager.AdvanceTurn();
                        }
                    }
                    break;

                case "melee_attack":
                    // Handle basic attack
                    Debug.Log("CombatService: Basic melee attack logic needs full implementation.");
                    // This could be an ability or direct damage calculation.
                    // For now, advance turn.
                    _encounterManager.AdvanceTurn();
                    break;

                case "use_item":
                    // Handle item use during combat
                    // This should ideally be routed back to InventoryService.
                    // Or, InventoryService.UseItem could be called directly by PlayerManager
                    // based on the message type, and it knows to use AbilityExecutionService
                    // with InCombat context if the game state is Combat.
                    // Let's assume InventoryService handles it correctly now.
                    if (actionData.TryGetValue("itemId", out var itemIdObj))
                    {
                        string itemId = itemIdObj.ToString();
                        // Delegate to InventoryService. It should check GameState and use the correct context.
                        // InventoryService.Instance.UseItem(playerId, itemId);
                        // Assume InventoryService handles turn advancement if needed, or we do it here.
                        // For consistency, let's advance the turn after any player action for now.
                        _encounterManager.AdvanceTurn();
                    }
                    break;

                default:
                    Debug.LogWarning($"CombatService: Unknown combat action type: {actionType}");
                    // TODO: Send error
                    break;
            }
        }
    }
    // --- End Player Action Processing ---

    // --- Message Handling ---

    public void HandleMessage(PlayerConnection player, Dictionary<string, object> msg)
    {
        // --- CORRECTED: Use player.NetworkId instead of player.SessionId ---
        string playerId = player.NetworkId;

        // Route combat-specific messages
        if (_isInCombat && playerId == _currentTurnPlayerId)
        {
            // It's this player's turn, process their combat action
            ProcessPlayerCombatAction(playerId, msg);
        }
        else if (_isInCombat)
        {
             // It's combat, but not this player's turn
             Debug.Log($"Received combat message from {playerId}, but it's not their turn ({_currentTurnPlayerId}).");
             // TODO: Send error message to client
        }
        else
        {
            // Not in combat, ignore or handle differently?
            Debug.Log($"Received combat message from {playerId}, but not in combat.");
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
        _isInCombat = false;
        _currentTurnPlayerId = null;
        // TODO: Clean up EncounterManager state
        // TODO: Send combat end message, distribute rewards, etc.
        Debug.Log("Combat ended!");
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