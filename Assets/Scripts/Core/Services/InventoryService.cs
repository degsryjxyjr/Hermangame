using UnityEngine;
using System.Collections.Generic;
using System.Linq; // If needed
using System;
public class InventoryService : MonoBehaviour
{
    public static InventoryService Instance { get; private set; }

    // Key: Player Network ID, Value: Their inventory state
    private Dictionary<string, PlayerInventory> _playerInventories = new Dictionary<string, PlayerInventory>();

    //Chace all items
    public ItemDefinition[] allItems;


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"Duplicate InventoryService instance detected! Destroying {gameObject.name}");
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        allItems = Resources.LoadAll<ItemDefinition>("Items");
        Debug.Log("InventoryService initialized and set as singleton");
    }

    // --- Initialization ---
    /// <summary>
    /// Initializes a player's inventory with starting items.
    /// </summary>
    public void InitializeInventory(string playerId, List<ItemDefinition> startingItems)
    {
        var inventory = new PlayerInventory();
        inventory.InitializeWithItems(startingItems);
        _playerInventories[playerId] = inventory;
        Debug.Log($"Initialized inventory for player {playerId} with {startingItems.Count} starting items.");
    }
    


    /// <summary>
    /// Gets an ItemDefinition by its ID from the cached items.
    /// </summary>
    /// <param name="itemId">The ID of the item to find.</param>
    /// <returns>The ItemDefinition if found, null otherwise.</returns>
    public ItemDefinition GetItemDefinition(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
        {
            Debug.LogWarning("GetItemDefinition: itemId is null or empty");
            return null;
        }

        // Search through all cached items
        foreach (var itemDef in allItems)
        {
            if (itemDef != null && itemDef.itemId == itemId)
            {
                return itemDef;
            }
        }

        Debug.LogWarning($"GetItemDefinition: Item with ID '{itemId}' not found");
        return null;
    }



    // --- Item Management ---
    /// <summary>
    /// Adds an item to a player's bag inventory.
    /// </summary>
    public bool AddItem(string playerId, ItemDefinition item, int quantity = 1)
    {
        if (!_playerInventories.TryGetValue(playerId, out var inventory))
        {
            Debug.LogWarning($"Cannot add item, inventory not found for player: {playerId}");
            return false;
        }

        bool success = true;
        for (int i = 0; i < quantity; i++)
        {
            if (!inventory.AddItemToBag(item))
            {
                success = false;
                Debug.LogWarning($"Failed to add item {item?.displayName ?? "Unknown"} to {playerId}'s inventory. Inventory full or item null?");
                // Depending on design, stop adding or continue?
                break; // Stop on first failure for this example
            }
        }

        if (success)
        {
            // TODO: Notify client about inventory change if needed immediately
            // Or rely on the caller to send updates
        }
        return success;
    }

    /// <summary>
    /// Transfers a specified quantity of an item from one player's inventory to another's.
    /// This is an atomic operation: either both the removal and addition succeed, or neither does.
    /// </summary>
    /// <param name="fromPlayerId">The NetworkId of the player giving the item.</param>
    /// <param name="toPlayerId">The NetworkId of the player receiving the item.</param>
    /// <param name="itemId">The ID of the item to transfer.</param>
    /// <param name="quantity">The quantity to transfer.</param>
    /// <returns>True if the transfer was successful, false otherwise.</returns>
    public bool TransferItem(string fromPlayerId, string toPlayerId, string itemId, int quantity)
    {
        if (string.IsNullOrEmpty(fromPlayerId) || string.IsNullOrEmpty(toPlayerId) || string.IsNullOrEmpty(itemId) || quantity <= 0)
        {
            Debug.LogWarning("InventoryService.TransferItem: Invalid arguments.");
            return false;
        }

        if (fromPlayerId == toPlayerId)
        {
            Debug.LogWarning("InventoryService.TransferItem: Cannot transfer item to the same player.");
            return false;
        }

        if (!_playerInventories.TryGetValue(fromPlayerId, out PlayerInventory fromInventory))
        {
            Debug.LogWarning($"InventoryService.TransferItem: Source inventory for player {fromPlayerId} not found.");
            return false;
        }

        if (!_playerInventories.TryGetValue(toPlayerId, out PlayerInventory toInventory))
        {
            Debug.LogWarning($"InventoryService.TransferItem: Target inventory for player {toPlayerId} not found.");
            return false;
        }

        // --- Validation: Check source has enough ---
        // Find the item slot in the source player's bag
        var sourceItemSlot = fromInventory.BagItems.Find(slot => slot.itemId == itemId);
        if (sourceItemSlot == null || sourceItemSlot.quantity < quantity)
        {
            Debug.LogWarning($"InventoryService.TransferItem: Player {fromPlayerId} does not have enough {itemId} to transfer ({sourceItemSlot?.quantity ?? 0} < {quantity}).");
            return false;
        }

        // --- Validation: Check target has space (optional, depends on your inventory system) ---
        // If your inventory has a hard limit, check it here.
        // For simplicity, we assume AddItem handles space checks.
        // You might want to pre-check if adding 'quantity' of 'itemId' would fit in 'toInventory'.

        // --- Perform Transfer ---
        Debug.Log($"InventoryService.TransferItem: Transferring {quantity}x {itemId} from {fromPlayerId} to {toPlayerId}.");

        // 1. Remove from source
        bool removed = RemoveItem(fromPlayerId, itemId, quantity);
        if (!removed)
        {
            // This should ideally not happen if validation passed, but check anyway
            Debug.LogError($"InventoryService.TransferItem: Failed to remove {quantity}x {itemId} from {fromPlayerId} during transfer.");
            return false;
        }

        // 2. Add to target
        // converting itemId to ItemDefention for AddItem function
        bool added = AddItem(toPlayerId, GetItemDefinition(itemId) , quantity);
        if (!added)
        {
            // If adding fails, we should ideally roll back the removal.
            // This is a simple implementation. A more robust one might use transactions/try-add patterns.
            Debug.LogError($"InventoryService.TransferItem: Failed to add {quantity}x {itemId} to {toPlayerId}. Attempting rollback...");
            // Rollback: Try to add the item back to the source
            bool rollbackSuccess = AddItem(fromPlayerId, GetItemDefinition(itemId), quantity);
            if (!rollbackSuccess)
            {
                Debug.LogError($"InventoryService.TransferItem: CRITICAL FAILURE - Rollback failed! {quantity}x {itemId} lost during transfer from {fromPlayerId}.");
                // In a real game, you'd have serious error handling/recovery here.
            }
            else
            {
                Debug.Log($"InventoryService.TransferItem: Rollback successful. {quantity}x {itemId} returned to {fromPlayerId}.");
            }
            return false;
        }

        // --- Transfer Successful ---
        Debug.Log($"InventoryService.TransferItem: Transfer of {quantity}x {itemId} from {fromPlayerId} to {toPlayerId} completed.");
        // Optionally send inventory updates to both players here, or let the caller do it
        // SendInventoryUpdate(fromPlayerId);
        // SendInventoryUpdate(toPlayerId);
        return true;
    }








    // --- Item Usage ---
    /// <summary>
    /// Uses an item from a player's inventory (e.g., consumes a potion or toggles equipment).
    /// This is the primary method for interacting with items, handling both bag and equipped items.
    /// </summary>
    /// <param name="playerId">The Network ID of the player using the item.</param>
    /// <param name="itemId">The ID of the item to use.</param>
    /// <returns>True if the item use process was initiated successfully, false otherwise.</returns>
    public bool UseItem(string playerId, string itemId)
    {
        // 1. Validate Player Inventory Exists
        if (!_playerInventories.TryGetValue(playerId, out var inventory))
        {
            Debug.LogWarning($"Cannot use item, inventory not found for player: {playerId}");
            return false;
        }


        // 2. FIND the InventorySlot - Use PlayerInventory helpers for cleaner code
        // Prioritize finding the item in equipped slots first to handle unequipping correctly
        // when duplicates exist in bag and equipped.
        InventorySlot itemSlotToUse = null;



        // a. First, try to find the item in the Equipped Items using the new helper
        itemSlotToUse = inventory.GetEquippedSlot(itemId);
        if (itemSlotToUse != null)
        {
            Debug.Log($"InventoryService.UseItem: Found item ID '{itemId}' in equipped slots. Prioritizing this instance.");
        }

        // b. If NOT found in equipped slots, try to find it in the Bag using the existing helper
        if (itemSlotToUse == null)
        {
            itemSlotToUse = inventory.GetBagSlot(itemId); // <-- Use existing helper method
            if (itemSlotToUse != null)
            {
                Debug.Log($"InventoryService.UseItem: Found item ID '{itemId}' in the bag.");
            }
        }


        // 3. Validate Item Found and Has Definition
        if (itemSlotToUse == null || itemSlotToUse.ItemDef == null)
        {
            // Provide a more informative log message
            Debug.Log($"Cannot use item, slot or ItemDef not found for ID: {itemId} in player {playerId}'s bag or equipped items.");
            return false;
        }

        // 4. Get Player Connection for Stat/Effect Modifications
        var player = PlayerManager.Instance.GetPlayer(playerId);
        if (player == null)
        {
            Debug.LogWarning($"Cannot use item, player connection not found for ID: {playerId}");
            return false;
        }

        // 5. Handle Item Type Logic Based on Found Slot
        bool itemUsed = false;
        switch (itemSlotToUse.ItemDef.itemType)
        {
            case ItemDefinition.ItemType.Consumable:
                Debug.Log($"Using consumable: {itemSlotToUse.ItemDef.displayName} (ID: {itemSlotToUse.itemId}) for player {player.LobbyData.Name}");

                bool itemConsumed = false;
                // --- Check for Linked Abilities  ---
                if (itemSlotToUse.ItemDef.linkedAbilities != null && itemSlotToUse.ItemDef.linkedAbilities.Count > 0)
                {
                    // --- Execute the FIRST linked ability ---
                    //  Get the first ability from the list
                    AbilityDefinition primaryLinkedAbility = itemSlotToUse.ItemDef.linkedAbilities[0];

                    // Add a null check for safety
                    if (primaryLinkedAbility != null)
                    {

                        Debug.Log($"Consumable {itemSlotToUse.ItemDef.displayName} has a linked ability: {primaryLinkedAbility.abilityName}. Executing via AbilityExecutionService.");

                        // Get the caster PlayerConnection instance
                        var casterConnection = player; // 'player' is the PlayerConnection instance from earlier in UseItem

                        // Determine the target. For consumables, it's usually the user.
                        // Get the target IDamageable instance. PlayerConnection implements IDamageable.
                        IDamageable targetConnection = player; // Self-targeting

                        ////As inventorySerivce handles item use we need to tell what the useContext is.
                        // We fetch the game state from gameStateManager and pass it to abilityExecutionService
                        var useContext = GameStateManager.Instance.GetCurrentGameState() == GameStateManager.GameState.Combat
                            ? AbilityExecutionService.AbilityContext.InCombat
                            : AbilityExecutionService.AbilityContext.OutOfCombat;

                        // Call the correct method on the AbilityExecutionService singleton
                        bool abilityExecuted = AbilityExecutionService.Instance.ExecuteAbilityFromItem(
                            caster: casterConnection,
                            target: targetConnection,
                            abilityDefinition: primaryLinkedAbility, // Pass the FIRST AbilityDefinition from the list
                            context: useContext
                        );
                        // --- END CORRECTED CALL ---

                        if (abilityExecuted)
                        {
                            // Only consume the item if the ability executed successfully
                            itemConsumed = inventory.RemoveItemFromBag(itemId, 1);
                            if (itemConsumed)
                            {
                                Debug.Log($"Successfully consumed item {itemSlotToUse.ItemDef.displayName} after linked ability execution.");
                            }
                            else
                            {
                                Debug.LogWarning($"Failed to remove consumed item {itemSlotToUse.ItemDef.displayName} from inventory after ability use.");
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"Linked ability {primaryLinkedAbility.abilityName} failed to execute for item {itemSlotToUse.ItemDef.displayName}. Item not consumed.");
                            itemConsumed = false; // Indicate failure
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"Consumable {itemSlotToUse.ItemDef.displayName} has a null primary linked ability in its list.");
                        // Handle this case (e.g., fallback to direct stat mods, or treat as failed use)
                        itemConsumed = false;
                    }
                }
                else
                {
                    // --- Fallback: Direct Stat Modification (if no linked abilities) ---
                    Debug.Log($"Consumable {itemSlotToUse.ItemDef.displayName} has no linked abilities. Applying direct stat modifiers (fallback).");

                    // Apply stats modifiers directly to PlayerConnection (using the new direct properties)
                    player.CurrentHealth = Mathf.Clamp(
                        player.CurrentHealth + itemSlotToUse.ItemDef.healthModifier,
                        0,
                        player.MaxHealth
                    );
                    player.Attack += itemSlotToUse.ItemDef.attackModifier;
                    player.Defense += itemSlotToUse.ItemDef.defenseModifier;
                    player.Magic += itemSlotToUse.ItemDef.magicModifier;

                    // Send stats update to client
                    GameServer.Instance.SendToPlayer(player.NetworkId, new
                    {
                        type = "stats_update",
                        currentHealth = player.CurrentHealth,
                        maxHealth = player.MaxHealth,
                        attack = player.Attack,
                        defense = player.Defense,
                        magic = player.Magic
                    });

                    // Consume the item
                    itemConsumed = inventory.RemoveItemFromBag(itemId, 1);
                    if (itemConsumed)
                    {
                        Debug.Log($"Consumed item {itemSlotToUse.ItemDef.displayName} using direct modifiers.");
                    }
                    else
                    {
                        Debug.LogWarning($"Failed to remove consumed item {itemSlotToUse.ItemDef.displayName} from inventory after direct modifier use.");
                    }
                }
                // Return true if the item use process (including consumption attempt) was initiated
                return itemConsumed; // Simplified success check
            // --- End Consumable Case ---

            case ItemDefinition.ItemType.Equipment:
                Debug.Log($"Toggling equipment: {itemSlotToUse.ItemDef.displayName} (ID: {itemSlotToUse.itemId}) for player {player.LobbyData.Name}");

                // --- KEY CHANGE 1: Declare variable for the unequipped item ---
                InventorySlot unequippedSlot = null;
                // --- END KEY CHANGE 1 ---

                // 6a. Delegate to the PlayerInventory's equip logic to handle state changes
                // This handles moving the item instance between Bag/Equipped dict and toggling isEquipped flag.
                // --- KEY CHANGE 2: Pass the out parameter ---
                itemUsed = inventory.EquipItem(itemId, out unequippedSlot);
                // --- END KEY CHANGE 2 ---

                if (itemUsed)
                {
                    // --- KEY CHANGE 3: Handle the unequipped item effects FIRST ---
                    // If EquipItem indicated an item was unequipped, remove its effects now.
                    if (unequippedSlot != null && unequippedSlot.ItemDef != null)
                    {
                        player.OnUnequipItem(unequippedSlot.ItemDef);
                        Debug.Log($"Item {unequippedSlot.ItemDef.displayName} was automatically unequipped (replaced) and its effects were removed.");
                    }
                    // --- END KEY CHANGE 3 ---

                    // 6b. Apply or Remove Effects based on the NEW state of the item
                    // Check the *new* state of the item after EquipItem has processed it.
                    // itemSlotToUse now refers to the same InventorySlot instance, but its properties
                    // (like isEquipped and its location in BagItems/EquippedItems) have been updated.
                    if (itemSlotToUse.isEquipped) // isEquipped is now TRUE -> Item was just EQUIPPED
                    {
                        // --- Apply Equipment Effects ---
                        player.OnEquipItem(itemSlotToUse.ItemDef);
                        Debug.Log($"Item {itemSlotToUse.ItemDef.displayName} was equipped and its effects were applied.");
                    }
                    else // isEquipped is now FALSE -> Item was just UNEQUIPPED
                    {
                        // --- Remove Equipment Effects ---
                        player.OnUnequipItem(itemSlotToUse.ItemDef);
                        Debug.Log($"Item {itemSlotToUse.ItemDef.displayName} was unequipped and its effects were removed.");
                    }
                    // Note: PlayerConnection methods are responsible for sending stats_update if needed.
                }
                else
                {
                    Debug.LogWarning($"Failed to toggle equipment state for item {itemSlotToUse.ItemDef.displayName}.");
                }
                break;

            default:
                Debug.Log($"No use behavior defined for item type: {itemSlotToUse.ItemDef.itemType} (Item: {itemSlotToUse.ItemDef.displayName})");
                itemUsed = false;
                break;
        }
        return itemUsed; // Return whether the item use process was initiated
    }

    // --- Message Handling ---
    public void HandleMessage(string sessionId, Dictionary<string, object> msg)
    {
        // 1. Validate Player Has Inventory
        if (!_playerInventories.ContainsKey(sessionId))
        {
            Debug.LogWarning($"Inventory message received for unknown player/session: {sessionId}");
            GameServer.Instance.SendToPlayer(sessionId, new { type = "error", message = "Player inventory not found." });
            return;
        }

        // 2. Parse Action
        if (msg.TryGetValue("action", out var actionObj))
        {
            string action = actionObj.ToString();
            switch (action)
            {
                case "get":
                    Debug.Log($"Player {sessionId} requested inventory data.");
                    SendInventoryUpdate(sessionId);
                    break;
                case "use":
                    // 3a. Handle "use" action (for consumables and equipping/unequipping)
                    if (msg.TryGetValue("itemId", out var itemIdObj))
                    {
                        string itemId = itemIdObj.ToString();
                        Debug.Log($"Player {sessionId} attempting to use item ID: {itemId}");
                        bool success = UseItem(sessionId, itemId);
                        if (success)
                        {
                            // Send updated inventory state after successful use
                            SendInventoryUpdate(sessionId);
                            // TODO: Potentially send a message about the item's specific effect to the client/UI
                        }
                        else
                        {
                            Debug.Log($"Failed to use item {itemId} for player {sessionId}");
                            GameServer.Instance.SendToPlayer(sessionId, new { type = "error", message = $"Failed to use item {itemId}." });
                        }

                    }
                    else
                    {
                        Debug.LogWarning("Inventory 'use' action missing 'itemId'");
                        GameServer.Instance.SendToPlayer(sessionId, new { type = "error", message = "Missing itemId for use action." });
                    }
                    break;

                default:
                    Debug.LogWarning($"Unknown inventory action: {action}");
                    GameServer.Instance.SendToPlayer(sessionId, new { type = "error", message = $"Unknown inventory action: {action}" });
                    break;
            }
        }
        else
        {
            Debug.LogWarning("Inventory message missing 'action' field");
            GameServer.Instance.SendToPlayer(sessionId, new { type = "error", message = "Inventory message missing 'action' field." });
        }
    }

    // --- Helper Method to Send Data to Client ---
    /// <summary>
    /// Sends the current state of a player's inventory (bag and equipped) to their client.
    /// </summary>
    public void SendInventoryUpdate(string playerId)
    {
        if (!_playerInventories.TryGetValue(playerId, out var inventory))
        {
            Debug.LogWarning($"Cannot send inventory update, inventory not found for player: {playerId}");
            return;
        }

        var itemsToSend = new List<object>();
        var equippedToSend = new Dictionary<string, object>(); // Key: Slot name, Value: Item data

        // --- Prepare Bag Items ---
        foreach (var slot in inventory.GetAllBagItems())
        {
            if (slot.ItemDef != null)
            {
                itemsToSend.Add(new
                {
                    id = slot.itemId,
                    name = slot.ItemDef.displayName,
                    quantity = slot.quantity,
                    icon = slot.ItemDef.icon != null ? $"images/icons/{slot.ItemDef.itemId}" : "images/icons/default-item.jpg",
                    isEquipped = slot.isEquipped // Might be redundant now, but send for client info
                    // Add other bag-specific properties the client needs
                });
            }
        }

        // --- Prepare Equipped Items ---
        foreach (var kvp in inventory.EquippedItems)
        {
            ItemDefinition.EquipmentSlot slotType = kvp.Key;
            InventorySlot equippedSlot = kvp.Value;

            if (equippedSlot != null && equippedSlot.ItemDef != null)
            {
                // Use the enum name as the key for the equipped slot on the client side
                equippedToSend[slotType.ToString()] = new
                {
                    id = equippedSlot.itemId,
                    name = equippedSlot.ItemDef.displayName,
                    // quantity is usually 1 for equipped items, maybe not needed
                    icon = equippedSlot.ItemDef.icon != null ? $"images/icons/{equippedSlot.ItemDef.itemId}" : "images/icons/default-item.jpg",
                    isEquipped = equippedSlot.isEquipped // Should be true
                    // Add other equipped-specific properties the client needs
                };
            }
            else
            {
                // Explicitly send null for empty slots if client expects it
                // equippedToSend[slotType.ToString()] = null;
                // Or just don't add the key, client assumes null/empty
            }
        }

        // Send the message via GameServer
        GameServer.Instance.SendToPlayer(playerId, new
        {
            type = "inventory_update",
            items = itemsToSend, // List of bag items
            equipped = equippedToSend // Dictionary of equipped items by slot
        });
        Debug.Log($"Sent inventory update to player {playerId} ({itemsToSend.Count} bag items, {equippedToSend.Count} equipped items).");
    }

    // --- Helper Methods  ---
    /// <summary>
    /// Gets a player's inventory by their ID.
    /// </summary>
    /// <param name="playerId">The ID of the player whose inventory to retrieve.</param>
    /// <returns>The PlayerInventory instance if found, null otherwise.</returns>
    public PlayerInventory GetPlayerInventory(string playerId)
    {
        if (_playerInventories.TryGetValue(playerId, out PlayerInventory inventory))
        {
            return inventory;
        }
        Debug.LogWarning($"Inventory not found for player ID: {playerId}");
        return null;
    }



    /// <summary>
    /// Finds the slot containing an item with the given ID (looks in bag).
    /// </summary>
    public InventorySlot GetBagSlot(string playerId, string itemId)
    {
        if (_playerInventories.TryGetValue(playerId, out var inventory))
        {
            return inventory.GetBagSlot(itemId);
        }
        return null;
    }

    /// <summary>
    /// Removes items by ID (e.g., for selling/discarding from bag).
    /// </summary>
    public bool RemoveItem(string playerId, string itemId, int quantity = 1)
    {
        if (_playerInventories.TryGetValue(playerId, out var inventory))
        {
            return inventory.RemoveItemFromBag(itemId, quantity);
        }
        return false;
    }

