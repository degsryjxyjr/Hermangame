using UnityEngine;

// Assets/Scripts/Core/Entity.cs
public abstract class Entity : MonoBehaviour
{
    [Header("Base Stats")]
    public int maxHealth;
    public int currentHealth;
    public int attackPower;
    
    [Header("Visuals")]
    public SpriteRenderer spriteRenderer;
    
    public Animator animator;

    public virtual void TakeDamage(int amount)
    {
        currentHealth -= amount;
        Debug.Log($"{name} took {amount} damage! Remaining HP: {currentHealth}");

        if (currentHealth <= 0)
            Die();
    }

    protected virtual void Die()
    {
        animator.Play("Death");
        // Handle death logic

        Destroy(gameObject, 0.5f);
    }
}
