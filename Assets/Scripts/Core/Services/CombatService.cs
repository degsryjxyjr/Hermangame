using UnityEngine;
using System.Collections.Generic;

public class CombatService : MonoBehaviour
{
    public void OnPlayerDisconnected(PlayerManager.PlayerConnection player)
    {
        // Handle combat disconnection
    }

    public void OnPlayerReconnected(PlayerManager.PlayerConnection player)
    {
        // Handle combat reconnection
    }

    public void HandleMessage(PlayerManager.PlayerConnection player, Dictionary<string, object> msg)
    {
        // Handle combat messages
    }
}