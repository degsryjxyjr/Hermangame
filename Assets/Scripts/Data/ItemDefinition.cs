// File: Scripts/Data/ItemDefinition.cs
using UnityEngine;
using System; // Add if not already present

[CreateAssetMenu(menuName = "Game/Item")]
public class ItemDefinition : ScriptableObject
{
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
    public int healthModifier;
    public int attackModifier;
    public int defenseModifier;
    public int magicModifier;

    [Header("Linked Ability (Complex Effects)")]
    // Link to an ability for complex effects
    public AbilityDefinition linkedAbility;
    
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
    
    [Header("Visuals")]
    public GameObject modelPrefab;

    // Optional: Add validation in the editor to ensure Equipment items have a slot
    private void OnValidate()
    {
        if (itemType == ItemType.Equipment && equipSlot == EquipmentSlot.None)
        {
            Debug.LogError($"ItemDefinition '{name}' is marked as Equipment but has 'None' for EquipSlot. This might be an error.", this);
        }
        if (itemType != ItemType.Equipment && equipSlot != EquipmentSlot.None)
        {
            Debug.LogError($"ItemDefinition '{name}' is not Equipment but has EquipSlot '{equipSlot}'. This might be an error.", this);
        }
    }
}