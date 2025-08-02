using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewLootTable", menuName = "Game/Loot Table")]
public class LootTable : ScriptableObject
{

    [Header("Loot Table Settings")]
    public string tableName; // For easy identification in the editor
    [Tooltip("Minimum Party.Level required for items in this table to appear in the shop.")]
    public int minLevelRequirement = 1;

    [Header("Loot Entries")]
    [SerializeField] private List<ItemDefinition> entries = new List<ItemDefinition>();

    // Provide read-only access to entries
    public IReadOnlyList<ItemDefinition> Entries => entries;
}