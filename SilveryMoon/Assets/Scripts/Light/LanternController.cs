using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using InventorySystem;

/// <summary>
/// World object that represents a lantern/light the player can equip.
/// - Implements Inventory.ILantern so Inventory.UseItem can call AddFuel on it.
/// - Manages its own fuel, brightness and drain logic.
/// - Uses a float accumulator for fuel so per-frame fractional drain doesn't get rounded away.
/// - Supports explicit player control: TurnOn(), TurnOff(), Toggle().
/// - Emits events when fuel or lit-state changes so UI can subscribe.
/// </summary>
[RequireComponent(typeof(Light))]
public class LanternController : MonoBehaviour, Inventory.ILantern
{
    [Header("References")]
    public Light unityLight;                 // main Unity Light used for brightness/range
    public ParticleSystem flameParticles;    // optional flame VFX to enable when lit

    [Header("Fuel")]
    public int maxFuel = 100;
    [SerializeField] int currentFuel = 0;     // integer view for editors/UI
    float fuelFloat = 0f;                     // internal fractional fuel accumulator

    [Header("Consumption")]
    [Tooltip("Base fuel consumption per second (before applying fuel efficiency).")]
    public float baseConsumptionPerSecond = 1f;
    // fuelEfficiencyMultiplier reduces consumption: higher means slower drain
    float fuelEfficiencyMultiplier = 1f;

    [Header("Brightness")]
    [Tooltip("Multiplier applied to light intensity when lit.")]
    public float brightnessMultiplier = 1f;

    [Header("Player control")]
    [Tooltip("If true the lantern will start lit when initialized (if it has fuel).")]
    public bool startLitOnInitialize = true;

    // whether the player has turned the lantern on (independent from fuel)
    bool playerRequestedOn = false;

    // Expose whether lit (true if player requested on AND fuel present)
    public bool IsLit => playerRequestedOn && fuelFloat > 0f;

    // Events for external subscribers (UI, managers)
    public event Action<int, int> OnFuelChanged;     // (currentFuel, maxFuel)
    public event Action<bool> OnLitStateChanged;     // (isLit)
    public event Action OnFuelDepletedEvent;

    bool lastLitState;

    void Reset()
    {
        unityLight = GetComponent<Light>();
    }

    void Awake()
    {
        if (unityLight == null) unityLight = GetComponent<Light>();
        // Ensure internal float matches serialized currentFuel on awake (useful for inspector defaults)
        fuelFloat = Mathf.Clamp(currentFuel, 0, maxFuel);
        lastLitState = IsLit;
        // Notify initial state
        OnFuelChanged?.Invoke(currentFuel, maxFuel);
        OnLitStateChanged?.Invoke(lastLitState);
        UpdateVisuals();
    }

    void Update()
    {
        if (IsLit)
        {
            // drain fuel based on base consumption scaled by efficiency
            float consumptionPerSecond = baseConsumptionPerSecond / Mathf.Max(0.0001f, fuelEfficiencyMultiplier);
            float deltaFuel = consumptionPerSecond * Time.deltaTime;

            fuelFloat = Mathf.Max(0f, fuelFloat - deltaFuel);

            // update integer view only when it changes
            int newCurrentFuel = Mathf.FloorToInt(fuelFloat);
            if (newCurrentFuel != currentFuel)
            {
                currentFuel = newCurrentFuel;
                OnFuelChanged?.Invoke(currentFuel, maxFuel);
            }

            if (fuelFloat <= 0f)
            {
                fuelFloat = 0f;
                currentFuel = 0;
                OnFuelDepleted();
            }

            UpdateVisuals();
            CheckLitStateChange();
        }
    }

