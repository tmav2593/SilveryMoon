using System;
using UnityEngine;
using UnityEngine.Events;
using InventorySystem;

/// <summary>
/// Example player stats that restorative items affect.
/// - Light/fuel behaviour is intentionally NOT handled here: the equipped lantern / light object is responsible for its own fuel, brightness and drain.
/// - PlayerStats only tracks Health and Hunger and exposes events for UI/other systems.
/// - When a lantern is equipped via EquipLantern(ItemDataSO), PlayerStats now raises an event so a world-object manager can spawn/attach the actual lantern prefab.
/// </summary>
public class PlayerStats : MonoBehaviour
{
    [Header("Max Values")]
    [SerializeField] int maxHealth = 100;
    [SerializeField] int maxHunger = 100;

    [Header("Current")]
    [SerializeField] int health = 100;
    [SerializeField] int hunger = 100;

    [Header("Equipped Items (references only)")]
    // The lantern/light object will manage its own behaviour (fuel, brightness, drain). PlayerStats only keeps a reference.
    public ItemDataSO equippedLantern;
    public ItemDataSO equippedTotem;

    // Events (C# events for code; UnityEvents for inspector wiring)
    public event Action<int, int> OnHealthChanged; // (newValue, delta)
    public event Action<int, int> OnHungerChanged; // (newValue, delta)

    // New event: invoked when a lantern item is equipped. Subscribers (e.g. a LightEquipmentManager) should spawn/attach the lantern GameObject.
    public event Action<ItemDataSO> OnLanternEquipped;

    [Serializable] public class IntEvent : UnityEvent<int> { }
    public IntEvent onHealthChangedUnity;
    public IntEvent onHungerChangedUnity;

    // Public read-only accessors
    public int MaxHealth => maxHealth;
    public int MaxHunger => maxHunger;
    public int Health => health;
    public int Hunger => hunger;

    /// <summary>
    /// Applies health (positive to heal, negative to damage).
    /// Returns the actual applied delta (can be less than requested if clamped).
    /// </summary>
    public int ApplyHealth(int amount)
    {
        int old = health;
        health = Mathf.Clamp(health + amount, 0, maxHealth);
        int delta = health - old;
        if (delta != 0)
        {
            OnHealthChanged?.Invoke(health, delta);
            onHealthChangedUnity?.Invoke(health);
        }

        Debug.Log($"ApplyHealth({amount}) => delta={delta} health={health}/{maxHealth}");
        return delta;
    }

    /// <summary>
    /// Applies hunger (positive to restore hunger).
    /// Returns the actual applied delta.
    /// </summary>
    public int ApplyHunger(int amount)
    {
        int old = hunger;
        hunger = Mathf.Clamp(hunger + amount, 0, maxHunger);
        int delta = hunger - old;
        if (delta != 0)
        {
            OnHungerChanged?.Invoke(hunger, delta);
            onHungerChangedUnity?.Invoke(hunger);
        }

        Debug.Log($"ApplyHunger({amount}) => delta={delta} hunger={hunger}/{maxHunger}");
        return delta;
    }

    /// <summary>
    /// Convenience: damage health (positive amount).
    /// </summary>
    public int TakeDamage(int amount)
    {
        if (amount <= 0) return 0;
        return ApplyHealth(-amount);
    }

    /// <summary>
    /// Equip a lantern ItemDataSO. The lantern object itself should handle light/fuel behaviour.
    /// This method assigns the reference and raises OnLanternEquipped so a LightEquipmentManager can spawn/attach the world object.
    /// </summary>
    public void EquipLantern(ItemDataSO lantern)
    {
        if (lantern == null || lantern.category != ItemCategory.LightEquipment)
        {
            Debug.LogWarning("Attempted to equip non-lantern item.");
            return;
        }

        equippedLantern = lantern;
        Debug.Log($"Equipped lantern (reference only): {lantern.itemName}. Lantern object should manage fuel/brightness.");

        // Notify listeners (e.g., LightEquipmentManager) to spawn/attach a lantern object in the world
        OnLanternEquipped?.Invoke(lantern);
    }

    /// <summary>
    /// Equip a totem (placeholder for psychic-power source).
    /// </summary>
    public void EquipTotem(ItemDataSO totem)
    {
        if (totem == null || totem.category != ItemCategory.Totem)
        {
            Debug.LogWarning("Attempted to equip non-totem item.");
            return;
        }

        equippedTotem = totem;
        Debug.Log($"Equipped totem {totem.itemName} (effect: {totem.totemEffectTag})");
    }

    /// <summary>
    /// Utility helpers for UI: normalized 0..1 values
    /// </summary>
    public float NormalizedHealth() => maxHealth > 0 ? (float)health / maxHealth : 0f;
    public float NormalizedHunger() => maxHunger > 0 ? (float)hunger / maxHunger : 0f;
}