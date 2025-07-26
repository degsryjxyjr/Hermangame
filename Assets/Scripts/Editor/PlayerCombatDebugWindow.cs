// File: Scripts/Editor/PlayerDataDebugWindow.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq; // Make sure this is included

public class PlayerCombatDebugWindow : EditorWindow
{
    [MenuItem("Window/Player Combat Debugger")]
    public static void ShowWindow()
    {
        GetWindow<PlayerCombatDebugWindow>("Player Combat View");
    }

    private Vector2 scrollPos;
    // Use a single array for foldouts, calculating index as needed
    // Index 0: Player details, Index 1: Inventory (Bag), Index 2: Inventory (Equipped)
    // For N players: Indices 0..N-1 (Player details), N..2N-1 (Bag), 2N..3N-1 (Equipped)
    private bool[] playerFoldouts;
    // Index for Combat Simulation section foldout
    private bool combatSimFoldout = false;

    // --- Combat Simulation State ---
    private string selectedCasterPlayerId = "";
    private string selectedAbilityId = "";
    private string selectedTargetId = ""; // Can be Player NetworkId or Enemy InstanceId (as string)
    private List<AbilityDefinition> availableAbilitiesCache = new List<AbilityDefinition>();
    private List<string> targetOptionsCache = new List<string>(); // Stores formatted strings like "Player: Name (ID)" or "Enemy: Goblin (12345)"
    private Dictionary<string, object> targetLookupCache = new Dictionary<string, object>(); // Maps formatted string to actual PlayerConnection or EnemyEntity instance
    // --- End Combat Simulation State ---

    void OnEnable()
    {
        playerFoldouts = new bool[0];
        // Populate caches when window opens
        PopulateAbilityCache();
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
        var combatService = CombatService.Instance; // Get reference

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
        if (playerFoldouts.Length != requiredFoldouts)
        {
            System.Array.Resize(ref playerFoldouts, requiredFoldouts);
        }

        for (int i = 0; i < playerCount; i++)
        {
            DrawPlayerData(players[i], i, inventoryService, combatService); // Pass combatService
        }

        // --- NEW: Combat Simulation Section ---
        EditorGUILayout.Space();
        combatSimFoldout = EditorGUILayout.Foldout(combatSimFoldout, "Combat Simulation (Direct Ability Calls)", true);
        if (combatSimFoldout)
        {
            EditorGUI.indentLevel++;
            DrawCombatSimulation(players, combatService);
            EditorGUI.indentLevel--;
        }
        // --- END NEW: Combat Simulation Section ---

        EditorGUILayout.EndScrollView();
    }

