using UnityEngine;
using System.Collections.Generic;

public class GridBuildingSystem : MonoBehaviour
{
    [Header("Grid")]
    public float cellSize = 1f;
    public LayerMask groundMask;            // для raycast'а (где ставим)
    public LayerMask placementBlockMask;    // слои, которые блокируют установку

    [Header("Preview Materials")]
    public Material previewValidMaterial;   // зелёный прозрачный
    public Material previewInvalidMaterial; // красный прозрачный

    [Header("Preview Settings")]
    public bool snapYToHitNormal = true;    // выравнивать по поверхности (опционально)
    public float rotateStepDegrees = 90f;   // шаг вращения при нажатии R

    private Camera cam;
    private PlayerPickupSystem pickup;
    private BuildingItem currentItem;
    private GameObject previewObject;
    private Renderer[] previewRenderers;

    // состояние вращения, которое применяется к preview и к установке
    private Quaternion currentRotation = Quaternion.identity;
    private float currentYaw = 0f; // угол в градусах вокруг Y

    void Start()
    {
        cam = Camera.main;
        pickup = GetComponent<PlayerPickupSystem>();
    }
    void Update()
    {
        RefreshCurrentItem();

        if (currentItem == null)
        {
            ClearPreview();
            return;
        }

        HandleRotationInput();
        UpdatePreviewPositionAndVisuals();

        // ЛКМ — попытка установки
        if (Input.GetMouseButtonDown(0))
            TryPlaceBuilding();

        // ПКМ — отмена только если реально идёт предпросмотр
        if (Input.GetMouseButtonDown(1))
        {
            // не отменяем, если здание было только что поднято этим же нажатием (чтобы избежать "поднять+вернуть")
            float sincePickup = Time.time - pickup.lastBuildingPickupTime;
            const float pickupIgnoreWindow = 0.12f; // окно в секундах, подбери по ощущению

            if (previewObject != null && pickup.HasHeldBuilding() && sincePickup > pickupIgnoreWindow)
            {
                CancelPlacement();
            }
        }

    }

    void HandleRotationInput()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            currentYaw = (currentYaw + rotateStepDegrees) % 360f;
            currentRotation = Quaternion.Euler(0f, currentYaw, 0f);
            // если preview существует — сразу применим
            if (previewObject != null)
                previewObject.transform.rotation = currentRotation;
        }
    }

    void RefreshCurrentItem()
    {
        GameObject held = pickup.GetHeldObject();

        if (held == null || !held.CompareTag("Building"))
        {
            ClearPreview();
            currentItem = null;
            return;
        }

        var item = held.GetComponent<BuildingItem>();
        if (item == null)
        {
            Debug.LogError("[Grid] Held object has no BuildingItem component!");
            ClearPreview();
            currentItem = null;
            return;
        }

        // Если поменялся предмет — пересоздаём preview и сбрасываем ротацию
        if (currentItem != item)
        {
            currentItem = item;
            currentYaw = 0f;
            currentRotation = Quaternion.identity;
            CreatePreview();
        }
    }

    void CreatePreview()
    {
        ClearPreview();

        if (currentItem.buildingPrefab == null)
        {
            Debug.LogError("[Grid] buildingPrefab is null in BuildingItem!");
            return;
        }

        previewObject = Instantiate(currentItem.buildingPrefab);
        previewObject.name = currentItem.buildingPrefab.name + "_Preview";

        previewRenderers = previewObject.GetComponentsInChildren<Renderer>(true);
        foreach (var r in previewRenderers)
            r.enabled = true;

        // применим текущую rotation (на случай, если игрок повернул до того, как preview создался)
        previewObject.transform.rotation = currentRotation;
    }

    void UpdatePreviewPositionAndVisuals()
    {
        if (previewObject == null) return;

        // Используем центр камеры (крестик) — ViewportPointToRay
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (!Physics.Raycast(ray, out RaycastHit hit, 100f, groundMask))
            return;

        Vector3 pos = hit.point;

        // Snap по сетке по XZ
        pos.x = Mathf.Round(pos.x / cellSize) * cellSize;
        pos.z = Mathf.Round(pos.z / cellSize) * cellSize;

        // Применяем rotation до расчёта границ и высоты
        previewObject.transform.rotation = currentRotation;

        // Сначала временно ставим preview на XZ (y оставим как есть), чтобы bounds были корректны
        previewObject.transform.position = pos;

        // Вычисляем bounds в мировых координатах и корректируем Y так, чтобы низ объекта лежал на hit.point.y
        if (snapYToHitNormal)
        {
            Bounds b = CalculateBounds(previewObject);
            float extentY = b.extents.y;
            pos.y = hit.point.y + extentY;
        }

        // Применяем окончательную позицию
        previewObject.transform.position = pos;

        // Проверка валидности установки
        bool valid = CheckPlacementValid();

        // Меняем материал предпросмотра
        ApplyPreviewMaterial(valid ? previewValidMaterial : previewInvalidMaterial);
    }

    Bounds CalculateBounds(GameObject obj)
    {
        Renderer[] rends = obj.GetComponentsInChildren<Renderer>(true);
        if (rends.Length == 0)
            return new Bounds(obj.transform.position, Vector3.one * 0.5f);

        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++)
            b.Encapsulate(rends[i].bounds);

        return b;
    }

    void ApplyPreviewMaterial(Material mat)
    {
        if (previewRenderers == null) return;
        if (mat == null) return;

        foreach (Renderer r in previewRenderers)
        {
            if (r == null) continue;
            Material[] arr = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < arr.Length; i++)
                arr[i] = mat;
            r.sharedMaterials = arr;
        }
    }

    bool CheckPlacementValid()
    {
        if (previewObject == null) return false;

        Bounds b = CalculateBounds(previewObject);
        Vector3 center = b.center;
        Vector3 halfExtents = b.extents + Vector3.one * 0.01f; // небольшая погрешность

        Collider[] hits = Physics.OverlapBox(center, halfExtents, previewObject.transform.rotation, placementBlockMask, QueryTriggerInteraction.Ignore);

        foreach (var c in hits)
        {
            if (c == null) continue;
            // Игнорируем части preview
            if (c.transform.IsChildOf(previewObject.transform)) continue;

            // Игнорируем объект, который игрок держит (он скрыт и не должен мешать)
            GameObject held = pickup.GetHeldObject();
            if (held != null && (c.transform.IsChildOf(held.transform) || c.transform == held.transform)) continue;

            return false;
        }

        return true;
    }

    void TryPlaceBuilding()
    {
        if (currentItem == null || previewObject == null)
        {
            Debug.LogError("[Grid] No currentItem or previewObject — cannot place.");
            return;
        }

        if (!CheckPlacementValid())
        {
            Debug.Log("[Grid] Cannot place here!");
            return;
        }

        GameObject held = pickup.GetHeldObject();
        if (held == null)
        {
            Debug.LogError("[Grid] Held object lost before placement.");
            return;
        }

        // Передаём и position и rotation, которые одинаковы с preview
        pickup.PlaceHeldBuildingAt(previewObject.transform.position, currentRotation);

        ClearPreview();
    }

    void CancelPlacement()
    {
        pickup.ReturnHeldBuildingToOriginalPosition();
        ClearPreview();
    }

    void ClearPreview()
    {
        if (previewObject != null)
            Destroy(previewObject);
        previewObject = null;
        previewRenderers = null;
    }
}
