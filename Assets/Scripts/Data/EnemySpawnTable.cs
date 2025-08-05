using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewEnemySpawnTable", menuName = "Game/Enemy Spawn Table")]
public class EnemySpawnTable : ScriptableObject
{
    [System.Serializable]
    public class EnemyEntry
    {
        [Tooltip("The enemy definition that can spawn.")]
        public EnemyDefinition enemy;

        [Tooltip("The minimum quantity of this enemy type that can appear in an encounter.")]
        public int minQuantity = 1;

        [Tooltip("The maximum quantity of this enemy type that can appear in an encounter.")]
        public int maxQuantity = 1;

        [Tooltip("Weight for this enemy's chance to be selected during encounter generation. Higher is more likely.")]
        public float weight = 1.0f;
    }

    [Header("Spawn Table Settings")]
    public string tableName; // For easy identification in the editor

    [Tooltip("Minimum Party.Level required for enemies in this table to appear in encounters.")]
    public int minLevelRequirement = 1;

    [Header("Enemy Entries")]
    [SerializeField] private List<EnemyEntry> entries = new List<EnemyEntry>();

    // Provide read-only access to entries
    public IReadOnlyList<EnemyEntry> Entries => entries;
}