using UnityEngine;
using System.Collections.Generic; // Needed for List<T>

[CreateAssetMenu(menuName = "Game/Ability")]
public class AbilityDefinition : ScriptableObject
{
    [Header("Basic Info")]
    public string abilityName;
    public Sprite icon;
    
    [Header("Resource Costs")]
    public int manaCost;
    public float cooldown; // Global or per-target? Consider this.

    [Header("Targeting")]
    [Tooltip("List of target types this ability can be used on. An empty list means targeting is handled specially or is implicit (like Self for potions).")]
    public List<TargetType> supportedTargetTypes = new List<TargetType> { TargetType.Self }; // Default to Self

    // Consider if you need separate range/radius for different target types
    // For now, keeping it simple
    public float range = 1f; // Effective range for selecting targets
    public float radius = 0f; // Radius for Area effects

    [Header("Effects")]
    [Tooltip("The core numerical effect. Negative values typically indicate healing.")]
    public int baseEffectValue; // Renamed from baseDamage for clarity

    [Header("Visuals & Animation")]
    public GameObject effectPrefab;
    public string animationTrigger;

    [Header("Usage Context")]
    public bool usableOutOfCombat = false;
    public bool usableInCombat = true;


    // --- Target Type Enum ---
    // Keep this comprehensive
    public enum TargetType 
    { 
        Self,           // Caster on themselves
        SingleAlly,     // One friendly target
        SingleEnemy,    // One hostile target
        Area,           // Area around a point (can be ally/enemy based on other logic or another field)
        AllAllies,      // All friendly targets
        AllEnemies      // All hostile targets
        // Add more as needed (e.g., DeadAlly for resurrection?)
    }

    // --- Helper Methods (Optional but useful) ---

    /// <summary>
    /// Checks if this ability supports targeting the caster themselves.
    /// </summary>
    public bool CanTargetSelf()
    {
        return supportedTargetTypes.Contains(TargetType.Self) || 
               supportedTargetTypes.Contains(TargetType.SingleAlly) || // Assuming caster is a valid SingleAlly target
               supportedTargetTypes.Contains(TargetType.Area) || // Caster can target self's location for area
               supportedTargetTypes.Contains(TargetType.AllAllies); // Caster is part of AllAllies
        // Adjust logic based on your specific game rules.
        // E.g., maybe AllAllies explicitly excludes self? Then remove that check.
    }

    /// <summary>
    /// Checks if this ability supports targeting a single ally (other than self).
    /// </summary>
    public bool CanTargetSingleAlly()
    {
        return supportedTargetTypes.Contains(TargetType.SingleAlly);
    }

    /// <summary>
    /// Checks if this ability supports targeting a single enemy.
    /// </summary>
    public bool CanTargetSingleEnemy()
    {
        return supportedTargetTypes.Contains(TargetType.SingleEnemy);
    }

    // Add similar helpers for other target types as needed.
}