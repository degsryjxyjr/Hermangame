// Scripts/Editor/PlayerDataDebugWindow.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Linq;

public class PlayerDataDebugWindow : EditorWindow
{
    [MenuItem("Window/Player Data Debugger")]
    public static void ShowWindow()
    {
        GetWindow<PlayerDataDebugWindow>("Player Data");
    }

    private Vector2 scrollPos;
    private bool[] playerFoldouts;
    private bool[] inventoryFoldouts;

    void OnEnable()
    {
        playerFoldouts = new bool[0];
        inventoryFoldouts = new bool[0];
    }

    void OnGUI()
    {
        if (!Application.isPlaying)
        {
            GUILayout.Label("Game must be running to view player data");
            return;
        }

        if (PlayerManager.Instance == null)
        {
            GUILayout.Label("Game services not initialized");
            return;
        }

        var inventoryService = FindFirstObjectByType<InventoryService>();
        if (inventoryService == null)
        {
            GUILayout.Label("InventoryService not found");
            return;
        }

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        var allPlayers = PlayerManager.Instance.GetAllPlayers();
        if (playerFoldouts.Length != allPlayers.Count)
        {
            playerFoldouts = new bool[allPlayers.Count];
            inventoryFoldouts = new bool[allPlayers.Count];
        }

        GUILayout.Label($"Active Players: {allPlayers.Count}", EditorStyles.boldLabel);

        for (int i = 0; i < allPlayers.Count; i++)
        {
            var player = allPlayers[i];
            playerFoldouts[i] = EditorGUILayout.Foldout(playerFoldouts[i], $"{player.LobbyData.Name} ({player.LobbyData.Role})");

            if (playerFoldouts[i])
            {
                DrawPlayerData(player, i, inventoryService);
                EditorGUILayout.Space(10);
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawPlayerData(PlayerManager.PlayerConnection player, int playerIndex, InventoryService inventoryService)
    {
        EditorGUILayout.BeginVertical("box");
        
        // Basic Info
        EditorGUILayout.LabelField($"Player ID: {player.NetworkId}");
        EditorGUILayout.LabelField($"Level: {player.GameData.level}");
        EditorGUILayout.LabelField($"XP: {player.GameData.experience}");
        
        // Stats
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Stats", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Health: {player.GameData.stats.currentHealth}/{player.GameData.stats.maxHealth}");
        EditorGUILayout.LabelField($"Attack: {player.GameData.stats.attack}");
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Defense: {player.GameData.stats.defense}");
        EditorGUILayout.LabelField($"Magic: {player.GameData.stats.magic}");
        EditorGUILayout.EndHorizontal();
        
        // Abilities
        EditorGUILayout.Space();
        if (player.GameData.unlockedAbilities.Count > 0)
        {
            EditorGUILayout.LabelField("Abilities", EditorStyles.boldLabel);
            foreach (var ability in player.GameData.unlockedAbilities)
            {
                EditorGUILayout.LabelField($"- {ability.abilityName} (CD: {ability.cooldown}s)");
            }
        }
        
        // Inventory
        EditorGUILayout.Space();
        var inventory = inventoryService.GetInventory(player.NetworkId);
        inventoryFoldouts[playerIndex] = EditorGUILayout.Foldout(inventoryFoldouts[playerIndex], $"Inventory ({inventory.Count}/{PlayerInventory.MAX_SLOTS})");

        if (inventoryFoldouts[playerIndex])
        {
            EditorGUI.indentLevel++;
            foreach (var slot in inventory)
            {
                EditorGUILayout.BeginHorizontal();
                
                // Item icon and name
                var content = new GUIContent(slot.ItemDef.displayName, slot.ItemDef.icon?.texture);
                EditorGUILayout.LabelField(content, GUILayout.Width(150));
                
                // Stack info
                if (slot.ItemDef.isStackable)
                {
                    EditorGUILayout.LabelField($"x{slot.quantity}", GUILayout.Width(40));
                }
                
                // Equipment indicator
                if (slot.isEquipped)
                {
                    EditorGUILayout.LabelField("(Equipped)", EditorStyles.miniLabel);
                }
                
                // Quick action buttons
                if (GUILayout.Button("Use", EditorStyles.miniButton, GUILayout.Width(40)))
                {
                    inventoryService.UseItem(player.NetworkId, slot.itemId);
                }
                
                EditorGUILayout.EndHorizontal();
            }
            EditorGUI.indentLevel--;
        }
        
        EditorGUILayout.EndVertical();
    }
}
#endif