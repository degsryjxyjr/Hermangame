// File: Scripts/Data/ItemDefinition.cs
using UnityEngine;
using System; // Add if not already present
using System.Collections.Generic;
using System.Linq;

[CreateAssetMenu(menuName = "Game/Item")]
public class ItemDefinition : ScriptableObject
{

    [Header("Basic")]
    public string itemId; // Consider making this [SerializeField] private set; and providing a getter if you want to prevent accidental changes after creation
    public string displayName;
    public Sprite icon;
    
    public enum ItemType { Consumable, Equipment, Quest, Material }
    public ItemType itemType;
    
    [Header("Stacking")]
    public bool isStackable = false; // Note: Equipment is typically NOT stackable. Enforce this in logic if needed.
    public int maxStack = 1;

    [Header("Direct Effects (simple)")]
    // Apply simple stat changes directly
    [Header("Health mod directly adds/decreases health!")]
    public int healthModifier;

    [Header("These are true modifiers")]
    public int attackModifier;
    public int defenseModifier;
    public int magicModifier;

    [Header("Linked Abilities (Complex Effects)")]
    [Tooltip("List of abilities granted or used by this item.")]
    public List<AbilityDefinition> linkedAbilities = new List<AbilityDefinition>(); 
    
    [Header("Equipment")]
    public EquipmentSlot equipSlot; // Crucial for the new system
    // Consider if classes should restrict certain slots. That might be handled by UI logic or a check in InventoryService.
    public enum EquipmentSlot
    {
        None,       // For non-equippable items
        MainHand,   // Primary weapon/shield/tool
        OffHand,    // Secondary weapon/shield/tool (classes might restrict this)
        Head,       // Helmets
        Body,       // Armor/Clothes
        Accessory   // Rings, Amulets, Belts etc. (Consider if multiples are allowed)
        // Add more specific slots if needed (Feet, Hands, etc.)
    }

    [Header("Equipment Effect (Passive) (Can handle complex effects)")]
    [Tooltip("Drag a GameObject with an IEquipmentEffect script attached here. Defines passive bonuses.")]
    public List<GameObject> equipmentEffectSources = new List<GameObject>(); // Reference to a GameObject holding the effect script

    /// <summary>
    /// Gets the list of IEquipmentEffect scripts associated with this item.
    /// </summary>
    /// <returns>A list of IEquipmentEffect instances (can be empty).</returns>
    public List<IEquipmentEffect> GetEquipmentEffects()
    {
        List<IEquipmentEffect> effects = new List<IEquipmentEffect>();
        if (equipmentEffectSources != null)
        {
            foreach (var sourceGO in equipmentEffectSources)
            {
                if (sourceGO != null)
                {
                    var effect = sourceGO.GetComponent<IEquipmentEffect>();
                    if (effect != null)
                    {
                        effects.Add(effect);
                    }
                }
            }
        }
        return effects;
    }

    
    [Header("Visuals")]
    public GameObject modelPrefab;

    // Optional: Add validation in the editor to ensure Equipment items have a slot
    private void OnValidate()
    {
        if (itemType == ItemType.Equipment && equipSlot == EquipmentSlot.None)
        {
            Debug.LogWarning($"ItemDefinition '{name}' is marked as Equipment but has 'None' for EquipSlot. This might be an error.", this);
        }
        if (itemType != ItemType.Equipment && equipSlot != EquipmentSlot.None)
        {
            Debug.LogWarning($"ItemDefinition '{name}' is not Equipment but has EquipSlot '{equipSlot}'. This might be an error.", this);
        }
    }
}