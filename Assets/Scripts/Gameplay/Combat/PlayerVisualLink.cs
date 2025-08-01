// File: Scripts/Gameplay/Combat/PlayerVisualLink.cs
using UnityEngine;
// --- MODIFIED: Use TMPro ---
using TMPro; // For TextMeshProUGUI
// --- END MODIFIED ---
using UnityEngine.UI; // For Slider
using System; // For EventHandler if using events

/// <summary>
/// A simple component to link a player's visual GameObject to its PlayerConnection data.
/// This allows visual scripts to access player information without needing direct references.
/// Also handles updating simple UI elements like name tags and health bars using TextMeshPro and Slider.
/// </summary>
public class PlayerVisualLink : MonoBehaviour
{
    [Header("UI References")]
    // --- MODIFIED: Change Text type ---
    [Tooltip("Drag the TextMeshProUGUI component that should display this player's name here.")]
    [SerializeField] private TextMeshProUGUI playerNameText; // Use TextMeshProUGUI
    // --- END MODIFIED ---

    [Tooltip("Drag the Slider component that represents this player's health bar here.")]
    [SerializeField] private Slider healthBarSlider; // Keep Slider for health bar

    [SerializeField]
    private PlayerConnection _linkedPlayerConnection;

    // --- OPTIONAL: Event Subscription (Assuming PlayerConnection has OnStatsUpdated) ---
    // Requires PlayerConnection to define: public event EventHandler OnStatsUpdated;
    // --- END OPTIONAL ---

    public PlayerConnection LinkedPlayerConnection => _linkedPlayerConnection;

    public void LinkToPlayer(PlayerConnection playerConnection)
    {
        // --- MODIFIED: Unsubscribe from old connection ---
        if (_linkedPlayerConnection != null)
        {
            // Assuming PlayerConnection has OnStatsUpdated event
            // _linkedPlayerConnection.OnStatsUpdated -= OnPlayerStatsUpdated;
        }
        // --- END MODIFIED ---

        _linkedPlayerConnection = playerConnection;

        // --- MODIFIED: Subscribe to new connection ---
        if (_linkedPlayerConnection != null)
        {
            this.name = $"PlayerVisual_{_linkedPlayerConnection.LobbyData?.Name ?? _linkedPlayerConnection.NetworkId ?? "Unknown"}";

            // Assuming PlayerConnection has OnStatsUpdated event
            // _linkedPlayerConnection.OnStatsUpdated += OnPlayerStatsUpdated; // Uncomment when event exists

            UpdateNameDisplay();
            UpdateHealthBar(); // Initialize health bar when linked
        }
        else
        {
            this.name = "PlayerVisual_Unlinked";
            // --- MODIFIED: Update UI for unlinked state ---
            if (playerNameText != null)
            {
                playerNameText.text = "Unlinked";
            }
            if (healthBarSlider != null)
            {
                 healthBarSlider.value = 0;
                 healthBarSlider.gameObject.SetActive(false); // Hide if unlinked
            }
            // --- END MODIFIED ---
        }
    }

    // --- MODIFIED: Updated for TextMeshProUGUI ---
    /// <summary>
    /// Updates the playerNameText component with the linked player's name.
    /// </summary>
    public void UpdateNameDisplay()
    {
        if (playerNameText != null && _linkedPlayerConnection != null)
        {
            playerNameText.text = _linkedPlayerConnection.LobbyData?.Name ?? "Unknown Player";
        }
        else if (playerNameText != null) // LinkedPlayerConnection is null
        {
             playerNameText.text = "Unlinked";
        }
        // If playerNameText is null, do nothing (component not assigned)
    }
    // --- END MODIFIED ---

    /// <summary>
    /// Updates the healthBarSlider component based on the linked player's current and max health.
    /// </summary>
    public void UpdateHealthBar()
    {
        if (healthBarSlider != null && _linkedPlayerConnection != null)
        {
            // Ensure MaxHealth is not zero to prevent division by zero
            float maxHealth = Mathf.Max(1, _linkedPlayerConnection.MaxHealth);
            float currentHealth = Mathf.Clamp(_linkedPlayerConnection.CurrentHealth, 0, maxHealth);

            // Calculate the normalized value (0.0 to 1.0)
            healthBarSlider.value = currentHealth / maxHealth;

            // Optional: Show/hide the health bar based on alive status or health
            healthBarSlider.gameObject.SetActive(true); // Always show for now, adjust as needed
        }
        else if (healthBarSlider != null) // LinkedPlayerConnection is null
        {
            healthBarSlider.value = 0;
            healthBarSlider.gameObject.SetActive(false); // Hide if no player linked
        }
        // If healthBarSlider is null, do nothing (component not assigned)
    }

    // --- OPTIONAL: Event Handler Method ---
    /// <summary>
    /// Called when the linked PlayerConnection's stats are updated.
    /// Assumes PlayerConnection fires an OnStatsUpdated event.
    /// </summary>
    private void OnPlayerStatsUpdated(object sender, EventArgs e)
    {
        // This method will be called whenever the PlayerConnection signals stat changes
        UpdateNameDisplay(); // Update name if needed (unlikely to change)
        UpdateHealthBar();   // Crucial: Update health bar when health changes
    }
    // --- END OPTIONAL ---

    private void OnDestroy()
    {
         // --- MODIFIED: Cleanup event subscription ---
        if (_linkedPlayerConnection != null)
        {
            // Assuming PlayerConnection has OnStatsUpdated event
            // _linkedPlayerConnection.OnStatsUpdated -= OnPlayerStatsUpdated; // Uncomment when event exists
        }
        // --- END MODIFIED ---
    }
}