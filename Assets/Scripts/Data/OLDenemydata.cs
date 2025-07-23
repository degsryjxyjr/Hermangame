
// Assets/Data/Entities/EnemyData.cs
using UnityEngine;

[CreateAssetMenu(fileName = "Enemy_", menuName = "Data/Entities/Enemy")]
public class EnemyData : ScriptableObject
{
    public string enemyName;
    public Sprite sprite;
    public GameObject prefab;
    
    [Header("Combat Stats")]
    public int health;
    public int damage;
    public int xpReward;
}