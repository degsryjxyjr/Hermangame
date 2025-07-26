// File: Scripts/Gameplay/Combat/EncounterManager.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq; // For sorting if needed

/// <summary>
/// Manages the state and flow of a single combat encounter.
/// Tracks active players and enemies, turn order, and handles encounter-specific logic.
/// </summary>
public class EncounterManager : MonoBehaviour
{
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


    private EncounterState _currentState = EncounterState.NotStarted;
    public EncounterState CurrentState => _currentState;

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
    public object CurrentTurnEntity { get; private set; } = null; // Convenience property

    // --- Events ---
    // These can be used to notify the CombatService or UI about state changes.
    public System.Action OnEncounterStarted;
    public System.Action OnEncounterEnded; // Pass victory/defeat state?
    public System.Action<object> OnTurnStarted; // Pass the entity whose turn it is
    public System.Action<PlayerConnection> OnPlayerAdded;
    public System.Action<EnemyEntity> OnEnemyAdded;
    public System.Action<EnemyEntity> OnEnemyDefeated; // Could pass loot drops here

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

        // 2. Spawn and Add Enemies
        // This assumes enemies are spawned in the combat scene at predetermined locations
        // or that their positions are defined by the encounter data.
        // For prototype, we'll just instantiate them at the manager's position.
        // A more robust system might involve spawn points or an encounter definition object.
        if (enemyDefinitions != null)
        {
            foreach (var enemyDef in enemyDefinitions)
            {
                if (enemyDef != null)
                {
                    // TODO: Determine spawn location, level, etc.
                    SpawnEnemy(enemyDef, Vector3.zero, 1); // Placeholder position and level
                }
            }
        }

        // 3. Determine Initial Turn Order
        // Placeholder: Players first, then enemies, all in order they were added.
        // A real system would use initiative rolls or speed stats.
        BuildInitialTurnOrder();

        // 4. Start the first turn
        AdvanceTurn();

        // 5. Notify listeners
        OnEncounterStarted?.Invoke();
        Debug.Log($"EncounterManager: Encounter started with {_activePlayers.Count} players and {_activeEnemies.Count} enemies. Turn order set.");
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

    private void SpawnEnemy(EnemyDefinition enemyDef, Vector3 position, int level)
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
        // enemyEntity.Initialize(enemyDef, level); // You'd need to add such a method to EnemyEntity
        
        // Ensure EnemyEntity has its definition set. If not, set it.
        if (enemyEntity._enemyDefinition == null) // Assuming you add a public property/field EnemyDefinition to EnemyEntity
        {
             enemyEntity.SetEnemyDefinition(enemyDef); // Add this method to EnemyEntity
             // Or directly if it's a public field: enemyEntity._enemyDefinition = enemyDef;
        }
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
        // Simple order: All players, then all enemies.
        // You can make this more sophisticated later (e.g., initiative based on speed).
        foreach (var player in _activePlayers)
        {
            _turnOrder.Add(player);
        }
        foreach (var enemy in _activeEnemies)
        {
            _turnOrder.Add(enemy);
        }
        Debug.Log($"EncounterManager: Built initial turn order with {_turnOrder.Count} entities.");
    }

    // --- Turn Management ---
    /// <summary>
    /// Advances the turn to the next active entity in the turn order.
    /// </summary>
    public void AdvanceTurn()
    {
        if (_currentState != EncounterState.Active)
        {
            Debug.LogWarning("Cannot advance turn, encounter is not active.");
            return;
        }

        if (_turnOrder.Count == 0)
        {
            Debug.LogError("Cannot advance turn, turn order is empty.");
            return;
        }

        // Find the next *alive* entity in the turn order.
        int attempts = 0;
        int maxAttempts = _turnOrder.Count; // Prevent infinite loop if all are dead
        do
        {
            _currentTurnIndex = (_currentTurnIndex + 1) % _turnOrder.Count;
            CurrentTurnEntity = _turnOrder[_currentTurnIndex];
            attempts++;

            // Check if the current entity is alive
            bool isAlive = false;
            if (CurrentTurnEntity is PlayerConnection player)
            {
                isAlive = player.IsAlive();
            }
            else if (CurrentTurnEntity is EnemyEntity enemy)
            {
                isAlive = enemy.IsAlive();
            }

            if (isAlive)
            {
                break; // Found an alive entity, use this turn
            }
            else
            {
                Debug.Log($"EncounterManager: Skipping dead entity {_currentTurnIndex} in turn order.");
                CurrentTurnEntity = null; // Clear for this dead turn
            }

        } while (attempts < maxAttempts);

        if (CurrentTurnEntity == null)
        {
            Debug.Log("EncounterManager: No alive entities found in turn order. Checking win/loss conditions.");
            CheckEncounterEndConditions();
            return; // Don't start a turn if the encounter ended
        }

        // A valid, alive entity has the turn
        OnTurnStarted?.Invoke(CurrentTurnEntity);
        Debug.Log($"EncounterManager: Turn advanced. Current turn: {GetCurrentTurnEntityName()} (Index: {_currentTurnIndex})");
        
        // TODO: Send message to all clients about whose turn it is.
        // This might involve sending the entire turn order or just the current entity ID.
    }

    private string GetCurrentTurnEntityName()
    {
        if (CurrentTurnEntity is IEntity entity)
        {
            return entity.GetEntityName();
        }
        else if (CurrentTurnEntity != null)
        {
            return CurrentTurnEntity.ToString();
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
        // if (CurrentTurnEntity == enemy)
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

    private void CheckEncounterEndConditions()
    {
        if (_currentState != EncounterState.Active) return; // Already ended

        // Check for Player Victory (All enemies defeated)
        if (_activeEnemies.Count == 0)
        {
            Debug.Log("EncounterManager: All enemies defeated. Player victory!");
            _currentState = EncounterState.Victory;
            EndEncounter();
            return;
        }

        // Check for Player Defeat (All players defeated)
        bool allPlayersDefeated = true;
        foreach (var player in _activePlayers)
        {
            if (player.IsAlive())
            {
                allPlayersDefeated = false;
                break;
            }
        }
        if (allPlayersDefeated)
        {
            Debug.Log("EncounterManager: All players defeated. Player defeat!");
            _currentState = EncounterState.Defeat;
            EndEncounter();
            return;
        }

        // If neither, the encounter continues.
        Debug.Log("EncounterManager: Encounter continues. Players: " +
                  $"{_activePlayers.Count(p => p.IsAlive())}/{_activePlayers.Count}, " +
                  $"Enemies: {_activeEnemies.Count}");
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
        // Add logic for "ally_1", "all_enemies", etc. as needed.

        Debug.Log($"EncounterManager: Could not resolve target specifier '{targetSpecifier}'");
        return null;
    }

    // --- Getters for Active Entities ---
    public List<PlayerConnection> GetActivePlayers() => new List<PlayerConnection>(_activePlayers);
    public List<EnemyEntity> GetActiveEnemies() => new List<EnemyEntity>(_activeEnemies);
    public List<object> GetTurnOrder() => new List<object>(_turnOrder); // Return copy to prevent external modification
    public int GetActivePlayerCount() => _activePlayers.Count;
    public int GetActiveEnemyCount() => _activeEnemies.Count;

    // --- Cleanup (Optional) ---
    private void OnDestroy()
    {
        // Clean up references to prevent memory leaks if needed
        _activePlayers.Clear();
        _activePlayersDict.Clear();
        _activeEnemies.Clear();
        _activeEnemiesDict.Clear();
        _turnOrder.Clear();
        CurrentTurnEntity = null;
    }
}