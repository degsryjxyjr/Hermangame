// File: Scripts/Editor/PlayerDataDebugWindow.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq; // Make sure this is included

public class PlayerDataDebugWindow : EditorWindow
{
    [MenuItem("Window/Player Data Debugger")]
    public static void ShowWindow()
    {
        GetWindow<PlayerDataDebugWindow>("Player Data");
    }

    private Vector2 scrollPos;
    private bool[] playerFoldouts;
    // Use a single array for foldouts, calculating index as needed
    // Index 0: Player details, Index 1: Inventory (Bag), Index 2: Inventory (Equipped)
    // For N players: Indices 0..N-1 (Player details), N..2N-1 (Bag), 2N..3N-1 (Equipped)
    private bool[] foldouts;

    void OnEnable()
    {
        playerFoldouts = new bool[0];
        foldouts = new bool[0];
    }

    void OnGUI()
    {
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("This window only works in Play Mode.", MessageType.Info);
            return;
        }

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        var playerManager = PlayerManager.Instance;
        var inventoryService = InventoryService.Instance;

        if (playerManager == null)
        {
            EditorGUILayout.HelpBox("PlayerManager not found.", MessageType.Warning);
            EditorGUILayout.EndScrollView();
            return;
        }

        var players = playerManager.GetAllPlayers();
        int playerCount = players.Count;

        // Resize foldout arrays if player count changed
        // Need 3 foldouts per player: Details, Bag, Equipped
        int requiredFoldouts = playerCount * 3;
        if (foldouts.Length != requiredFoldouts)
        {
            System.Array.Resize(ref foldouts, requiredFoldouts);
        }

        for (int i = 0; i < playerCount; i++)
        {
            DrawPlayerData(players[i], i, inventoryService);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawPlayerData(PlayerConnection player, int playerIndex, InventoryService inventoryService)
    {
        EditorGUILayout.BeginVertical("box");
        
        // Calculate foldout indices for this player
        int detailsFoldoutIndex = playerIndex * 3 + 0;
        int bagFoldoutIndex = playerIndex * 3 + 1;
        int equippedFoldoutIndex = playerIndex * 3 + 2;

        foldouts[detailsFoldoutIndex] = EditorGUILayout.Foldout(foldouts[detailsFoldoutIndex], $"Player: {player.LobbyData?.Name ?? "Unknown"} (ID: {player.NetworkId})", true);
        if (foldouts[detailsFoldoutIndex])
        {
            EditorGUI.indentLevel++;

            // Basic Player Info
            EditorGUILayout.LabelField("Session ID:", player.NetworkId);
            EditorGUILayout.LabelField("Reconnect Token:", player.ReconnectToken ?? "None");
            EditorGUILayout.LabelField("Last Activity:", player.LastActivityTime.ToString("F2"));

            // Lobby Data
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Lobby Data", EditorStyles.boldLabel);
            if (player.LobbyData != null)
            {
                EditorGUILayout.LabelField("Name:", player.LobbyData.Name);
                EditorGUILayout.LabelField("Role:", player.LobbyData.Role);
                EditorGUILayout.LabelField("Is Ready:", player.LobbyData.IsReady.ToString());
            }
            else
            {
                EditorGUILayout.LabelField("Lobby Data: None");
            }

            // --- Editable Game Stats ---
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Game Stats (Editable)", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck(); // Start checking for changes

            // Add Total Actions field (editable)
            int newTotalActions = EditorGUILayout.IntField("Total Actions", player.TotalActions);
            EditorGUILayout.LabelField("Actions Remaining", player.ActionsRemaining.ToString());
            
            // Existing stats (keep these)
            int newMaxHealth = EditorGUILayout.IntField("Max Health", player.MaxHealth);
            int newCurrentHealth = EditorGUILayout.IntField("Current Health", player.CurrentHealth);
            int newAttack = EditorGUILayout.IntField("Attack", player.Attack);
            int newDefense = EditorGUILayout.IntField("Defense", player.Defense);
            int newMagic = EditorGUILayout.IntField("Magic", player.Magic);

            if (EditorGUI.EndChangeCheck())
            {
                // Apply changes
                player.TotalActions = Mathf.Max(1, newTotalActions);
                
                // Existing stat changes
                player.MaxHealth = Mathf.Max(1, newMaxHealth);
                player.CurrentHealth = Mathf.Clamp(newCurrentHealth, 0, player.MaxHealth);
                player.Attack = Mathf.Max(0, newAttack);
                player.Defense = Mathf.Max(0, newDefense);
                player.Magic = Mathf.Max(0, newMagic);

                // Send update to client
                player.SendStatsUpdateToClient();
                Debug.Log($"Updated {player.LobbyData?.Name}'s stats via debug window");
                Repaint();
            }
            // Action Buttons
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Action Management(IN COMBAT ONLY!!!!!!)", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Reset Remaining Actions"))
            {
                player.ResetActionBudgetForNewTurn();
                Debug.Log($"Reset actions for {player.LobbyData?.Name}");
                Repaint();
            }
            
            if (GUILayout.Button("+1 Remaining Action"))
            {
                player.ModifyCurrentActionBudget(1);
                Debug.Log($"Added action to {player.LobbyData?.Name}");
                Repaint();
            }
            
            if (GUILayout.Button("-1 Remaining Action"))
            {
                player.ModifyCurrentActionBudget(-1);
                Debug.Log($"Removed action from {player.LobbyData?.Name}");
                Repaint();
            }
            
            EditorGUILayout.EndHorizontal();
            // --- End Editable Game Stats ---

            // --- Updated Section: Inventory Actions ---
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Inventory Actions", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Random Item"))
            {
                AddRandomItemToPlayer(player.NetworkId, inventoryService);
            }
            if (GUILayout.Button("Clear Inventory"))
            {
                ClearPlayerInventory(player.NetworkId, inventoryService);
            }
            EditorGUILayout.EndHorizontal();

            // --- Updated Section: Inventory Display ---
            EditorGUILayout.Space();
