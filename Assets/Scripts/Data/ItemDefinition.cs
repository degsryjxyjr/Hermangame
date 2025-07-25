using UnityEngine;
[CreateAssetMenu(menuName = "Game/Item")]
public class ItemDefinition : ScriptableObject
{
    public string itemId;
    public string displayName;
    public Sprite icon;
    
    public enum ItemType { Consumable, Equipment, Quest, Material }
    public ItemType itemType;
    
    [Header("Stacking")]
    public bool isStackable = false;
    public int maxStack = 1;

    [Header("Direct Effects (simple)")]
    // apply simple stat changes directl
    public int healthModifier;
    public int attackModifier;
    public int defenseModifier;
    public int magicModifier;

    [Header("Linked Ability (Complex Effects)")]
    // Link to an ability for complex effects
    public AbilityDefinition linkedAbility;
    
    [Header("Equipment")]
    public EquipmentSlot equipSlot;
    public enum EquipmentSlot { None, MainHand, OffHand, Head, Body, Accessory }
    
    [Header("Visuals")]
    public GameObject modelPrefab;
}