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
    public void InitializeForEncounter(List<PlayerConnection> players /*, Encounter details */)
    {
        Debug.Log("CombatService: Initializing for new encounter.");

        // 1. Find or create EncounterManager
        // Option A: Assume one exists in the scene (e.g., on a dedicated GameObject)
        _encounterManager = FindFirstObjectByType<EncounterManager>();
        // Option B: Create one dynamically
        // if (_encounterManager == null)
        // {
        //     GameObject emGO = new GameObject("EncounterManager");
        //     _encounterManager = emGO.AddComponent<EncounterManager>();
        // }

        if (_encounterManager == null)
        {
            Debug.LogError("CombatService: Could not find or create EncounterManager!");
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
        List<EnemyDefinition> enemiesToSpawn = new List<EnemyDefinition>(); // Placeholder
        // Example: enemiesToSpawn.Add(Resources.Load<EnemyDefinition>("Enemies/Goblin"));
        _encounterManager.StartEncounter(players, enemiesToSpawn /*, other details */);

        // The EncounterManager will now spawn enemies, set up turn order, and notify us via events.
    }

    // --- EncounterManager Event Handlers ---
    private void OnEncounterStarted_Internal()
    {
        Debug.Log("CombatService: Received OnEncounterStarted from EncounterManager.");
        // TODO: Send initial encounter state to all clients
        // e.g., player/enemy HP, turn order
        // GameServer.Instance.Broadcast(new { type = "encounter_start", ... });
    }

    private void OnEncounterEnded_Internal()
    {
        Debug.Log("CombatService: Received OnEncounterEnded from EncounterManager.");
        EncounterManager.EncounterState endState = _encounterManager.CurrentState;
        // TODO: Handle victory/defeat
        // - Award XP, distribute loot
        // - Transition to Loot scene or Map scene
        // - Update player states
        // GameStateManager.Instance.ChangeState(GameStateManager.GameState.Loot); // or Map
        // For now, just log
        if (endState == EncounterManager.EncounterState.Victory)
        {
            Debug.Log("CombatService: Players are victorious!");
        }
        else if (endState == EncounterManager.EncounterState.Defeat)
        {
            Debug.Log("CombatService: Players have been defeated!");
        }
    }

    private void OnTurnStarted_Internal(object turnEntity)
    {
        Debug.Log($"CombatService: Received OnTurnStarted for {GetEntityNameSafe(turnEntity)}.");
        // TODO: Send message to all clients about whose turn it is.
        // GameServer.Instance.Broadcast(new { type = "turn_start", entity = ... });
        
        // If it's an enemy's turn, trigger its AI
        if (turnEntity is EnemyEntity enemy)
        {
            TriggerEnemyAI(enemy);
        }
    }

    private void OnPlayerAdded_Internal(PlayerConnection player) { /* ... */ }
    private void OnEnemyAdded_Internal(EnemyEntity enemy)
    {
        // When an enemy is added, give it a reference back to this EncounterManager
        // so it can call OnEnemyDefeatedInternal when it dies.
        if (enemy != null)
        {
            enemy.EncounterManager = _encounterManager; // Or this reference if EncounterManager doesn't pass itself
        }
    }
    private void OnEnemyDefeated_Internal(EnemyEntity enemy)
    {
        Debug.Log($"CombatService: Received OnEnemyDefeated for {enemy?.GetEntityName() ?? "Unknown Enemy"}.");
        // TODO: Handle enemy defeat
        // - Generate/distribute loot
        // - Award XP
        // - Update client UI
        // GameServer.Instance.Broadcast(new { type = "enemy_defeated", enemyId = ... });
    }
    // --- End Event Handlers ---

    // --- Helper for Event Handlers ---
    private string GetEntityNameSafe(object entity)
    {
        if (entity is IEntity iEntity) return iEntity.GetEntityName();
        if (entity != null) return entity.ToString();
        return "Null";
    }
    // --- End Helper ---

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