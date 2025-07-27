using UnityEngine;

// Scripts/Gameplay/Entities/IDamageable.cs
public interface IDamageable // Often makes sense to inherit from IEntity
{
    int TakeDamage(int amount, AbilityDefinition.DamageType type = AbilityDefinition.DamageType.Physical); // Returns damage taken/remaining?
    bool IsAlive(); // Might be shared with IDamageable
}