using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;
using System;
using System.Collections;
using System.Collections.Generic;

public class GameStateManager : MonoBehaviour
{
    public static GameStateManager Instance { get; private set; }

    public enum GameState
    {
        Lobby,
        Map,
        Combat,
        Shop,
        Loot,
        GameOver
    }

    [Header("Scene Names")]
    [SerializeField] private string _lobbyScene = "Lobby";
    [SerializeField] private string _mapScene = "Map";
    [SerializeField] private string _combatScene = "CombatScene";
    [SerializeField] private string _lootScene = "LootScene";
    [SerializeField] private string _shopScene = "ShopScene";

    public GameState CurrentState { get; private set; } = GameState.Lobby;

    // Store encounter data temporarily for combat (if needed, or keep it in CombatService)
    // private List<PlayerConnection> _pendingPlayersForEncounter;
    // private List<EnemyDefinition> _pendingEnemiesForEncounter;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        Debug.Log("GameStateManager initialized");
        SceneManager.sceneLoaded += OnSceneLoaded; // Listen for scene loads
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded; // Unsubscribe
    }
    public GameState GetCurrentGameState()
    {
        return CurrentState;
    }

    public void ChangeState(GameState newState, bool forceReload = false)
    {
        if (CurrentState == newState && !forceReload)
            return;

        // Handle cleanup/exit logic for the old state *before* changing CurrentState
        OnStateExit(CurrentState);

        // --- CRITICAL CHANGE 1: Update state but defer scene-dependent actions ---
        CurrentState = newState;
        Debug.Log($"Changing state to {newState}");

        // Handle scene transitions
        string targetScene = GetSceneForState(newState);
        if (!string.IsNullOrEmpty(targetScene))
        {
            if (SceneManager.GetActiveScene().name != targetScene || forceReload)
            {
                // --- CRITICAL CHANGE 2: Exit early for ALL scene loads ---
                LoadScene(targetScene);
                // Defer OnStateEnter and Broadcast until OnSceneLoaded callback
                return;
            }
        }

        // --- CRITICAL CHANGE 3: If NO scene load was needed, handle entry logic and broadcast directly ---
        // This covers states like Lobby, Shop, Loot, GameOver if they don't change scenes
        // or if the target scene is already active and not being reloaded.
        OnStateEnter(newState); // Handle entry logic for states NOT requiring a scene load *right now*

        // Notify all clients if no scene load was involved
        GameServer.Instance?.Broadcast(new
        {
            type = "game_state_change",
            state = newState.ToString(),
            // scene is only relevant if a scene change was intended
            scene = targetScene // Send the intended scene, even if not loaded here
        });
    }

    // --- (Keep PrepareEncounterData if you moved it back here, or handle it in CombatService) ---
    // private void PrepareEncounterData() { ... } 

    // This is called AFTER a scene finishes loading and activating
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"GameStateManager: Scene '{scene.name}' loaded (Mode: {mode}). Current State: {CurrentState}");
        
        // --- CRITICAL CHANGE 4: Always perform state entry logic and broadcast AFTER scene load ---
        // Determine the intended scene for the current state
        string expectedSceneForCurrentState = GetSceneForState(CurrentState);

        // Check if the loaded scene matches the expected scene for the current state
        if (!string.IsNullOrEmpty(expectedSceneForCurrentState) && scene.name == expectedSceneForCurrentState)
        {
            Debug.Log($"GameStateManager: Loaded scene '{scene.name}' matches current state '{CurrentState}'. Finalizing state transition.");
            
            // Now that the scene is loaded, perform any state entry logic that depends on it
            OnStateEnter(CurrentState); 

            // Notify all clients that the state and scene are ready
            GameServer.Instance?.Broadcast(new
            {
                type = "game_state_change",
                state = CurrentState.ToString(),
                scene = scene.name // Confirm the loaded scene name
            });
        }
        else if(string.IsNullOrEmpty(expectedSceneForCurrentState))
        {
            // Handle states that don't have an associated scene (e.g., if you add one later)
             Debug.Log($"GameStateManager: Loaded scene '{scene.name}', but current state '{CurrentState}' has no associated scene. Performing general OnStateEnter.");
             OnStateEnter(CurrentState);
             // Decide if you want to broadcast here for non-scene states
             // GameServer.Instance?.Broadcast(...); 
        }
        else
        {
             Debug.LogWarning($"GameStateManager: Loaded scene '{scene.name}' does not match expected scene '{expectedSceneForCurrentState}' for state '{CurrentState}'.");
             // Optionally handle mismatch (e.g., load correct scene, log error)
             // For now, we just don't finalize the state transition logic.
        }
       
    }

    private void OnStateEnter(GameState newState)
    {
        switch (newState)
        {
            case GameState.Combat:
                // --- Handle Combat Initialization AFTER scene load ---
                Debug.Log("GameStateManager: Entering Combat state logic (scene should be loaded). Initializing encounter...");
                // Gather players for the encounter
                List<PlayerConnection> playersInCombat = PlayerManager.Instance.GetAllPlayers();

                CombatService combatService = CombatService.Instance;
                if (combatService != null)
                {
                    // --- CLEANED UP: Prepare encounter data ---
                    List<PlayerConnection> playersForEncounter = playersInCombat;
                    List<EnemyDefinition> enemiesForEncounter = new List<EnemyDefinition>();

                    // Load the test enemy
                    EnemyDefinition goblinDef = Resources.Load<EnemyDefinition>("Entities/Enemy/Rat");
                    if (goblinDef != null)
                    {
                        enemiesForEncounter.Add(goblinDef);
                        Debug.Log("GameStateManager: Added Rat to test encounter.");
                    }
                    else
                    {
                        Debug.LogWarning("GameStateManager: Could not load 'Rat' EnemyDefinition for test encounter.");
                    }

                    // Pass both player and enemy lists
                    // This will now run in the context of the CombatScene
                    combatService.InitializeForEncounter(playersForEncounter, enemiesForEncounter);
                    // --- END CLEANED UP ---
                }
                else
                {
                    Debug.LogError("GameStateManager: Could not find CombatService instance when entering Combat state!");
                }
                break;
             case GameState.Map:
                 // Add any Map-specific logic here if needed *after* MapScene is loaded
                 Debug.Log("GameStateManager: Entered Map state. MapScene should now be active.");
                 break;
            // Handle other state entries if needed (Shop, Loot, etc.)
            default:
                // For states like Lobby, if they don't load a new scene every time,
                // or if they do load a scene, OnStateEnter runs after the scene loads.
                Debug.Log($"GameStateManager: Entered state {newState}. No specific entry logic defined or scene load completed.");
                break;
        }
    }

    private void OnStateExit(GameState oldState)
    {
        switch (oldState)
        {
            case GameState.Combat:
                Debug.Log("GameStateManager: Exiting Combat state. Performing cleanup...");
                // Clear pending data if exiting combat before it started properly
                // _pendingPlayersForEncounter = null;
                // _pendingEnemiesForEncounter = null;
                break;
            // Handle other state exits if needed
            default:
                break;
        }
    }

    private string GetSceneForState(GameState state)
    {
        return state switch
        {
            GameState.Lobby => _lobbyScene,
            GameState.Map => _mapScene,
            GameState.Combat => _combatScene,
            GameState.Shop => _shopScene,
            GameState.Loot => _lootScene,
            _ => null // No scene change for other states
        };
    }

    private void LoadScene(string sceneName)
    {
        Debug.Log($"Loading scene: {sceneName}");
        StartCoroutine(LoadSceneAsync(sceneName));
    }

    private IEnumerator LoadSceneAsync(string sceneName)
    {
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        operation.allowSceneActivation = false; // Crucial for control

        while (!operation.isDone)
        {
            float progress = Mathf.Clamp01(operation.progress / 0.9f);
            Debug.Log($"Loading progress: {progress * 100}%");

            if (operation.progress >= 0.9f)
            {
                operation.allowSceneActivation = true; // Allow scene to become active
            }

            yield return null;
        }
        // Scene is now loaded and active
        Debug.Log($"Scene '{sceneName}' fully loaded and activated.");
        // Note: OnSceneLoaded callback will be triggered by Unity after this.
    }
}