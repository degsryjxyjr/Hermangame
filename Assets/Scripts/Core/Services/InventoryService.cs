using UnityEngine;
using System.Collections.Generic;
using System.Linq; // If needed
using System;

public class InventoryService : MonoBehaviour
{
    public static InventoryService Instance { get; private set; }

    // Key: Player Network ID, Value: Their inventory state
    private Dictionary<string, PlayerInventory> _playerInventories = new Dictionary<string, PlayerInventory>();

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

    // --- Item Usage ---
    /// <summary>
    /// Uses an item from a player's inventory (e.g., consumes a potion or toggles equipment).
    /// </summary>
    public bool UseItem(string playerId, string itemId)
    {
        if (!_playerInventories.TryGetValue(playerId, out var inventory))
        {
            Debug.LogWarning($"Cannot use item, inventory not found for player: {playerId}");
            return false;
        }

        // Check bag first
        var bagSlot = inventory.GetBagSlot(itemId);
        if (bagSlot == null || bagSlot.ItemDef == null)
        {
            // If not in bag, check if it's an equipped item being unequipped
            // The EquipItem logic handles toggling, so we can try to "equip" it again,
            // which will unequip it if it's already equipped.
            // However, UseItem is generally for consumables or equipping from bag.
            // Let's assume UseItem for equipment means Equip/Unequip from bag.
            // If the item isn't in the bag, it can't be used this way.
            Debug.Log($"Cannot use item, slot or ItemDef not found for ID: {itemId} in player {playerId}'s bag.");
            return false;
        }

        var player = PlayerManager.Instance.GetPlayer(playerId);
        if (player == null)
        {
            Debug.LogWarning($"Cannot use item, player connection not found for ID: {playerId}");
            return false;
        }

        bool itemUsed = false;
        switch (bagSlot.ItemDef.itemType)
        {
            case ItemDefinition.ItemType.Consumable:
                Debug.Log($"Using consumable: {bagSlot.ItemDef.displayName} (ID: {bagSlot.itemId}) for player {player.LobbyData.Name}");

                bool itemConsumed = false;
                if (bagSlot.ItemDef.linkedAbility != null)
                {
                    Debug.Log($"Consumable {bagSlot.ItemDef.displayName} has a linked ability: {bagSlot.ItemDef.linkedAbility.abilityName}. Executing via CombatService.");

                    bool abilityExecuted = CombatService.Instance.ExecuteAbilityFromItem(
                        casterPlayerId: playerId,
                        targetPlayerId: playerId, // Self-targeting for potions
                        abilityDefinition: bagSlot.ItemDef.linkedAbility
                    );

                    if (abilityExecuted)
                    {
                        itemConsumed = inventory.RemoveItemFromBag(itemId, 1);
                        if (itemConsumed)
                        {
                            Debug.Log($"Successfully consumed item {bagSlot.ItemDef.displayName} after linked ability execution.");
                        }
                        else
                        {
                            Debug.LogWarning($"Failed to remove consumed item {bagSlot.ItemDef.displayName} from inventory after ability use.");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"Linked ability {bagSlot.ItemDef.linkedAbility.abilityName} failed to execute for item {bagSlot.ItemDef.displayName}. Item not consumed.");
                    }
                }
                else
                {
                    Debug.Log($"Consumable {bagSlot.ItemDef.displayName} has no linked ability. Applying direct stat modifiers (fallback).");

                    player.CurrentHealth = Mathf.Clamp(
                        player.CurrentHealth + bagSlot.ItemDef.healthModifier,
                        0,
                        player.MaxHealth
                    );
                    player.Attack += bagSlot.ItemDef.attackModifier;
                    player.Defense += bagSlot.ItemDef.defenseModifier;
                    player.Magic += bagSlot.ItemDef.magicModifier;

                    GameServer.Instance.SendToPlayer(player.NetworkId, new
                    {
                        type = "stats_update",
                        currentHealth = player.CurrentHealth,
                        maxHealth = player.MaxHealth,
                        attack = player.Attack,
                        defense = player.Defense,
                        magic = player.Magic
                    });

                    itemConsumed = inventory.RemoveItemFromBag(itemId, 1);
                    if (itemConsumed)
                    {
                        Debug.Log($"Consumed item {bagSlot.ItemDef.displayName} using direct modifiers.");
                    }
                    else
                    {
                        Debug.LogWarning($"Failed to remove consumed item {bagSlot.ItemDef.displayName} from inventory after direct modifier use.");
                    }
                }
                itemUsed = itemConsumed;
                break;

            case ItemDefinition.ItemType.Equipment:
                Debug.Log($"Toggling equipment: {bagSlot.ItemDef.displayName} (ID: {bagSlot.itemId}) for player {player.LobbyData.Name}");
                // Delegate to the inventory's equip logic
                itemUsed = inventory.EquipItem(itemId);
                if (itemUsed)
                {
                    // The EquipItem method handles the internal state change (isEquipped flag, movement between lists/dict)
                    // TODO: Potentially apply/remove stat bonuses from equipment here
                    // This would involve reading the item's modifiers and adjusting player stats
                    // ApplyEquipmentBonuses(player, bagSlot.ItemDef, bagSlot.isEquipped); // isEquipped is now toggled
                }
                else
                {
                    Debug.LogWarning($"Failed to toggle equipment for item {bagSlot.ItemDef.displayName}.");
                }
                break;
            default:
                Debug.Log($"No use behavior defined for item type: {bagSlot.ItemDef.itemType} (Item: {bagSlot.ItemDef.displayName})");
                itemUsed = false;
                break;
        }
        return itemUsed;
    }

    // --- Equipment Handling ---
    // The core logic is now in PlayerInventory.EquipItem. InventoryService delegates to it.
    // You might expose a direct Equip method if needed, but UseItem can handle it for now.

    // --- Message Handling ---
    public void HandleMessage(string sessionId, Dictionary<string, object> msg)
    {
        if (!_playerInventories.ContainsKey(sessionId))
        {
            Debug.LogWarning($"Inventory message received for unknown player/session: {sessionId}");
            GameServer.Instance.SendToPlayer(sessionId, new { type = "error", message = "Player inventory not found." });
            return;
        }

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
                // Add cases for other actions like "equip" (if separate from "use"), "discard", "drop" etc.
                case "equip":
                    if (msg.TryGetValue("itemId", out var equipItemIdObj))
                    {
                        string equipItemId = equipItemIdObj.ToString();
                        // Use the existing logic or a new dedicated method
                        bool equipSuccess = _playerInventories[sessionId].EquipItem(equipItemId);
                        if (equipSuccess) SendInventoryUpdate(sessionId);
                        // ... handle success/failure
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

    // --- Helper Methods (Optional) ---
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