    /// <summary>
    /// Adds fuel to the lantern. Called by Inventory when using a Light restorative item.
    /// </summary>
    public void AddFuel(int amount)
    {
        if (amount <= 0) return;
        fuelFloat = Mathf.Clamp(fuelFloat + amount, 0f, maxFuel);
        int old = currentFuel;
        currentFuel = Mathf.FloorToInt(fuelFloat);
        if (currentFuel != old)
            OnFuelChanged?.Invoke(currentFuel, maxFuel);

        // If player previously requested on but there was no fuel, turning on will be possible now.
        CheckLitStateChange();
        UpdateVisuals();
        Debug.Log($"Lantern: Added {amount} fuel. Now {currentFuel}/{maxFuel} (float={fuelFloat:F2}).");
    }

    /// <summary>
    /// Initialize lantern behaviour from an ItemDataSO (LightEquipment).
    /// Should be called immediately after instantiating the prefab.
    /// </summary>
    public void Initialize(ItemDataSO item)
    {
        if (item == null) return;

        brightnessMultiplier = Mathf.Max(0.01f, item.brightnessValue / 50f);
        fuelEfficiencyMultiplier = Mathf.Max(0.01f, item.fuelEfficiency / 50f);

        maxFuel = Mathf.Clamp(100 + item.fuelEfficiency, 10, 1000);

        // If fuelFloat is zero, optionally start with a small amount or leave empty:
        if (fuelFloat <= 0f)
            fuelFloat = Mathf.Min(10f, maxFuel);

        currentFuel = Mathf.FloorToInt(fuelFloat);

        // Set initial on/off depending on configuration
        playerRequestedOn = startLitOnInitialize && fuelFloat > 0f;

        // notify listeners about initial values
        OnFuelChanged?.Invoke(currentFuel, maxFuel);
        CheckLitStateChange();
        UpdateVisuals();
    }

    void UpdateVisuals()
    {
        bool lit = IsLit;

        if (unityLight != null)
        {
            unityLight.enabled = lit;
            float fuelRatio = maxFuel > 0 ? (float)currentFuel / maxFuel : 0f;
            unityLight.intensity = brightnessMultiplier * (0.5f + 0.5f * fuelRatio);
            unityLight.range = Mathf.Clamp(5f * brightnessMultiplier, 1f, 50f) + 10f * fuelRatio;
        }

        if (flameParticles != null)
        {
            var em = flameParticles.emission;
            em.enabled = lit;
            if (lit)
            {
                var main = flameParticles.main;
                main.startSize = 0.1f + 0.2f * brightnessMultiplier;
                main.startColor = Color.Lerp(Color.yellow, Color.white, Mathf.Clamp01(brightnessMultiplier / 2f));
            }
        }
    }

    void OnFuelDepleted()
    {
        Debug.Log("Lantern: Fuel depleted.");
        OnFuelDepletedEvent?.Invoke();
        UpdateVisuals();
        CheckLitStateChange();
    }

    void CheckLitStateChange()
    {
        bool currentLit = IsLit;
        if (currentLit != lastLitState)
        {
            lastLitState = currentLit;
            OnLitStateChanged?.Invoke(currentLit);
        }
    }

    /// <summary>
    /// Returns current fuel amount (integer) for UI.
    /// </summary>
    public int GetCurrentFuel() => currentFuel;

    /// <summary>
    /// Optionally force set fuel (useful for save/load)
    /// </summary>
    public void SetFuel(int amount)
    {
        fuelFloat = Mathf.Clamp(amount, 0, maxFuel);
        int old = currentFuel;
        currentFuel = Mathf.FloorToInt(fuelFloat);
        if (currentFuel != old)
            OnFuelChanged?.Invoke(currentFuel, maxFuel);
        CheckLitStateChange();
        UpdateVisuals();
    }

    public void TurnOn()
    {
        if (fuelFloat <= 0f)
        {
            Debug.Log("Lantern: Cannot turn on - no fuel.");
            playerRequestedOn = false;
            CheckLitStateChange();
            UpdateVisuals();
            return;
        }

        playerRequestedOn = true;
        CheckLitStateChange();
        UpdateVisuals();
    }

    public void TurnOff()
    {
        playerRequestedOn = false;
        CheckLitStateChange();
        UpdateVisuals();
    }

    public void Toggle()
    {
        if (playerRequestedOn) TurnOff(); else TurnOn();
    }
}