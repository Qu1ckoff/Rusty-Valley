using UnityEngine;
using System.Collections.Generic;

public class InteractionUIManager : MonoBehaviour
{
    [Header("Ссылки")]
    public PlayerPickupSystem pickupSystem;
    public Camera playerCamera;

    [Header("Настройки UI-позиционирования")]
    public float interactionDistance = 5f;
    [Tooltip("Насколько высоко над объектом висит плавающая подсказка")]
    public float floatHeightOffset = 0.5f;

    [Header("1. Подсказки при наведении (Hover -> Floating UI)")]
    public List<HoverMapping> hoverMappings;

    [Header("2. Подсказки при удержании (Held -> Corner UI)")]
    public List<HeldMapping> heldMappings;

    // --- Структуры для Инспектора ---

    [System.Serializable]
    public struct HoverMapping
    {
        public string name;
        public LayerMask targetMask;
        [Tooltip("Плавающая подсказка, появляется при наведении")]
        public GameObject floatingUI;
    }

    [System.Serializable]
    public struct HeldMapping
    {
        public string objectTag; // Ore, Building
        [Tooltip("Статичная угловая подсказка, появляется при удержании")]
        public GameObject cornerUI;
    }

    // --- Переменные состояния ---
    private GameObject currentActiveUI;
    private Collider currentHoverCollider;

    void Start()
    {
        if (playerCamera == null) playerCamera = Camera.main;
        if (pickupSystem == null) pickupSystem = GetComponent<PlayerPickupSystem>();

        HideAll();
    }

    void Update()
    {
        // 1. Проверяем, держим ли мы что-то в руках
        GameObject heldObj = pickupSystem.GetHeldObject();

        if (heldObj != null)
        {
            // --- СОСТОЯНИЕ: УДЕРЖАНИЕ ---
            HandleHeldState(heldObj);
        }
        else
        {
            // --- СОСТОЯНИЕ: НАВЕДЕНИЕ ---
            HandleHoverState();
        }
    }

    // --- Логика Удержания (Held) ---
    void HandleHeldState(GameObject heldObj)
    {
        currentHoverCollider = null;

        GameObject uiToShow = null;

        // Ищем подходящий UI по тегу предмета
        foreach (var map in heldMappings)
        {
            if (heldObj.CompareTag(map.objectTag))
            {
                uiToShow = map.cornerUI;
                break;
            }
        }

        // Активируем UI. updatePosition = false, т.к. это статичная подсказка в углу.
        ActivateUI(uiToShow, false);
    }

    // --- Логика Наведения (Hover) ---
    void HandleHoverState()
    {
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, interactionDistance))
        {
            CheckLayers(hit.collider);
        }
        else
        {
            HideAll();
        }
    }

    void CheckLayers(Collider hitCol)
    {
        bool matchFound = false;

        foreach (var map in hoverMappings)
        {
            // Проверка маски слоя
            if ((map.targetMask.value & (1 << hitCol.gameObject.layer)) != 0)
            {
                matchFound = true;
                currentHoverCollider = hitCol;

                // Активируем UI. updatePosition = true, т.к. это плавающая подсказка.
                ActivateUI(map.floatingUI, true);
                break;
            }
        }

        if (!matchFound) HideAll();
    }

    // --- Управление активацией и позицией ---
    void ActivateUI(GameObject ui, bool updatePosition)
    {
        // Сначала скрываем предыдущий UI, если он отличается от текущего
        if (currentActiveUI != ui)
        {
            if (currentActiveUI != null) currentActiveUI.SetActive(false);

            if (ui != null)
            {
                ui.SetActive(true);
                currentActiveUI = ui;
            }
        }

        // Если это плавающая подсказка, обновляем позицию
        if (ui != null && updatePosition && currentHoverCollider != null)
        {
            UpdateFloatingPosition(ui, currentHoverCollider);
        }
    }

    void UpdateFloatingPosition(GameObject ui, Collider target)
    {
        // Находим верхнюю точку объекта
        Vector3 worldPos = target.bounds.center;
        worldPos.y = target.bounds.max.y + floatHeightOffset;

        Vector3 screenPos = playerCamera.WorldToScreenPoint(worldPos);

        // Если объект перед камерой
        if (screenPos.z > 0)
        {
            ui.transform.position = screenPos;
        }
        else
        {
            // Скрываем, если объект за спиной, но UI активен
            ui.SetActive(false);
        }
    }

    void HideAll()
    {
        if (currentActiveUI != null)
        {
            currentActiveUI.SetActive(false);
            currentActiveUI = null;
        }
        currentHoverCollider = null;
    }
}