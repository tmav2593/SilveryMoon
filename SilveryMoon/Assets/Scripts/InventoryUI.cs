// Place in Assets/Scripts/InventoryUI.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

/// <summary>
/// Inventory UI manager with paging support and open/close toggling.
/// - Shows only `slotsPerPage` slots at a time and lets player flip pages to view the rest.
/// - Details panel always visible. When nothing is selected it shows "None" for name/description and blanks the icon.
/// - You can toggle the inventory with the configured input (new Input System InputActionReference) or fallback toggleKey.
/// - On close, if the selected item is LightEquipment it will be equipped on the player (via PlayerStats/LightEquipmentManager).
/// - Also exposes an explicit Equip button in the details panel.
/// - New: HUD-like sliders for HP, Hunger and currently equipped lantern fuel are exposed here while the inventory is open.
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
    public Button equipButton; // explicit equip button

    [Header("Open/Close")]
    [Tooltip("Optional InputActionReference (new Input System) for toggling the inventory UI (e.g. map to 'I' or gamepad menu). If null, fallback to toggleKey.")]
    public InputActionReference toggleAction;
    [Tooltip("Fallback keyboard key to toggle inventory if toggleAction is not assigned.")]
    public KeyCode toggleKey = KeyCode.I;
    public GameObject uiRoot; // top-level UI object to enable/disable (defaults to this.gameObject)

    [Header("PlayerInput (optional)")]
    public PlayerInput playerInput;

    [Header("HUD Sliders (inside Inventory UI)")]
    [Tooltip("Slider and optional text for Health (normalized 0..1, and numeric text).")]
    public Slider hpSlider;
    public TMP_Text hpText;
    [Tooltip("Slider and optional text for Hunger.")]
    public Slider hungerSlider;
    public TMP_Text hungerText;

    [Header("Equipped Lantern display (inside Inventory UI)")]
    [Tooltip("Slider and text for currently equipped lantern fuel. Hidden when no lantern is equipped.")]
    public Slider lanternFuelSlider;
    public TMP_Text lanternFuelText;
    [Tooltip("Single image used to represent lantern ON/OFF inside the inventory UI.")]
    public Image lanternStateIcon;
    public Sprite lanternOnSprite;
    public Sprite lanternOffSprite;

    [Header("Light manager (optional)")]
    [Tooltip("If not assigned, InventoryUI will try to find a LightEquipmentManager on the player.")]
    public LightEquipmentManager lightManager;

    // internals
    List<InventorySlotUI> slotUIs = new List<InventorySlotUI>();
    int currentPage = 0; // zero-based
    int totalPages = 1;
    int selectedGlobalIndex = -1;
    bool isOpen = false;

    // currently subscribed lantern controller (for fuel updates)
    LanternController subscribedLantern;

    void Awake()
    {
        if (uiRoot == null) uiRoot = this.gameObject;
    }

    void OnEnable()
    {
        if (toggleAction != null && toggleAction.action != null)
            toggleAction.action.performed += OnTogglePerformed;

        if (playerInput != null)
            playerInput.actions["ToggleInventory"].performed += OnTogglePerformed;
    }

    void OnDisable()
    {
        if (toggleAction != null && toggleAction.action != null)
            toggleAction.action.performed -= OnTogglePerformed;

        if (playerInput != null)
            playerInput.actions["ToggleInventory"].performed -= OnTogglePerformed;
    }

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

        if (equipButton != null)
            equipButton.onClick.AddListener(OnEquipButtonPressed);

        // Ensure details panel is visible and shows the "none" state initially
        if (detailsPanel != null) detailsPanel.SetActive(true);
        ShowNoneDetails();

        // initial refresh
        SetPage(0);
        RefreshUI();

        // start closed by default
        CloseInventory();

        // wire up playerStats events for health/hunger UI
        if (playerStats == null)
            playerStats = FindObjectOfType<PlayerStats>();

        if (playerStats != null)
        {
            playerStats.OnHealthChanged += OnHealthChanged;
            playerStats.OnHungerChanged += OnHungerChanged;

            // initialize sliders immediately
            UpdateHealthUIImmediate();
            UpdateHungerUIImmediate();
        }

        // find / subscribe to LightEquipmentManager
        if (lightManager == null)
        {
            if (playerStats != null)
                lightManager = playerStats.GetComponent<LightEquipmentManager>();

            if (lightManager == null)
                lightManager = FindObjectOfType<LightEquipmentManager>();
        }

        if (lightManager != null)
        {
            lightManager.OnLanternEquippedEvent += OnLanternEquipped;
            lightManager.OnLanternUnequippedEvent += OnLanternUnequipped;

            // If a lantern is already equipped at startup, subscribe to it
            var existing = lightManager.CurrentLantern;
            if (existing != null)
                SubscribeToLantern(existing);
            else
                UpdateLanternDisplayNone();
        }
        else
        {
            UpdateLanternDisplayNone();
        }

        // Ensure lantern UI hidden initially if no lantern
        if (lanternFuelSlider != null) lanternFuelSlider.gameObject.SetActive(subscribedLantern != null);
        if (lanternFuelText != null) lanternFuelText.gameObject.SetActive(subscribedLantern != null);
        if (lanternStateIcon != null) lanternStateIcon.gameObject.SetActive(subscribedLantern != null);
    }

    void OnDestroy()
    {
        // cleanup subscriptions
        if (inventory != null) inventory.OnInventoryChanged -= RefreshUI;
        if (playerStats != null)
        {
            playerStats.OnHealthChanged -= OnHealthChanged;
            playerStats.OnHungerChanged -= OnHungerChanged;
        }

        if (lightManager != null)
        {
            lightManager.OnLanternEquippedEvent -= OnLanternEquipped;
            lightManager.OnLanternUnequippedEvent -= OnLanternUnequipped;
        }

        UnsubscribeFromLantern();
    }

    void Update()
    {
        // fallback toggle with keyboard if no InputAction provided
        if (toggleAction == null || toggleAction.action == null)
        {
            if (Keyboard.current != null)
            {
                // if using new input system but no action provided, also allow KeyCode fallback
                if (Input.GetKeyDown(toggleKey))
                    ToggleInventory();
            }
            else
            {
                if (Input.GetKeyDown(toggleKey))
                    ToggleInventory();
            }
        }
    }

    void OnTogglePerformed(InputAction.CallbackContext ctx)
    {
        ToggleInventory();
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

        // Refresh HP/Hunger display even if unchanged (useful when opening)
        UpdateHealthUIImmediate();
        UpdateHungerUIImmediate();
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

        if (equipButton != null)
            equipButton.interactable = slot.item != null && slot.item.category == InventorySystem.ItemCategory.LightEquipment;
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

        if (equipButton != null)
            equipButton.interactable = false;
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

    void OnEquipButtonPressed()
    {
        if (selectedGlobalIndex < 0 || selectedGlobalIndex >= inventory.items.Count) return;

        var slot = inventory.items[selectedGlobalIndex];
        if (slot == null || slot.item == null) return;

        TryEquipItem(slot.item);
    }

    /// <summary>
    /// Try to equip the provided item (LightEquipment). Does NOT remove it from inventory.
    /// Delegates to PlayerStats.EquipLantern which fires OnLanternEquipped to spawn the world object.
    /// </summary>
    bool TryEquipItem(ItemDataSO item)
    {
        if (item == null) return false;
        if (playerStats == null)
        {
            playerStats = FindObjectOfType<PlayerStats>();
            if (playerStats == null)
            {
                Debug.LogWarning("InventoryUI: No PlayerStats available to equip item.");
                return false;
            }
        }

        if (item.category != InventorySystem.ItemCategory.LightEquipment)
        {
            Debug.LogWarning($"InventoryUI: Item '{item.itemName}' is not equippable light equipment.");
            return false;
        }

        playerStats.EquipLantern(item);
        Debug.Log($"InventoryUI: Equipped item '{item.itemName}' via PlayerStats.EquipLantern.");
        return true;
    }

    #region Health / Hunger UI handlers

    void OnHealthChanged(int newValue, int delta)
    {
        UpdateHealthUIImmediate();
    }

    void OnHungerChanged(int newValue, int delta)
    {
        UpdateHungerUIImmediate();
    }

    void UpdateHealthUIImmediate()
    {
        if (playerStats == null) return;
        float n = playerStats.NormalizedHealth();
        if (hpSlider != null)
        {
            hpSlider.value = Mathf.Clamp01(n);
            hpSlider.gameObject.SetActive(true);
        }
        if (hpText != null)
            hpText.text = $"{playerStats.Health} / {playerStats.MaxHealth}";
    }

    void UpdateHungerUIImmediate()
    {
        if (playerStats == null) return;
        float n = playerStats.NormalizedHunger();
        if (hungerSlider != null)
        {
            hungerSlider.value = Mathf.Clamp01(n);
            hungerSlider.gameObject.SetActive(true);
        }
        if (hungerText != null)
            hungerText.text = $"{playerStats.Hunger} / {playerStats.MaxHunger}";
    }

    #endregion

    #region Lantern subscription & UI

    void OnLanternEquipped(LanternController lantern)
    {
        SubscribeToLantern(lantern);
    }

    void OnLanternUnequipped()
    {
        UnsubscribeFromLantern();
        UpdateLanternDisplayNone();
    }

    void SubscribeToLantern(LanternController lantern)
    {
        UnsubscribeFromLantern();

        subscribedLantern = lantern;
        if (subscribedLantern == null)
        {
            UpdateLanternDisplayNone();
            return;
        }

        // show lantern UI
        if (lanternFuelSlider != null) lanternFuelSlider.gameObject.SetActive(true);
        if (lanternFuelText != null) lanternFuelText.gameObject.SetActive(true);
        if (lanternStateIcon != null) lanternStateIcon.gameObject.SetActive(true);

        // initialize
        UpdateLanternFuelDisplay(subscribedLantern.GetCurrentFuel(), subscribedLantern.maxFuel);
        UpdateLanternStateDisplay(subscribedLantern.IsLit);

        // subscribe events
        subscribedLantern.OnFuelChanged += OnLanternFuelChanged;
        subscribedLantern.OnLitStateChanged += OnLanternLitStateChanged;
        subscribedLantern.OnFuelDepletedEvent += OnLanternFuelDepleted;
    }

    void UnsubscribeFromLantern()
    {
        if (subscribedLantern != null)
        {
            subscribedLantern.OnFuelChanged -= OnLanternFuelChanged;
            subscribedLantern.OnLitStateChanged -= OnLanternLitStateChanged;
            subscribedLantern.OnFuelDepletedEvent -= OnLanternFuelDepleted;
            subscribedLantern = null;
        }
    }

    void OnLanternFuelChanged(int current, int max)
    {
        UpdateLanternFuelDisplay(current, max);
    }

    void OnLanternLitStateChanged(bool isLit)
    {
        UpdateLanternStateDisplay(isLit);
    }

    void OnLanternFuelDepleted()
    {
        UpdateLanternFuelDisplay(0, subscribedLantern != null ? subscribedLantern.maxFuel : 0);
        UpdateLanternStateDisplay(false);
    }

    void UpdateLanternFuelDisplay(int current, int max)
    {
        if (lanternFuelSlider != null)
        {
            float n = (max > 0) ? (float)current / max : 0f;
            lanternFuelSlider.value = Mathf.Clamp01(n);
            lanternFuelSlider.gameObject.SetActive(max > 0);
        }
        if (lanternFuelText != null)
        {
            lanternFuelText.text = $"{current} / {max}";
            lanternFuelText.gameObject.SetActive(max > 0);
        }
    }

    void UpdateLanternStateDisplay(bool isLit)
    {
        if (lanternStateIcon == null) return;

        if (isLit && lanternOnSprite != null)
        {
            lanternStateIcon.sprite = lanternOnSprite;
            lanternStateIcon.enabled = true;
        }
        else if (!isLit && lanternOffSprite != null)
        {
            lanternStateIcon.sprite = lanternOffSprite;
            lanternStateIcon.enabled = true;
        }
        else
        {
            lanternStateIcon.sprite = null;
            lanternStateIcon.enabled = false;
        }
    }

    void UpdateLanternDisplayNone()
    {
        if (lanternFuelSlider != null) lanternFuelSlider.gameObject.SetActive(false);
        if (lanternFuelText != null) lanternFuelText.gameObject.SetActive(false);
        if (lanternStateIcon != null) lanternStateIcon.gameObject.SetActive(false);
    }

    #endregion

    #region Open / Close / Toggle

    public void ToggleInventory()
    {
        if (isOpen) CloseInventory();
        else OpenInventory();
    }

    public void OpenInventory()
    {
        isOpen = true;
        if (uiRoot != null) uiRoot.SetActive(true);

        // refresh contents every time we open
        RefreshUI();

        // Optionally lock player controls here by disabling your player controller component(s).
        // If you want that behavior, assign playerStats.gameObject or specific controller and disable it here.
    }

    public void CloseInventory()
    {
        // On close we optionally equip the selected LightEquipment item automatically
        if (selectedGlobalIndex >= 0 && selectedGlobalIndex < inventory.items.Count)
        {
            var slot = inventory.items[selectedGlobalIndex];
            if (slot != null && slot.item != null && slot.item.category == InventorySystem.ItemCategory.LightEquipment)
            {
                TryEquipItem(slot.item);
            }
        }

        isOpen = false;
        if (uiRoot != null) uiRoot.SetActive(false);

        // Optionally re-enable player controls here if you disabled them on OpenInventory()
    }

    #endregion

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