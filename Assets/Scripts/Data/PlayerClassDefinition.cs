using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
[CreateAssetMenu(menuName = "Game/Player/Class", fileName = "NewClass")]
public class PlayerClassDefinition : ScriptableObject
{
    [Header("Basic Info")]
    public string className;
    public Sprite classIcon;
    [TextArea] public string description;

    [Header("Base Stats (Level 1)")]
    public int baseHealth;
    public int baseAttack;
    public int baseDefense;
    public int baseMagic;
    public float attackRange = 1f;

    [Header("Action Budget  (Level 1)")]
    public int baseActions = 1;

    [Header("Visuals")]
    public GameObject characterPrefab;
    public AnimatorOverrideController animatorController;

    [Header("Starting Abilities & Equipment")]
    [Tooltip("Abilities the player starts with at Level 1.")]
    public List<AbilityDefinition> startingAbilities;
    [Tooltip("Items the player starts with at Level 1.")]
    public List<ItemDefinition> startingEquipment;

    // --- MODIFIED: Use List of the nested serializable class ---
    [Header("Progression - Level Up Bonuses")]
    [Tooltip("Bonuses granted at each level. Managed by the custom editor.")]
    // Use the nested class name
    public List<LevelUpBonusData> levelUpBonuses = new List<LevelUpBonusData>(); // Initialize the list

    // --- Helper Method (Updated) ---
    /// <summary>
    /// Gets the LevelUpBonusData for a specific level, if it exists.
    /// </summary>
    /// <param name="level">The target level.</param>
    /// <returns>The LevelUpBonusData, or null if not found for that level.</returns>
    public LevelUpBonusData GetBonusForLevel(int level)
    {
        if (levelUpBonuses == null) return null;
        return levelUpBonuses.FirstOrDefault(bonus => bonus != null && bonus.targetLevel == level);
    }

    // --- OPTIONAL: Custom Editor Validation/Sorting ---
    // Keep the OnValidate for sorting and basic cleanup
    #if UNITY_EDITOR
    private void OnValidate()
    {
        if (levelUpBonuses != null)
        {
            // Remove null entries
            levelUpBonuses.RemoveAll(b => b == null);

            // Sort by targetLevel (important for display and logic)
            levelUpBonuses.Sort((a, b) => a.targetLevel.CompareTo(b.targetLevel));

            // Ensure levels are sequential starting from 2 (Level 1 is starting stuff)
            // And that targetLevel is set correctly. This is tricky in OnValidate alone.
            // A custom PropertyDrawer or Editor script is better for full control.
            // Basic check:
            for(int i = 0; i < levelUpBonuses.Count; i++)
            {
                if(levelUpBonuses[i] != null)
                {
                     // This check is limited, a custom editor is better for ensuring sequential levels starting from 2
                     // and auto-incrementing when adding.
                     // OnValidate runs automatically but isn't great for complex UI logic like "Add Level" buttons.
                }
            }
        }
    }
    #endif
}


// --- NEW: Nested Serializable Class for Level Up Bonuses ---
/// <summary>
/// Defines the bonuses a player receives when leveling up to a specific level.
/// This is a serializable class, not a ScriptableObject, so it's embedded within PlayerClassDefinition.
/// </summary>
[System.Serializable] // Make sure this is marked as serializable
public class LevelUpBonusData
{
    [Header("Level Requirement")]
    [Tooltip("DO NOT EDIT MANUALLY. Set by PlayerClassDefinition editor script.")]
    // Hide this field in the inspector, or make it non-editable, as it should be managed by the editor script.
    // We will use a PropertyDrawer or custom editor to display it nicely and manage the level.
    [HideInInspector] 
    public int targetLevel = 1; // Default, will be managed by editor script

    [Header("Stat Modifiers")]
    public int maxHealthBonus = 0;
    public int attackBonus = 0;
    public int defenseBonus = 0;
    public int magicBonus = 0;
    public int actionBonus = 0;

    [Header("New Abilities")]
    [Tooltip("Abilities unlocked at this level.")]
    public List<AbilityDefinition> newAbilities;

    [Header("New Items")]
    [Tooltip("Items granted at this level.")]
    public List<ItemDefinition> grantedItems;

    // Optional: Constructor for editor script convenience
    public LevelUpBonusData(int level)
    {
        this.targetLevel = level;
        // Initialize lists
        this.newAbilities = new List<AbilityDefinition>();
        this.grantedItems = new List<ItemDefinition>();
    }
}
// --- END NEW CLASS ---