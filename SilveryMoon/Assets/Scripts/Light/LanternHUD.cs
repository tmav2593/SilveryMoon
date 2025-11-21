using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// HUD component to display equipped lantern fuel and on/off state.
/// - Shows/hides itself when a lantern is equipped/unequipped.
/// - Subscribes to the LanternController events to update slider, text and on/off icon (single Image whose sprite switches).
/// - Assign LightEquipmentManager in the inspector (or auto-find on Start).
/// - Assign hudRoot (GameObject) that contains slider, text and icon so it can be hidden entirely.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class LanternHUD : MonoBehaviour
{
    [Header("References")]
    public LightEquipmentManager lightManager;
    public GameObject hudRoot;       // root object that will be shown/hidden when a lantern is equipped
    public Slider fuelSlider;        // normalized 0..1 slider
    public TMP_Text fuelText;        // "current / max"

    [Header("On/Off Icon (single image)")]
    [Tooltip("Single Image used to display either the 'on' or 'off' sprite depending on lantern state.")]
    public Image stateIcon;
    public Sprite onSprite;
    public Sprite offSprite;

    LanternController subscribedLantern;

    void Start()
    {
        if (lightManager == null)
            lightManager = FindObjectOfType<LightEquipmentManager>();

        if (hudRoot != null)
            hudRoot.SetActive(false);

        if (lightManager != null)
        {
            lightManager.OnLanternEquippedEvent += OnLanternEquipped;
            lightManager.OnLanternUnequippedEvent += OnLanternUnequipped;

            // If a lantern is already equipped at startup, subscribe to it
            var existing = lightManager.CurrentLantern;
            if (existing != null)
                SubscribeToLantern(existing);
        }
        else
        {
            Debug.LogWarning("LanternHUD: No LightEquipmentManager assigned/found.");
        }
    }

    void OnDestroy()
    {
        if (lightManager != null)
        {
            lightManager.OnLanternEquippedEvent -= OnLanternEquipped;
            lightManager.OnLanternUnequippedEvent -= OnLanternUnequipped;
        }

        UnsubscribeFromLantern();
    }

    void OnLanternEquipped(LanternController lantern)
    {
        if (lantern == null)
        {
            // show hudRoot but no data (visual-only prefab without LanternController)
            if (hudRoot != null) hudRoot.SetActive(true);
            ClearDisplay();
            return;
        }

        SubscribeToLantern(lantern);
    }

    void OnLanternUnequipped()
    {
        UnsubscribeFromLantern();
        if (hudRoot != null) hudRoot.SetActive(false);
    }

    void SubscribeToLantern(LanternController lantern)
    {
        UnsubscribeFromLantern();

        subscribedLantern = lantern;
        if (subscribedLantern == null) return;

        // Ensure HUD visible
        if (hudRoot != null) hudRoot.SetActive(true);

        // Initialize display
        UpdateFuelDisplay(subscribedLantern.GetCurrentFuel(), subscribedLantern != null ? subscribedLantern.maxFuel : 0);
        UpdateLitDisplay(subscribedLantern.IsLit);

        // Subscribe to events
        subscribedLantern.OnFuelChanged += OnFuelChanged;
        subscribedLantern.OnLitStateChanged += OnLitStateChanged;
        subscribedLantern.OnFuelDepletedEvent += OnFuelDepleted;
    }

    void UnsubscribeFromLantern()
    {
        if (subscribedLantern != null)
        {
            subscribedLantern.OnFuelChanged -= OnFuelChanged;
            subscribedLantern.OnLitStateChanged -= OnLitStateChanged;
            subscribedLantern.OnFuelDepletedEvent -= OnFuelDepleted;
            subscribedLantern = null;
        }
    }

    void OnFuelChanged(int current, int max)
    {
        UpdateFuelDisplay(current, max);
    }

    void OnLitStateChanged(bool isLit)
    {
        UpdateLitDisplay(isLit);
    }

    void OnFuelDepleted()
    {
        // ensure UI shows zero and 'off' icon
        UpdateFuelDisplay(0, subscribedLantern != null ? subscribedLantern.maxFuel : 0);
        UpdateLitDisplay(false);
    }

    void UpdateFuelDisplay(int current, int max)
    {
        if (fuelSlider != null)
        {
            float n = (max > 0) ? (float)current / max : 0f;
            fuelSlider.value = Mathf.Clamp01(n);
            fuelSlider.gameObject.SetActive(max > 0);
        }

        if (fuelText != null)
        {
            fuelText.text = $"{current} / {max}";
            fuelText.gameObject.SetActive(max > 0);
        }
    }

    void UpdateLitDisplay(bool isLit)
    {
        // Use a single Image (stateIcon) and swap its sprite based on isLit.
        if (stateIcon != null)
        {
            if (isLit && onSprite != null)
            {
                stateIcon.sprite = onSprite;
                stateIcon.enabled = true;
            }
            else if (!isLit && offSprite != null)
            {
                stateIcon.sprite = offSprite;
                stateIcon.enabled = true;
            }
            else
            {
                // If a sprite is missing, hide the icon to avoid showing stale graphics
                stateIcon.sprite = null;
                stateIcon.enabled = false;
            }
        }
    }

    void ClearDisplay()
    {
        // Called when there's no lantern controller to display data for.
        if (fuelSlider != null) fuelSlider.gameObject.SetActive(false);
        if (fuelText != null)
        {
            fuelText.text = "";
            fuelText.gameObject.SetActive(false);
        }
        if (stateIcon != null)
        {
            stateIcon.sprite = null;
            stateIcon.enabled = false;
        }
    }
}