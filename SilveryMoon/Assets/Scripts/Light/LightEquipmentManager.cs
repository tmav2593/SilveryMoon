using System;
using UnityEngine;
using InventorySystem;
using UnityEngine.InputSystem;

/// <summary>
/// Attach this to the Player GameObject.
/// - Subscribes to PlayerStats.OnLanternEquipped and spawns/attaches a LanternController prefab to the provided attachPoint.
/// - Prefers the ItemDataSO.worldPrefab when equipping; falls back to lanternPrefab if item.worldPrefab is null.
/// - Provides methods and input binding to toggle the equipped lantern on/off.
/// - Emits events when a lantern is equipped/unequipped so UI can show/hide HUD accordingly.
/// </summary>
public class LightEquipmentManager : MonoBehaviour
{
    [Header("References")]
    public PlayerStats playerStats;
    [Tooltip("Fallback prefab to instantiate for light equipment if ItemDataSO.worldPrefab is not assigned.")]
    public GameObject lanternPrefab;

    [Tooltip("Transform on the player where the lantern will be attached (e.g. hand or hip).")]
    public Transform attachPoint;

    [Header("Input (optional)")]
    [Tooltip("Optional InputActionReference to toggle the equipped lantern (new Input System).")]
    public InputActionReference toggleLanternAction;
    [Tooltip("Fallback key to toggle lantern if no InputAction is provided.")]
    public KeyCode toggleLanternKey = KeyCode.L;

    [Header("Equip behavior")]
    [Tooltip("If true, a newly equipped lantern will start switched on (if it has fuel).")]
    public bool equipStartsOn = true;

    // Current spawned lantern
    GameObject currentLanternGO;
    LanternController currentLantern;

    // Events for UI to subscribe to
    public event Action<LanternController> OnLanternEquippedEvent;
    public event Action OnLanternUnequippedEvent;

    void Reset()
    {
        if (playerStats == null)
            playerStats = GetComponent<PlayerStats>();
    }

    void OnEnable()
    {
        if (toggleLanternAction != null && toggleLanternAction.action != null)
            toggleLanternAction.action.performed += OnToggleLanternPerformed;
    }

    void OnDisable()
    {
        if (toggleLanternAction != null && toggleLanternAction.action != null)
            toggleLanternAction.action.performed -= OnToggleLanternPerformed;
    }

    void Start()
    {
        if (playerStats == null)
            playerStats = GetComponent<PlayerStats>();

        if (playerStats != null)
        {
            playerStats.OnLanternEquipped += OnLanternEquipped;
        }

        // If the player already has an equippedLantern reference (from save/Inspector), equip it
        if (playerStats != null && playerStats.equippedLantern != null)
        {
            EquipItem(playerStats.equippedLantern);
        }
    }

    void Update()
    {
        // Fallback keyboard toggle if no InputAction assigned
        if ((toggleLanternAction == null || toggleLanternAction.action == null) && Input.GetKeyDown(toggleLanternKey))
        {
            ToggleEquippedLantern();
        }
    }

    void OnDestroy()
    {
        if (playerStats != null)
            playerStats.OnLanternEquipped -= OnLanternEquipped;
    }

    void OnLanternEquipped(ItemDataSO item)
    {
        EquipItem(item);
    }

    /// <summary>
    /// Expose the current LanternController (may be null).
    /// </summary>
    public LanternController CurrentLantern => currentLantern;

    /// <summary>
    /// Equip an ItemDataSO (LightEquipment) by instantiating the lantern prefab and initializing it.
    /// If a lantern is already equipped it will be destroyed first.
    /// Uses item.worldPrefab if present, otherwise falls back to the configured lanternPrefab.
    /// </summary>
    public void EquipItem(ItemDataSO item)
    {
        if (item == null)
        {
            UnequipCurrent();
            return;
        }

        if (item.category != ItemCategory.LightEquipment)
        {
            Debug.LogWarning("LightEquipmentManager: Tried to equip an item that's not LightEquipment.");
            return;
        }

        // Remove existing lantern
        UnequipCurrent();

        // Choose prefab: prefer item.worldPrefab
        GameObject prefabToSpawn = item.worldPrefab != null ? item.worldPrefab : lanternPrefab;

        if (prefabToSpawn == null)
        {
            Debug.LogError("LightEquipmentManager: No prefab available to spawn for the lantern. Assign item.worldPrefab or set lanternPrefab as fallback.");
            return;
        }

        // Instantiate and parent under attachPoint (or under player if attachPoint is null)
        Transform parent = attachPoint != null ? attachPoint : this.transform;
        currentLanternGO = Instantiate(prefabToSpawn, parent);
        currentLanternGO.transform.localPosition = Vector3.zero;
        currentLanternGO.transform.localRotation = Quaternion.identity;

        currentLantern = currentLanternGO.GetComponent<LanternController>();
        if (currentLantern == null)
        {
            Debug.LogWarning("LightEquipmentManager: spawned prefab does not contain LanternController. If prefab should be a visual-only object, implement LanternController or remove the expectation.");
            // still keep the spawned object as a visual if desired
            OnLanternEquippedEvent?.Invoke(null);
            return;
        }

        // Initialize lantern from item stats
        currentLantern.Initialize(item);

        // Apply default on/off state
        if (equipStartsOn)
            currentLantern.TurnOn();
        else
            currentLantern.TurnOff();

        // Notify listeners (HUD) that a lantern has been equipped
        OnLanternEquippedEvent?.Invoke(currentLantern);

        Debug.Log($"LightEquipmentManager: Equipped lantern object for item '{item.itemName}'.");
    }

    /// <summary>
    /// Unequip and destroy the current lantern GameObject (if any).
    /// </summary>
    public void UnequipCurrent()
    {
        if (currentLantern != null)
        {
            // notify UI that lantern will be removed
            OnLanternUnequippedEvent?.Invoke();
        }

        if (currentLanternGO != null)
        {
            Destroy(currentLanternGO);
            currentLanternGO = null;
            currentLantern = null;
        }
    }

    /// <summary>
    /// If an equipped lantern exists, add fuel to it (forward to LanternController).
    /// </summary>
    public void AddFuelToEquippedLantern(int amount)
    {
        if (currentLantern != null)
            currentLantern.AddFuel(amount);
        else
            Debug.LogWarning("LightEquipmentManager: No lantern equipped to add fuel to.");
    }

    /// <summary>
    /// Toggle the equipped lantern on/off (player request).
    /// </summary>
    public void ToggleEquippedLantern()
    {
        if (currentLantern == null)
        {
            Debug.Log("LightEquipmentManager: No lantern equipped to toggle.");
            return;
        }

        currentLantern.Toggle();
    }

    /// <summary>
    /// Force turn on the equipped lantern (player request).
    /// </summary>
    public void TurnOnEquippedLantern()
    {
        if (currentLantern == null)
        {
            Debug.Log("LightEquipmentManager: No lantern equipped to turn on.");
            return;
        }

        currentLantern.TurnOn();
    }

    /// <summary>
    /// Force turn off the equipped lantern (player request).
    /// </summary>
    public void TurnOffEquippedLantern()
    {
        if (currentLantern == null)
        {
            Debug.Log("LightEquipmentManager: No lantern equipped to turn off.");
            return;
        }

        currentLantern.TurnOff();
    }

    void OnToggleLanternPerformed(InputAction.CallbackContext ctx)
    {
        ToggleEquippedLantern();
    }
}