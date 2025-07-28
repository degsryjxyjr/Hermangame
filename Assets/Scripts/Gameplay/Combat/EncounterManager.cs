// File: Scripts/Gameplay/Combat/EncounterManager.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq; // For sorting if needed
using System;

/// <summary>
/// Manages the state and flow of a single combat encounter.
/// Tracks active players and enemies, turn order, and handles encounter-specific logic.
/// </summary>
public class EncounterManager : MonoBehaviour
{
    public static EncounterManager Instance { get; private set; }



    // --- Encounter State ---
    public enum EncounterState
    {
        NotStarted,
        Active,
        Victory,   // Players win
        Defeat     // Players lose
    }

    [Header("Test Setup")]
    [Tooltip("Drag an EnemyDefinition here for quick testing.")]
    public EnemyDefinition testEnemyDefinition;

    [Header("Enemy Spawn Transfrom")]
    [Tooltip("Drag a Transform here for the enemy spawnpoint.")]
    public Transform enemySpawnPoint;


    private EncounterState _currentState = EncounterState.NotStarted;

    // --- Participants ---
    // Use Lists to maintain order, Dictionaries for fast lookup by ID/Instance
    private List<PlayerConnection> _activePlayers = new List<PlayerConnection>();
    private Dictionary<string, PlayerConnection> _activePlayersDict = new Dictionary<string, PlayerConnection>(); // Key: NetworkId

    private List<EnemyEntity> _activeEnemies = new List<EnemyEntity>();
    private Dictionary<int, EnemyEntity> _activeEnemiesDict = new Dictionary<int, EnemyEntity>(); // Key: InstanceId (or a unique ID you assign)

    // --- Turn Order ---
    // A simple list for now. Index 0 is the current turn holder.
    // You can expand this to a more complex initiative system later.
    private List<object> _turnOrder = new List<object>(); // Can hold PlayerConnection or EnemyEntity
    private int _currentTurnIndex = -1; // -1 means no turn active yet
    public object _currentTurnEntity = null; // This will now be IActionBudget

    // --- Events ---
    // These can be used to notify the CombatService or UI about state changes.
    public System.Action OnEncounterStarted;
    public System.Action OnEncounterEnded; // Pass victory/defeat state?
    public System.Action<object> OnTurnStarted; // Pass the entity whose turn it is
    public System.Action<PlayerConnection> OnPlayerAdded;
    public System.Action<EnemyEntity> OnEnemyAdded;
    public System.Action<EnemyEntity> OnEnemyDefeated; // Could pass loot drops here

    // Properties
    public EncounterState CurrentState => _currentState;
    public object CurrentTurnEntity => _currentTurnEntity;
    public List<PlayerConnection> GetActivePlayers() => new List<PlayerConnection>(_activePlayers);
    public List<EnemyEntity> GetActiveEnemies() => new List<EnemyEntity>(_activeEnemies);

