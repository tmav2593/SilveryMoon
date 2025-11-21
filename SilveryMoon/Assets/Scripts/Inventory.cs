using System;
using System.Collections.Generic;
using UnityEngine;
using InventorySystem;

/// <summary>
/// Simple inventory component. Keeps a list of InventorySlot.
/// Methods: AddItem, RemoveItem, UseItem.
/// Uses ItemDataSO ScriptableObjects for item definitions.
/// Emits OnInventoryChanged whenever the inventory contents change.
/// </summary>
public class Inventory : MonoBehaviour
{
    public int capacity = 30;
    public List<InventorySlot> items = new List<InventorySlot>();

    /// <summary>
    /// Event invoked whenever the inventory contents change (item added/removed/used).
    /// UI should subscribe to this to refresh automatically.
    /// </summary>
    public event Action OnInventoryChanged;

    public interface ILantern
    {
        void AddFuel(int amount);
    }

    // Try to add item. Returns true if fully added, false if inventory full (or partially added not supported here).
    public bool AddItem(ItemDataSO item, int amount = 1)
    {
        if (item == null) return false;

        bool changed = false;

        // if stackable, try to find existing stack
        if (item.stackable)
        {
            foreach (var slot in items)
            {
                if (slot.item == item && slot.count < item.maxStack)
                {
                    int available = item.maxStack - slot.count;
                    int toAdd = Mathf.Min(available, amount);
                    slot.count += toAdd;
                    amount -= toAdd;
                    changed = changed || toAdd > 0;
                    if (amount <= 0)
                    {
                        OnInventoryChanged?.Invoke();
                        return true;
                    }
                }
            }
        }

        // add new slots while capacity allows
        while (amount > 0 && items.Count < capacity)
        {
            int toPut = item.stackable ? Mathf.Min(amount, item.maxStack) : 1;
            items.Add(new InventorySlot(item, toPut));
            amount -= toPut;
            changed = true;
        }

        if (changed) OnInventoryChanged?.Invoke();
        return amount == 0;
    }

    // Remove up to amount of item, returns true if fully removed, false if not enough items
    public bool RemoveItem(ItemDataSO item, int amount = 1)
    {
        if (item == null) return false;

        bool changed = false;

        for (int i = items.Count - 1; i >= 0 && amount > 0; i--)
        {
            var slot = items[i];
            if (slot.item == item)
            {
                int remove = Mathf.Min(slot.count, amount);
                slot.count -= remove;
                amount -= remove;
                changed = changed || remove > 0;
                if (slot.count <= 0) items.RemoveAt(i);
            }
        }

        if (changed) OnInventoryChanged?.Invoke();
        return amount == 0;
    }

    /// <summary>
    /// Use one item from inventory. Returns true if used/consumed successfully.
    /// - For Restorative.Health/Hunger: applies to PlayerStats (if present).
    /// - For Restorative.Light: tries to find an ILantern on the targetStats GameObject or this Inventory's GameObject (or children)
    ///   and calls AddFuel(healValue). If no lantern found, the item is consumed but a warning is logged.
    /// - For LightEquipment / Totem: equips by calling PlayerStats.EquipLantern / EquipTotem (these only store references).
    /// </summary>
    public bool UseItem(ItemDataSO item, PlayerStats targetStats = null)
    {
        if (item == null) return false;

        // ensure item exists in inventory and remove one copy
        if (!RemoveItem(item, 1))
        {
            Debug.LogWarning($"Attempted to use item '{item.itemName}' but it was not found in inventory.");
            return false;
        }

        // notify removal already invoked by RemoveItem

        // resolve target stats (default to component on same GameObject)
        if (targetStats == null)
            targetStats = GetComponent<PlayerStats>();

        // Handle category
        if (item.category == ItemCategory.Restorative)
        {
            switch (item.restorativeType)
            {
                case RestorativeType.Health:
                    if (targetStats != null)
                        targetStats.ApplyHealth(item.healValue);
                    else
                        Debug.LogWarning("No PlayerStats found to apply health restorative.");
                    break;

                case RestorativeType.Hunger:
                    if (targetStats != null)
                        targetStats.ApplyHunger(item.healValue);
                    else
                        Debug.LogWarning("No PlayerStats found to apply hunger restorative.");
                    break;

                case RestorativeType.Light:
                    // PlayerStats no longer manages light. Try to find a lantern-like component (ILantern) to accept fuel.
                    ILantern lantern = null;

                    // Prefer a lantern that's a child of the targetStats (if provided)
                    if (targetStats != null)
                        lantern = targetStats.GetComponentInChildren<ILantern>();

                    // If not found, fall back to looking on this Inventory's GameObject
                    if (lantern == null)
                        lantern = GetComponentInChildren<ILantern>();

                    if (lantern != null)
                    {
                        lantern.AddFuel(item.healValue);
                    }
                    else
                    {
                        Debug.LogWarning($"Consumed '{item.itemName}' (Light restorative) but no ILantern was found to receive fuel. You should implement ILantern on your lantern/light component.");
                    }
                    break;
            }
        }
        else if (item.category == ItemCategory.LightEquipment)
        {
            if (targetStats != null)
            {
                targetStats.EquipLantern(item);
            }
            else
            {
                Debug.LogWarning("No PlayerStats to equip lantern reference to. Item consumed but not equipped.");
            }
        }
        else if (item.category == ItemCategory.Totem)
        {
            if (targetStats != null)
            {
                targetStats.EquipTotem(item);
            }
            else
            {
                Debug.LogWarning("No PlayerStats to equip totem to. Item consumed but not equipped.");
            }
        }
        else
        {
            Debug.Log($"Used miscellaneous item '{item.itemName}'.");
        }

        // Inventory contents already changed via RemoveItem -> ensure UI/listeners are refreshed:
        OnInventoryChanged?.Invoke();
        return true;
    }
}