    private void DrawPlayerData(PlayerConnection player, int playerIndex, InventoryService inventoryService, CombatService combatService) // Add combatService parameter
    {
        EditorGUILayout.BeginVertical("box");
        
        // Calculate foldout indices for this player
        int detailsFoldoutIndex = playerIndex * 3 + 0;
        int bagFoldoutIndex = playerIndex * 3 + 1;
        int equippedFoldoutIndex = playerIndex * 3 + 2;

        playerFoldouts[detailsFoldoutIndex] = EditorGUILayout.Foldout(playerFoldouts[detailsFoldoutIndex], $"Player: {player.LobbyData?.Name ?? "Unknown"} (ID: {player.NetworkId})", true);
        if (playerFoldouts[detailsFoldoutIndex])
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

            // Use the direct properties from the refactored PlayerConnection
            int newMaxHealth = EditorGUILayout.IntField("Max Health", player.MaxHealth);
            int newCurrentHealth = EditorGUILayout.IntField("Current Health", player.CurrentHealth);
            int newAttack = EditorGUILayout.IntField("Attack", player.Attack);
            int newDefense = EditorGUILayout.IntField("Defense", player.Defense);
            int newMagic = EditorGUILayout.IntField("Magic", player.Magic);
            int newLevel = EditorGUILayout.IntField("Level", player.Level);
            int newExp = EditorGUILayout.IntField("Experience", player.Experience);

            // If any stat field was changed, update the player's data
            if (EditorGUI.EndChangeCheck())
            {
                // Clamp current health between 0 and the (potentially new) max health
                newCurrentHealth = Mathf.Clamp(newCurrentHealth, 0, newMaxHealth);
                newMaxHealth = Mathf.Max(1, newMaxHealth); // Ensure MaxHealth is at least 1

                // Update the direct properties on the PlayerConnection instance
                player.MaxHealth = newMaxHealth;
                player.CurrentHealth = newCurrentHealth;
                player.Attack = newAttack;
                player.Defense = newDefense;
                player.Magic = newMagic;
                player.Level = newLevel;
                player.Experience = newExp;

                // Send update to client so the UI reflects the change
                GameServer.Instance.SendToPlayer(player.NetworkId, new
                {
                    type = "stats_update",
                    currentHealth = player.CurrentHealth,
                    maxHealth = player.MaxHealth,
                    attack = player.Attack,
                    defense = player.Defense,
                    magic = player.Magic
                    // Add other stats if needed by client
                });
                Debug.Log($"Stats updated for {player.LobbyData.Name} via Debug Window.");
                // Repaint to reflect changes immediately
                Repaint();
            }
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
                playerFoldouts[bagFoldoutIndex] = EditorGUILayout.Foldout(playerFoldouts[bagFoldoutIndex], $"Bag Items ({bagItems.Count})", true);
                if (playerFoldouts[bagFoldoutIndex])
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
                playerFoldouts[equippedFoldoutIndex] = EditorGUILayout.Foldout(playerFoldouts[equippedFoldoutIndex], $"Equipped Items ({equippedItems.Count})", true);
                if (playerFoldouts[equippedFoldoutIndex])
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
                                
                                // Use/Equip button (calls UseItem for equipping/unequipping, just like the client)
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

    // --- NEW: Combat Simulation Drawing Logic ---
    private void DrawCombatSimulation(List<PlayerConnection> players, CombatService combatService)
    {
        if (combatService == null)
        {
            EditorGUILayout.HelpBox("CombatService not found. Cannot simulate combat.", MessageType.Warning);
            return;
        }

        // Ensure caches are populated
        if (availableAbilitiesCache.Count == 0) PopulateAbilityCache();

        EditorGUILayout.LabelField("Simulate Direct Ability Calls (Bypasses Client)", EditorStyles.miniBoldLabel);

        // --- 1. Select Caster Player ---
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("1. Select Caster (Player):", EditorStyles.boldLabel);
        List<string> playerOptions = new List<string> { "None" };
        List<string> playerIds = new List<string> { "" };
        foreach (var p in players)
        {
            if (p != null)
            {
                playerOptions.Add($"{p.LobbyData?.Name ?? "Unknown"} ({p.NetworkId})");
                playerIds.Add(p.NetworkId);
            }
        }

        int currentCasterIndex = playerIds.IndexOf(selectedCasterPlayerId);
        if (currentCasterIndex == -1) currentCasterIndex = 0; // Default to "None"

        int newCasterIndex = EditorGUILayout.Popup("Caster Player", currentCasterIndex, playerOptions.ToArray());
        if (newCasterIndex >= 0 && newCasterIndex < playerIds.Count)
        {
            selectedCasterPlayerId = playerIds[newCasterIndex];
        }

        // --- 2. Select Ability ---
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("2. Select Ability:", EditorStyles.boldLabel);
        List<string> abilityOptions = new List<string> { "None" };
        List<string> abilityIds = new List<string> { "" };
        foreach (var abil in availableAbilitiesCache)
        {
            if (abil != null)
            {
                // Use the ScriptableObject's name (filename without .asset) for loading
                string assetName = abil.name; // e.g., "FireBallAbility"
                abilityOptions.Add($"{abil.abilityName} ({assetName})"); // Show both name and asset name for clarity
                abilityIds.Add(assetName); // Store the asset name for Resources.Load

                //OLD NOT WORKING CODE
                //abilityOptions.Add(abil.abilityName);
                //abilityIds.Add(abil.abilityName); // Using name as ID for simplicity in Resources.Load
            }
        }

        int currentAbilityIndex = abilityIds.IndexOf(selectedAbilityId);
        if (currentAbilityIndex == -1) currentAbilityIndex = 0; // Default to "None"

        int newAbilityIndex = EditorGUILayout.Popup("Ability", currentAbilityIndex, abilityOptions.ToArray());
        if (newAbilityIndex >= 0 && newAbilityIndex < abilityIds.Count)
        {
            selectedAbilityId = abilityIds[newAbilityIndex];
        }

        // --- 3. Select Target ---
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("3. Select Target (Player or Enemy):", EditorStyles.boldLabel);
        
        // Populate target cache if needed or if player list changed significantly
        // For simplicity, repopulate on every GUI draw while the section is open.
        // A more efficient way would be to listen to player connect/disconnect events.
        PopulateTargetCache(players, combatService);

        List<string> targetDisplayOptions = new List<string> { "None" };
        targetDisplayOptions.AddRange(targetOptionsCache);

        int currentTargetIndex = targetOptionsCache.IndexOf(selectedTargetId) + 1; // +1 because "None" is index 0
        if (currentTargetIndex == 0) currentTargetIndex = 0; // Default to "None"

        int newTargetIndex = EditorGUILayout.Popup("Target", currentTargetIndex, targetDisplayOptions.ToArray());
        if (newTargetIndex > 0 && newTargetIndex <= targetOptionsCache.Count) // Adjust for "None" offset
        {
            // newTargetIndex 1 maps to targetOptionsCache[0]
            selectedTargetId = targetOptionsCache[newTargetIndex - 1]; // Subtract 1 for "None" offset
        }
        else
        {
            selectedTargetId = ""; // "None" selected
        }

        // --- 4. Execute Button ---
        EditorGUILayout.Space();
        EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(selectedCasterPlayerId) || string.IsNullOrEmpty(selectedAbilityId) || string.IsNullOrEmpty(selectedTargetId));
        if (GUILayout.Button("Execute Ability", GUILayout.Height(30)))
        {
            ExecuteSimulatedAbility(combatService);
        }
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.Space();
    }