    public List<object> GetTurnOrder() => new List<object>(_turnOrder); // Return copy to prevent external modification
    public int GetActivePlayerCount() => _activePlayers.Count;
    public int GetActiveEnemyCount() => _activeEnemies.Count;



    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // If another instance exists, destroy this one
            Debug.LogWarning($"Duplicate EncounterManager instance detected! Destroying {gameObject.name}");
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        Debug.Log("EncounterManager initialized as singleton");
    }

    // --- Initialization ---
    /// <summary>
    /// Starts the encounter with the given players and enemy definitions.
    /// </summary>
    public void StartEncounter(List<PlayerConnection> players, List<EnemyDefinition> enemyDefinitions /*, other encounter details */)
    {
        if (_currentState != EncounterState.NotStarted)
        {
            Debug.LogWarning("Cannot start encounter, state is not NotStarted.");
            return;
        }
        Debug.Log("EncounterManager: Starting new encounter.");
        _currentState = EncounterState.Active;

        // 1. Add Players
        foreach (var player in players)
        {
            if (player != null)
            {
                AddPlayer(player);
            }
        }
        CombatService.Instance.StartCombat(players);
        // 2. Spawn and Add Enemies
        foreach (var enemyDef in enemyDefinitions)
        {
            if (enemyDef != null)
            {
                SpawnEnemy(enemyDef, enemySpawnPoint.position);
            }
        }

        // 3. Build Turn Order (Initial)
        // A simple, fixed order for now. Players first, then enemies.
        BuildInitialTurnOrder();

        // 4. Start the first turn (which will reset the first entity's action budget)
        AdvanceTurn();

        // 5. Notify listeners
        OnEncounterStarted?.Invoke();
        Debug.Log($"EncounterManager: Encounter started with {_activePlayers.Count} players and {_activeEnemies.Count} enemies.");
    }

    private void AddPlayer(PlayerConnection player)
    {
        if (player == null || _activePlayersDict.ContainsKey(player.NetworkId))
        {
            Debug.LogWarning($"Cannot add player {player?.NetworkId ?? "null"} to encounter (null or duplicate).");
            return;
        }
        _activePlayers.Add(player);
        _activePlayersDict.Add(player.NetworkId, player);
        OnPlayerAdded?.Invoke(player);
        Debug.Log($"EncounterManager: Added player {player.LobbyData?.Name ?? "Unknown"} ({player.NetworkId}) to encounter.");
    }

    private void SpawnEnemy(EnemyDefinition enemyDef, Vector3 position, int level = 1)
    {
        if (enemyDef?.modelPrefab == null)
        {
            Debug.LogError($"Cannot spawn enemy, definition or modelPrefab is null for {enemyDef?.displayName ?? "Unknown Enemy"}.");
            return;
        }

        // 1. Instantiate the enemy's visual/model prefab
        Debug.Log($"Attempting to spawn enemy prefab: {enemyDef.modelPrefab.name} at position {position}");
        GameObject enemyGO = Instantiate(enemyDef.modelPrefab, position, Quaternion.identity);
        Debug.Log($"Spawned enemy GameObject: {enemyGO.name} at position {enemyGO.transform.position}");


        // 2. Get or Add the EnemyEntity component
        EnemyEntity enemyEntity = enemyGO.GetComponent<EnemyEntity>();
        if (enemyEntity == null)
        {
            enemyEntity = enemyGO.AddComponent<EnemyEntity>();
            Debug.LogWarning($"Enemy prefab {enemyDef.modelPrefab.name} did not have EnemyEntity component. Added one.");
        }

        // 3. Initialize the EnemyEntity with its definition and level
        // This requires the EnemyEntity script to have a public method or be set up via inspector/script.
        // Assuming EnemyEntity has a method or is set up to take the definition.
        // If EnemyEntity has a serialized field for EnemyDefinition, you might set it here or ensure it's pre-configured on the prefab.
        // For now, let's assume it reads the definition from its serialized field or Awake handles it if not set.
        // If you need to pass data explicitly:
        enemyEntity.InitializeFromDefinition(level);
        
        enemyEntity.EncounterManager = this; // Set the reference
        // EnemyEntity.Awake should then initialize stats etc. based on the definition.
        // If level needs to be passed explicitly and EnemyEntity.Awake runs before we can set it,
        // you might need to call an Initialize method after setting the definition.
        // Let's assume EnemyEntity.Awake handles initialization if _enemyDefinition is set.
        
        // 4. Register the enemy with this EncounterManager
        _activeEnemies.Add(enemyEntity);
        // Use InstanceId for now, but consider a more robust unique ID if needed.
        _activeEnemiesDict.Add(enemyEntity.GetInstanceID(), enemyEntity); 
        OnEnemyAdded?.Invoke(enemyEntity);
        Debug.Log($"EncounterManager: Spawned and added enemy {enemyEntity.GetEntityName()} (Instance ID: {enemyEntity.GetInstanceID()}) to encounter.");
    }

    private void BuildInitialTurnOrder()
    {
        _turnOrder.Clear();
        // Simple example: Add all players, then all enemies
        _turnOrder.AddRange(_activePlayers.Cast<object>());
        _turnOrder.AddRange(_activeEnemies.Cast<object>());

        // For a more complex system, you'd sort based on initiative/speed here.
        // Example (requires speed stat and sorting logic):
        // _turnOrder = _turnOrder.OrderBy(entity => {
        //     if (entity is IEntity e) return -e.GetSpeed(); // Assuming higher speed goes first
        //     return 0;
        // }).ToList();

        _currentTurnIndex = -1; // Reset index
        _currentTurnEntity = null; // Reset current entity
        Debug.Log($"EncounterManager: Built initial turn order with {_turnOrder.Count} entities.");
    }

    // --- Turn Management ---
    /// <summary>
    /// Advances the turn to the next entity in the turn order.
    /// Automatically handles ending the encounter if win/loss conditions are met.
    /// </summary>
    public void AdvanceTurn()
    {
        // --- NEW: No specific reset needed here for the *previous* entity ---
        // The IActionBudget.ResetActionBudgetForNewTurn() will handle the new entity's state.
        // --- END NEW ---

        if (_currentState != EncounterState.Active)
        {
            Debug.LogWarning("Cannot advance turn, encounter is not active.");
            return;
        }

        // Check win/loss before advancing
        if (CheckEncounterEndConditions())
        {
            // Encounter ended, don't proceed with turn advancement
            return;
        }

        // Find the next alive entity in the turn order
        int startIndex = _currentTurnIndex;
        int attempts = 0;
        int maxAttempts = _turnOrder.Count;

        do
        {
            _currentTurnIndex = (_currentTurnIndex + 1) % _turnOrder.Count;
            _currentTurnEntity = _turnOrder[_currentTurnIndex];
            attempts++;

            // Check if the entity is alive
            bool isAlive = false;
            if (_currentTurnEntity is IEntity entity)
            {
                isAlive = entity.IsAlive();
            }
            else if (_currentTurnEntity is PlayerConnection player)
            {
                isAlive = player.IsAlive(); // Assuming PlayerConnection has IsAlive()
            }

            if (isAlive)
            {
                break; // Found an alive entity, break the loop
            }
            else
            {
                _currentTurnEntity = null; // Mark as no valid turn entity found yet for this iteration
            }

        } while (_currentTurnIndex != startIndex && attempts < maxAttempts);

        // Final check if we found a valid, alive entity
        if (_currentTurnEntity == null || !IsCurrentTurnEntityAlive())
        {
            Debug.LogError("EncounterManager: Could not find a valid, alive entity for the next turn after cycling through the turn order!");
            // Handle this error state, maybe end encounter in a draw or error?
            _currentState = EncounterState.Defeat; // Or NotStarted? Needs definition.
            EndEncounter();
            return; // Don't start a turn if the encounter ended
        }

        // A valid, alive entity has the turn

        // --- NEW: Reset Action State for the *New* Current Entity ---
        if (_currentTurnEntity is IActionBudget newActionEntity)
        {
            newActionEntity.ResetActionBudgetForNewTurn();
            Debug.Log($"EncounterManager: Turn advanced. Current turn: {GetCurrentTurnEntityName()} (Index: {_currentTurnIndex}). Action budget reset.");
        }
        else
        {
             Debug.LogWarning($"EncounterManager: Turn advanced. Current turn: {GetCurrentTurnEntityName()} (Index: {_currentTurnIndex}). This entity does not implement IActionBudget.");
        }
        // --- END NEW ---

        OnTurnStarted?.Invoke(CurrentTurnEntity);
        Debug.Log($"EncounterManager: Turn advanced. Current turn: {GetCurrentTurnEntityName()} (Index: {_currentTurnIndex})");

        // TODO: Send message to all clients about whose turn it is.
        // This might involve sending the entire turn order or just the current entity ID.
    }

    private bool IsCurrentTurnEntityAlive()
    {
        if (_currentTurnEntity is EnemyEntity entity) return entity.IsAlive();
        if (_currentTurnEntity is PlayerConnection player) return player.IsAlive();
        return false; // Shouldn't happen if turn order is managed correctly
    }
    // --- END Modified: AdvanceTurn Logic ---

    private string Get_currentTurnEntityName()
    {
        if (_currentTurnEntity is IEntity entity)
        {
            return entity.GetEntityName();
        }
        else if (_currentTurnEntity != null)
        {
            return _currentTurnEntity.ToString();
        }
        return "None";
    }


    

    // --- Entity Management ---
    /// <summary>
    /// Called when an enemy is defeated (e.g., by its OnDeath method).
    /// Removes the enemy from active lists and checks end conditions.
    /// </summary>
    public void OnEnemyDefeatedInternal(EnemyEntity enemy)
    {
        if (enemy == null || !_activeEnemies.Contains(enemy))
        {
            // Enemy might already be removed or was not part of this encounter
            return;
        }

        Debug.Log($"EncounterManager: Enemy {enemy.GetEntityName()} has been defeated.");
        _activeEnemies.Remove(enemy);
        _activeEnemiesDict.Remove(enemy.GetInstanceID()); // Or use its unique ID if you have one

        // Remove from turn order
        _turnOrder.Remove(enemy);
        // If the defeated enemy was the current turn holder, advance the turn immediately?
        // Or let the normal AdvanceTurn logic handle it on the next cycle?
        // Let's let AdvanceTurn handle it to keep logic centralized there.
        // if (_currentTurnEntity == enemy)
        // {
        //     Debug.Log("Defeated enemy was the current turn holder. Advancing turn...");
        //     AdvanceTurn(); // This might cause issues if called mid-action. Better handled by CombatService after action resolves.
        // }

        // Determine loot drops (if needed here, or handled by EnemyEntity.OnDeath -> CombatService -> here)
        // List<ItemDefinition> loot = DetermineLoot(enemy); // Implement if needed

        // Notify listeners (e.g., CombatService can handle rewards, UI updates)
        OnEnemyDefeated?.Invoke(enemy);

        // Check if this ends the encounter
        CheckEncounterEndConditions();
    }

    private bool CheckEncounterEndConditions()
    {
        // Check if all players are dead
        if (_activePlayers.All(p => !p.IsAlive()))
        {
            Debug.Log("EncounterManager: All players defeated. Player defeat!");
            _currentState = EncounterState.Defeat;
            EndEncounter();
            return true;
        }
        // Check if all enemies are dead
        if (_activeEnemies.All(e => !e.IsAlive()))
        {
            Debug.Log("EncounterManager: All enemies defeated. Player victory!");
            _currentState = EncounterState.Victory;
            EndEncounter();
            return true;
        }

        // If neither, the encounter continues.
        Debug.Log("EncounterManager: Encounter continues. Players: " +
                  $"{_activePlayers.Count(p => p.IsAlive())}/{_activePlayers.Count}, " +
                  $"Enemies: {_activeEnemies.Count}");
        return false;
    }

    public string GetCurrentTurnEntityName()
    {
        if (_currentTurnEntity is EnemyEntity enemy) return enemy.GetEntityName();
        if (_currentTurnEntity is PlayerConnection player) return player.NetworkId;
        return "Unknown Entity";
    }

    


    private void EndEncounter()
    {
        Debug.Log($"EncounterManager: Encounter ended with state: {_currentState}");
        // Perform any cleanup if necessary
        // Stop any running coroutines, clear references, etc.

        // Notify listeners (CombatService should handle rewards, scene transitions, etc.)
        OnEncounterEnded?.Invoke();

        // TODO: Handle rewards, experience, loot distribution (likely in CombatService)
    }

    // --- Target Resolution ---
    /// <summary>
    /// Resolves a target specifier string (like "enemy_1", "player_2", "self") into an IDamageable instance.
    /// This is a simplified version. A full system needs more context (who is targeting).
    /// </summary>
    /// <param name="targetSpecifier">The string specifier (e.g., "enemy_1").</param>
    /// <param name="requester">The entity requesting the target (for context like "self" or enemy targeting players).</param>
    /// <returns>The resolved IDamageable entity, or null if not found or invalid.</returns>
    public IDamageable ResolveTarget(string targetSpecifier, object requester)
    {
        if (string.IsNullOrEmpty(targetSpecifier) || _currentState != EncounterState.Active)
        {
            return null;
        }

        // Example logic for simple specifiers
        if (targetSpecifier.StartsWith("enemy_"))
        {
            if (int.TryParse(targetSpecifier.Substring(6), out int index) && index > 0)
            {
                // "enemy_1" -> index 1 -> list index 0
                int listIndex = index - 1;
                if (listIndex < _activeEnemies.Count)
                {
                    return _activeEnemies[listIndex];
                }
            }
        }
        else if (targetSpecifier.StartsWith("player_"))
        {
            // This is trickier as player specifiers might be NetworkId or an index.
            // Let's assume it's the NetworkId for now.
            string playerId = targetSpecifier.Substring(7); // Remove "player_"
            if (_activePlayersDict.TryGetValue(playerId, out PlayerConnection player))
            {
                return player;
            }
        }
        else if (targetSpecifier == "self")
        {
            // Return the requester if it's a valid damageable entity
            if (requester is IDamageable damageable)
            {
                return damageable;
            }
        }
        // Example: Resolve by InstanceID string for enemies
        if (int.TryParse(targetSpecifier, out int instanceId) && _activeEnemiesDict.TryGetValue(instanceId, out EnemyEntity enemyTarget))
        {
            return enemyTarget;
        }
        // Add logic for "ally_1", "all_enemies", etc. as needed.

        Debug.Log($"EncounterManager: Could not resolve target specifier '{targetSpecifier}'");
        return null;
    }




    // --- Methods to Record Actions (via Interface) ---

    /// <summary>
    /// Records that an action has been used by the current entity.
    /// Should be called by CombatService after any successful action.
    /// </summary>
    /// <param name="actionCost">The cost of the action (defaults to 1)</param>
    public void RecordActionUsed(int actionCost = 1)
    {
        if (_currentTurnEntity is IActionBudget actionEntity)
        {
            try
            {
                bool consumed = actionEntity.ConsumeAction(actionCost);
                if (consumed)
                {
                    Debug.Log($"EncounterManager: {actionCost} cost action recorded for current entity. " +
                            $"Remaining: {actionEntity.ActionsRemaining}/{actionEntity.TotalActions}");
                }
                else
                {
                    Debug.LogWarning($"EncounterManager: Failed to record {actionCost} cost action for current entity. " +
                                    $"Insufficient actions ({actionEntity.ActionsRemaining}/{actionEntity.TotalActions})");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"EncounterManager: Error recording action for current entity: {ex.Message}");
            }
        }
        else
        {
            Debug.LogWarning("EncounterManager: RecordActionUsed called, but current turn entity does not implement IActionBudget.");
        }
    }

    /// <summary>
    /// Checks if the current entity has exhausted their action budget.
    /// Used by CombatService to determine if the entity's turn should end.
    /// </summary>
    /// <returns>
    /// True if actions are exhausted (or entity doesn't use actions).
    /// False if actions remain.
    /// </returns>
    public bool AreCurrentEntityActionsExhausted()
    {
        if (_currentTurnEntity is IActionBudget actionEntity)
        {
            bool exhausted = actionEntity.ActionsRemaining <= 0;
            if (exhausted)
            {
                //Debug.Log($"EncounterManager: Current entity has exhausted their action budget");
            }
            return exhausted;
        }
        // Entities without action budgets don't auto-end turns from action exhaustion
        return false; 
    }

    public int GetActionsRemaining()
    {
        if (_currentTurnEntity is IActionBudget actionEntity)
        {
            int actions = actionEntity.ActionsRemaining;
            return actions;
        }
        Debug.LogWarning("Couldnt get actions remaining as entiy doesnt implement IActionBudget");
        return 0; // Not an entity with an action budget
    }

    // --- END: Methods to Record Actions ---

    // --- Helper Methods to Check Action Availability ---

    /// <summary>
    /// Checks if the current entity has any actions available
    /// </summary>
    /// <param name="requiredActions">Minimum number of actions needed (default 1)</param>
    /// <returns>True if entity can perform at least one action</returns>
    public bool IsActionAvailable(int requiredActions = 1)
    {
        if (_currentTurnEntity is IActionBudget actionEntity)
        {
            bool available = actionEntity.ActionsRemaining >= requiredActions;
            if (!available)
            {
                Debug.Log($"EncounterManager: current entity needs {requiredActions} action(s), " +
                        $"but only has {actionEntity.ActionsRemaining}/{actionEntity.TotalActions}");
            }
            return available;
        }
        return false; // Not an entity with an action budget
    }

    // --- END Helper Methods ---



    // --- Cleanup (Optional) ---

    public void CleanUpEncounter()
    {
        // Clean up references to prevent memory leaks if needed
        _activePlayers.Clear();
        _activePlayersDict.Clear();
        _activeEnemies.Clear();
        _activeEnemiesDict.Clear();
        _turnOrder.Clear();
        _currentTurnEntity = null;
    }




    private void OnDestroy()
    {
        // Clean up references to prevent memory leaks if needed
        _activePlayers.Clear();
        _activePlayersDict.Clear();
        _activeEnemies.Clear();
        _activeEnemiesDict.Clear();
        _turnOrder.Clear();
        _currentTurnEntity = null;
    }
}