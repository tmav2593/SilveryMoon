using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using InventorySystem;

[CreateAssetMenu(fileName = "NewItem", menuName = "Inventory/ItemData", order = 0)]
public class ItemDataSO : ScriptableObject
{
    [Header("Basic")]
    public string itemName;
    public Sprite icon;
    [TextArea] public string description;
    public ItemCategory category;
    public bool stackable = true;
    public int maxStack = 99;

    [Header("Restorative (if category == Restorative)")]
    public RestorativeType restorativeType;
    public int healValue;

    [Header("Light Equipment (if category == LightEquipment)")]
    public int fuelEfficiency;
    public int brightnessValue;

    [Header("Totem")]
    public string totemEffectTag;

    public string GetSummary()
    {
        if (category == ItemCategory.Restorative || category == ItemCategory.Restorative) // fallback naming if needed
            return $"{itemName} ({restorativeType}) heals {healValue}";
        if (category == ItemCategory.LightEquipment)
            return $"{itemName} (Light) brightness {brightnessValue}, efficiency {fuelEfficiency}";
        return itemName;
    }
}