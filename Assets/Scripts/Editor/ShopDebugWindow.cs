using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class ShopDebugWindow : EditorWindow
{
    private string[] playerNames;
    private int selectedPlayerIndex = 0;
    private Vector2 shopScrollPos;
    private Vector2 playerInvScrollPos; // Scroll position for player inventory
    private List<ShopItemDisplay> shopItems = new List<ShopItemDisplay>();
    private List<PlayerItemDisplay> playerItems = new List<PlayerItemDisplay>(); // List for player items
    private int playerCoins;
    private bool needsRefresh;

    [MenuItem("Window/Debug/Shop Simulator")]
    public static void ShowWindow()
    {
        GetWindow<ShopDebugWindow>("Shop Simulator");
    }

    private void OnEnable()
    {
        // Subscribe to update events
        EditorApplication.update += OnEditorUpdate;
        needsRefresh = true;
    }

    private void OnDisable()
    {
        // Unsubscribe to prevent memory leaks
        EditorApplication.update -= OnEditorUpdate;
    }

    private void OnEditorUpdate()
    {
        if (needsRefresh)
        {
            needsRefresh = false;
            Repaint();
        }
    }

    private void OnGUI()
    {
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to use the Shop Simulator", MessageType.Warning);
            return;
        }

        if (ShopService.Instance == null)
        {
            EditorGUILayout.HelpBox("No active ShopService found in scene", MessageType.Error);
            return;
        }

        LoadPlayerList();
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Player Selection", EditorStyles.boldLabel);
        var newSelectedIndex = EditorGUILayout.Popup("Player", selectedPlayerIndex, playerNames);
        
        if (newSelectedIndex != selectedPlayerIndex)
        {
            selectedPlayerIndex = newSelectedIndex;
            needsRefresh = true;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Player Status", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Coins: {playerCoins}");

        if (GUILayout.Button("Refresh Data"))
        {
            needsRefresh = true;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Shop Items", EditorStyles.boldLabel);
        
        // Make a local copy to prevent modification during iteration
        var itemsToDisplay = new List<ShopItemDisplay>(shopItems);
        
        shopScrollPos = EditorGUILayout.BeginScrollView(shopScrollPos, GUILayout.Height(200)); // Set a fixed height
        foreach (var item in itemsToDisplay)
        {
            DrawShopItem(item);
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Player Inventory Items", EditorStyles.boldLabel);

        // Make a local copy for player items
        var playerItemsToDisplay = new List<PlayerItemDisplay>(playerItems);

        playerInvScrollPos = EditorGUILayout.BeginScrollView(playerInvScrollPos, GUILayout.Height(200)); // Set a fixed height
        if (playerItemsToDisplay.Count == 0)
        {
             EditorGUILayout.LabelField("No items available in player inventory.");
        }
        else
        {
            foreach (var item in playerItemsToDisplay)
            {
                 DrawPlayerItem(item);
            }
        }
        EditorGUILayout.EndScrollView();

        if (needsRefresh)
        {
            RefreshData();
        }
    }

    private void LoadPlayerList()
    {
        var players = PlayerManager.Instance?.GetAllPlayers();
        if (players == null || players.Count == 0)
        {
            playerNames = new[] { "No Players Found" };
            return;
        }

        playerNames = players.Select(p => p.LobbyData?.Name ?? p.NetworkId).ToArray();
    }

    private void RefreshData()
    {
        if (playerNames.Length == 0 || selectedPlayerIndex >= playerNames.Length) 
            return;

        var playerId = PlayerManager.Instance.GetAllPlayers()[selectedPlayerIndex].NetworkId;
        var inventory = InventoryService.Instance.GetPlayerInventory(playerId);
        
        // Get player coins
        // Assuming ShopService.Instance.CoinItemId exists and is the correct ID
        playerCoins = inventory.GetTotalQuantity(ShopService.Instance.CoinItemId); 
        
        // Get shop items - create new list to avoid modification issues
        var newShopItems = new List<ShopItemDisplay>();
        var shopData = ShopService.Instance.GetShopDataForDebug();
        foreach (var item in shopData)
        {
            newShopItems.Add(new ShopItemDisplay {
                itemId = item.Item1,
                itemName = item.Item2,
                price = item.Item3,
                slotIndex = item.Item4
            });
        }
        shopItems = newShopItems; // Atomic swap

        // --- NEW: Get Player Inventory Items ---
        var newPlayerItems = new List<PlayerItemDisplay>();
        // Get all items from the player's bag (assuming GetAllBagItems exists)
        var playerBagItems = inventory.GetAllBagItems(); // Or equivalent method
        foreach(var slot in playerBagItems)
        {
            if (slot.ItemDef != null && slot.quantity > 0) // Ensure item exists and has quantity
            {
                 // For selling, we might use base price or a derived sell price.
                 // Let's assume we use half the base price for simplicity in debug, like ShopService might.
                 // You could also fetch the actual sell price logic if needed.
                 int sellPrice = Mathf.Max(1, slot.ItemDef.basePrice / 2); 
                 newPlayerItems.Add(new PlayerItemDisplay {
                      itemId = slot.itemId,
                      itemName = slot.ItemDef.displayName ?? slot.itemId,
                      quantity = slot.quantity,
                      sellPricePerUnit = sellPrice // Price the shop would pay per unit
                 });
            }
        }
        playerItems = newPlayerItems; // Atomic swap
        // --- END NEW ---

        needsRefresh = false;
    }

    private void DrawShopItem(ShopItemDisplay item)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        EditorGUILayout.LabelField(item.itemName, EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Price: {item.price} coins");
        EditorGUILayout.LabelField($"ID: {item.itemId}");
        
        if (GUILayout.Button("Buy"))
        {
            BuyItem(item);
        }
        
        EditorGUILayout.EndVertical();
    }

    // --- NEW: Draw Player Item for Selling ---
    private void DrawPlayerItem(PlayerItemDisplay item)
    {
         EditorGUILayout.BeginVertical(EditorStyles.helpBox);

         EditorGUILayout.LabelField(item.itemName, EditorStyles.boldLabel);
         EditorGUILayout.LabelField($"Quantity: {item.quantity}");
         EditorGUILayout.LabelField($"Sell Price: {item.sellPricePerUnit} coins each");

         EditorGUI.BeginDisabledGroup(item.quantity <= 0);
         if (GUILayout.Button($"Sell 1"))
         {
             SellItem(item, 1);
         }
         // Optional: Add buttons for selling different quantities (e.g., Sell 5, Sell All)
         /*
         if (item.quantity > 1)
         {
             if (GUILayout.Button($"Sell 5"))
             {
                 int amount = Mathf.Min(5, item.quantity);
                 SellItem(item, amount);
             }
             if (GUILayout.Button($"Sell All ({item.quantity})"))
             {
                 SellItem(item, item.quantity);
             }
         }
         */
         EditorGUI.EndDisabledGroup();

         EditorGUILayout.EndVertical();
    }
    // --- END NEW ---

    private void BuyItem(ShopItemDisplay item)
    {
        if (playerNames.Length == 0 || selectedPlayerIndex >= playerNames.Length) 
            return;

        var player = PlayerManager.Instance.GetAllPlayers()[selectedPlayerIndex];
        
        // Create the same message a real client would send
        var buyMessage = new Dictionary<string, object> {
            ["type"] = "shop_action", // Ensure 'type' is included if your routing expects it
            ["action"] = "buy",
            ["item_id"] = item.itemId,
            ["quantity"] = 1
        };

        // Call the shop service directly (simulating network message)
        ShopService.Instance.HandleMessage(player, buyMessage);
        
        // Schedule a refresh rather than doing it immediately
        needsRefresh = true;
        
        Debug.Log($"[ShopDebugWindow] Sent BUY request for {item.itemName} (ID: {item.itemId}) to player {playerNames[selectedPlayerIndex]}");
    }

    // --- NEW: Sell Item Method ---
    private void SellItem(PlayerItemDisplay item, int quantity)
    {
         if (playerNames.Length == 0 || selectedPlayerIndex >= playerNames.Length)
             return;

         if (quantity <= 0 || quantity > item.quantity)
         {
             Debug.LogWarning($"[ShopDebugWindow] Invalid sell quantity {quantity} for item {item.itemName} (Player has {item.quantity})");
             return;
         }

         var player = PlayerManager.Instance.GetAllPlayers()[selectedPlayerIndex];

         // Create the same message a real client would send for selling
         var sellMessage = new Dictionary<string, object> {
             ["type"] = "shop_action", // Ensure 'type' is included
             ["action"] = "sell",
             ["item_id"] = item.itemId,
             ["quantity"] = quantity
         };

         // Call the shop service directly (simulating network message)
         ShopService.Instance.HandleMessage(player, sellMessage);

         // Schedule a refresh
         needsRefresh = true;

         Debug.Log($"[ShopDebugWindow] Sent SELL request for {quantity}x {item.itemName} (ID: {item.itemId}) to player {playerNames[selectedPlayerIndex]}");
    }
    // --- END NEW ---

    // --- Classes for Display Data ---
    private class ShopItemDisplay
    {
        public string itemId;
        public string itemName;
        public int price;
        public int slotIndex;
    }

    // --- NEW: Class for Player Inventory Items ---
    private class PlayerItemDisplay
    {
        public string itemId;
        public string itemName;
        public int quantity;
        public int sellPricePerUnit; // Price the shop pays per unit
    }
    // --- END NEW ---
}