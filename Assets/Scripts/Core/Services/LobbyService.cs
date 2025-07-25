using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;


public class LobbyService : MonoBehaviour
{
    public static LobbyService Instance { get; private set; }

    private List<PlayerConnection> _players = new();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void AddPlayer(PlayerConnection player)
    {
        if (!_players.Contains(player))
        {
            _players.Add(player);
            UpdateLobbyState();
        }
    }

    public void OnPlayerDisconnected(PlayerConnection player)
    {
        _players.RemoveAll(p => p.NetworkId == player.NetworkId);
        UpdateLobbyState();
    }

    public void OnPlayerReconnected(PlayerConnection player)
    {
        if (!_players.Contains(player))
        {
            _players.Add(player);
            UpdateLobbyState();
        }
    }

    public void HandleMessage(PlayerConnection player, Dictionary<string, object> msg)
    {
        if (player == null || msg == null)
        {
            Debug.LogError("Null player or message in HandleMessage");
            return;
        }

        try
        {
            if (msg.TryGetValue("type", out var typeObj))
            {
                string type = typeObj.ToString();
                if (type == "set_ready" && msg.TryGetValue("isReady", out var isReadyObj))
                {
                    player.LobbyData.IsReady = (bool)isReadyObj;
                    UpdateLobbyState();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error handling lobby message: {ex.Message}");
        }
    }

    private void UpdateLobbyState()
    {
        bool canStart = _players.Count >= 2 && _players.All(p => p.LobbyData.IsReady);

        // Update Unity host UI
        if (LobbyUI.Instance != null)
        {
            LobbyUI.Instance.UpdatePlayers(_players, canStart);
        }

        // Update each player's browser view
        foreach (var player in _players)
        {
            GameServer.Instance.SendToPlayer(player.NetworkId, new 
            {
                type = "lobby_update",
                players = _players.Select(p => new {
                    Name = p.LobbyData.Name,
                    Role = p.LobbyData.Role,
                    IsReady = p.LobbyData.IsReady
                }),
                canStart = canStart
            });
        }
    }

    private bool CanStartGame()
    {
        bool canStart = _players.Count >= 2 && _players.All(p => p.LobbyData.IsReady);
        if (!canStart)
        {
            Debug.LogWarning("Cannot start game - not all players are ready or not enough players");
        }
        return canStart;
    }

    private void InitializePlayerData()
    {
        foreach (var player in _players)
        {
            PlayerManager.Instance.InitializeGameData(player);
        }
    }

    public void StartGame()
    {
        if (!CanStartGame()) return;
        
        InitializePlayerData();
        GameStateManager.Instance.ChangeState(GameStateManager.GameState.Map);
    }
}