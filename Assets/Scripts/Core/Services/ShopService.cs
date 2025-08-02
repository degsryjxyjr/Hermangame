using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq; // Needed for .Where() and .ToList()
using TMPro;
using UnityEngine.UI;
/// <summary>
/// Manages the shop system for the game.
/// Handles inventory (shared), restocking based on party level, and processing purchases.
/// This service is expected to be added to a GameObject in the Shop scene.
/// </summary>
public class ShopService : MonoBehaviour
{
    #region Nested SimpleShopInventory Class

    /// <summary>
    /// A simplified class to manage the shop's current stock.
    /// Uses a list of ItemDefinitions with fixed size.
    /// Stock is set once when the service initializes (scene loads).
    /// </summary>
    [Serializable] // Make it serializable for potential inspector tweaks
    private class SimpleShopInventory
    {
        // Change the internal storage
        // Stores the items currently available for purchase in fixed slots.
        // (ItemDefinition, Modified Price) tuples. Null itemDef represents an empty slot.
        public List<(ItemDefinition itemDef, int modifiedPrice)> ItemsForSale { get; private set; } = new List<(ItemDefinition, int)>();

        // Add a property for price variability (set during initialization)
        public int PriceVariability { get; set; } = 0;

        /// <summary>
        /// Gets the maximum number of item slots the shop has.
        /// </summary>
        public int MaxSlots { get; private set; }

        public SimpleShopInventory(int maxSlots)
        {
            MaxSlots = maxSlots;
            PriceVariability = 0; // Default, will be set by ShopService
            // Initialize list with empty tuples to represent empty slots
            for (int i = 0; i < MaxSlots; i++)
            {
                ItemsForSale.Add((null, 0));
            }
        }

        /// <summary>
        /// Checks if a specific slot index has an item.
        /// </summary>
        public bool HasItem(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= MaxSlots) return false;
            // OLD (causes CS0019): return ItemsForSale[slotIndex] != null;
            // NEW: Check if the ItemDefinition part of the tuple is not null
            return ItemsForSale[slotIndex].itemDef != null;
        }

