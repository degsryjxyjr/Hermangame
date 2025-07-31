using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class TradeSimulatorWindow : EditorWindow
{
    private string requesterId;
    private string targetId;
    private string currentSessionId;
    
    private Vector2 scrollPosition;
    private Dictionary<string, int> requesterOffer = new Dictionary<string, int>();
    private Dictionary<string, int> targetOffer = new Dictionary<string, int>();
    private bool requesterConfirmed;
    private bool targetConfirmed;
    
    private string[] playerIds;
    private string[] playerNames;
    private int requesterIndex;
    private int targetIndex;
    
    private string[] requesterItemIds;
    private string[] requesterItemNames;
    private int requesterItemIndex;
    
    private string[] targetItemIds;
    private string[] targetItemNames;
    private int targetItemIndex;
    
    private int itemQuantity = 1;
    
    [MenuItem("Window/Trade Simulator (Server)")]
    public static void ShowWindow()
    {
        GetWindow<TradeSimulatorWindow>("Trade Simulator (Server)");
    }

    private void OnEnable()
    {
        RefreshPlayerList();
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Server Trade Simulation", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // Player selection
        EditorGUILayout.LabelField("Select Players", EditorStyles.boldLabel);
        
        if (playerIds == null || playerIds.Length < 2)
        {
            EditorGUILayout.HelpBox("Need at least 2 players to trade", MessageType.Warning);
            if (GUILayout.Button("Refresh Player List"))
            {
                RefreshPlayerList();
            }
            EditorGUILayout.EndScrollView();
            return;
        }
        
        requesterIndex = EditorGUILayout.Popup("Requester", requesterIndex, playerNames);
        targetIndex = EditorGUILayout.Popup("Target", targetIndex, playerNames);
        
        requesterId = playerIds[requesterIndex];
        targetId = playerIds[targetIndex];
        
        if (requesterId == targetId)
        {
            EditorGUILayout.HelpBox("Requester and Target cannot be the same player", MessageType.Error);
            EditorGUILayout.EndScrollView();
            return;
        }
        
        EditorGUILayout.Space();
        
        // Session management
        if (string.IsNullOrEmpty(currentSessionId))
        {
            if (GUILayout.Button("Send Trade Request"))
            {
                SendTradeRequest();
            }
        }
        else
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Active Session: {currentSessionId}", EditorStyles.boldLabel);
            
            // Display player names instead of IDs
            EditorGUILayout.LabelField($"Requester: {playerNames[requesterIndex]}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Target: {playerNames[targetIndex]}", EditorStyles.boldLabel);
            
            // Item selection for requester
            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"{playerNames[requesterIndex]}'s Items", EditorStyles.boldLabel);
            
            if (requesterItemNames != null && requesterItemNames.Length > 0)
            {
                requesterItemIndex = EditorGUILayout.Popup("Item", requesterItemIndex, requesterItemNames);
                itemQuantity = EditorGUILayout.IntField("Quantity", itemQuantity);
                itemQuantity = Mathf.Max(1, itemQuantity);
                
                if (GUILayout.Button("Add to Requester Offer"))
                {
                    AddItemToOffer(requesterId, requesterItemIds[requesterItemIndex], itemQuantity);
                }
            }
            else
            {
                EditorGUILayout.LabelField("No items in inventory");
            }
            
            // Item selection for target
            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"{playerNames[targetIndex]}'s Items", EditorStyles.boldLabel);
            
            if (targetItemNames != null && targetItemNames.Length > 0)
            {
                targetItemIndex = EditorGUILayout.Popup("Item", targetItemIndex, targetItemNames);
                itemQuantity = EditorGUILayout.IntField("Quantity", itemQuantity);
                itemQuantity = Mathf.Max(1, itemQuantity);
                
                if (GUILayout.Button("Add to Target Offer"))
                {
                    AddItemToOffer(targetId, targetItemIds[targetItemIndex], itemQuantity);
                }
            }
            else
            {
                EditorGUILayout.LabelField("No items in inventory");
            }
            
            // Current offers
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Current Offers", EditorStyles.boldLabel);
            
            EditorGUILayout.LabelField($"{playerNames[requesterIndex]}'s Offer:");
            DisplayOffer(requesterOffer);
            
            EditorGUILayout.LabelField($"{playerNames[targetIndex]}'s Offer:");
            DisplayOffer(targetOffer);
            
            // Trade actions
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Trade Actions", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            // Change confirm button to Cancel if already confirmed
            if (requesterConfirmed)
            {
                if (GUILayout.Button("Requester Cancel", GUILayout.Width(150)))
                {
                    CancelConfirmation(requesterId);
                }
            }
            else
            {
                GUI.enabled = requesterOffer.Count > 0;
                if (GUILayout.Button("Requester Confirm", GUILayout.Width(150)))
                {
                    ConfirmTrade(requesterId);
                }
                GUI.enabled = true;
            }
            
            if (targetConfirmed)
            {
                if (GUILayout.Button("Target Cancel", GUILayout.Width(150)))
                {
                    CancelConfirmation(targetId);
                }
            }
            else
            {
                GUI.enabled = targetOffer.Count > 0;
                if (GUILayout.Button("Target Confirm", GUILayout.Width(150)))
                {
                    ConfirmTrade(targetId);
                }
                GUI.enabled = true;
            }
            EditorGUILayout.EndHorizontal();
            
            if (GUILayout.Button("Cancel Entire Trade"))
            {
                CancelTrade();
            }
            
            // Status
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Status:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"{playerNames[requesterIndex]} Confirmed: {(requesterConfirmed ? "Yes" : "No")}");
            EditorGUILayout.LabelField($"{playerNames[targetIndex]} Confirmed: {(targetConfirmed ? "Yes" : "No")}");
            
            // Check if trade was completed
            if (TradingService.Instance.GetActiveTradingSessions().ContainsKey(currentSessionId) == false)
            {
                // Trade was completed or cancelled elsewhere
                currentSessionId = null;
                requesterOffer.Clear();
                targetOffer.Clear();
                requesterConfirmed = false;
                targetConfirmed = false;
                Repaint();
            }
        }
        
        EditorGUILayout.EndScrollView();
    }
    
    private void RefreshPlayerList()
    {
        if (PlayerManager.Instance == null)
        {
            playerIds = new string[0];
            playerNames = new string[0];
            return;
        }
        
        var players = PlayerManager.Instance.GetAllPlayers();
        playerIds = players.Select(p => p.NetworkId).ToArray();
        playerNames = players.Select(p => p.LobbyData?.Name ?? p.NetworkId).ToArray();
        
        if (playerIds.Length > 0)
        {
            requesterIndex = 0;
            targetIndex = playerIds.Length > 1 ? 1 : 0;
        }
    }
    
    private void RefreshPlayerItems()
    {
        if (InventoryService.Instance == null) return;
        
        // Refresh requester items
        var requesterInventory = InventoryService.Instance.GetPlayerInventory(requesterId);
        if (requesterInventory != null)
        {
            var bagItems = requesterInventory.GetAllBagItems();
            requesterItemIds = bagItems.Select(i => i.itemId).ToArray();
            requesterItemNames = bagItems.Select(i => $"{i.ItemDef.displayName} (x{i.quantity})").ToArray();
            requesterItemIndex = requesterItemNames.Length > 0 ? 0 : -1;
        }
        
        // Refresh target items
        var targetInventory = InventoryService.Instance.GetPlayerInventory(targetId);
        if (targetInventory != null)
        {
            var bagItems = targetInventory.GetAllBagItems();
            targetItemIds = bagItems.Select(i => i.itemId).ToArray();
            targetItemNames = bagItems.Select(i => $"{i.ItemDef.displayName} (x{i.quantity})").ToArray();
            targetItemIndex = targetItemNames.Length > 0 ? 0 : -1;
        }
    }
    
    private void DisplayOffer(Dictionary<string, int> offer)
    {
        if (offer.Count == 0)
        {
            EditorGUILayout.LabelField("  No items offered");
            return;
        }
        
        foreach (var item in offer)
        {
            EditorGUILayout.BeginHorizontal();
            var itemDef = InventoryService.Instance.GetItemDefinition(item.Key);
            string displayName = itemDef != null ? itemDef.displayName : item.Key;
            EditorGUILayout.LabelField($"  {displayName} x{item.Value}");
            
            if (GUILayout.Button("Remove", GUILayout.Width(80)))
            {
                RemoveItemFromOffer(offer, item.Key, 1);
            }
            
            if (GUILayout.Button("Remove All", GUILayout.Width(80)))
            {
                RemoveItemFromOffer(offer, item.Key, item.Value);
            }
            
            EditorGUILayout.EndHorizontal();
        }
    }
    
    private void SendTradeRequest()
    {
        var msg = new Dictionary<string, object>
        {
            ["trade_action"] = "request",
            ["target_player_id"] = targetId
        };
        
        TradingService.Instance.HandleTradeMessage(requesterId, msg);
        
        // Get the created session
        var session = TradingService.Instance.GetActiveTradingSessions().Values
            .FirstOrDefault(s => s.Player1Id == requesterId && s.Player2Id == targetId);
        
        if (session != null)
        {
            currentSessionId = session.SessionId;
            requesterConfirmed = false;
            targetConfirmed = false;
            requesterOffer.Clear();
            targetOffer.Clear();
            
            RefreshPlayerItems();
            
            Debug.Log($"Trade session started: {currentSessionId}");
        }
    }
    
    private void AddItemToOffer(string playerId, string itemId, int quantity)
    {
        if (string.IsNullOrEmpty(currentSessionId)) return;
        
        var msg = new Dictionary<string, object>
        {
            ["trade_action"] = "add_item",
            ["session_id"] = currentSessionId,
            ["item_id"] = itemId,
            ["quantity"] = quantity
        };
        
        TradingService.Instance.HandleTradeMessage(playerId, msg);
        
        // Update local offer display
        var offer = playerId == requesterId ? requesterOffer : targetOffer;
        if (offer.ContainsKey(itemId))
        {
            offer[itemId] += quantity;
        }
        else
        {
            offer[itemId] = quantity;
        }
        
        // Reset confirmations when offer changes
        if (playerId == requesterId) requesterConfirmed = false;
        if (playerId == targetId) targetConfirmed = false;
        
        // Refresh items to reflect changes in inventory
        RefreshPlayerItems();
    }
    
    private void RemoveItemFromOffer(Dictionary<string, int> offer, string itemId, int quantity)
    {
        if (string.IsNullOrEmpty(currentSessionId)) return;
        
        // Determine which player this offer belongs to
        string playerId = offer == requesterOffer ? requesterId : targetId;
        
        var msg = new Dictionary<string, object>
        {
            ["trade_action"] = "remove_item",
            ["session_id"] = currentSessionId,
            ["item_id"] = itemId,
            ["quantity"] = quantity
        };
        
        TradingService.Instance.HandleTradeMessage(playerId, msg);
        
        // Update local offer
        if (offer.ContainsKey(itemId))
        {
            offer[itemId] -= quantity;
            if (offer[itemId] <= 0)
            {
                offer.Remove(itemId);
            }
        }
        
        // Reset confirmations when offer changes
        if (playerId == requesterId) requesterConfirmed = false;
        if (playerId == targetId) targetConfirmed = false;
        
        // Refresh items to reflect changes in inventory
        RefreshPlayerItems();
    }
    
    private void ConfirmTrade(string playerId)
    {
        if (string.IsNullOrEmpty(currentSessionId)) return;
        
        var msg = new Dictionary<string, object>
        {
            ["trade_action"] = "confirm",
            ["session_id"] = currentSessionId
        };
        
        TradingService.Instance.HandleTradeMessage(playerId, msg);
        
        // Update local confirmation status
        if (playerId == requesterId) requesterConfirmed = true;
        if (playerId == targetId) targetConfirmed = true;
    }
    
    private void CancelConfirmation(string playerId)
    {
        if (string.IsNullOrEmpty(currentSessionId)) return;
        
        // Simulate removing confirmation by removing and re-adding an item
        var offer = playerId == requesterId ? requesterOffer : targetOffer;
        if (offer.Count > 0)
        {
            var firstItem = offer.First();
            RemoveItemFromOffer(offer, firstItem.Key, 0); // Remove 0 to just reset confirmation
        }
    }
    
    private void CancelTrade()
    {
        if (string.IsNullOrEmpty(currentSessionId)) return;
        
        // Either player can cancel
        var msg = new Dictionary<string, object>
        {
            ["trade_action"] = "cancel",
            ["session_id"] = currentSessionId
        };
        
        TradingService.Instance.HandleTradeMessage(requesterId, msg);
        
        // Reset state
        currentSessionId = null;
        requesterOffer.Clear();
        targetOffer.Clear();
        requesterConfirmed = false;
        targetConfirmed = false;
    }
}