#if UNITY_EDITOR
    /// <summary>
    /// (Editor Debugging Only) Attempts to get the PlayerInventory instance for a player.
    /// </summary>
    /// <returns>True if the player's inventory was found, false otherwise.</returns>
    public bool TryGetPlayerInventory(string playerId, out PlayerInventory playerInv)
    {
        return _playerInventories.TryGetValue(playerId, out playerInv);
    }
#endif
}

/// <summary>
/// Represents a single stack of items in the player's inventory or an equipped item.
/// </summary>
[Serializable] // Keep Serializable for Unity inspector and potential saving
public class InventorySlot
{
    // --- Core Data ---
    /// <summary>
    /// Reference to the immutable ScriptableObject definition for this item type.
    /// </summary>
    public ItemDefinition ItemDef;

    /// <summary>
    /// Cached ID from the ItemDefinition for easier and faster lookups.
    /// </summary>
    public string itemId;

    /// <summary>
    /// The number of items in this stack (relevant for stackable items).
    /// </summary>
    public int quantity;

    /// <summary>
    /// Indicates whether this item is currently equipped (if equippable).
    /// Note: For specific equipment slots, the PlayerInventory will track which slot this item occupies.
    /// </summary>
    public bool isEquipped; // This might become less relevant with specific slot tracking, but can be kept for simple checks.

    // --- Constructor ---
    /// <summary>
    /// Creates a new inventory slot based on an ItemDefinition.
    /// </summary>
    /// <param name="itemDef">The definition for the item type this slot represents.</param>
    public InventorySlot(ItemDefinition itemDef)
    {
        if (itemDef == null)
        {
            Debug.LogError("Cannot create InventorySlot with null ItemDefinition.");
            // Consider throwing an exception or handling this state more explicitly
            return;
        }

        this.ItemDef = itemDef;
        this.itemId = itemDef.itemId; // Cache the ID
        // Stackables start at 1, non-stackables also represented by quantity 1 for consistency
        this.quantity = 1;
        this.isEquipped = false; // Starts unequipped
    }

    // --- Optional: Parameterless constructor for Unity serialization ---
    // Unity sometimes needs this for lists/arrays to work correctly in the Inspector.
    // If you encounter issues with lists of InventorySlot in other ScriptableObjects or MonoBehaviours, uncomment this.
    // public InventorySlot() { }
}