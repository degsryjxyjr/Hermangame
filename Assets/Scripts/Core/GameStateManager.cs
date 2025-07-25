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
    }

    public void ChangeState(GameState newState, bool forceReload = false)
    {
        if (CurrentState == newState && !forceReload)
            return;

        // Handle cleanup/exit logic for the old state *before* changing CurrentState
        OnStateExit(CurrentState);

        CurrentState = newState;
        Debug.Log($"Changing state to {newState}");

        // Handle setup/entry logic for the new state *after* changing CurrentState
        OnStateEnter(newState);

        // Handle scene transitions (this part remains largely the same)
        string targetScene = GetSceneForState(newState);
        if (!string.IsNullOrEmpty(targetScene))
        {
            if (SceneManager.GetActiveScene().name != targetScene || forceReload)
            {
                LoadScene(targetScene);
            }
        }

        // Notify all clients (this part remains largely the same)
        GameServer.Instance?.Broadcast(new
        {
            type = "game_state_change",
            state = newState.ToString(),
            scene = targetScene
        });
    }

    private void OnStateEnter(GameState newState)
    {
        switch (newState)
        {
            case GameState.Combat:
                // --- NEW: Initialize Combat Encounter ---
                Debug.Log("GameStateManager: Entering Combat state. Initializing encounter...");
                // Gather players for the encounter
                // This assumes all players in PlayerManager should join the combat.
                // You might have logic to select a subset.
                List<PlayerConnection> playersInCombat = PlayerManager.Instance.GetAllPlayers();

                // Get the CombatService instance (assuming it exists or will be in the scene)
                // If CombatService is persistent (DontDestroyOnLoad), this works.
                // If it's scene-specific, it will be available after the scene loads.
                // Let's assume it's persistent for this interaction.
                CombatService combatService = CombatService.Instance;
                if (combatService != null)
                {
                    // Tell the CombatService to initialize for this specific encounter.
                    // Pass the list of players involved.
                    combatService.InitializeForEncounter(playersInCombat /*, encounter details */);
                }
                else
                {
                    Debug.LogError("GameStateManager: Could not find CombatService instance when entering Combat state!");
                }
                // --- END NEW ---
                break;
            // Handle other state entries if needed
            default:
                break;
        }
    }

    private void OnStateExit(GameState oldState)
    {
        switch (oldState)
        {
            case GameState.Combat:
                // --- NEW: Cleanup/Teardown for Combat ---
                Debug.Log("GameStateManager: Exiting Combat state. Performing cleanup...");
                // Potentially notify CombatService to reset or prepare for shutdown
                // CombatService.Instance?.OnCombatStateExit(); // Add such a method if needed
                // --- END NEW ---
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
        operation.allowSceneActivation = false;

        while (!operation.isDone)
        {
            float progress = Mathf.Clamp01(operation.progress / 0.9f);
            Debug.Log($"Loading progress: {progress * 100}%");

            if (operation.progress >= 0.9f)
            {
                operation.allowSceneActivation = true;
            }

            yield return null;
        }
    }
}