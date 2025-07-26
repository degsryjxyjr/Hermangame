// File: Scripts/Editor/PlayerDataDebugWindow.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq; // Make sure this is included

public class CombatDebugWindow : EditorWindow
{
    [MenuItem("Window/Combat Debugger")]
    public static void ShowWindow()
    {
        GetWindow<CombatDebugWindow>("Player Combat View");
    }
    private Vector2 scrollPos;
    private bool combatSimFoldout = false;
    private bool enemyListFoldout = false; // Foldout for enemy list

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
        var combatService = CombatService.Instance; // Get reference

        if (playerManager == null)
        {
            EditorGUILayout.HelpBox("PlayerManager not found.", MessageType.Warning);
            EditorGUILayout.EndScrollView();
            return;
        }

        var players = playerManager.GetAllPlayers();

        // --- Combat Simulation Section ---
        EditorGUILayout.Space();
        combatSimFoldout = EditorGUILayout.Foldout(combatSimFoldout, "Combat Simulation (Direct Ability Calls)", true);
        if (combatSimFoldout)
        {
            EditorGUI.indentLevel++;
            DrawCombatSimulation(players, combatService);
            EditorGUI.indentLevel--;
        }
        // --- END Combat Simulation Section ---

        // --- Enemy List Section ---
        EditorGUILayout.Space();
        enemyListFoldout = EditorGUILayout.Foldout(enemyListFoldout, "Active Enemies", true);
        if (enemyListFoldout)
        {
            EditorGUI.indentLevel++;
            DrawEnemyList(combatService);
            EditorGUI.indentLevel--;
        }
        // --- END Enemy List Section ---

        EditorGUILayout.EndScrollView();
    }


    // --- Combat Simulation Drawing Logic ---
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
        // Populate target cache if needed
        PopulateTargetCache(players, combatService);
        List<string> targetDisplayOptions = new List<string> { "None" };
        targetDisplayOptions.AddRange(targetOptionsCache);
        int currentTargetIndex = targetOptionsCache.IndexOf(selectedTargetId) + 1; // +1 because "None" is index 0
        if (currentTargetIndex == 0) currentTargetIndex = 0; // Default to "None"
        int newTargetIndex = EditorGUILayout.Popup("Target", currentTargetIndex, targetDisplayOptions.ToArray());
        if (newTargetIndex > 0 && newTargetIndex <= targetOptionsCache.Count) // Adjust for "None" offset
        {
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
            var encounterManager = combatService.GetEncounterManager();
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

        // 4. Determine Context (Assume InCombat for simulation)
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
            // Optionally, manually advance turn
            // combatService.AdvanceTurn();
        }
        else
        {
            Debug.LogWarning($"PlayerDataDebugWindow: Simulated ability '{abilityDef.abilityName}' failed to execute.");
        }
    }
    // --- END Combat Simulation Drawing Logic ---


    // --- Enemy List Drawing Logic ---
    private void DrawEnemyList(CombatService combatService)
    {
        if (combatService == null)
        {
            EditorGUILayout.HelpBox("CombatService not found.", MessageType.Warning);
            return;
        }

        var encounterManager = combatService.GetEncounterManager();
        if (encounterManager == null)
        {
             EditorGUILayout.HelpBox("EncounterManager not found.", MessageType.Info);
             return;
        }

        var activeEnemies = encounterManager.GetActiveEnemies();
        if (activeEnemies == null || activeEnemies.Count == 0)
        {
            EditorGUILayout.LabelField("No active enemies.");
            return;
        }

        EditorGUILayout.LabelField($"Active Enemies ({activeEnemies.Count})", EditorStyles.boldLabel);

        foreach (var enemy in activeEnemies)
        {
            if (enemy != null)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField($"Enemy: {enemy.GetEntityName() ?? "Unknown Enemy"}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Instance ID:", enemy.GetInstanceID().ToString());
                EditorGUILayout.LabelField($"Current Health:", enemy.CurrentHealth.ToString());
                EditorGUILayout.LabelField($"Max Health:", enemy.MaxHealth.ToString());
                EditorGUILayout.LabelField($"Attack:", enemy.Attack.ToString());
                EditorGUILayout.LabelField($"Defense:", enemy.Defense.ToString());
                EditorGUILayout.LabelField($"Magic:", enemy.Magic.ToString());
                EditorGUILayout.LabelField($"Is Alive:", enemy.IsAlive().ToString());
                // Add more stats as needed from EnemyEntity or EnemyDefinition
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
            }
        }
    }
    // --- END Enemy List Drawing Logic ---
}
#endif