// HealthDisplay.cs
using TMPro;
using UnityEngine;

public class HealthDisplay : MonoBehaviour
{
    public Entity character;
    public TMP_Text healthText;
    
    void Update()
    {
        healthText.text = $"{character.name}: {character.currentHealth}/{character.maxHealth}";
    }
}