using UnityEngine;

// Scripts/Gameplay/Entities/IHealable.cs
public interface IHealable : IEntity
{
    int ReceiveHealing(int amount); // Returns effective heal?
    bool IsAlive(); // Might be shared with IDamageable
}