using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Linq;
using System;
using System.Collections.Generic;
public class LobbyUI : MonoBehaviour
{
    public static LobbyUI Instance { get; private set; }

    [Header("UI References")]
    public TMP_Text roomCodeText;
    public Transform playerListContainer;
    public GameObject playerEntryPrefab;
    public Button startGameButton;
    private string roomCode;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        if (GameServer.Instance != null)
        {
            roomCode = GameServer.Instance.GetRoomCode();
            roomCodeText.text = $"Room: {roomCode}";
        }
        else
        {
            Debug.LogError("[LobbyUI] GameServer.Instance is null! Make sure:\n");
        }
    }

    public void UpdatePlayers(List<PlayerManager.PlayerConnection> players, bool canStart)
    {
        // Clear existing entries
        foreach (Transform child in playerListContainer)
        {
            Destroy(child.gameObject);
        }

        // Create new entries
        foreach (var player in players)
        {
            var entry = Instantiate(playerEntryPrefab, playerListContainer);
            var entryScript = entry.GetComponent<PlayerEntry>();
            entryScript.Initialize(player.LobbyData.Name, player.LobbyData.Role);
            entryScript.SetReadyState(player.LobbyData.IsReady);
        }

        startGameButton.interactable = canStart;
    }

    // Called from Start Game button in UI
    public void OnStartGamePressed()
    {
        GameServer.Instance.Broadcast(new { type = "game_start" });
        LobbyService.Instance.StartGame();
    }
}