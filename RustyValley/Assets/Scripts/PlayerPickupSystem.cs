using UnityEngine;
using System.Collections.Generic;

public class PlayerPickupSystem : MonoBehaviour
{
    [Header("Interaction")]
    public float interactDistance = 3f;
    public LayerMask interactMask; // слой(и) для взаимодействия (Building, Ore)

    [Header("Hold")]
    public Transform holdPoint;    // куда помещается объект визуально (для Ore или иконки)
    public Transform hiddenParent; // куда временно помещать picked Building (можно Player.transform)

    [HideInInspector]
    public float lastBuildingPickupTime = -10f; // время последнего подъёма здания

    private GameObject heldObject;
    private Camera cam;

    // Сохраняем исходные трансформы и состояния, чтобы можно было вернуть объект
    private Transform originalParent;
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private Vector3 originalScale;
    private List<Renderer> originalRenderers = new List<Renderer>();
    private List<Collider> originalColliders = new List<Collider>();
    private List<bool> originalRendererStates = new List<bool>();
    private List<bool> originalColliderStates = new List<bool>();

    void Start()
    {
        cam = Camera.main;
        if (hiddenParent == null) hiddenParent = transform; // по умолчанию — сам игрок
    }

    void Update()
    {
        HandlePickup();
        HandleDrop();
    }

    void HandlePickup()
    {
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f));

        if (!Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactMask))
            return;

        GameObject target = hit.collider.gameObject;

        // =======================
        // Подбор Building (ПКМ)
        // =======================
        if (Input.GetMouseButtonDown(1)) // ПКМ
        {
            // Если уже что-то держим — не берем новое
            if (heldObject)
                return;

            if (target.CompareTag("Building"))
            {
                PickUpBuilding(target);
                return;
            }
        }

        // =======================
        // Подбор Ore (E)
        // =======================
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (heldObject)
                return;

            if (target.CompareTag("Ore"))
            {
                PickUpOre(target);
                return;
            }
        }
    }

    void HandleDrop()
    {
        // Q — выбросить ore
        if (Input.GetKeyDown(KeyCode.Q))
        {
            if (heldObject == null)
                return;

            if (heldObject.CompareTag("Ore"))
                DropOre();
        }
    }

    // -------- Pick up building (для установки) --------
    void PickUpBuilding(GameObject obj)
    {
        // Сохраняем ссылку
        heldObject = obj;

        // Сохраняем исходные трансформы и parent
        originalParent = obj.transform.parent;
        originalPosition = obj.transform.position;
        originalRotation = obj.transform.rotation;
        originalScale = obj.transform.localScale;

        // Сохраняем рендереры и их состояния, а также коллайдеры
        originalRenderers.Clear();
        originalRenderers.AddRange(obj.GetComponentsInChildren<Renderer>(true));
        originalRendererStates.Clear();
        foreach (var r in originalRenderers)
            originalRendererStates.Add(r.enabled);

        originalColliders.Clear();
        originalColliders.AddRange(obj.GetComponentsInChildren<Collider>(true));
        originalColliderStates.Clear();
        foreach (var c in originalColliders)
            originalColliderStates.Add(c.enabled);

        // Отключаем рендеры и коллайдеры (объект "исчезает" из мира, но остаётся активным)
        foreach (var r in originalRenderers)
            r.enabled = false;
        foreach (var c in originalColliders)
            c.enabled = false;

        lastBuildingPickupTime = Time.time;

        Debug.Log("[Pickup] Building picked up: " + obj.name);

        // Перемещаем под hiddenParent, но сохраняем мировой масштаб
        obj.transform.SetParent(hiddenParent, true); // worldPositionStays = true
        // дополнительно, чтобы исключить изменение scale, явно восстановим
        obj.transform.localScale = originalScale;

        Debug.Log("[Pickup] Building picked up: " + obj.name);
    }

    // -------- Pick up ore (кладём в руку) --------
    void PickUpOre(GameObject obj)
    {
        heldObject = obj;

        // Отключаем физику и коллайдеры
        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb) rb.isKinematic = true;

        Collider col = obj.GetComponent<Collider>();
        if (col) col.enabled = false;

        // Перемещаем в holdPoint и закрепляем (для визуала)
        obj.transform.SetParent(holdPoint, true);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localRotation = Quaternion.identity;
        obj.transform.localScale = originalScale = obj.transform.localScale; // на всякий случай сохраняем

        Debug.Log("[Pickup] Ore picked up: " + obj.name);
    }

    // -------- Drop ore (Q) --------
    void DropOre()
    {
        // Помещаем чуть вперед от камеры
        heldObject.transform.SetParent(null, true);
        heldObject.transform.position = cam.transform.position + cam.transform.forward * 1f;

        Rigidbody rb = heldObject.GetComponent<Rigidbody>();
        if (rb) rb.isKinematic = false;

        Collider col = heldObject.GetComponent<Collider>();
        if (col) col.enabled = true;

        Debug.Log("[Drop] Ore dropped: " + heldObject.name);
        heldObject = null;
    }

    // -------- Восстановление picked building в исходную позицию (отмена) --------
    public void ReturnHeldBuildingToOriginalPosition()
    {
        if (heldObject == null) return;
        if (!heldObject.CompareTag("Building")) return;

        // Восстановим parent/transform
        heldObject.transform.SetParent(originalParent, true);
        heldObject.transform.position = originalPosition;
        heldObject.transform.rotation = originalRotation;
        heldObject.transform.localScale = originalScale;

        // Восстановим рендеры и коллайдеры в их исходные состояния
        for (int i = 0; i < originalRenderers.Count; i++)
        {
            if (originalRenderers[i] != null)
                originalRenderers[i].enabled = originalRendererStates[i];
        }
        for (int i = 0; i < originalColliders.Count; i++)
        {
            if (originalColliders[i] != null)
                originalColliders[i].enabled = originalColliderStates[i];
        }

        Debug.Log("[Return] Building returned to original position: " + heldObject.name);
        heldObject = null;
    }

    // -------- Place building at position (при успешной установке) --------
    public void PlaceHeldBuildingAt(Vector3 position, Quaternion rotation)
    {
        if (heldObject == null) return;
        if (!heldObject.CompareTag("Building")) return;

        // Перемещаем оригинал на позицию и включаем его визу/коллайдеры
        heldObject.transform.SetParent(null, true);
        heldObject.transform.position = position;
        heldObject.transform.rotation = rotation;
        heldObject.transform.localScale = originalScale;

        foreach (var r in originalRenderers)
            if (r != null) r.enabled = true;
        foreach (var c in originalColliders)
            if (c != null) c.enabled = true;

        Debug.Log("[Place] Building placed: " + heldObject.name);
        heldObject = null;
    }

    // --- доступ для GridBuildingSystem ---
    public GameObject GetHeldObject()
    {
        return heldObject;
    }

    public bool HasHeldBuilding()
    {
        return heldObject != null && heldObject.CompareTag("Building");
    }
}
