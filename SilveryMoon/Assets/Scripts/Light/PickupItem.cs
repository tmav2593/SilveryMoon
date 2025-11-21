using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using InventorySystem;

/// <summary>
/// Attach to a world pickup object (or the worldPrefab assigned on ItemDataSO).
/// - Configure the `item` (ItemDataSO) and optionally a pickup sound/effect.
/// - On trigger with a GameObject that has an Inventory component, attempts to add the item to the inventory.
/// - If AddItem succeeds, the pickup object is destroyed (or optionally disabled) and a log is printed.
/// - If inventory is full, nothing is removed and a warning is logged (you can add UI feedback).
/// </summary>
[RequireComponent(typeof(Collider))]
public class PickupItem : MonoBehaviour
{
    [Tooltip("Item asset this pickup represents.")]
    public ItemDataSO item;

    [Tooltip("How many units to add when picked up.")]
    public int amount = 1;

    [Tooltip("If true, pickup only works on trigger enter with 'Player' tag (optional).")]
    public bool requirePlayerTag = true;

    [Tooltip("Optional: play this AudioClip on pickup.")]
    public AudioClip pickupSound;

    AudioSource audioSource;

    void Awake()
    {
        // ensure collider is trigger
        var c = GetComponent<Collider>();
        if (!c.isTrigger) c.isTrigger = true;

        if (pickupSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.clip = pickupSound;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (requirePlayerTag && other.gameObject.tag != "Player") return;

        // look for Inventory on the colliding object or its parents
        var inventory = other.GetComponentInChildren<Inventory>();
        if (inventory == null)
        {
            // also try parent (if collider belongs to child)
            inventory = other.GetComponentInParent<Inventory>();
        }

        if (inventory == null)
        {
            Debug.LogWarning("PickupItem: No Inventory found on colliding object. Make sure player has an Inventory component.");
            return;
        }

        if (item == null)
        {
            Debug.LogWarning("PickupItem: No ItemDataSO assigned to pickup.");
            return;
        }

        bool added = inventory.AddItem(item, amount);
        if (added)
        {
            if (audioSource != null)
                audioSource.Play();

            Debug.Log($"Picked up {amount}x {item.itemName}.");

            // Optionally show UI feedback here before destroying; for simplicity we destroy immediately.
            Destroy(gameObject);
        }
        else
        {
            Debug.LogWarning($"Could not add {item.itemName} to inventory (full).");
            // Optionally show "inventory full" UI. Do not destroy pickup.
        }
    }
}