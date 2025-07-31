#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

[CustomEditor(typeof(PlayerClassDefinition))]
public class PlayerClassEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Draw the default inspector for fields NOT related to levelUpBonuses
        serializedObject.Update(); // Start editing

        // Get the SerializedProperty for the levelUpBonuses list
        SerializedProperty levelUpBonusesProp = serializedObject.FindProperty("levelUpBonuses");

        // Draw all properties except levelUpBonuses using the default inspector
        DrawPropertiesExcluding(serializedObject, "levelUpBonuses", "m_Script"); // Exclude m_Script and levelUpBonuses

        // --- Custom UI for Level Up Bonuses ---
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Level Up Bonuses", EditorStyles.boldLabel);

        // Add Button
        if (GUILayout.Button("Add Level Up Bonus"))
        {
            AddNewLevelBonus(levelUpBonusesProp);
        }

        // Display the list with custom UI for each element
        if (levelUpBonusesProp.arraySize > 0)
        {
            EditorGUI.indentLevel++;
            for (int i = 0; i < levelUpBonusesProp.arraySize; i++)
            {
                SerializedProperty bonusProp = levelUpBonusesProp.GetArrayElementAtIndex(i);
                SerializedProperty targetLevelProp = bonusProp.FindPropertyRelative("targetLevel");

                // Foldout for each bonus
                string foldoutLabel = $"Level {targetLevelProp.intValue} Bonus";
                bool wasExpanded = IsLevelBonusExpanded(i);
                bool isExpanded = EditorGUILayout.Foldout(wasExpanded, foldoutLabel, true);

                if (isExpanded)
                {
                    EditorGUI.indentLevel++;
                    // Draw the fields of LevelUpBonusData
                    DrawLevelBonusProperties(bonusProp, targetLevelProp.intValue);
                    EditorGUI.indentLevel--;

                    EditorGUILayout.Space();
                    // Remove Button for this specific bonus
                    if (GUILayout.Button("Remove This Level Bonus"))
                    {
                        levelUpBonusesProp.DeleteArrayElementAtIndex(i);
                        // Adjust indices if needed (though targetLevel is managed by us)
                        serializedObject.ApplyModifiedProperties();
                        return; // Exit after modification to avoid issues
                    }
                    EditorGUILayout.Space();
                }
                SetLevelBonusExpanded(i, isExpanded); // Save foldout state
            }
            EditorGUI.indentLevel--;
        }
        else
        {
            EditorGUILayout.HelpBox("No level up bonuses defined yet. Click 'Add Level Up Bonus' to start.", MessageType.Info);
        }

        serializedObject.ApplyModifiedProperties(); // Apply changes
    }

    private void AddNewLevelBonus(SerializedProperty levelUpBonusesProp)
    {
        levelUpBonusesProp.arraySize++;
        int newIndex = levelUpBonusesProp.arraySize - 1;
        SerializedProperty newBonusProp = levelUpBonusesProp.GetArrayElementAtIndex(newIndex);

        // Find the next level number (should be highest current level + 1, or 2 if list is empty/only level 1 exists implicitly)
        int nextLevel = 2; // Default first level to add bonus for
        for (int i = 0; i < levelUpBonusesProp.arraySize - 1; i++) // Check existing elements (not the new one)
        {
            SerializedProperty existingBonusProp = levelUpBonusesProp.GetArrayElementAtIndex(i);
            SerializedProperty existingLevelProp = existingBonusProp.FindPropertyRelative("targetLevel");
            if (existingLevelProp != null && existingLevelProp.intValue >= nextLevel)
            {
                nextLevel = existingLevelProp.intValue + 1;
            }
        }

        // Set the targetLevel for the new bonus
        SerializedProperty newLevelProp = newBonusProp.FindPropertyRelative("targetLevel");
        if (newLevelProp != null)
        {
            newLevelProp.intValue = nextLevel;
        }

        // Initialize lists if needed (Unity usually does this)
        // SerializedProperty newAbilitiesProp = newBonusProp.FindPropertyRelative("newAbilities");
        // SerializedProperty newItemsProp = newBonusProp.FindPropertyRelative("grantedItems");
        // if (newAbilitiesProp != null) { /* Ensure list is initialized */ }
        // if (newItemsProp != null) { /* Ensure list is initialized */ }
    }

    private void DrawLevelBonusProperties(SerializedProperty bonusProp, int level)
    {
        // Display Level (read-only or informational)
        EditorGUILayout.LabelField($"Target Level: {level}", EditorStyles.miniLabel); // Read-only display

        // Draw Stat Modifiers
        EditorGUILayout.PropertyField(bonusProp.FindPropertyRelative("maxHealthBonus"));
        EditorGUILayout.PropertyField(bonusProp.FindPropertyRelative("attackBonus"));
        EditorGUILayout.PropertyField(bonusProp.FindPropertyRelative("defenseBonus"));
        EditorGUILayout.PropertyField(bonusProp.FindPropertyRelative("magicBonus"));
        EditorGUILayout.PropertyField(bonusProp.FindPropertyRelative("actionBonus"));

        // Draw New Abilities List
        EditorGUILayout.PropertyField(bonusProp.FindPropertyRelative("newAbilities"), true); // `true` for nested lists

        // Draw Granted Items List
        EditorGUILayout.PropertyField(bonusProp.FindPropertyRelative("grantedItems"), true); // `true` for nested lists
    }

    // --- Simple Foldout State Management (using EditorPrefs) ---
    private bool IsLevelBonusExpanded(int index)
    {
        PlayerClassDefinition classDef = target as PlayerClassDefinition;
        if (classDef == null) return false;
        string key = $"PlayerClassEditor_{classDef.name}_LevelBonus_{index}_Expanded";
        return EditorPrefs.GetBool(key, false);
    }

    private void SetLevelBonusExpanded(int index, bool isExpanded)
    {
        PlayerClassDefinition classDef = target as PlayerClassDefinition;
        if (classDef == null) return;
        string key = $"PlayerClassEditor_{classDef.name}_LevelBonus_{index}_Expanded";
        EditorPrefs.SetBool(key, isExpanded);
    }
    // --- End Foldout State Management ---
}
#endif