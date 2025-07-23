using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;
using System;
using System.Collections;
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

        CurrentState = newState;
        Debug.Log($"Changing state to {newState}");

        // Handle scene transitions
        string targetScene = GetSceneForState(newState);
        if (!string.IsNullOrEmpty(targetScene))
        {
            if (SceneManager.GetActiveScene().name != targetScene || forceReload)
            {
                LoadScene(targetScene);
            }
        }

        // Notify all clients
        GameServer.Instance?.Broadcast(new 
        {
            type = "game_state_change",
            state = newState.ToString(),
            scene = targetScene
        });
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