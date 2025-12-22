using UnityEngine;
using UnityEngine.EventSystems;

public class DraggableBlock : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("If true = this is a palette sample and will be cloned on drag")]
    public bool isPaletteSample = false;

    [Header("Reference to palette item (only for palette samples)")]
    public BlockPaletteItem paletteItem;

    [Header("Runtime")]
    public Canvas rootCanvas;

    private RectTransform rect;
    private CanvasGroup canvasGroup;

    // drag state
    private Transform originalParent;
    private Vector2 originalAnchoredPos;

    private DraggableBlock spawnedClone; // if palette sample, this is the clone we drag

    private void Awake()
    {
        rect = GetComponent<RectTransform>();

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (rootCanvas == null) rootCanvas = GetComponentInParent<Canvas>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (rootCanvas == null) return;

        // ≈сли это образец из палитры Ч создаЄм клон и тащим клон, а не образец
        if (isPaletteSample)
        {
            if (paletteItem == null || paletteItem.blockUIPrefab == null)
            {
                Debug.LogError("Palette sample dragging requires BlockPaletteItem + blockUIPrefab.");
                return;
            }

            var cloneGo = Instantiate(paletteItem.blockUIPrefab, rootCanvas.transform);
            spawnedClone = cloneGo.GetComponent<DraggableBlock>();
            if (spawnedClone == null) spawnedClone = cloneGo.AddComponent<DraggableBlock>();

            //  лон Ч уже Ќ≈ sample
            spawnedClone.isPaletteSample = false;
            spawnedClone.paletteItem = null;
            spawnedClone.rootCanvas = rootCanvas;

            // —тартова€ позици€ клона = позици€ мыши
            spawnedClone.ForceStartDragAt(eventData);

            // „тобы драг продолжилс€ на клоне Ч вручную дернем BeginDrag дл€ клона
            spawnedClone.OnBeginDrag(eventData);
            return;
        }

        // ќбычный блок (уже в воркспейсе) Ч тащим его
        originalParent = transform.parent;
        originalAnchoredPos = rect.anchoredPosition;

        // ѕереносим наверх в Canvas, чтобы был поверх всего
        transform.SetParent(rootCanvas.transform, worldPositionStays: true);

        canvasGroup.blocksRaycasts = false;
        canvasGroup.alpha = 0.85f;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (isPaletteSample)
        {
            // ѕалитра сама не двигаетс€
            return;
        }

        if (rootCanvas == null) return;

        // ѕеремещаем UI блок под мышь
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rootCanvas.transform as RectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPoint
        );

        rect.anchoredPosition = localPoint;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (isPaletteSample)
        {
            // ≈сли это образец Ч endDrag должен отработать на клоне
            return;
        }

        canvasGroup.blocksRaycasts = true;
        canvasGroup.alpha = 1f;

        // ≈сли отпустили над зоной воркспейса Ч она сама подхватит (DropZone)
        // ≈сли нет Ч возвращаем обратно или удал€ем (ниже логика)
        var placed = TryPlaceIntoDropZone(eventData);

        if (!placed)
        {
            // ≈сли блок был уже в воркспейсе Ч возвращаем на место
            if (originalParent != null)
            {
                transform.SetParent(originalParent, worldPositionStays: false);
                rect.anchoredPosition = originalAnchoredPos;
            }
            else
            {
                // если откуда-то непон€тно Ч просто уничтожаем
                Destroy(gameObject);
            }
        }
    }
private bool TryPlaceIntoDropZone(PointerEventData eventData)
    {
        if (eventData.pointerEnter == null) return false;

        // »щем DropZone в объекте под мышью (или в родител€х)
        var zone = eventData.pointerEnter.GetComponentInParent<DropZone>();
        if (zone == null) return false;

        zone.Accept(this);
        return true;
    }

    private void ForceStartDragAt(PointerEventData eventData)
    {
        // поставить клон в позицию курсора сразу
        if (rootCanvas == null) return;
        if (rect == null) rect = GetComponent<RectTransform>();

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rootCanvas.transform as RectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPoint
        );

        rect.anchoredPosition = localPoint;
    }
}