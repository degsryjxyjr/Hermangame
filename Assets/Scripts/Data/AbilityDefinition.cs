using UnityEngine;

[CreateAssetMenu(menuName = "Game/Ability")]
public class AbilityDefinition : ScriptableObject
{
    public string abilityName;
    public Sprite icon;
    public float cooldown;
    public int manaCost;
    public TargetType targetType;
    public GameObject effectPrefab;
    
    public enum TargetType { Self, SingleEnemy, Area, AllEnemies }
    
    [Header("Stats")]
    public int baseDamage;
    public float range;
    public float radius; // For area effects
    
    [Header("Animation")]
    public string animationTrigger;
}