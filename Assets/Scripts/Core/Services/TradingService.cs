using System.Collections.Generic;
using UnityEngine;
using System; // For Guid

/// <summary>
/// Manages player-to-player trading sessions.
/// Handles requests, responses, item offers, confirmations, and execution.
/// </summary>
public class TradingService : MonoBehaviour
{
    public static TradingService Instance { get; private set; }

    // Key: Session ID, Value: TradingSession data
    private Dictionary<string, TradingSession> _activeTradingSessions = new Dictionary<string, TradingSession>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"Duplicate TradingService instance detected! Destroying {gameObject.name}");
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // Assuming this service lives for the duration of the game or is managed by GameStateManager
        // DontDestroyOnLoad(gameObject); // Uncomment if needed
        Debug.Log("TradingService initialized and set as singleton");
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// Gets a read-only collection of all active trading sessions.
    /// </summary>
    /// <returns>ReadOnlyDictionary of active trading sessions by session ID.</returns>
    public IReadOnlyDictionary<string, TradingSession> GetActiveTradingSessions()
    {
        return new System.Collections.ObjectModel.ReadOnlyDictionary<string, TradingSession>(_activeTradingSessions);
    }


    #region Message Handling (Called by PlayerManager)

    /// <summary>
    /// Handles incoming trade-related messages from players.
    /// </summary>
    /// <param name="playerId">The NetworkId of the player sending the message.</param>
    /// <param name="msg">The message dictionary.</param>
    public void HandleTradeMessage(string playerId, Dictionary<string, object> msg)
    {
        if (msg == null || !msg.TryGetValue("trade_action", out var actionObj))
        {
            Debug.LogWarning($"TradingService: Invalid trade message received from player {playerId}.");
            SendErrorMessage(playerId, "Invalid trade message format.");
            return;
        }

        string action = actionObj.ToString();

        try
        {
            switch (action)
            {
                case "request":
                    HandleTradeRequest(playerId, msg);
                    break;
                case "response":
                    HandleTradeResponse(playerId, msg);
                    break;
                case "add_item":
                    HandleAddItem(playerId, msg);
                    break;
                case "remove_item":
                    HandleRemoveItem(playerId, msg);
                    break;
                case "confirm":
                    HandleConfirmTrade(playerId, msg);
                    break;
                case "cancel":
                    HandleCancelTrade(playerId, msg);
                    break;
                default:
                    Debug.LogWarning($"TradingService: Unknown trade action '{action}' from player {playerId}.");
                    SendErrorMessage(playerId, $"Unknown trade action: {action}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"TradingService: Error handling trade message '{action}' from player {playerId}: {ex.Message}");
            SendErrorMessage(playerId, "An error occurred processing your trade action.");
        }
    }

    #endregion

    #region Trade Logic

    /// <summary>
    /// Initiates a trade request from one player to another.
    /// </summary>
    private void HandleTradeRequest(string requesterId, Dictionary<string, object> msg)
    {
        if (!msg.TryGetValue("target_player_id", out var targetIdObj))
        {
            Debug.LogWarning($"TradingService: Trade request from {requesterId} missing target_player_id.");
            SendErrorMessage(requesterId, "Trade request missing target player.");
            return;
        }

        string targetId = targetIdObj.ToString();

        // --- Validation ---
        if (requesterId == targetId)
        {
            Debug.LogWarning($"TradingService: Player {requesterId} tried to trade with themselves.");
            SendErrorMessage(requesterId, "You cannot trade with yourself.");
            return;
        }

        PlayerConnection requester = PlayerManager.Instance.GetPlayer(requesterId);
        PlayerConnection target = PlayerManager.Instance.GetPlayer(targetId);

        if (requester == null)
        {
            Debug.LogError($"TradingService: Requester player {requesterId} not found.");
            // No one to send error to
            return;
        }

        if (target == null)
        {
            Debug.LogWarning($"TradingService: Target player {targetId} for trade request from {requesterId} not found.");
            SendErrorMessage(requesterId, "Target player not found.");
            return;
        }

        // Check if either player is already in a trade
        if (FindSessionByParticipant(requesterId) != null)
        {
            SendErrorMessage(requesterId, "You are already in a trade.");
            return;
        }
        if (FindSessionByParticipant(targetId) != null)
        {
            SendErrorMessage(requesterId, "Target player is already in a trade.");
            return;
        }

        // --- Create Session ---
        string sessionId = Guid.NewGuid().ToString();
        TradingSession newSession = new TradingSession(sessionId, requesterId, targetId);
        _activeTradingSessions[sessionId] = newSession;

        Debug.Log($"TradingService: Trade request initiated. Session {sessionId} between {requesterId} and {targetId}.");

        // --- Notify Players ---
        // Notify requester (acknowledge request sent)
        GameServer.Instance.SendToPlayer(requesterId, new Dictionary<string, object>
        {
            ["type"] = "trade_status",
            ["status"] = "request_sent",
            ["target_player_id"] = targetId,
            ["target_player_name"] = target.LobbyData?.Name ?? "Unknown"
        });

        // Notify target (request received)
        GameServer.Instance.SendToPlayer(targetId, new Dictionary<string, object>
        {
            ["type"] = "trade_status",
            ["status"] = "trade_request",
            ["requester_player_id"] = requesterId,
            ["requester_player_name"] = requester.LobbyData?.Name ?? "Unknown",
            ["session_id"] = sessionId
        });
    }

    /// <summary>
    /// Handles the response from the target player to a trade request.
    /// </summary>
    private void HandleTradeResponse(string responderId, Dictionary<string, object> msg)
    {
        if (!msg.TryGetValue("session_id", out var sessionIdObj) ||
            !msg.TryGetValue("accepted", out var acceptedObj))
        {
            Debug.LogWarning($"TradingService: Trade response from {responderId} missing session_id or accepted status.");
            SendErrorMessage(responderId, "Invalid trade response.");
            return;
        }

        string sessionId = sessionIdObj.ToString();
        bool accepted = Convert.ToBoolean(acceptedObj);

        if (!_activeTradingSessions.TryGetValue(sessionId, out TradingSession session))
        {
            Debug.LogWarning($"TradingService: Trade response for non-existent session {sessionId} from player {responderId}.");
            SendErrorMessage(responderId, "Trade session not found.");
            return;
        }

        string requesterId = session.Player1Id; // Player who initiated the request
        string targetId = session.Player2Id;   // Player who responded

        if (responderId != targetId)
        {
            Debug.LogWarning($"TradingService: Player {responderId} responded to session {sessionId} but is not the target ({targetId}).");
            SendErrorMessage(responderId, "You are not the target of this trade request.");
            return;
        }

        if (accepted)
        {
            Debug.Log($"TradingService: Trade request accepted. Session {sessionId} is now active.");
            // Session is now active. Notify both players.
            var startData = new Dictionary<string, object>
            {
                ["type"] = "trade_status",
                ["status"] = "started",
                ["session_id"] = sessionId,
                ["partner_id"] = session.GetOtherPlayerId(responderId),
                ["partner_name"] = PlayerManager.Instance.GetPlayer(session.GetOtherPlayerId(responderId))?.LobbyData?.Name ?? "Unknown"
            };
            GameServer.Instance.SendToPlayer(responderId, startData);
            startData["partner_id"] = requesterId;
            startData["partner_name"] = PlayerManager.Instance.GetPlayer(requesterId)?.LobbyData?.Name ?? "Unknown";
            GameServer.Instance.SendToPlayer(requesterId, startData);
        }
        else
        {
            Debug.Log($"TradingService: Trade request declined. Session {sessionId} cancelled.");
            // Cancel the session
            _activeTradingSessions.Remove(sessionId);
            // Notify requester
            GameServer.Instance.SendToPlayer(requesterId, new Dictionary<string, object>
            {
                ["type"] = "trade_status",
                ["status"] = "declined",
                ["decliner_id"] = responderId,
                ["decliner_name"] = PlayerManager.Instance.GetPlayer(responderId)?.LobbyData?.Name ?? "Unknown"
            });
            // Optionally notify responder that their decline was processed
            GameServer.Instance.SendToPlayer(responderId, new Dictionary<string, object>
            {
                ["type"] = "trade_status",
                ["status"] = "cancelled",
                ["reason"] = "You declined the trade request."
            });
        }
    }

    /// <summary>
    /// Handles a player adding an item to the trade offer.
    /// </summary>
    private void HandleAddItem(string playerId, Dictionary<string, object> msg)
    {
        if (!msg.TryGetValue("session_id", out var sessionIdObj) ||
            !msg.TryGetValue("item_id", out var itemIdObj) ||
            !msg.TryGetValue("quantity", out var quantityObj))
        {
            Debug.LogWarning($"TradingService: Add item message from {playerId} missing required fields.");
            SendErrorMessage(playerId, "Invalid add item request.");
            return;
        }

        string sessionId = sessionIdObj.ToString();
        string itemId = itemIdObj.ToString();
        int quantity = Convert.ToInt32(quantityObj);

        if (!_activeTradingSessions.TryGetValue(sessionId, out TradingSession session))
        {
            Debug.LogWarning($"TradingService: Add item for non-existent session {sessionId} from player {playerId}.");
            SendErrorMessage(playerId, "Trade session not found.");
            return;
        }

        // Validate player is part of session
        var playerOffers = session.GetOffersForPlayer(playerId);
        if (playerOffers == null)
        {
            Debug.LogWarning($"TradingService: Player {playerId} not part of session {sessionId} tried to add item.");
            SendErrorMessage(playerId, "You are not part of this trade.");
            return;
        }

        // Validate item exists in player's inventory and quantity is sufficient
        // This requires checking the InventoryService
        if (InventoryService.Instance == null)
        {
            Debug.LogError("TradingService: InventoryService.Instance is null during add item.");
            SendErrorMessage(playerId, "Internal error checking inventory.");
            return;
        }

        PlayerInventory playerInventory = InventoryService.Instance.GetPlayerInventory(playerId);
        if (playerInventory == null)
        {
            Debug.LogWarning($"TradingService: Player inventory for {playerId} not found during add item.");
            SendErrorMessage(playerId, "Your inventory could not be accessed.");
            return;
        }

        // Find the item slot in the player's inventory
        var itemSlot = playerInventory.BagItems.Find(slot => slot.itemId == itemId);
        if (itemSlot == null || itemSlot.quantity < quantity || quantity <= 0)
        {
            Debug.LogWarning($"TradingService: Player {playerId} tried to add {quantity}x {itemId}, but only has {itemSlot?.quantity ?? 0} or item not found.");
            SendErrorMessage(playerId, "Invalid item or quantity.");
            return;
        }

        // --- Update Session ---
        // Add or update the offer
        if (playerOffers.ContainsKey(itemId))
        {
            playerOffers[itemId].quantity += quantity;
        }
        else
        {
            playerOffers[itemId] = new TradingItemOffer(itemId, quantity);
        }
        // Reset confirmation if items change
        session.SetPlayerConfirmation(playerId, false);
        session.SetPlayerConfirmation(session.GetOtherPlayerId(playerId), false); // Reset partner's confirm too

        Debug.Log($"TradingService: Player {playerId} added {quantity}x {itemId} to trade session {sessionId}.");

        // --- Notify Players ---
        // Notify the player who added the item (confirmation)
        GameServer.Instance.SendToPlayer(playerId, new Dictionary<string, object>
        {
            ["type"] = "trade_status",
            ["status"] = "item_added",
            ["item_id"] = itemId,
            ["quantity_added"] = quantity,
            ["new_total_quantity"] = playerOffers[itemId].quantity
        });

        // Notify the other player about the change
        string partnerId = session.GetOtherPlayerId(playerId);
        GameServer.Instance.SendToPlayer(partnerId, new Dictionary<string, object>
        {
            ["type"] = "trade_update",
            ["update_type"] = "partner_item_added",
            ["partner_id"] = playerId,
            ["item_id"] = itemId,
            ["item_name"] = itemSlot.ItemDef.displayName,
            ["quantity"] = quantity,
            ["icon"] = $"images/icons/{itemSlot.ItemDef.itemId}"
            // Optionally include item name/icon path if needed client-side immediately
        });
    }

    /// <summary>
    /// Handles a player removing an item from the trade offer.
    /// </summary>
    private void HandleRemoveItem(string playerId, Dictionary<string, object> msg)
    {
        // Implementation is very similar to HandleAddItem
        // ... (validate session, player, item/quantity)
        // ... (update session offers dictionary, subtract quantity or remove entry)
        // ... (reset confirmations)
        // ... (notify both players)
        // Placeholder implementation
        if (!msg.TryGetValue("session_id", out var sessionIdObj) ||
           !msg.TryGetValue("item_id", out var itemIdObj) ||
           !msg.TryGetValue("quantity", out var quantityObj))
        {
            Debug.LogWarning($"TradingService: Remove item message from {playerId} missing required fields.");
            SendErrorMessage(playerId, "Invalid remove item request.");
            return;
        }

        string sessionId = sessionIdObj.ToString();
        string itemId = itemIdObj.ToString();
        int quantityToRemove = Convert.ToInt32(quantityObj);

        if (!_activeTradingSessions.TryGetValue(sessionId, out TradingSession session))
        {
            Debug.LogWarning($"TradingService: Remove item for non-existent session {sessionId} from player {playerId}.");
            SendErrorMessage(playerId, "Trade session not found.");
            return;
        }

        var playerOffers = session.GetOffersForPlayer(playerId);
        if (playerOffers == null)
        {
            Debug.LogWarning($"TradingService: Player {playerId} not part of session {sessionId} tried to remove item.");
            SendErrorMessage(playerId, "You are not part of this trade.");
            return;
        }

        // Check if the item is offered and the quantity is valid
        if (!playerOffers.TryGetValue(itemId, out TradingItemOffer offer) || offer.quantity < quantityToRemove || quantityToRemove <= 0)
        {
            Debug.LogWarning($"TradingService: Player {playerId} tried to remove {quantityToRemove}x {itemId}, but only offered {offer?.quantity ?? 0} or item not offered.");
            SendErrorMessage(playerId, "Invalid item or quantity to remove.");
            return;
        }

        // --- Update Session ---
        offer.quantity -= quantityToRemove;
        if (offer.quantity <= 0)
        {
            playerOffers.Remove(itemId); // Remove entry if quantity is zero or negative
        }
        // Reset confirmation if items change
        session.SetPlayerConfirmation(playerId, false);
        session.SetPlayerConfirmation(session.GetOtherPlayerId(playerId), false); // Reset partner's confirm too

        Debug.Log($"TradingService: Player {playerId} removed {quantityToRemove}x {itemId} from trade session {sessionId}.");

        // --- Notify Players ---
        GameServer.Instance.SendToPlayer(playerId, new Dictionary<string, object>
        {
            ["type"] = "trade_status",
            ["status"] = "item_removed",
            ["item_id"] = itemId,
            ["quantity_removed"] = quantityToRemove,
            ["new_total_quantity"] = playerOffers.ContainsKey(itemId) ? playerOffers[itemId].quantity : 0
        });

        string partnerId = session.GetOtherPlayerId(playerId);
        GameServer.Instance.SendToPlayer(partnerId, new Dictionary<string, object>
        {
            ["type"] = "trade_update",
            ["update_type"] = "partner_item_removed",
            ["partner_id"] = playerId,
            ["item_id"] = itemId,
            ["quantity"] = quantityToRemove
        });
    }

    /// <summary>
    /// Handles a player confirming the trade.
    /// </summary>
    private void HandleConfirmTrade(string playerId, Dictionary<string, object> msg)
    {
        if (!msg.TryGetValue("session_id", out var sessionIdObj))
        {
            Debug.LogWarning($"TradingService: Confirm trade message from {playerId} missing session_id.");
            SendErrorMessage(playerId, "Invalid confirm trade request.");
            return;
        }

        string sessionId = sessionIdObj.ToString();

        if (!_activeTradingSessions.TryGetValue(sessionId, out TradingSession session))
        {
            Debug.LogWarning($"TradingService: Confirm trade for non-existent session {sessionId} from player {playerId}.");
            SendErrorMessage(playerId, "Trade session not found.");
            return;
        }

        // Validate player is part of session
        if (session.GetOffersForPlayer(playerId) == null)
        {
            Debug.LogWarning($"TradingService: Player {playerId} not part of session {sessionId} tried to confirm.");
            SendErrorMessage(playerId, "You are not part of this trade.");
            return;
        }

        // --- Update Session Confirmation ---
        session.SetPlayerConfirmation(playerId, true);
        Debug.Log($"TradingService: Player {playerId} confirmed trade session {sessionId}.");

        string partnerId = session.GetOtherPlayerId(playerId);

        // --- Notify Partner ---
        GameServer.Instance.SendToPlayer(partnerId, new Dictionary<string, object>
        {
            ["type"] = "trade_update",
            ["update_type"] = "partner_confirmed",
            ["partner_id"] = playerId
        });

        // --- Check for Mutual Confirmation and Execute ---
        if (session.Player1Confirmed && session.Player2Confirmed)
        {
            Debug.Log($"TradingService: Both players confirmed trade session {sessionId}. Executing trade.");
            ExecuteTrade(session);
        }
        // If not both confirmed yet, do nothing else.
    }

    /// <summary>
    /// Handles a player cancelling the trade.
    /// </summary>
    private void HandleCancelTrade(string playerId, Dictionary<string, object> msg)
    {
        // Cancellation can happen at any time by either party.
        // Find session by player ID (as session ID might not always be readily available client-side)
        TradingSession session = FindSessionByParticipant(playerId);

        if (session == null)
        {
            // Might be a stale request or player not in a trade. Log and ignore.
            Debug.Log($"TradingService: Player {playerId} requested cancel trade but is not in an active session.");
            // Optionally send a status update
            GameServer.Instance.SendToPlayer(playerId, new Dictionary<string, object>
            {
                ["type"] = "trade_status",
                ["status"] = "cancelled",
                ["reason"] = "You are not currently in a trade."
            });
            return;
        }

        string sessionId = session.SessionId;
        string partnerId = session.GetOtherPlayerId(playerId);

        Debug.Log($"TradingService: Trade session {sessionId} cancelled by player {playerId}.");

        // Remove the session
        _activeTradingSessions.Remove(sessionId);

        // Notify the player who cancelled (acknowledge)
        GameServer.Instance.SendToPlayer(playerId, new Dictionary<string, object>
        {
            ["type"] = "trade_status",
            ["status"] = "cancelled",
            ["reason"] = "You cancelled the trade."
        });

        // Notify the partner (if still connected)
        if (partnerId != null && PlayerManager.Instance.GetPlayer(partnerId) != null)
        {
            GameServer.Instance.SendToPlayer(partnerId, new Dictionary<string, object>
            {
                ["type"] = "trade_status",
                ["status"] = "cancelled",
                ["reason"] = $"{PlayerManager.Instance.GetPlayer(playerId)?.LobbyData?.Name ?? "Player"} cancelled the trade."
            });
        }
    }

    /// <summary>
    /// Executes the trade by transferring items between players.
    /// </summary>
    private void ExecuteTrade(TradingSession session)
    {
        string sessionId = session.SessionId;
        string player1Id = session.Player1Id;
        string player2Id = session.Player2Id;

        Debug.Log($"TradingService: Executing trade between {player1Id} and {player2Id}.");

        bool success = true;
        string errorMessage = "";

        // --- Perform Transfers ---
        // Use the new TransferItem method in InventoryService
        if (InventoryService.Instance != null)
        {
            // Transfer items from Player 1 to Player 2
            foreach (var offer in session.Player1Offers.Values)
            {
                if (!InventoryService.Instance.TransferItem(player1Id, player2Id, offer.itemId, offer.quantity))
                {
                    success = false;
                    errorMessage = $"Failed to transfer {offer.quantity}x {offer.itemId} from {player1Id} to {player2Id}.";
                    Debug.LogError($"TradingService: {errorMessage}");
                    break; // Stop on first failure
                }
            }

            // If first transfer succeeded, transfer items from Player 2 to Player 1
            if (success)
            {
                foreach (var offer in session.Player2Offers.Values)
                {
                    if (!InventoryService.Instance.TransferItem(player2Id, player1Id, offer.itemId, offer.quantity))
                    {
                        success = false;
                        errorMessage = $"Failed to transfer {offer.quantity}x {offer.itemId} from {player2Id} to {player1Id}.";
                        Debug.LogError($"TradingService: {errorMessage}");
                        // Note: In a more robust system, you might try to roll back the first transfer.
                        // For simplicity here, we'll just report the error.
                        break;
                    }
                }
            }
        }
        else
        {
            success = false;
            errorMessage = "InventoryService is not available.";
            Debug.LogError($"TradingService: {errorMessage}");
        }

        // Remove the session as it's now complete (successfully or not)
        _activeTradingSessions.Remove(sessionId);

        // --- Notify Players ---
        if (success)
        {
            Debug.Log($"TradingService: Trade session {sessionId} completed successfully.");
            var completionData = new Dictionary<string, object>
            {
                ["type"] = "trade_status",
                ["status"] = "completed"
            };
            GameServer.Instance.SendToPlayer(player1Id, completionData);
            GameServer.Instance.SendToPlayer(player2Id, completionData);

            // Optional: Send inventory updates to both players
            InventoryService.Instance.SendInventoryUpdate(player1Id);
            InventoryService.Instance.SendInventoryUpdate(player2Id);
        }
        else
        {
            Debug.LogError($"TradingService: Trade session {sessionId} failed. {errorMessage}");
            var failureData = new Dictionary<string, object>
            {
                ["type"] = "trade_status",
                ["status"] = "failed",
                ["reason"] = errorMessage
            };
            GameServer.Instance.SendToPlayer(player1Id, failureData);
            GameServer.Instance.SendToPlayer(player2Id, failureData);
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Finds an active trading session that involves the given player ID.
    /// </summary>
    private TradingSession FindSessionByParticipant(string playerId)
    {
        foreach (var session in _activeTradingSessions.Values)
        {
            if (session.Player1Id == playerId || session.Player2Id == playerId)
            {
                return session;
            }
        }
        return null;
    }

    /// <summary>
    /// Sends a generic error message to a player.
    /// </summary>
    private void SendErrorMessage(string playerId, string message)
    {
        if (GameServer.Instance != null)
        {
            GameServer.Instance.SendToPlayer(playerId, new Dictionary<string, object>
            {
                ["type"] = "trade_status", // Or a specific "trade_error" type
                ["status"] = "error",
                ["message"] = message
            });
        }
    }

    /// <summary>
    /// Handles cleanup if a player disconnects while in a trade.
    /// This should be called by PlayerManager when a player disconnects.
    /// </summary>
    public void OnPlayerDisconnected(string playerId)
    {
        TradingSession session = FindSessionByParticipant(playerId);
        if (session != null)
        {
            string partnerId = session.GetOtherPlayerId(playerId);
            string sessionId = session.SessionId;

            Debug.Log($"TradingService: Player {playerId} disconnected during trade session {sessionId}. Cancelling trade.");

            // Remove the session
            _activeTradingSessions.Remove(sessionId);

            // Notify the partner if they are still connected
            if (partnerId != null && PlayerManager.Instance.GetPlayer(partnerId) != null)
            {
                GameServer.Instance.SendToPlayer(partnerId, new Dictionary<string, object>
                {
                    ["type"] = "trade_status",
                    ["status"] = "cancelled",
                    ["reason"] = $"{PlayerManager.Instance.GetPlayer(playerId)?.LobbyData?.Name ?? "Player"} disconnected."
                });
            }
        }
    }

    #endregion
}

/// <summary>
/// Represents an item offer within a trading session.
/// </summary>
[System.Serializable]
public class TradingItemOffer
{
    public string itemId;
    public int quantity;

    public TradingItemOffer(string itemId, int quantity)
    {
        this.itemId = itemId;
        this.quantity = quantity;
    }
}

/// <summary>
/// Represents the state and data of an active trade between two players.
/// </summary>
public class TradingSession
{
    public string SessionId { get; } // Unique ID for the session
    public string Player1Id { get; }
    public string Player2Id { get; }
    
    // Offers: Key is itemId, Value is the offer details
    public Dictionary<string, TradingItemOffer> Player1Offers { get; } = new Dictionary<string, TradingItemOffer>();
    public Dictionary<string, TradingItemOffer> Player2Offers { get; } = new Dictionary<string, TradingItemOffer>();
    
    public bool Player1Confirmed { get; set; } = false;
    public bool Player2Confirmed { get; set; } = false;

    public TradingSession(string sessionId, string player1Id, string player2Id)
    {
        SessionId = sessionId;
        Player1Id = player1Id;
        Player2Id = player2Id;
    }

    // Helper to get the other player's ID
    public string GetOtherPlayerId(string playerId)
    {
        if (playerId == Player1Id) return Player2Id;
        if (playerId == Player2Id) return Player1Id;
        return null; // Not part of this session
    }

    // Helper to get the correct offer dictionary for a player
    public Dictionary<string, TradingItemOffer> GetOffersForPlayer(string playerId)
    {
        if (playerId == Player1Id) return Player1Offers;
        if (playerId == Player2Id) return Player2Offers;
        return null; // Not part of this session
    }

    // Helper to get the correct confirmation status for a player
    public bool IsPlayerConfirmed(string playerId)
    {
        if (playerId == Player1Id) return Player1Confirmed;
        if (playerId == Player2Id) return Player2Confirmed;
        return false; // Not part of this session
    }

    // Helper to set the correct confirmation status for a player
    public void SetPlayerConfirmation(string playerId, bool confirmed)
    {
        if (playerId == Player1Id) Player1Confirmed = confirmed;
        else if (playerId == Player2Id) Player2Confirmed = confirmed;
        // Ignore if not part of session
    }
}