    private void PopulateAbilityCache()
    {
        availableAbilitiesCache.Clear();
        // Load all abilities from Resources (adjust path as needed)
        // This is a simple way, a more robust system might maintain a registry.
        AbilityDefinition[] allAbilities = Resources.LoadAll<AbilityDefinition>("Abilities");
        if (allAbilities != null)
        {
            availableAbilitiesCache.AddRange(allAbilities);
            Debug.Log($"PlayerDataDebugWindow: Populated ability cache with {allAbilities.Length} abilities.");
        }
    }

    private void PopulateTargetCache(List<PlayerConnection> players, CombatService combatService)
    {
        targetOptionsCache.Clear();
        targetLookupCache.Clear();

        // Add Players
        foreach (var player in players)
        {
            if (player != null)
            {
                string playerKey = $"Player: {player.LobbyData?.Name ?? "Unknown"} ({player.NetworkId})";
                targetOptionsCache.Add(playerKey);
                targetLookupCache[playerKey] = player; // Store PlayerConnection instance
            }
        }

        // Add Enemies (if in combat and EncounterManager exists)
        if (combatService != null)
        {
            // Get the EncounterManager from CombatService if it's active
            // This requires CombatService to expose its _encounterManager field.
            // Let's assume it has a public property or a way to get it.
            // A common pattern is to make it internal/static or provide a getter.
            // For now, we'll access the private field via reflection (not ideal but works for editor tools).
            // A better way is to add a public getter to CombatService:
            /*
            // In CombatService.cs
            public EncounterManager GetEncounterManager() => _encounterManager;
            */
            // And then use: var encounterManager = combatService.GetEncounterManager();
            
            // Using reflection (Editor-only, acceptable for debug tools)
            var encounterManagerField = typeof(CombatService).GetField("_encounterManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (encounterManagerField != null)
            {
                var encounterManager = encounterManagerField.GetValue(combatService) as EncounterManager;
                if (encounterManager != null)
                {
                    var activeEnemies = encounterManager.GetActiveEnemies();
                    foreach (var enemy in activeEnemies)
                    {
                        if (enemy != null)
                        {
                            // Use InstanceID as a unique identifier for the enemy instance in the scene
                            string enemyKey = $"Enemy: {enemy.GetEntityName() ?? "Unknown Enemy"} (ID: {enemy.GetInstanceID()})";
                            targetOptionsCache.Add(enemyKey);
                            targetLookupCache[enemyKey] = enemy; // Store EnemyEntity instance
                        }
                    }
                }
            }
            else
            {
                Debug.LogWarning("PlayerDataDebugWindow: Could not find '_encounterManager' field in CombatService via reflection.");
            }
        }
    }

    private void ExecuteSimulatedAbility(CombatService combatService)
    {
        if (combatService == null)
        {
            Debug.LogError("PlayerDataDebugWindow: Cannot execute ability, CombatService is null.");
            return;
        }

        if (string.IsNullOrEmpty(selectedCasterPlayerId) || string.IsNullOrEmpty(selectedAbilityId) || string.IsNullOrEmpty(selectedTargetId))
        {
            Debug.LogWarning("PlayerDataDebugWindow: Cannot execute ability, caster, ability, or target is not selected.");
            return;
        }

        // 1. Resolve Caster
        var casterPlayer = PlayerManager.Instance.GetPlayer(selectedCasterPlayerId);
        if (casterPlayer == null)
        {
            Debug.LogError($"PlayerDataDebugWindow: Cannot execute ability, caster player '{selectedCasterPlayerId}' not found.");
            return;
        }

        // 2. Resolve Ability
        // Load the ability definition (using Resources.Load for prototype, as done in cache)
        AbilityDefinition abilityDef = Resources.Load<AbilityDefinition>($"Abilities/{selectedAbilityId}");
        if (abilityDef == null)
        {
            Debug.LogError($"PlayerDataDebugWindow: Cannot execute ability, AbilityDefinition '{selectedAbilityId}' not found in Resources/Abilities.");
            return;
        }

        // 3. Resolve Target
        if (!targetLookupCache.TryGetValue(selectedTargetId, out var targetObject))
        {
            Debug.LogError($"PlayerDataDebugWindow: Cannot execute ability, target '{selectedTargetId}' not found in lookup cache.");
            return;
        }

        List<IDamageable> resolvedTargets = new List<IDamageable>();
        if (targetObject is IDamageable damageableTarget)
        {
            resolvedTargets.Add(damageableTarget);
        }
        else
        {
            Debug.LogError($"PlayerDataDebugWindow: Selected target '{selectedTargetId}' does not implement IDamageable.");
            return;
        }

        // 4. Determine Context (Assume InCombat for simulation, but could be configurable)
        AbilityExecutionService.AbilityContext context = AbilityExecutionService.AbilityContext.InCombat;

        // 5. Call AbilityExecutionService directly
        Debug.Log($"PlayerDataDebugWindow: Simulating ability execution:\n" +
                  $"  Caster: {casterPlayer.LobbyData?.Name ?? "Unknown"} ({casterPlayer.NetworkId})\n" +
                  $"  Ability: {abilityDef.abilityName}\n" +
                  $"  Target: {selectedTargetId}\n" +
                  $"  Context: {context}");

        bool success = AbilityExecutionService.Instance.ExecuteAbility(
            caster: casterPlayer,
            targets: resolvedTargets,
            abilityDefinition: abilityDef,
            context: context
        );

        if (success)
        {
            Debug.Log($"PlayerDataDebugWindow: Simulated ability '{abilityDef.abilityName}' executed successfully.");
            // Optionally, manually advance turn if simulating in-combat actions
            // This depends on your combat flow. You might want a checkbox in the UI for this.
            // combatService.AdvanceTurn(); // Or call EncounterManager.AdvanceTurn() if exposed
        }
        else
        {
            Debug.LogWarning($"PlayerDataDebugWindow: Simulated ability '{abilityDef.abilityName}' failed to execute.");
        }
    }
    // --- END NEW: Combat Simulation Drawing Logic ---


    // --- Helper Methods for Inventory Actions (Updated) ---

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