        /// <summary>
        /// Gets the ItemDefinition at a specific slot index.
        /// Returns null if index is invalid or slot is empty.
        /// </summary>
        public ItemDefinition GetItem(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= MaxSlots) return null;
            // OLD: return ItemsForSale[slotIndex];
            // NEW: Return the ItemDefinition part of the tuple
            return ItemsForSale[slotIndex].itemDef;
        }

        /// <summary>
        /// Gets the modified price for the item in a specific slot index.
        /// Returns 0 if index is invalid or slot is empty.
        /// </summary>
        public int GetModifiedPrice(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= MaxSlots) return 0;
            // Return the stored modified price, or calculate if somehow 0 and item exists
            var slotData = ItemsForSale[slotIndex];
            if (slotData.itemDef != null && slotData.modifiedPrice > 0)
            {
                return slotData.modifiedPrice;
            }
            else if (slotData.itemDef != null)
            {
                // Fallback calculation if stored price was 0 (shouldn't happen after restock)
                return Mathf.Max(1, slotData.itemDef.basePrice); // Ensure price is at least 1
            }
            return 0;
        }

        /// <summary>
        /// Removes the item from a specific slot index, making it empty.
        /// </summary>
        /// <returns>True if an item was present and removed.</returns>
        public bool RemoveItem(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= MaxSlots) return false;
            // OLD (check): if (ItemsForSale[slotIndex] != null)
            // NEW (check): Check if the ItemDefinition part is not null
            if (ItemsForSale[slotIndex].itemDef != null)
            {
                // OLD (assignment): ItemsForSale[slotIndex] = null;
                // NEW (assignment): Set the tuple elements, keeping the structure
                ItemsForSale[slotIndex] = (null, 0); // (ItemDefinition is null, price is 0 for empty slot)
                // Debug.Log($"SimpleShopInventory: Item removed from slot {slotIndex}.");
                return true;
            }
            return false; // Slot was already empty
        }

        /// <summary>
        /// Clears all items from the shop's inventory, making all slots empty.
        /// </summary>
        public void Clear()
        {
            for (int i = 0; i < MaxSlots; i++)
            {
                // OLD (assignment): ItemsForSale[i] = null;
                // NEW (assignment): Set the tuple elements for each slot
                ItemsForSale[i] = (null, 0); // (ItemDefinition is null, price is 0 for empty slot)
            }
            // Debug.Log("SimpleShopInventory: Cleared all items for sale.");
        }

        /// <summary>
        /// Repopulates the shop's inventory with a fixed number of items.
        /// Selects items randomly from the provided applicable loot tables.
        /// Applies price variability.
        /// Fills ALL maxItems slots if possible (repeats items if necessary).
        /// </summary>
        public void Restock(List<LootTable> applicableTables, int maxItems, int priceVariability)
        {
            // Store the variability setting for potential future use or reference
            PriceVariability = priceVariability;

            Clear(); // Start fresh

            if (applicableTables == null || applicableTables.Count == 0)
            {
                Debug.Log("SimpleShopInventory: No applicable loot tables provided for restock. Shop is now empty.");
                return;
            }

            if (maxItems <= 0)
            {
                Debug.LogWarning("SimpleShopInventory: Max items for restock is <= 0. Shop will be empty.");
                return;
            }

            Debug.Log($"SimpleShopInventory: Starting restock for {maxItems} slots (Price Variability: ±{priceVariability}).");

            // Collect all possible items from applicable tables
            List<ItemDefinition> allPossibleItems = new List<ItemDefinition>();
            foreach (var table in applicableTables)
            {
                foreach (var itemDef in table.Entries)
                {
                    if (itemDef != null)
                    {
                        allPossibleItems.Add(itemDef);
                    }
                }
            }

            if (allPossibleItems.Count == 0)
            {
                Debug.LogWarning("SimpleShopInventory: No valid items found in applicable loot tables. Shop will be empty.");
                return;
            }

            // Shuffle the list of possible items to ensure randomness for selection
            Shuffle(allPossibleItems);

            System.Random rng = new System.Random(); // Use System.Random for consistency

            // --- MODIFIED LOGIC ---
            // Fill ALL shop slots up to maxItems.
            // If we run out of unique items, we'll start picking randomly again (allowing duplicates).
            for (int i = 0; i < maxItems; i++)
            {
                // Pick a random item from the available list for this slot
                // Use modulo to wrap around if i >= allPossibleItems.Count, ensuring we always pick something
                int itemIndex = i % allPossibleItems.Count; // Start sequentially, then wrap
                // For better randomness, especially with fewer items, pick randomly each time:
                // int itemIndex = rng.Next(0, allPossibleItems.Count);

                ItemDefinition itemToPlace = allPossibleItems[itemIndex];
                int basePrice = itemToPlace.basePrice;

                // --- Apply Price Variability ---
                int modifiedPrice = basePrice;
                if (priceVariability > 0)
                {
                    // Generate random variance between -priceVariability and +priceVariability
                    int variance = rng.Next(-priceVariability, priceVariability + 1); // +1 because max is exclusive
                    modifiedPrice = Mathf.Max(1, basePrice + variance); // Ensure price doesn't go below 1
                }
                // --- END Apply Price Variability ---

                // Place item and its modified price in slot i
                ItemsForSale[i] = (itemToPlace, modifiedPrice);
                // Debug.Log($"SimpleShopInventory: Placed {itemToPlace.itemId} (Base: {basePrice}, Modified: {modifiedPrice}) in slot {i}.");
            }
            // --- END MODIFIED LOGIC ---

            Debug.Log($"SimpleShopInventory: Restock completed. All {maxItems} slots filled.");
        }

         /// <summary>
        /// Shuffles a list in place using the Fisher-Yates algorithm.
        /// </summary>
        private void Shuffle<T>(List<T> list)
        {
            System.Random rng = new System.Random();
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        /// <summary>
        /// Gets a list of non-null ItemDefinitions currently for sale.
        /// Useful for sending data or UI updates.
        /// </summary>
        public List<ItemDefinition> GetNonNullItems()
        {
            // return ItemsForSale.Where(item => item != null).ToList(); // OLD
            return ItemsForSale.Where(slot => slot.itemDef != null).Select(slot => slot.itemDef).ToList(); // NEW
        }

        /// <summary>
        /// Gets the data needed for the UI: a list of (ItemDefinition, modifiedPrice, slotIndex) for non-null items.
        /// </summary>
        // Update the return type and logic
        public List<(ItemDefinition itemDef, int modifiedPrice, int slotIndex)> GetUIItemData()
        {
            var uiData = new List<(ItemDefinition, int, int)>();
            for (int i = 0; i < ItemsForSale.Count; i++)
            {
                // if (ItemsForSale[i] != null) // OLD
                if (ItemsForSale[i].itemDef != null) // NEW
                {
                    // uiData.Add((ItemsForSale[i], i)); // OLD
                    uiData.Add((ItemsForSale[i].itemDef, ItemsForSale[i].modifiedPrice, i)); // NEW
                }
            }
            return uiData;
        }
    }

    #endregion

    #region ShopService Implementation

    public static ShopService Instance { get; private set; }

    [Header("Configuration")]
    [SerializeField] private string _coinItemId = "Coin"; // ID of your coin ItemDefinition
    [SerializeField] private string _lootTablesResourcePath = "LootTables"; // Path under Resources folder
    [SerializeField] private int _maxShopItems = 8; // Easily configurable number of shop slots
    [SerializeField] private int _baseShopMoney = 100; // Configurable base amount
    [SerializeField] private int _shopMoney ; // Actual number of coins the shop has(relevant when players want to sell items)
    [SerializeField] private int _priceVariability = 2; // Price variability (e.g., 10 means -10 to +10)

    public string CoinItemId => _coinItemId;

    [Header("UI References")]
    [Tooltip("The parent Transform where item UI prefabs will be instantiated.")]
    [SerializeField] private Transform _itemsUIParent; // Assign in Inspector
    [Tooltip("The prefab to instantiate for each item in the shop UI.")]
    [SerializeField] private GameObject _itemUIPrefab; // Assign in Inspector

    [SerializeField] private TMP_Text ShopMoneyText;

    [SerializeField] private PlayerInventory _npcPlayerInv; // Assign in Inspector

    private SimpleShopInventory _shopInventory; // Use the simplified ShopInventory class
    private List<LootTable> _allShopLootTables = new List<LootTable>();
    private const string SHOP_NPC_ID = "SHOP_NPC"; // Reserved ID for the shop's "coin stash" in InventoryService

    private void Awake()
    {
        // Ensure only one instance exists in the scene
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"Duplicate ShopService instance detected in scene! Destroying {gameObject.name}");
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // Do NOT use DontDestroyOnLoad, as this service is scene-specific
        Debug.Log("ShopService initialized for this scene.");
        Initialize();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
        // removing the temp shop inventory. this way the shops coin reserves update correctly for selling items
        InventoryService.Instance.RemoveInventory(SHOP_NPC_ID);
        Debug.Log("ShopService removed temp inv.");
    }

    private void Initialize()
    {
        // Initialize the simplified shop inventory instance with the specified max size
        _shopInventory = new SimpleShopInventory(_maxShopItems);

        // Load all loot tables from the specified Resources path
        LootTable[] loadedTables = Resources.LoadAll<LootTable>(_lootTablesResourcePath);
        _allShopLootTables = new List<LootTable>(loadedTables);
        Debug.Log($"ShopService: Loaded {_allShopLootTables.Count} loot tables from Resources/{_lootTablesResourcePath}");

        // Restock the shop inventory based on the current party level
        RestockShop();

        // Create a temp PlayerInventory for Transfering items
        // Initialize Shop NPC Inventory ---
        if (InventoryService.Instance != null)
        {
            // Use the helper to ensure an inventory exists for the SHOP_NPC.
            _npcPlayerInv = InventoryService.Instance.InitializeOrGetTempInventory(SHOP_NPC_ID, _shopMoney);

        }
        else
        {
            Debug.LogError("ShopService: InventoryService.Instance is null during ShopService initialization!");
        }

        // Populate the Unity UI
        UpdateShopUI();

        // Broadcast shop items
        BroadcastShopData();
    }

    // Helper to get the current party level
    private int GetCurrentPartyLevel()
    {
        // Access Party level via PlayerManager's main party instance
        if (PlayerManager.Instance?._mainPlayerParty != null)
        {
            return PlayerManager.Instance._mainPlayerParty.Level;
        }
        Debug.LogWarning("ShopService: Could not get Party level. Defaulting to 1 for restock.");
        return 1;
    }

    // Determine which loot tables are applicable based on the current party level
    private List<LootTable> DetermineApplicableLootTables(int currentPartyLevel)
    {
        return _allShopLootTables.Where(table => table.minLevelRequirement <= currentPartyLevel).ToList();
    }

    // Core restocking logic
    private void RestockShop()
    {
        int currentLevel = GetCurrentPartyLevel();
        List<LootTable> applicableTables = DetermineApplicableLootTables(currentLevel);

        Debug.Log($"ShopService: Restocking shop for party level {currentLevel}. Applicable tables: {applicableTables.Count}");

        // Delegate restocking to the SimpleShopInventory instance
        _shopInventory.Restock(applicableTables, _maxShopItems, _priceVariability);


        // --- Calculate Dynamic Shop Money ---
        int baseMoneyForLevel = _baseShopMoney * currentLevel; // Scale base money with level

        System.Random rng = new System.Random(); // Use consistent RNG
        int moneyVariance = 0;
        if (_priceVariability > 0)
        {
            // Generate random variance between -priceVariability and +priceVariability
            moneyVariance = rng.Next(-_priceVariability, _priceVariability + 1);
        }
        _shopMoney = Mathf.Max(0, baseMoneyForLevel + moneyVariance); // Ensure money doesn't go negative
        Debug.Log($"ShopService: Calculated shop money: Base({_baseShopMoney}) * Level({currentLevel}) = {baseMoneyForLevel} + Variance({moneyVariance}) = Total({_shopMoney})");
        // --- END  ---


        Debug.Log("ShopService: Shop restocking completed.");
    }

    // Updates the server-side Unity UI to reflect the current shop inventory
    private void UpdateShopUI()
    {
        if (_itemsUIParent == null)
        {
            Debug.LogWarning("ShopService: Items UI Parent is not assigned. Cannot update shop UI.");
            return;
        }

        if (_itemUIPrefab == null)
        {
            Debug.LogWarning("ShopService: Item UI Prefab is not assigned. Cannot update shop UI.");
            return;
        }

        // Clear existing UI elements
        foreach (Transform child in _itemsUIParent)
        {
            Destroy(child.gameObject);
        }

        // Get UI data: (ItemDefinition, slotIndex)
        var uiItemData = _shopInventory.GetUIItemData();

        // Instantiate prefabs for each item
        // NEW loop structure to handle modifiedPrice
        foreach (var (itemDef, modifiedPrice, slotIndex) in uiItemData)
        {
            GameObject itemUIInstance = Instantiate(_itemUIPrefab, _itemsUIParent);

            var uiEntry = itemUIInstance.GetComponent<ShopItemUIEntry>();
            if (uiEntry != null)
            {
                // Pass the modified price
                uiEntry.Initialize(itemDef, this, modifiedPrice); // Pass 'this' if UI needs to callback (e.g., on buy button click) and the modifiedPrice
            }
            else
            {
                Debug.LogWarning($"ShopService: Item UI Prefab {_itemUIPrefab.name} is missing ShopItemUIEntry component.");
            }
            // For now, just a placeholder log
            Debug.Log($"Instantiated UI for {itemDef.itemId} (Slot {slotIndex}, Price: {modifiedPrice})");
        }

        // getting most up to date shopMoney from inventory
        _shopMoney = _npcPlayerInv.GetTotalQuantity(_coinItemId);

        // update shop money text
        ShopMoneyText.text = $"Shop has {_shopMoney} coins.";

        Debug.Log($"ShopService: Unity UI updated with {_itemsUIParent.childCount} items.");
    }


    /// <summary>
    /// Public method to allow UI elements (if they have callbacks) to trigger a purchase.
    /// This bypasses the network message system for direct UI interaction on the host.
    /// </summary>
    /// <param name="slotIndex">The index of the item slot in the shop inventory.</param>
    /// <param name="playerId">The ID of the player making the purchase.</param>
    public void AttemptPurchaseFromUI(int slotIndex, string playerId)
    {
        if (slotIndex < 0 || slotIndex >= _maxShopItems)
        {
            Debug.LogWarning($"ShopService.AttemptPurchaseFromUI: Invalid slot index {slotIndex}.");
            // TODO: Potentially send an error to the player if UI actions trigger network messages
            return;
        }

        ItemDefinition itemDef = _shopInventory.GetItem(slotIndex);
        if (itemDef == null)
        {
            Debug.LogWarning($"ShopService.AttemptPurchaseFromUI: No item found in slot {slotIndex}.");
            // TODO: Send error
            return;
        }

        // Create a simulated message to reuse the existing purchase logic
        var simulatedMessage = new Dictionary<string, object>
        {
            { "item_id", itemDef.itemId },
            { "quantity", 1 }, // Assuming buying 1 item per UI click
            // Slot index isn't strictly needed by HandleBuyRequest, but could be useful for UI sync
            // if you track items by slot rather than ID after purchase.
        };

        // Call the main purchase handler
        HandleBuyRequest(playerId, simulatedMessage, slotIndex); // Pass slot index for UI update
    }


    // Called when a player sends a "shop_action" message (e.g., via WebSocket)
    // This is the primary entry point for networked player interactions.
    public void HandleMessage(string playerId, Dictionary<string, object> message)
    {
        if (message == null || !message.ContainsKey("action"))
        {
            Debug.LogWarning("ShopService: Received shop message missing 'action' field.");
            SendErrorMessage(playerId, "Invalid shop action request.");
            return;
        }

        string action = message["action"].ToString();

        switch (action)
        {
            case "buy":
                HandleBuyRequest(playerId, message, -1); // -1 indicates it came from network, not specific UI slot
                break;

            case "sell": // Implement if needed
                HandleSellRequest(playerId, message);
                break;

            case "view":
                // For network view, just send current state. UI is already populated.
                SendShopDataToPlayer(playerId);
                break;

            default:
                Debug.LogWarning($"ShopService: Unknown shop action '{action}'.");
                SendErrorMessage(playerId, $"Unknown shop action: {action}");
                break;
        }
    }

    // Handles buy requests, either from network or UI
    private void HandleBuyRequest(string playerId, Dictionary<string, object> message, int sourceSlotIndex = -1)
    {
        // --- Message Parsing ---
        if (!message.TryGetValue("item_id", out var itemIdObj))
        {
            Debug.LogWarning("ShopService: Buy request missing 'item_id'.");
            SendErrorMessage(playerId, "Invalid buy request: Missing item.");
            return;
        }

        // Quantity is optional in UI context (defaults to 1), but required from network for clarity
        int quantity = 1;
        if (message.ContainsKey("quantity"))
        {
            if (!int.TryParse(message["quantity"].ToString(), out quantity) || quantity <= 0)
            {
                Debug.LogWarning("ShopService: Buy request has invalid 'quantity'.");
                SendErrorMessage(playerId, "Invalid buy request: Quantity must be a positive number.");
                return;
            }
        }
        // If quantity is missing (UI case), it defaults to 1 above.

        string itemId = itemIdObj.ToString();

        // --- Validation ---
        // 1. Get ItemDefinition
        ItemDefinition itemDef = InventoryService.Instance?.GetItemDefinition(itemId);
        if (itemDef == null)
        {
            Debug.LogError($"ShopService: Could not find ItemDefinition for {itemId} during buy request.");
            SendErrorMessage(playerId, "Item data not found.");
            return;
        }

        // 2. Validate shop has the item (find its slot) - Logic remains, but uses updated inventory structure internally
        int itemSlotIndex = -1;
        bool itemFound = false;
        // Iterate through shop inventory to find the item and its slot
        for (int i = 0; i < _maxShopItems; i++) // Use _maxShopItems as the limit
        {
            ItemDefinition currentItem = _shopInventory.GetItem(i); // Uses updated GetItem
            if (currentItem != null && currentItem.itemId == itemId)
            {
                itemSlotIndex = i;
                itemFound = true;
                break;
            }
        }

        if (!itemFound)
        {
            Debug.LogWarning($"ShopService: Player {playerId} tried to buy {itemId}, but item is not available in the shop.");
            SendErrorMessage(playerId, "Item is not available in the shop.");
            return;
        }

        // 3. Calculate cost using the MODIFIED price from the specific slot
        // OLD: int totalCost = itemDef.basePrice * quantity;
        // NEW: Get the modified price stored for this specific item slot
        int itemModifiedPrice = _shopInventory.GetModifiedPrice(itemSlotIndex); // Uses updated GetModifiedPrice
        int totalCost = itemModifiedPrice * quantity; // Use modified price

        if (quantity != 1)
        {
            Debug.Log($"ShopService: Processing purchase for quantity {quantity} of {itemId} (Modified Price: {itemModifiedPrice} each). Cost: {totalCost}");
        }

        // 4. Validate player has enough coins
        var playerInventory = InventoryService.Instance?.GetPlayerInventory(playerId);
        if (playerInventory == null)
        {
            SendErrorMessage(playerId, "Your inventory could not be accessed.");
            return;
        }
        // Assuming PlayerInventory has a method like GetTotalQuantity or specific coin handling
        // Check Pasted_Text_1754141553131.txt / Pasted_Text_1754141542487.txt for exact method name
        // Let's assume GetTotalQuantity exists based on context clues.
        int playerCoins = playerInventory.GetTotalQuantity(_coinItemId);
        if (playerCoins < totalCost)
        {
            Debug.LogWarning($"ShopService: Player {playerId} cannot afford {quantity}x {itemId} (Cost: {totalCost}, Has: {playerCoins}).");
            SendErrorMessage(playerId, "You do not have enough coins.");
            return;
        }

        // --- Transaction ---
        bool transactionSuccess = true;
        string transactionError = "";

        // a. Transfer coins from player to shop
        if (!InventoryService.Instance.TransferItem(playerId, SHOP_NPC_ID, _coinItemId, totalCost))
        {
            transactionSuccess = false;
            transactionError = "Failed to transfer coins from your inventory.";
            Debug.LogError($"ShopService: Failed to transfer {totalCost} coins from {playerId} to shop.");
        }

        // b. Transfer item from shop to player (only if coin transfer succeeded)
        if (transactionSuccess)
        {
            if (!InventoryService.Instance.AddItem(playerId, itemDef, quantity)) // AddItem takes ItemDef
            {
                transactionSuccess = false;
                transactionError = "Failed to add item to your inventory.";
                Debug.LogError($"ShopService: Failed to add {quantity}x {itemId} to {playerId}'s inventory. Attempting coin rollback...");

                // Attempt to rollback coin transfer
                bool rollbackSuccess = InventoryService.Instance.TransferItem(SHOP_NPC_ID, playerId, _coinItemId, totalCost);
                if (!rollbackSuccess)
                {
                    Debug.LogError($"ShopService: CRITICAL - Rollback of {totalCost} coins to {playerId} failed!");
                    transactionError += " CRITICAL FAILURE: Coin rollback failed!";
                }
                else
                {
                    Debug.Log($"ShopService: Rollback of {totalCost} coins to {playerId} successful.");
                }
            }
        }

        // --- Post-Transaction & Notification ---
        if (transactionSuccess)
        {
            // Update shop stock: Remove the item from its slot
            bool stockUpdated = _shopInventory.RemoveItem(itemSlotIndex);
            if (!stockUpdated)
            {
                Debug.LogWarning($"ShopService: Item {itemId} was not found in slot {itemSlotIndex} during stock update. This might indicate a sync issue.");
                // The purchase still succeeded for the player, but the shop UI might be stale.
            }
            else
            {
                Debug.Log($"ShopService: Successfully updated shop stock. Removed {itemId} from slot {itemSlotIndex}.");
            }

            // Notify player of success
            var successData = new Dictionary<string, object>
            {
                ["type"] = "shop_result",
                ["action"] = "buy",
                ["success"] = true,
                ["item_id"] = itemId,
                ["quantity"] = quantity,
                ["total_cost"] = totalCost,
                ["slot_index"] = itemSlotIndex // Inform client/UI which slot was affected
            };
            GameServer.Instance.SendToPlayer(playerId, successData);
            Debug.Log($"ShopService: Player {playerId} successfully bought {quantity}x {itemId} for {totalCost} coins.");

            // Update the server-side UI to reflect the sold item
            // Find the UI element corresponding to the slot and remove/disable it
            // This requires a way to link slotIndex to the instantiated UI element.
            // Option 1: Store references in a list/dictionary in ShopService
            // Option 2: Iterate through _itemsUIParent children and find one tagged/linked to slotIndex
            // For simplicity here, just call UpdateShopUI to rebuild (inefficient but works)
            // A more efficient way would be to find the specific UI element and destroy it.
            UpdateShopUI(); // TODO: Optimize this to only remove the specific item's UI element

        }
        else
        {
            // Notify player of failure
            SendErrorMessage(playerId, $"Purchase failed: {transactionError}");
        }
    }


    /// <summary>
    /// Handles a player's request to sell an item to the shop.
    /// </summary>
    private void HandleSellRequest(string playerId, Dictionary<string, object> message)
    {
        // --- Message Parsing ---
        if (!message.TryGetValue("item_id", out var itemIdObj) || !message.TryGetValue("quantity", out var quantityObj))
        {
            Debug.LogWarning("ShopService: Sell request missing 'item_id' or 'quantity'.");
            SendErrorMessage(playerId, "Invalid sell request: Missing item or quantity.");
            return;
        }

        string itemId = itemIdObj.ToString();
        if (!int.TryParse(quantityObj.ToString(), out int quantity) || quantity <= 0)
        {
            Debug.LogWarning("ShopService: Sell request has invalid 'quantity'.");
            SendErrorMessage(playerId, "Invalid sell request: Quantity must be a positive number.");
            return;
        }

        // --- Validation ---
        // 1. Get ItemDefinition
        ItemDefinition itemDef = InventoryService.Instance?.GetItemDefinition(itemId);
        if (itemDef == null)
        {
            Debug.LogError($"ShopService: Could not find ItemDefinition for {itemId} during sell request.");
            SendErrorMessage(playerId, "Item data not found.");
            return;
        }

        // 2. Validate player has the item(s) to sell
        var playerInventory = InventoryService.Instance?.GetPlayerInventory(playerId);
        if (playerInventory == null)
        {
            SendErrorMessage(playerId, "Your inventory could not be accessed.");
            return;
        }
        int playerItemCount = playerInventory.GetTotalQuantity(itemId);
        if (playerItemCount < quantity)
        {
            Debug.LogWarning($"ShopService: Player {playerId} tried to sell {quantity}x {itemId}, but only has {playerItemCount}.");
            SendErrorMessage(playerId, "You do not have enough of that item.");
            return;
        }

        // 3. Calculate value (using item's base price, or modified price if you prefer)
        // For selling, let's use base price for simplicity, or perhaps a fixed % less than buy price.
        // Example: Sell for 50% of base price.
        int sellPricePerItem = Mathf.Max(1, itemDef.basePrice / 2); // Ensure at least 1 coin per item
        int totalValue = sellPricePerItem * quantity;

        // 4. Validate shop has enough coins
        // Get the shop's coin inventory (the one we created/managed)


        if (_npcPlayerInv == null)
        {
            Debug.LogWarning($"ShopService: _npcPlayerInv not set. Trying to set it now");

            _npcPlayerInv = InventoryService.Instance?.GetPlayerInventory(SHOP_NPC_ID);

            if (_npcPlayerInv == null)
            {
                Debug.LogError("ShopService: SHOP_NPC inventory could not be accessed for selling.");
                SendErrorMessage(playerId, "Shop cannot process sale right now.");
                return;
            }
            
        }
        int shopCoins = _npcPlayerInv.GetTotalQuantity(_coinItemId);
        if (shopCoins < totalValue)
        {
            Debug.LogWarning($"ShopService: Shop cannot afford to buy {quantity}x {itemId} (Cost: {totalValue}, Has: {shopCoins}).");
            SendErrorMessage(playerId, "The shop cannot afford that transaction.");
            return;
        }

        // --- Transaction (Mirroring TradingService.ExecuteTrade logic) ---
        bool transactionSuccess = true;
        string transactionError = "";

        // a. Transfer item from player to shop (using SHOP_NPC_ID)
        if (!InventoryService.Instance.TransferItem(playerId, SHOP_NPC_ID, itemId, quantity))
        {
            transactionSuccess = false;
            transactionError = "Failed to transfer item from your inventory to the shop.";
            Debug.LogError($"ShopService: Failed to transfer {quantity}x {itemId} from {playerId} to shop.");
        }

        // b. Transfer coins from shop to player (only if item transfer succeeded)
        if (transactionSuccess)
        {
            if (!InventoryService.Instance.TransferItem(SHOP_NPC_ID, playerId, _coinItemId, totalValue))
            {
                transactionSuccess = false;
                transactionError = "Failed to transfer coins from the shop to your inventory.";
                Debug.LogError($"ShopService: Failed to transfer {totalValue} coins from shop to {playerId}. Attempting item rollback...");

                // Attempt to rollback item transfer (like TradingService might do in a full implementation)
                bool rollbackSuccess = InventoryService.Instance.TransferItem(SHOP_NPC_ID, playerId, itemId, quantity);
                if (!rollbackSuccess)
                {
                    Debug.LogError($"ShopService: CRITICAL - Rollback of {quantity}x {itemId} to {playerId} failed!");
                    transactionError += " CRITICAL FAILURE: Item rollback failed!";
                }
                else
                {
                    Debug.Log($"ShopService: Rollback of {quantity}x {itemId} to {playerId} successful.");
                }
            }
        }

        // --- Post-Transaction & Notification ---
        if (transactionSuccess)
        {
            // Notify player of success
            var successData = new Dictionary<string, object>
            {
                ["type"] = "shop_result",
                ["action"] = "sell", // Indicate this was a sell action
                ["success"] = true,
                ["item_id"] = itemId,
                ["quantity"] = quantity,
                ["total_value"] = totalValue // Send the amount received
                // Optionally, send updated shop coin count if needed by client/UI
            };
            GameServer.Instance.SendToPlayer(playerId, successData);
            Debug.Log($"ShopService: Player {playerId} successfully sold {quantity}x {itemId} for {totalValue} coins.");

            // Optional: Update the shop's displayed coin count if it's shown somewhere
            // This would require sending an update or rebroadcasting shop data
            // BroadcastShopData(); // Or a more targeted update

            // updating shopMoney as shop lost some in the sell
            _shopMoney = _npcPlayerInv.GetTotalQuantity(_coinItemId);
            Debug.Log($"ShopService: Updated _shopMoney to {_shopMoney}.");


            //updating UI
            UpdateShopUI();

            // broadcasting because shopMoney changed
            BroadcastShopData();

        }
        else
        {
            // Notify player of failure
            SendErrorMessage(playerId, $"Sale failed: {transactionError}");
        }
    }



    // Sends the current shop state to a specific player
    private void SendShopDataToPlayer(string playerId)
    {
        // Prepare item data for the client
        var itemsForClient = new List<object>();
        var uiItemData = _shopInventory.GetUIItemData(); // List<(ItemDef, modifiedPrice, slotIndex)>

        // NEW loop to send modified price:
        foreach (var (itemDef, modifiedPrice, slotIndex) in uiItemData)
        {
            if (itemDef != null)
            {
                itemsForClient.Add(new Dictionary<string, object>
                {
                    ["id"] = itemDef.itemId,
                    ["name"] = itemDef.displayName ?? itemDef.itemId,
                    ["icon"] = $"images/icons/{itemDef.itemId}",
                    ["slot_index"] = slotIndex,
                    ["price"] = modifiedPrice // Send the modified price
                    // Quantity is implicitly 1 for shop items
                });
            }
        }

        // Get player's coin count
        int playerCoins = 0;
        if (InventoryService.Instance != null)
        {
            var playerInventory = InventoryService.Instance.GetPlayerInventory(playerId);
            if (playerInventory != null)
            {
                // Use the correct method name from InventoryService
                playerCoins = playerInventory.GetTotalQuantity(_coinItemId);
            }
        }

        // Send data to the player
        var shopData = new Dictionary<string, object>
        {
            ["type"] = "shop_data",
            ["items"] = itemsForClient,
            ["shop_coins"] = _shopMoney
        };

        GameServer.Instance.SendToPlayer(playerId, shopData);
        Debug.Log($"ShopService: Sent shop data to player {playerId} ({itemsForClient.Count} items, {playerCoins} coins).");
    }

    public void BroadcastShopData()
    {
        // Prepare item data for the client
        var itemsForClient = new List<object>();
        var uiItemData = _shopInventory.GetUIItemData(); // List<(ItemDef, modifiedPrice, slotIndex)>

        // NEW loop to send modified price:
        foreach (var (itemDef, modifiedPrice, slotIndex) in uiItemData)
        {
            if (itemDef != null)
            {
                itemsForClient.Add(new Dictionary<string, object>
                {
                    ["id"] = itemDef.itemId,
                    ["name"] = itemDef.displayName ?? itemDef.itemId,
                    ["icon"] = $"images/icons/{itemDef.itemId}",
                    ["slot_index"] = slotIndex,
                    ["price"] = modifiedPrice // Send the modified price
                    // Quantity is implicitly 1 for shop items
                });
            }
        }

        var shopData = new Dictionary<string, object>
        {
            ["type"] = "shop_data",
            ["items"] = itemsForClient,
            ["shop_coins"] = _shopMoney
        };

        GameServer.Instance.Broadcast(shopData);
        Debug.Log($"ShopService: Broadcast shop items");
    }
    
    public List<(string, string, int, int)> GetShopDataForDebug()
    {
        var data = new List<(string, string, int, int)>();
        var uiData = _shopInventory.GetUIItemData();

        foreach (var (itemDef, price, slotIndex) in uiData)
        {
            if (itemDef != null)
            {
                data.Add((itemDef.itemId, itemDef.displayName, price, slotIndex));
            }
        }

        return data;
    }


    // Sends an error message to a player
    private void SendErrorMessage(string playerId, string message)
    {
        var errorData = new Dictionary<string, object>
        {
            ["type"] = "shop_error",
            ["message"] = message
        };
        GameServer.Instance.SendToPlayer(playerId, errorData);

        // --- NEW: Notify Editor Window (if present) ---
#if UNITY_EDITOR
        //ShopDebugWindow.LogShopMessage($"[ShopService ERROR to {playerId}] {message}");
#endif
        // --- END NEW ---
    }

    #endregion
}