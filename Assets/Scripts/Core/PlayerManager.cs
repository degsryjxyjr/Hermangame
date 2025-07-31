using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PlayerManager : MonoBehaviour
{
    public static PlayerManager Instance { get; private set; }

    // Active and disconnected sessions
    private readonly Dictionary<string, PlayerConnection> _activeConnections = new();
    private readonly Dictionary<string, DisconnectedSession> _disconnectedSessions = new();

    [Header("Session Settings")]
    [Tooltip("How long to keep disconnected sessions before cleanup (seconds)")]
    [SerializeField] private float _sessionTimeout = 300f; // 5 minutes

    // Services
    private LobbyService _lobby;
    private CombatService _combat;

    private AbilityExecutionService _abilityExecution;

    private InventoryService _inventory;
    private ClassManager _class;

    public Party _mainPlayerParty { get; private set; }

    private void Awake()
    {
        if (Instance != null)
        {
            Debug.LogWarning($"Duplicate PlayerManager instance detected! Destroying {gameObject.name}");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        Debug.Log("PlayerManager initialized and set as singleton");

        InitializeServices();
    }

    private void InitializeServices()
    {
        Debug.Log("Initializing gameplay services...");
        _lobby = gameObject.AddComponent<LobbyService>();
        _combat = gameObject.AddComponent<CombatService>();
        _inventory = gameObject.AddComponent<InventoryService>();
        _class  = gameObject.AddComponent<ClassManager>();
        _abilityExecution  = gameObject.AddComponent<AbilityExecutionService>();
        Debug.Log($"Services initialized: Lobby={_lobby != null}, Combat={_combat != null}, Class={_class != null}, Inventory={_inventory != null}, AbilityExec={_abilityExecution != null}");
    }

    private void Update()
    {
        CleanupExpiredSessions();
    }

    #region Session Management
    public void HandleNetworkMessage(string sessionId, Dictionary<string, object> msg)
    {
        Debug.Log($"Received message from {sessionId}: {JsonUtility.ToJson(msg)}");

        try
        {
            string messageType = msg["type"].ToString();
            Debug.Log($"Message type: {messageType}");

            switch (messageType)
            {
                case "join":
                    HandleJoin(sessionId, msg);
                    break;

                case "reconnect":
                    HandleReconnect(sessionId, msg);
                    break;

                case "start_game":
                    if (_lobby != null)
                    {
                        _lobby.StartGame();
                    }
                    else
                    {
                        Debug.LogError("LobbyService reference is null when trying to start game");
                    }
                    break;

                case "inventory":
                    if (_inventory != null)
                    {
                        _inventory.HandleMessage(sessionId, msg);
                    }
                    else
                    {
                        Debug.LogError("InventoryService reference is null when trying to handle inventory message");
                    }
                    break;

                default:
                    RouteMessageToService(sessionId, msg);
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed processing message from {sessionId}: {ex.Message}");
        }
    }

    private void HandleJoin(string sessionId, Dictionary<string, object> joinData)
    {
        Debug.Log($"New join attempt from {sessionId}");

        // 1. Room Code Verification
        if (joinData.TryGetValue("roomCode", out var code) &&
            code.ToString() != GameServer.Instance.GetRoomCode())
        {
            Debug.Log($"Failed join - invalid room code from {sessionId}");
            GameServer.Instance.SendToPlayer(sessionId, new
            {
                type = "join_error",
                message = "Invalid room code"
            });
            return;
        }
        // 2. Game State Check
        if (GameStateManager.Instance.CurrentState != GameStateManager.GameState.Lobby)
        {
            Debug.Log($"Rejecting join - game in progress (State: {GameStateManager.Instance.CurrentState})");
            GameServer.Instance.SendToPlayer(sessionId, new {
                type = "join_error",
                message = "Game already in progress"
            });
            return;
        }

        // 3. Create connection
        var connection = CreateNewConnection(sessionId, joinData);
        _activeConnections.Add(sessionId, connection);
        Debug.Log($"New player connected: {connection.LobbyData.Name} (ID: {sessionId})");

        SendJoinResponse(sessionId, connection);
    }

    private PlayerConnection CreateNewConnection(string sessionId, Dictionary<string, object> joinData)
    {
        Debug.Log($"Creating new player connection for {sessionId}");
        //use class names
        string className = joinData["class"].ToString();
        var classDef = ClassManager.Instance.GetAvailableClasses()
            .FirstOrDefault(c => c.className == className);
        if (classDef == null)
        {
            Debug.LogError($"Class {className} not found, using fallback");
            classDef = ClassManager.Instance.GetAvailableClasses().First();
        }
        return new PlayerConnection(sessionId)
        {
            LobbyData = new LobbyPlayerData
            {
                Name = joinData["name"].ToString(),
                Role = className,
                IsReady = false
            },
            // --- REMOVED: GameData = ScriptableObject.CreateInstance<PlayerGameData>(), ---
            LastActivityTime = Time.time,
            ReconnectToken = Guid.NewGuid().ToString()
        };
    }

    private void SendJoinResponse(string sessionId, PlayerConnection connection)
    {
        Debug.Log($"Sending join success to {connection.LobbyData.Name}");

        GameServer.Instance.SendToPlayer(sessionId, new
        {
            type = "join_success",
            playerId = sessionId,
            reconnectToken = connection.ReconnectToken,
            profile = new
            {
                name = connection.LobbyData.Name,
                role = connection.LobbyData.Role
            }
        });

        _lobby.AddPlayer(connection);
    }

    // Update the HandleReconnect method to use the new return type
    private void HandleReconnect(string sessionId, Dictionary<string, object> reconnectData)
    {
        Debug.Log($"Reconnect attempt from session {sessionId}");

        if (!reconnectData.TryGetValue("reconnectToken", out var token))
        {
            Debug.Log($"Reconnect failed - missing token from {sessionId}");
            SendReconnectResponse(sessionId, false, "Missing token");
            return;
        }

        var cachedSession = FindCachedSession(token.ToString());
        if (!cachedSession.HasValue || cachedSession.Value.Session == null)
        {
            Debug.Log($"Reconnect failed - invalid/expired token from {sessionId}");
            SendReconnectResponse(sessionId, false, "Session expired");
            return;
        }

        Debug.Log($"Found cached session for {cachedSession.Value.Session.LobbyData.Name}");
        CompleteReconnection(sessionId, cachedSession.Value);
    }

    private DisconnectedSession? FindCachedSession(string token)
    {
        foreach (var kvp in _disconnectedSessions)
        {
            if (kvp.Value.Session.ReconnectToken == token)
            {
                Debug.Log($"Found matching session for token {token}");
                return kvp.Value;
            }
        }
        Debug.Log($"No cached session found for token {token}");
        return null;
    }

    private void CompleteReconnection(string sessionId, DisconnectedSession cachedSession)
    {
        var connection = cachedSession.Session;
        Debug.Log($"Completing reconnection for {connection.LobbyData.Name}");

        // Update connection with new session ID
        connection.LastActivityTime = Time.time;

        _activeConnections.Add(sessionId, connection);
        _disconnectedSessions.Remove(cachedSession.OriginalSessionId);

        Debug.Log($"Player {connection.LobbyData.Name} reconnected (new ID: {sessionId})");

        // Notify services
        if (GameStateManager.Instance.CurrentState == GameStateManager.GameState.Lobby)
        {
            Debug.Log("Notifying lobby of reconnection");
            _lobby.OnPlayerReconnected(connection);
        }
        else if (GameStateManager.Instance.CurrentState == GameStateManager.GameState.Combat)
        {
            Debug.Log("Notifying combat system of reconnection");
            _combat.OnPlayerReconnected(connection);
        }

        SendReconnectResponse(sessionId, true, "Reconnected successfully");
    }

    private void SendReconnectResponse(string sessionId, bool success, string message)
    {
        GameServer.Instance.SendToPlayer(sessionId, new
        {
            type = success ? "reconnect_success" : "reconnect_error",
            message = message
        });
    }
    #endregion

    #region Disconnection Handling
    public void HandleDisconnect(string sessionId)
    {
        Debug.Log($"Handling disconnect for session {sessionId}");

        if (!_activeConnections.TryGetValue(sessionId, out var connection))
        {
            Debug.LogWarning($"Disconnect called for unknown session: {sessionId}");
            return;
        }

        try
        {
            Debug.Log($"Player {connection.LobbyData?.Name} disconnected, caching session");

            // Cache the disconnected session
            _disconnectedSessions.Add(sessionId, new DisconnectedSession
            {
                Session = connection,
                OriginalSessionId = sessionId,
                DisconnectTime = Time.time
            });

            _activeConnections.Remove(sessionId);

            Debug.Log($"Session cached with token: {connection.ReconnectToken}");

            // Safely notify services
            if (GameStateManager.Instance != null)
            {
                switch (GameStateManager.Instance.CurrentState)
                {
                    case GameStateManager.GameState.Lobby:
                        if (_lobby != null)
                        {
                            Debug.Log("Notifying lobby of disconnect");
                            _lobby.OnPlayerDisconnected(connection);
                        }
                        break;
                        
                    case GameStateManager.GameState.Combat:
                        if (_combat != null)
                        {
                            Debug.Log("Notifying combat system of disconnect");
                            _combat.OnPlayerDisconnected(connection);
                        }
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error during disconnect handling: {ex.Message}");
        }
    }

    private void CleanupExpiredSessions()
    {
        var expiredSessions = _disconnectedSessions
            .Where(x => Time.time - x.Value.DisconnectTime > _sessionTimeout)
            .ToList();

        if (expiredSessions.Count > 0)
        {
            Debug.Log($"Cleaning up {expiredSessions.Count} expired sessions");
        }

        foreach (var session in expiredSessions)
        {
            Debug.Log($"Removing expired session: {session.Key} (Player: {session.Value.Session.LobbyData.Name})");
            _disconnectedSessions.Remove(session.Key);
        }
    }
    #endregion

    #region Debug Utilities
    public void PrintActiveSessions()
    {
        Debug.Log("=== ACTIVE SESSIONS ===");
        foreach (var kvp in _activeConnections)
        {
            Debug.Log($"{kvp.Key}: {kvp.Value.LobbyData.Name} (Last: {kvp.Value.LastActivityTime})");
        }
    }

    public void PrintCachedSessions()
    {
        Debug.Log("=== CACHED SESSIONS ===");
        foreach (var kvp in _disconnectedSessions)
        {
            var timeRemaining = _sessionTimeout - (Time.time - kvp.Value.DisconnectTime);
            Debug.Log($"{kvp.Key}: {kvp.Value.Session.LobbyData.Name} (Expires in: {timeRemaining:F1}s)");
        }
    }
    #endregion

    #region Message Routing
    private void RouteMessageToService(string sessionId, Dictionary<string, object> msg)
    {
        if (!_activeConnections.TryGetValue(sessionId, out var connection))
        {
            Debug.LogWarning($"No active connection found for session {sessionId}");
            return;
        }

        connection.LastActivityTime = Time.time;

        try
        {
            if (GameStateManager.Instance == null)
            {
                Debug.LogError("GameStateManager instance is null");
                return;
            }

            switch (GameStateManager.Instance.CurrentState)
            {
                case GameStateManager.GameState.Lobby:
                    if (_lobby == null)
                    {
                        Debug.LogError("LobbyService reference is null");
                        return;
                    }
                    _lobby.HandleMessage(connection, msg);
                    break;
                    
                case GameStateManager.GameState.Combat:
                    if (_combat == null)
                    {
                        Debug.LogError("CombatService reference is null");
                        return;
                    }
                    _combat.HandleMessage(connection, msg);
                    break;
                    
                default:
                    Debug.LogWarning($"Unhandled game state: {GameStateManager.Instance.CurrentState}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error routing message: {ex.Message}");
        }
    }
    #endregion

    #region Public Accessors
    public PlayerConnection GetPlayer(string sessionId)
    {
        return _activeConnections.TryGetValue(sessionId, out var connection) ? connection : null;
    }

    public List<PlayerConnection> GetAllPlayers()
    {
        return _activeConnections.Values.ToList();
    }

    public bool IsPlayerConnected(string sessionId)
    {
        return _activeConnections.ContainsKey(sessionId);
    }

    // Modify the method signature to accept the list of players for the party
    // Remove the old single PlayerConnection parameter version or make it obsolete/private
    public void InitializeGameData(List<PlayerConnection> playersForParty)
    {
        if (playersForParty == null || playersForParty.Count == 0)
        {
            Debug.LogError("PlayerManager: Cannot initialize game data, player list is null or empty.");
            return;
        }

        Debug.Log($"PlayerManager: Initializing game data for {playersForParty.Count} players.");

        // --- 1. Create the Main Party ---
        _mainPlayerParty = new Party(playersForParty);
        Debug.Log("PlayerManager: Main player party created.");

        // --- 2. Initialize Individual Player Data ---
        // Iterate through the players passed in (which are now the party members)
        foreach (var connection in playersForParty)
        {
            if (connection == null) continue; // Safety check

            // --- Find the PlayerClassDefinition ---
            // Use the Role (which should be the class name) from LobbyData
            string className = connection.LobbyData?.Role ?? "UnknownClass";
            var classDef = ClassManager.Instance.GetAvailableClasses()
                                            .FirstOrDefault(c => c.className == className);

            if (classDef == null)
            {
                Debug.LogError($"PlayerManager: Class definition '{className}' not found for player {connection.LobbyData?.Name ?? connection.NetworkId}. Using fallback.");
                // Use a fallback class, e.g., the first available one
                var availableClasses = ClassManager.Instance.GetAvailableClasses();
                if (availableClasses != null && availableClasses.Count > 0)
                {
                    classDef = availableClasses[0]; // Or handle error more gracefully
                }
                else
                {
                    Debug.LogError("PlayerManager: No player classes available!");
                    // Cannot proceed without a class definition
                    continue; // Skip this player
                }
            }

            // --- Create and Assign Player Game Data ---
            connection.ClassDefinition = classDef;
            connection.InitializeStats();
            // --- UPDATED: Assign to direct property ---
            connection.UnlockedAbilities = new List<AbilityDefinition>(classDef.startingAbilities);
            // Initialize inventory through InventoryService
            _inventory.InitializeInventory(connection.NetworkId, classDef.startingEquipment);

            Debug.Log($"PlayerManager: Game data initialized for {connection.LobbyData?.Name ?? connection.NetworkId} (Class: {classDef.className}, Level: {_mainPlayerParty.Level}).");

            // --- Optional: Send Initial Stats to Client ---
            // This ensures the client has the initial stats based on class and level
            connection.SendStatsUpdateToClient();
        }

        // --- 3. Notify Party Members of Initial State ---
        // Send the initial party state (level, XP - which is 0, members) to all members
        _mainPlayerParty.SendPartyUpdateToMembers();
        Debug.Log("PlayerManager: Initial party state sent to members.");
    }
    #endregion

    #region Data Structures
    private struct DisconnectedSession
    {
        public PlayerConnection Session;
        public string OriginalSessionId;
        public float DisconnectTime;
    }

    #endregion
}