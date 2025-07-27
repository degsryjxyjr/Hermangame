// Scripts/Gameplay/Entities/IEntity.cs
using UnityEngine;

public interface IEntity : IDamageable, IHealable, IActionBudget
{
    string GetEntityName();
    Transform GetPosition(); // Or Transform, or a specific Location class
    // Add methods/events for state changes if needed generally
    // event System.Action OnEntityChanged; 
}