#if UNITY_EDITOR // Ensure this code only compiles in the editor
            PlayerInventory playerInventory = null;
            bool hasInventory = inventoryService != null && inventoryService.TryGetPlayerInventory(player.NetworkId, out playerInventory);

            if (hasInventory)
            {
                // Get bag and equipped items
                var bagItems = playerInventory.GetAllBagItems();
                var equippedItems = playerInventory.GetAllEquippedItems(); // Or access EquippedItems dict directly

                // Combined count for display
                int totalItemCount = bagItems.Count + equippedItems.Count;

                EditorGUILayout.LabelField($"Inventory Summary: Total Items: {totalItemCount}, Bag: {bagItems.Count}, Equipped: {equippedItems.Count}, Max Bag Slots: {PlayerInventory.MAX_BAG_SLOTS}");

                // Display Bag Items
                foldouts[bagFoldoutIndex] = EditorGUILayout.Foldout(foldouts[bagFoldoutIndex], $"Bag Items ({bagItems.Count})", true);
                if (foldouts[bagFoldoutIndex])
                {
                    EditorGUI.indentLevel++;
                    if (bagItems.Count > 0)
                    {
                        foreach (var slot in bagItems)
                        {
                            if (slot.ItemDef != null)
                            {
                                EditorGUILayout.BeginHorizontal();
                                
                                // --- Show Item Icon ---
                                if (slot.ItemDef.icon != null)
                                {
                                    // Create a small texture from the sprite for display
                                    Rect iconRect = GUILayoutUtility.GetRect(20, 20);
                                    EditorGUI.DrawTextureTransparent(iconRect, slot.ItemDef.icon.texture, ScaleMode.ScaleToFit);
                                }
                                else
                                {
                                    EditorGUILayout.LabelField("?", GUILayout.Width(20)); // Placeholder if no icon
                                }
                                // --- End Show Item Icon ---

                                EditorGUILayout.LabelField($"{slot.ItemDef.displayName} (ID: {slot.itemId}, Qty: {slot.quantity})");
                                
                                // Use button (calls UseItem)
                                if (GUILayout.Button("Use", EditorStyles.miniButton, GUILayout.Width(40)))
                                {
                                    bool useSuccess = inventoryService.UseItem(player.NetworkId, slot.itemId);
                                    if (useSuccess)
                                    {
                                        Debug.Log($"Used item {slot.ItemDef.displayName} for {player.LobbyData.Name}");
                                        // Repaint to reflect changes
                                        Repaint();
                                    }
                                    else
                                    {
                                        Debug.LogWarning($"Failed to use item {slot.ItemDef.displayName} for {player.LobbyData.Name}");
                                    }
                                }
                                // Remove button (calls RemoveItem)
                                if (GUILayout.Button("Remove", EditorStyles.miniButton, GUILayout.Width(50)))
                                {
                                    bool removeSuccess = inventoryService.RemoveItem(player.NetworkId, slot.itemId, slot.quantity);
                                    if (removeSuccess)
                                    {
                                        Debug.Log($"Removed item {slot.ItemDef.displayName} (x{slot.quantity}) from {player.LobbyData.Name}'s inventory");
                                        Repaint();
                                    }
                                    else
                                    {
                                        Debug.LogWarning($"Failed to remove item {slot.ItemDef.displayName} from {player.LobbyData.Name}'s inventory");
                                    }
                                }
                                EditorGUILayout.EndHorizontal();
                            }
                        }
                    }
                    else
                    {
                        EditorGUILayout.LabelField("Bag is empty.");
                    }
                    EditorGUI.indentLevel--;
                }

                // Display Equipped Items
                EditorGUILayout.Space();
                foldouts[equippedFoldoutIndex] = EditorGUILayout.Foldout(foldouts[equippedFoldoutIndex], $"Equipped Items ({equippedItems.Count})", true);
                if (foldouts[equippedFoldoutIndex])
                {
                    EditorGUI.indentLevel++;
                    if (equippedItems.Count > 0)
                    {
                        foreach (var slot in equippedItems)
                        {
                            if (slot.ItemDef != null)
                            {
                                EditorGUILayout.BeginHorizontal();
                                
                                // --- Show Item Icon ---
                                if (slot.ItemDef.icon != null)
                                {
                                    Rect iconRect = GUILayoutUtility.GetRect(20, 20);
                                    EditorGUI.DrawTextureTransparent(iconRect, slot.ItemDef.icon.texture, ScaleMode.ScaleToFit);
                                }
                                else
                                {
                                    EditorGUILayout.LabelField("?", GUILayout.Width(20)); // Placeholder if no icon
                                }
                                // --- End Show Item Icon ---

                                EditorGUILayout.LabelField($"{slot.ItemDef.displayName} (ID: {slot.itemId}, Slot: {slot.ItemDef.equipSlot})");
                                
                                // Use UseItem for equipping/unequipping, just like the client
                                if (GUILayout.Button("Unequip", EditorStyles.miniButton, GUILayout.Width(60)))
                                {
                                    // Call UseItem, which handles the toggle logic correctly
                                    bool toggleSuccess = inventoryService.UseItem(player.NetworkId, slot.itemId);
                                    if (toggleSuccess)
                                    {
                                        Debug.Log($"Toggled equipment (unequipped) for {slot.ItemDef.displayName} for {player.LobbyData.Name} via Debug Window");
                                        Repaint();
                                    }
                                    else
                                    {
                                        Debug.LogWarning($"Failed to toggle equipment (unequip) for {slot.ItemDef.displayName} for {player.LobbyData.Name} via Debug Window");
                                    }
                                }
                                EditorGUILayout.EndHorizontal();
                            }
                        }
                    }
                    else
                    {
                        EditorGUILayout.LabelField("Nothing equipped.");
                    }
                    EditorGUI.indentLevel--;
                }

            }
            else
            {
                EditorGUILayout.LabelField("Inventory data not found for player.");
            }
