
// Assets/Data/Entities/PlayerData.cs
using UnityEngine;

[CreateAssetMenu(fileName = "Player_", menuName = "Data/Entities/Player")]
public class PlayerDataOLD : ScriptableObject
{
    public string className;
    public Sprite baseSprite;
    public GameObject prefab;
    
    [Header("Base Stats")]
    public int baseHealth;
    public int baseAttack;
    public int healthPerLevel;
}