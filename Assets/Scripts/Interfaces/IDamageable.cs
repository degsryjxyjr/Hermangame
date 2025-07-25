using UnityEngine;

// Scripts/Gameplay/Entities/IDamageable.cs
public interface IDamageable : IEntity // Often makes sense to inherit from IEntity
{
    int TakeDamage(int amount, DamageType type = DamageType.Physical); // Returns damage taken/remaining?
    bool IsAlive();
    // event System.Action OnDeath;
}