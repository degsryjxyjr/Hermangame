using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Defines all properties and behaviors of an in-game ability, spell, or skill.
/// Create assets from: Create -> Game -> Ability
/// </summary>
[CreateAssetMenu(menuName = "Game/Ability")]
public class AbilityDefinition : ScriptableObject
{
    #region Basic Information
    [Header("Basic Information")]
    
    [Tooltip("The display name of this ability (shown in UI)")]
    public string abilityName = "New Ability";
    
    [Tooltip("Icon displayed in action bars and tooltips")]
    public Sprite icon;
    #endregion

    #region Resource Costs
    [Header("Resource Costs")]
    
    [Tooltip("How many action points this ability consumes when used")]
    [Min(0)]
    public int actionCost = 1;
    
    [Tooltip("Cooldown time in seconds before this ability can be used again")]
    [Min(0)]
    public float cooldown = 0f;
    #endregion

    #region Targeting
    [Header("Targeting")]
    
    [Tooltip("What kinds of targets this ability can be used on")]
    public List<TargetType> supportedTargetTypes = new List<TargetType> { TargetType.Self };
    
    [Tooltip("Maximum range from caster in game units (0 = self only)")]
    [Min(0)]
    public float range = 1f;
    
    [Tooltip("Area of effect radius in game units (0 = single target)")]
    [Min(0)]
    public float radius = 0f;
    #endregion

    #region Effects
    [Header("Effects")]
    
    [Tooltip("Base effect value (positive for damage, negative for healing)")]
    public int baseEffectValue = 10;
    
    [Tooltip("Type of damage or healing this ability applies")]
    public DamageType damageType = DamageType.Physical;
    
    [Tooltip("Percentage of caster's Attack stat added to damage (0.5 = 50% of Attack added)")]
    [Range(0f, 2f)]
    public float attackScaling = 0f;
    
    [Tooltip("Percentage of caster's Magic stat added to damage (0.5 = 50% of Magic added)")]
    [Range(0f, 2f)]
    public float magicScaling = 0f;
    #endregion

    #region Effect Logic
    [Header("Effect Logic")]
    
    [Tooltip("Prefab containing the IAbilityEffect component that implements this ability's behavior")]
    public GameObject effectLogicSource;
    #endregion

    #region Visuals & Feedback
    [Header("Visuals & Feedback")]
    
    [Tooltip("Visual effect prefab spawned when this ability is used")]
    public GameObject effectPrefab;
    
    [Tooltip("Animation trigger name to activate on the caster")]
    public string animationTrigger;
    #endregion

    #region Usage Context
    [Header("Usage Context")]
    
    [Tooltip("Can this ability be used outside of combat? (e.g. utility spells)")]
    public bool usableOutOfCombat = false;
    
    [Tooltip("Can this ability be used during combat?")]
    public bool usableInCombat = true;
    #endregion

    /// <summary>
    /// Valid target types for this ability
    /// </summary>
    public enum TargetType 
    { 
        /// <summary>Ability targets the caster themselves</summary>
        Self,
        /// <summary>Ability targets one friendly unit (may include caster)</summary>
        SingleAlly,
        /// <summary>Ability targets one hostile unit</summary>
        SingleEnemy,
        /// <summary>Ability affects an area (targeting handled by effect logic)</summary>
        Area,
        /// <summary>Ability affects all friendly units (may include caster)</summary>
        AllAllies,
        /// <summary>Ability affects all hostile units</summary>
        AllEnemies
    }

    /// <summary>
    /// Types of damage/healing this ability can deal
    /// </summary>
    public enum DamageType
    {
        /// <summary>Physical damage (reduced by Defense)</summary>
        Physical,
        /// <summary>Magic damage (reduced by Magic and a bit by Defense)</summary>
        Magic,
        /// <summary>Pure damage (bypasses all resistances)</summary>
        Pure
    }

    #region Public Methods
    /// <summary>
    /// Gets the IAbilityEffect component that implements this ability's behavior
    /// </summary>
    /// <returns>The effect component, or null if not found</returns>
    public IAbilityEffect GetEffectLogic()
    {
        if (effectLogicSource == null)
        {
            Debug.LogWarning($"{abilityName} has no effect logic source assigned");
            return null;
        }
        
        var effect = effectLogicSource.GetComponent<IAbilityEffect>();
        if (effect == null)
        {
            Debug.LogWarning($"{abilityName}'s effect logic source has no IAbilityEffect component");
        }
        return effect;
    }

    // Check if ability is from an item
    public bool IsItemAbility() {
        // Check if any item references this ability
        return InventoryService.Instance.allItems.Any(item => 
            item.linkedAbilities != null && 
            item.linkedAbilities.Contains(this));
    }

    /// <summary>
    /// Checks if this ability can target the caster themselves
    /// </summary>
    /// <returns>True if self-targeting is allowed</returns>
    public bool CanTargetSelf()
    {
        return supportedTargetTypes.Contains(TargetType.Self) ||
               supportedTargetTypes.Contains(TargetType.SingleAlly) ||
               supportedTargetTypes.Contains(TargetType.AllAllies);
    }

    /// <summary>
    /// Checks if this ability can target a specific ally
    /// </summary>
    /// <returns>True if single ally targeting is allowed</returns>
    public bool CanTargetSingleAlly()
    {
        return supportedTargetTypes.Contains(TargetType.SingleAlly);
    }

    /// <summary>
    /// Checks if this ability can target a specific enemy
    /// </summary>
    /// <returns>True if single enemy targeting is allowed</returns>
    public bool CanTargetSingleEnemy()
    {
        return supportedTargetTypes.Contains(TargetType.SingleEnemy);
    }
    #endregion
}