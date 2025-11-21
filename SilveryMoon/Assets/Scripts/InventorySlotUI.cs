// Place in Assets/Scripts/InventorySlotUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI component for a single inventory slot (attach to your slot prefab).
/// The prefab should contain:
/// - Image iconImage
/// - Text countText (to show stack count)
/// - Button rootButton (to receive clicks)
/// This script expects Setup/SetupEmpty calls to pass the global index, so clicks map to the correct global slot.
/// </summary>
public class InventorySlotUI : MonoBehaviour
{
    public Image iconImage;
    public TMP_Text countText;
    public Button rootButton;

    int slotIndex; // global index across inventory capacity
    InventoryUI parentUI;

    InventorySlot currentSlot;

    void Awake()
    {
        if (rootButton != null)
            rootButton.onClick.AddListener(OnClicked);
    }

    public void SetupEmpty(int globalIndex, InventoryUI parent)
    {
        slotIndex = globalIndex;
        parentUI = parent;
        currentSlot = null;
        gameObject.SetActive(true);
        if (iconImage) iconImage.enabled = false;
        if (countText) countText.text = "";
    }

    public void Setup(InventorySlot slot, int globalIndex, InventoryUI parent)
    {
        slotIndex = globalIndex;
        parentUI = parent;
        currentSlot = slot;
        gameObject.SetActive(true);

        if (iconImage)
        {
            if (slot != null && slot.item != null && slot.item.icon != null)
            {
                iconImage.enabled = true;
                iconImage.sprite = slot.item.icon;
            }
            else
            {
                iconImage.enabled = false;
                iconImage.sprite = null;
            }
        }

        if (countText)
        {
            if (slot != null && slot.item != null && slot.item.stackable)
            {
                countText.text = slot.count > 1 ? slot.count.ToString() : "";
            }
            else
            {
                countText.text = "";
            }
        }
    }

    void OnClicked()
    {
        // notify parent with the local slot index translated in parent
        if (parentUI != null)
            parentUI.OnSlotClicked(slotIndex);
    }

    public InventorySlot GetSlot() => currentSlot;
}