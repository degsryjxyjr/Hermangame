// File: Scripts/Editor/PlayerCombatDebugWindow.cs
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
    private bool combatSimFoldout = false;
    private bool enemyListFoldout = false;

    // --- NEW: Mode Toggle ---
    private enum SimulationMode { DirectExecution, CombatService }
    private SimulationMode currentMode = SimulationMode.DirectExecution; // Default to Direct for easy testing
    // --- END NEW ---

    // --- Combat Simulation State (Used by both modes, but executed differently) ---
    private string selectedCasterPlayerId = "";
    private string selectedAbilityId = ""; // Now stores the Asset Name (e.g., FireBallAbility)
    private string selectedTargetId = ""; // Stores the lookup key (e.g., "Player: Name (ID)" or "Enemy: Name (12345)")
    private List<AbilityDefinition> availableAbilitiesCache = new List<AbilityDefinition>();
    private List<string> targetOptionsCache = new List<string>();
    private Dictionary<string, object> targetLookupCache = new Dictionary<string, object>(); // Key: display string, Value: PlayerConnection or EnemyEntity
    // --- End Combat Simulation State ---

    void OnEnable()
    {
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
        var combatService = CombatService.Instance;

        if (playerManager == null)
        {
            EditorGUILayout.HelpBox("PlayerManager not found.", MessageType.Warning);
            EditorGUILayout.EndScrollView();
            return;
        }

        var players = playerManager.GetAllPlayers();

        // --- NEW: Mode Selection ---
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Simulation Mode", EditorStyles.boldLabel);
        currentMode = (SimulationMode)EditorGUILayout.EnumPopup("Mode", currentMode);
        EditorGUILayout.HelpBox(
            currentMode == SimulationMode.DirectExecution ?
            "Directly calls AbilityExecutionService. Bypasses CombatService, turn checks, and action limits. Good for testing ability effects." :
            "Calls CombatService.ProcessPlayerCombatAction. Respects turn order, action limits, and advances turns. Simulates client message flow.",
            MessageType.Info
        );
        // --- END NEW ---

        // --- Combat Simulation Section ---
        EditorGUILayout.Space();
        combatSimFoldout = EditorGUILayout.Foldout(combatSimFoldout, "Combat Simulation", true);
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

        if (availableAbilitiesCache.Count == 0) PopulateAbilityCache();

        EditorGUILayout.LabelField(
            currentMode == SimulationMode.DirectExecution ?
            "Simulate Ability Execution (Direct Call)" :
            "Simulate Client Message (CombatService)",
            EditorStyles.miniBoldLabel
        );

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
        if (currentCasterIndex == -1) currentCasterIndex = 0;
        int newCasterIndex = EditorGUILayout.Popup("Caster Player", currentCasterIndex, playerOptions.ToArray());
        if (newCasterIndex >= 0 && newCasterIndex < playerIds.Count)
        {
            selectedCasterPlayerId = playerIds[newCasterIndex];
        }

        // --- 2. Select Ability ---
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("2. Select Ability:", EditorStyles.boldLabel);
        List<string> abilityOptions = new List<string> { "None" };
        List<string> abilityIds = new List<string> { "" }; // Stores Asset Names
        foreach (var abil in availableAbilitiesCache)
        {
            if (abil != null)
            {
                string assetName = abil.name; // Use asset name for Resources.Load
                abilityOptions.Add($"{abil.abilityName} ({assetName})");
                abilityIds.Add(assetName);
            }
        }
        int currentAbilityIndex = abilityIds.IndexOf(selectedAbilityId);
        if (currentAbilityIndex == -1) currentAbilityIndex = 0;
        int newAbilityIndex = EditorGUILayout.Popup("Ability", currentAbilityIndex, abilityOptions.ToArray());
        if (newAbilityIndex >= 0 && newAbilityIndex < abilityIds.Count)
        {
            selectedAbilityId = abilityIds[newAbilityIndex];
        }

        // --- 3. Select Target ---
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("3. Select Target (Player or Enemy):", EditorStyles.boldLabel);
        PopulateTargetCache(players, combatService);
        List<string> targetDisplayOptions = new List<string> { "None" };
        targetDisplayOptions.AddRange(targetOptionsCache);
        int currentTargetIndex = targetOptionsCache.IndexOf(selectedTargetId) + 1;
        if (currentTargetIndex == 0) currentTargetIndex = 0;
        int newTargetIndex = EditorGUILayout.Popup("Target", currentTargetIndex, targetDisplayOptions.ToArray());
        if (newTargetIndex > 0 && newTargetIndex <= targetOptionsCache.Count)
        {
            selectedTargetId = targetOptionsCache[newTargetIndex - 1];
        }
        else
        {
            selectedTargetId = "";
        }

        // --- 4. Execute Button ---
        EditorGUILayout.Space();
        EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(selectedCasterPlayerId) || string.IsNullOrEmpty(selectedAbilityId) || string.IsNullOrEmpty(selectedTargetId));
        string buttonLabel = currentMode == SimulationMode.DirectExecution ? "Execute Ability (Direct)" : "Send Action to CombatService";
        if (GUILayout.Button(buttonLabel, GUILayout.Height(30)))
        {
            if (currentMode == SimulationMode.DirectExecution)
            {
                ExecuteAbilityDirectly(combatService);
            }
            else // CombatService Mode
            {
                SimulateClientMessage(combatService);
            }
        }
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.Space();
    }

    private void PopulateAbilityCache()
    {
        availableAbilitiesCache.Clear();
        AbilityDefinition[] allAbilities = Resources.LoadAll<AbilityDefinition>("Abilities");
        if (allAbilities != null)
        {
            availableAbilitiesCache.AddRange(allAbilities);
            Debug.Log($"PlayerCombatDebugWindow: Populated ability cache with {allAbilities.Length} abilities.");
        }
    }

    private void PopulateTargetCache(List<PlayerConnection> players, CombatService combatService)
    {
        targetOptionsCache.Clear();
        targetLookupCache.Clear();
        foreach (var player in players)
        {
            if (player != null)
            {
                string playerKey = $"Player: {player.LobbyData?.Name ?? "Unknown"} ({player.NetworkId})";
                targetOptionsCache.Add(playerKey);
                targetLookupCache[playerKey] = player;
            }
        }
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
                        string enemyKey = $"Enemy: {enemy.GetEntityName() ?? "Unknown Enemy"} (ID: {enemy.GetInstanceID()})";
                        targetOptionsCache.Add(enemyKey);
                        targetLookupCache[enemyKey] = enemy;
                    }
                }
            }
        }
    }

    // --- NEW: Direct Execution Method ---
    private void ExecuteAbilityDirectly(CombatService combatService)
    {
        Debug.Log("PlayerCombatDebugWindow: Executing ability directly via AbilityExecutionService.");

        // 1. Resolve Caster
        var casterPlayer = PlayerManager.Instance.GetPlayer(selectedCasterPlayerId);
        if (casterPlayer == null)
        {
            Debug.LogError($"PlayerCombatDebugWindow: Cannot execute ability, caster player '{selectedCasterPlayerId}' not found.");
            return;
        }

        // 2. Resolve Ability
        AbilityDefinition abilityDef = Resources.Load<AbilityDefinition>($"Abilities/{selectedAbilityId}");
        if (abilityDef == null)
        {
            Debug.LogError($"PlayerCombatDebugWindow: Cannot execute ability, AbilityDefinition '{selectedAbilityId}' not found.");
            return;
        }

        // 3. Resolve Target
        if (!targetLookupCache.TryGetValue(selectedTargetId, out var targetObject) || !(targetObject is IDamageable damageableTarget))
        {
            Debug.LogError($"PlayerCombatDebugWindow: Cannot execute ability, target '{selectedTargetId}' not found or not damageable.");
            return;
        }
        List<IDamageable> targets = new List<IDamageable> { damageableTarget };

        var useContext = GameStateManager.Instance.GetCurrentGameState() == GameStateManager.GameState.Combat 
            ? AbilityExecutionService.AbilityContext.InCombat 
            : AbilityExecutionService.AbilityContext.OutOfCombat;

        // 4. Call AbilityExecutionService directly (Bypasses all combat rules)
        bool success = AbilityExecutionService.Instance.ExecuteAbility(
            caster: casterPlayer,
            targets: targets,
            abilityDefinition: abilityDef,
            context: useContext
        );

        if (success)
        {
            Debug.Log($"PlayerCombatDebugWindow: Direct execution of '{abilityDef.abilityName}' succeeded.");
        }
        else
        {
            Debug.LogWarning($"PlayerCombatDebugWindow: Direct execution of '{abilityDef.abilityName}' failed.");
        }
    }
    // --- END NEW: Direct Execution Method ---

    // --- NEW: Simulate Client Message Method ---
    private void SimulateClientMessage(CombatService combatService)
    {
        Debug.Log("PlayerCombatDebugWindow: Simulating client message to CombatService.");

        // 1. Validate selections (basic check)
        if (string.IsNullOrEmpty(selectedCasterPlayerId) || string.IsNullOrEmpty(selectedAbilityId) || string.IsNullOrEmpty(selectedTargetId))
        {
            Debug.LogWarning("PlayerCombatDebugWindow: Cannot simulate message, caster, ability, or target is not selected.");
            return;
        }

        // 2. Resolve Target *Specifier* (what the client would send)
        // The client typically sends a simple identifier.
        // For players: NetworkId
        // For enemies: InstanceID (as string) or a custom ID assigned by the server
        string targetSpecifier = null;
        if (targetLookupCache.TryGetValue(selectedTargetId, out var targetObj))
        {
            if (targetObj is PlayerConnection targetPlayer)
            {
                targetSpecifier = targetPlayer.NetworkId;
            }
            else if (targetObj is EnemyEntity targetEnemy)
            {
                targetSpecifier = targetEnemy.GetInstanceID().ToString(); // Use InstanceID string
                // Alternative: If EncounterManager assigns unique enemy IDs, use that.
            }
        }

        if (string.IsNullOrEmpty(targetSpecifier))
        {
            Debug.LogError($"PlayerCombatDebugWindow: Could not determine target specifier for '{selectedTargetId}'.");
            return;
        }

        // 3. Construct the message data (Dictionary mimicking JSON)
        // This structure should match what your real frontend sends.
        var actionData = new Dictionary<string, object>
        {
            { "type", "use_ability" }, // Standard action type
            { "abilityId", selectedAbilityId }, // Send the Asset Name used for Resources.Load
            { "target", targetSpecifier } // Send the resolved specifier
            // Add other fields if your protocol requires them (e.g., context if not implied)
        };

        // 4. Call CombatService.ProcessPlayerCombatAction
        // This is the core of simulating the client-server flow.
        combatService.ProcessPlayerCombatAction(selectedCasterPlayerId, actionData);

        Debug.Log($"PlayerCombatDebugWindow: Simulated message sent for player '{selectedCasterPlayerId}' using ability '{selectedAbilityId}' on target '{targetSpecifier}'.");
    }
    // --- END NEW: Simulate Client Message Method ---


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

                // --- NEW: Display Enemy Action Budget ---
                if (enemy is IActionBudget enemyActionBudget)
                {
                    EditorGUILayout.LabelField("Action Budget:", EditorStyles.miniBoldLabel);
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField($"Actions: {enemyActionBudget.TotalActions} (Remaining: {enemyActionBudget.ActionsRemaining})");
                    EditorGUI.indentLevel--;
                }
                else
                {
                    EditorGUILayout.LabelField("Action Budget: Not Implemented", EditorStyles.miniLabel);
                }
                // --- END NEW ---

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
            }
        }
    }
    // --- END Enemy List Drawing Logic ---
}
#endif