#endif // UNITY_EDITOR
            // --- End Updated Section ---

            // Abilities (Placeholder, update if you have a different structure)
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Abilities", EditorStyles.boldLabel);
            if (player.UnlockedAbilities != null && player.UnlockedAbilities.Count > 0)
            {
                foreach (var ability in player.UnlockedAbilities)
                {
                    EditorGUILayout.LabelField($"- {ability?.abilityName ?? "Unknown Ability"}");
                }
            }
            else
            {
                EditorGUILayout.LabelField("No abilities unlocked.");
            }


            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndVertical();
    }

    // --- Helper Methods for Inventory Actions ---

    private void AddRandomItemToPlayer(string playerId, InventoryService inventoryService)
    {
        // Find all ItemDefinitions in the project (Resources folder assumed)
        ItemDefinition[] allItems = Resources.LoadAll<ItemDefinition>("Items");
        if (allItems != null && allItems.Length > 0)
        {
            // Pick a random item
            ItemDefinition randomItem = allItems[Random.Range(0, allItems.Length)];
            // Add one copy of it
            bool addSuccess = inventoryService.AddItem(playerId, randomItem, 1);
            if (addSuccess)
            {
                Debug.Log($"Added random item '{randomItem.displayName}' to player {playerId}");
                // Refresh the inventory display by forcing a repaint
                Repaint();
            }
            else
            {
                Debug.LogWarning($"Failed to add random item '{randomItem.displayName}' to player {playerId}");
            }
        }
        else
        {
            Debug.LogWarning("No ItemDefinitions found in Resources/Items to add.");
        }
    }

    private void ClearPlayerInventory(string playerId, InventoryService inventoryService)
    {
        // Get the PlayerInventory instance for this player using the new debug helper
        PlayerInventory playerInventory = null;
        bool hasInventory = inventoryService != null && inventoryService.TryGetPlayerInventory(playerId, out playerInventory);

        if (hasInventory)
        {
            // Get a list of items to remove to avoid modifying during iteration
            var bagItemsToRemove = new List<InventorySlot>(playerInventory.GetAllBagItems());
            var equippedItemsToRemove = new List<InventorySlot>(playerInventory.GetAllEquippedItems());

            bool allRemoved = true;

            // Remove items from the bag
            foreach (var slot in bagItemsToRemove)
            {
                if (slot != null)
                {
                    bool removeSuccess = inventoryService.RemoveItem(playerId, slot.itemId, slot.quantity);
                    if (!removeSuccess)
                    {
                        Debug.LogWarning($"Failed to remove bag item {slot.itemId} while clearing inventory.");
                        allRemoved = false;
                    }
                }
            }

            // Unequip items by calling UseItem on them
            // This correctly handles the state changes and effect removal.
            foreach (var slot in equippedItemsToRemove)
            {
                if (slot != null)
                {
                    // Calling UseItem on an equipped item should unequip it.
                    bool unequipSuccess = inventoryService.UseItem(playerId, slot.itemId);
                    if (!unequipSuccess)
                    {
                        Debug.LogWarning($"Failed to unequip item {slot.itemId} while clearing inventory.");
                        allRemoved = false;
                        // Even if unequip fails, we should still try to remove it from wherever it ended up
                        // If UseItem fails, the item might still be in the equipped state.
                        // Let's try removing it directly from the equipped slot by ID if UseItem didn't work.
                        // However, PlayerInventory doesn't have a direct "RemoveEquippedItem" method.
                        // The safest way is to rely on UseItem. If it fails, log it.
                    }
                }
            }

            // Re-fetch bag items after unequipping, in case the unequip process modified the bag
            // This might not be strictly necessary as UseItem should handle moving items back to the bag.
            // var bagItemsToRemoveAfterUnequip = new List<InventorySlot>(playerInventory.GetAllBagItems());
            // foreach (var slot in bagItemsToRemoveAfterUnequip)
            // {
            //     if (slot != null)
            //     {
            //         bool removeSuccess = inventoryService.RemoveItem(playerId, slot.itemId, slot.quantity);
            //         if (!removeSuccess)
            //         {
            //             Debug.LogWarning($"Failed to remove bag item {slot.itemId} (after unequipping) while clearing inventory.");
            //             allRemoved = false;
            //         }
            //     }
            // }

            if (allRemoved)
            {
                Debug.Log($"Cleared inventory for player {playerId}");
            }
            else
            {
                Debug.LogWarning($"Attempted to clear inventory for player {playerId}, but some items failed to remove/unequip.");
            }
            // Refresh the inventory display by forcing a repaint
            Repaint();
        }
        else
        {
            Debug.Log($"Inventory for player {playerId} not found or already empty.");
        }
    }
    // --- End Helper Methods ---
}
#endif