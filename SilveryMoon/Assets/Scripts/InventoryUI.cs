// Place in Assets/Scripts/InventoryUI.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Inventory UI manager with paging support.
/// - Shows only `slotsPerPage` slots at a time and lets player flip pages to view the rest.
/// - Each visible slot maps to a global slot index in the inventory (0..capacity-1).
/// - If a global index is < inventory.items.Count the slot shows an item, otherwise it's empty.
/// - Details panel is always visible. When nothing is selected it shows "None" for name/description and blanks the icon.
/// Setup:
///  - Assign inventory (your Inventory component).
///  - Assign slotPrefab (prefab with InventorySlotUI).
///  - Assign gridParent (Transform with GridLayoutGroup).
///  - Configure slotsPerPage to how many icons you want per page (e.g., 12).
///  - Assign prevPageButton/nextPageButton and pageNumberText to navigate pages.
///  - Hook details UI as before (detailsIcon, detailsName, detailsDescription, useButton).
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class InventoryUI : MonoBehaviour
{
    [Header("References")]
    public Inventory inventory;
    public GameObject slotPrefab; // prefab that has InventorySlotUI component
    public Transform gridParent;  // parent (with GridLayoutGroup) to instantiate slots under
    public PlayerStats playerStats; // optional, passed to Inventory.UseItem

    [Header("Paging")]
    [Tooltip("How many slots to show per page")]
    public int slotsPerPage = 12;
    [Tooltip("Optional previous/next page buttons")]
    public Button prevPageButton;
    public Button nextPageButton;
    [Tooltip("Optional UI text to show e.g. '1 / 3'")]
    public TMP_Text pageNumberText;

    [Header("Details Panel (always visible)")]
    public GameObject detailsPanel;
    public Image detailsIcon;
    public TMP_Text detailsName;
    public TMP_Text detailsDescription;
    public Button useButton;

    List<InventorySlotUI> slotUIs = new List<InventorySlotUI>();
    int currentPage = 0; // zero-based
    int totalPages = 1;
    int selectedGlobalIndex = -1;

    void Start()
    {
        if (inventory == null)
        {
            Debug.LogError("InventoryUI: Please assign an Inventory reference.");
            return;
        }

        if (slotPrefab == null || gridParent == null)
        {
            Debug.LogError("InventoryUI: slotPrefab and gridParent must be assigned.");
            return;
        }

        // clamp slotsPerPage
        slotsPerPage = Mathf.Max(1, slotsPerPage);

        // compute total pages based on inventory capacity (fixed-slot model)
        totalPages = Mathf.Max(1, Mathf.CeilToInt((float)inventory.capacity / slotsPerPage));

        // create the visible slots (only slotsPerPage objects)
        InitializeSlots();

        // subscribe
        inventory.OnInventoryChanged += RefreshUI;

        // wire page buttons
        if (prevPageButton != null) prevPageButton.onClick.AddListener(PrevPage);
        if (nextPageButton != null) nextPageButton.onClick.AddListener(NextPage);

        if (useButton != null)
            useButton.onClick.AddListener(OnUseButtonPressed);

        // Ensure details panel is visible and shows the "none" state initially
        if (detailsPanel != null) detailsPanel.SetActive(true);
        ShowNoneDetails();

        // initial refresh
        SetPage(0);
        RefreshUI();
    }

    void OnDestroy()
    {
        if (inventory != null) inventory.OnInventoryChanged -= RefreshUI;
        if (prevPageButton != null) prevPageButton.onClick.RemoveListener(PrevPage);
        if (nextPageButton != null) nextPageButton.onClick.RemoveListener(NextPage);
        if (useButton != null) useButton.onClick.RemoveListener(OnUseButtonPressed);
    }

    void InitializeSlots()
    {
        // Clear any previously-created runtime children under the gridParent
        foreach (Transform child in gridParent)
        {
            Destroy(child.gameObject);
        }

        slotUIs.Clear();

        for (int i = 0; i < slotsPerPage; i++)
        {
            GameObject go = Instantiate(slotPrefab, gridParent);
            go.name = $"Slot_{i}";
            var slotUI = go.GetComponent<InventorySlotUI>();
            if (slotUI == null)
            {
                Debug.LogError("InventoryUI: slotPrefab must have an InventorySlotUI component.");
                Destroy(go);
                continue;
            }
            // Initialize as empty; real global index will be set in RefreshUI (SetPage).
            slotUI.SetupEmpty(i, this);
            slotUIs.Add(slotUI);
        }
    }

    public void RefreshUI()
    {
        // Fill visible slot UI elements with current inventory data (based on current page)
        for (int local = 0; local < slotUIs.Count; local++)
        {
            int globalIndex = currentPage * slotsPerPage + local;
            if (globalIndex < inventory.items.Count)
            {
                slotUIs[local].Setup(inventory.items[globalIndex], globalIndex, this);
            }
            else
            {
                // If globalIndex < capacity but no item placed, show empty slot.
                if (globalIndex < inventory.capacity)
                    slotUIs[local].SetupEmpty(globalIndex, this);
                else
                    slotUIs[local].gameObject.SetActive(false); // beyond capacity, hide slot
            }
        }

        // If selection points to an item not on this page, clear selection details (keeps details panel visible)
        if (selectedGlobalIndex < 0)
        {
            ShowNoneDetails();
        }
        else
        {
            int pageOfSelection = selectedGlobalIndex / slotsPerPage;
            if (pageOfSelection != currentPage)
            {
                // deselect when switching away from selected item
                ShowNoneDetails();
            }
            else
            {
                // refresh details for selected
                ShowDetailsForGlobalIndex(selectedGlobalIndex);
            }
        }

        UpdatePagingControls();
    }

    void UpdatePagingControls()
    {
        // compute totalPages again in case capacity changed
        totalPages = Mathf.Max(1, Mathf.CeilToInt((float)inventory.capacity / slotsPerPage));
        currentPage = Mathf.Clamp(currentPage, 0, totalPages - 1);

        if (prevPageButton != null)
            prevPageButton.interactable = currentPage > 0;
        if (nextPageButton != null)
            nextPageButton.interactable = currentPage < totalPages - 1;
        if (pageNumberText != null)
            pageNumberText.text = $"{currentPage + 1} / {totalPages}";
    }

    public void OnSlotClicked(int globalIndex)
    {
        // Public API for direct global-index clicks
        selectedGlobalIndex = globalIndex;
        ShowDetailsForGlobalIndex(globalIndex);
    }

    // Called by InventorySlotUI when a slot is clicked. The slot provides its global index.
    public void OnSlotClickedLocal(int localSlotIndex)
    {
        int globalIndex = currentPage * slotsPerPage + localSlotIndex;
        OnSlotClicked(globalIndex);
    }

    void ShowDetailsForGlobalIndex(int globalIndex)
    {
        if (globalIndex < 0 || globalIndex >= inventory.capacity)
        {
            ShowNoneDetails();
            return;
        }

        InventorySlot slot = null;
        if (globalIndex < inventory.items.Count)
            slot = inventory.items[globalIndex];

        if (slot == null || slot.item == null)
        {
            ShowNoneDetails();
            return;
        }

        selectedGlobalIndex = globalIndex;

        // Keep details panel visible but populate fields with the selected item
        if (detailsPanel != null) detailsPanel.SetActive(true);

        if (detailsIcon != null)
        {
            detailsIcon.enabled = slot.item.icon != null;
            detailsIcon.sprite = slot.item.icon;
        }
        if (detailsName != null) detailsName.text = slot.item.itemName;
        if (detailsDescription != null) detailsDescription.text = slot.item.description;

        if (useButton != null)
            useButton.interactable = true;
    }

    void ShowNoneDetails()
    {
        selectedGlobalIndex = -1;
        if (detailsPanel != null) detailsPanel.SetActive(true);

        if (detailsIcon != null)
        {
            detailsIcon.sprite = null;
            detailsIcon.enabled = false; // hide the image when there's no icon
        }
        if (detailsName != null) detailsName.text = "None";
        if (detailsDescription != null) detailsDescription.text = "None";

        if (useButton != null)
            useButton.interactable = false;
    }

    void OnUseButtonPressed()
    {
        if (selectedGlobalIndex < 0 || selectedGlobalIndex >= inventory.items.Count) return;

        var slot = inventory.items[selectedGlobalIndex];
        if (slot == null || slot.item == null) return;

        bool used = inventory.UseItem(slot.item, playerStats);
        if (!used)
            Debug.LogWarning($"Failed to use item {slot.item.itemName}");
        else
        {
            // OnInventoryChanged will trigger RefreshUI via subscription
            // Clear selection if that item no longer exists
            if (selectedGlobalIndex >= inventory.items.Count)
                ShowNoneDetails();
        }
    }

    public void NextPage()
    {
        SetPage(currentPage + 1);
    }

    public void PrevPage()
    {
        SetPage(currentPage - 1);
    }

    public void SetPage(int pageIndex)
    {
        int maxPage = Mathf.Max(0, Mathf.CeilToInt((float)inventory.capacity / slotsPerPage) - 1);
        currentPage = Mathf.Clamp(pageIndex, 0, maxPage);
        RefreshUI();
    }
}