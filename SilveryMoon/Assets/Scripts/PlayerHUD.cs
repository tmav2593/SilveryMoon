using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// HUD binder that links PlayerStats (Health and Hunger) to UI Sliders and optional text fields.
/// - Subscribes to PlayerStats events (both C# events and UnityEvents) and updates UI when values change.
/// - Keeps sliders in 0..1 range (sets slider.minValue = 0, slider.maxValue = 1).
/// - Optionally smooths slider changes (tweak smoothingSpeed).
/// 
/// Usage:
/// - Add this component to a HUD GameObject (child of Canvas).
/// - Assign PlayerStats (or leave empty to auto-find a PlayerStats in scene).
/// - Assign healthSlider, hungerSlider (UnityEngine.UI.Slider).
/// - Optionally assign healthText/hungerText (TextMeshProUGUI) to show numeric values.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class PlayerHUD : MonoBehaviour
{
    [Header("References")]
    public PlayerStats playerStats;
    public Slider healthSlider;
    public Slider hungerSlider;
    public TMP_Text healthText;
    public TMP_Text hungerText;

    [Header("Smoothing (optional)")]
    public bool smooth = true;
    [Tooltip("Higher = faster following")]
    public float smoothingSpeed = 8f;

    // internal targets for smoothing
    float targetHealthNormalized = 0f;
    float targetHungerNormalized = 0f;

    void Start()
    {
        // Auto-find player stats if not assigned
        if (playerStats == null)
        {
            playerStats = FindObjectOfType<PlayerStats>();
            if (playerStats == null)
                Debug.LogWarning("PlayerHUD: No PlayerStats found in scene. Assign one in inspector.");
        }

        // Ensure sliders have 0..1 range
        if (healthSlider != null)
        {
            healthSlider.minValue = 0f;
            healthSlider.maxValue = 1f;
        }

        if (hungerSlider != null)
        {
            hungerSlider.minValue = 0f;
            hungerSlider.maxValue = 1f;
        }

        // Subscribe to events
        if (playerStats != null)
        {
            playerStats.OnHealthChanged += OnHealthChangedEvent;          // C# event (newValue, delta)
            playerStats.OnHungerChanged += OnHungerChangedEvent;          // C# event (newValue, delta)

            if (playerStats.onHealthChangedUnity != null)
                playerStats.onHealthChangedUnity.AddListener(OnHealthChangedUnity); // UnityEvent<int>

            if (playerStats.onHungerChangedUnity != null)
                playerStats.onHungerChangedUnity.AddListener(OnHungerChangedUnity); // UnityEvent<int>

            // initialize UI to current values
            UpdateHealthUIImmediate();
            UpdateHungerUIImmediate();
        }
    }

    void OnDestroy()
    {
        // Unsubscribe to avoid leaks / null-ref on domain reload
        if (playerStats != null)
        {
            playerStats.OnHealthChanged -= OnHealthChangedEvent;
            playerStats.OnHungerChanged -= OnHungerChangedEvent;

            if (playerStats.onHealthChangedUnity != null)
                playerStats.onHealthChangedUnity.RemoveListener(OnHealthChangedUnity);

            if (playerStats.onHungerChangedUnity != null)
                playerStats.onHungerChangedUnity.RemoveListener(OnHungerChangedUnity);
        }
    }

    void Update()
    {
        // Smooth slider value changes if enabled
        if (smooth)
        {
            if (healthSlider != null)
                healthSlider.value = Mathf.Lerp(healthSlider.value, targetHealthNormalized, 1f - Mathf.Exp(-smoothingSpeed * Time.deltaTime));

            if (hungerSlider != null)
                hungerSlider.value = Mathf.Lerp(hungerSlider.value, targetHungerNormalized, 1f - Mathf.Exp(-smoothingSpeed * Time.deltaTime));
        }
    }

    // Event handlers (C# events)
    void OnHealthChangedEvent(int newValue, int delta)
    {
        UpdateHealthUIImmediate();
    }

    void OnHungerChangedEvent(int newValue, int delta)
    {
        UpdateHungerUIImmediate();
    }

    // Event handlers (UnityEvents)
    void OnHealthChangedUnity(int newValue)
    {
        UpdateHealthUIImmediate();
    }

    void OnHungerChangedUnity(int newValue)
    {
        UpdateHungerUIImmediate();
    }

    // Update helpers
    void UpdateHealthUIImmediate()
    {
        if (playerStats == null) return;

        targetHealthNormalized = Mathf.Clamp01(playerStats.NormalizedHealth());
        if (!smooth && healthSlider != null) healthSlider.value = targetHealthNormalized;
        if (healthText != null) healthText.text = $"{playerStats.Health} / {playerStats.MaxHealth}";
    }

    void UpdateHungerUIImmediate()
    {
        if (playerStats == null) return;

        targetHungerNormalized = Mathf.Clamp01(playerStats.NormalizedHunger());
        if (!smooth && hungerSlider != null) hungerSlider.value = targetHungerNormalized;
        if (hungerText != null) hungerText.text = $"{playerStats.Hunger} / {playerStats.MaxHunger}";
    }

    /// <summary>
    /// Public API to force a refresh (useful after loading save data).
    /// </summary>
    public void RefreshAll()
    {
        UpdateHealthUIImmediate();
        UpdateHungerUIImmediate();
    }
}
