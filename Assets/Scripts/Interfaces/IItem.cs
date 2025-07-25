// Scripts/Data/IItem.cs (or Scripts/Gameplay/Items/IItem.cs)
using UnityEngine;

public interface IItem
{
    // Data usually comes from the ScriptableObject
    ItemDefinition Definition { get; } 

    /// <summary>
    /// Attempts to use the item.
    /// </summary>
    /// <param name="user">The player using the item.</param>
    /// <returns>True if the item was consumed/used successfully, false otherwise.</returns>
    bool Use(PlayerConnection user);

    // Could add methods for equipping if logic is complex, 
    // though InventoryService might handle simple equip logic
    // bool Equip(PlayerManager.PlayerConnection user);
    // bool Unequip(PlayerManager.PlayerConnection user);
}