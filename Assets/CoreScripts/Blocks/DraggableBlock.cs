using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class DraggableBlock : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Palette")]
    public bool isPaletteSample = false;
    public BlockPaletteItem paletteItem;

    [Header("Scene refs")]
    public Canvas rootCanvas;
    public RectTransform dragLayer;            // DragLayer на Canvas (обязательно!)
    public BlockChainManager chainManager;

    [Header("Delete UI")]
    public GameObject deleteButton;

    private CanvasGroup canvasGroup;

    // palette drag clone
    private GameObject cloneGO;
    private RectTransform cloneRT;
    private CanvasGroup cloneCG;

    // normal drag
    private Transform originalParent;
    private Vector2 originalAnchoredPos;
    private RectTransform myRT;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

        myRT = GetComponent<RectTransform>();

        if (rootCanvas == null) rootCanvas = GetComponentInParent<Canvas>();
        if (dragLayer == null && rootCanvas != null)
        {
            var t = rootCanvas.transform.Find("DragLayer");
            if (t != null) dragLayer = t as RectTransform;
        }

        if (isPaletteSample) SetDeleteVisible(false);
        else SetDeleteVisible(true);
    }

    private void EnsureRefs()
    {
        if (chainManager == null)
            chainManager = FindAnyObjectByType<BlockChainManager>();

        if (rootCanvas == null)
            rootCanvas = GetComponentInParent<Canvas>();

        if (dragLayer == null && rootCanvas != null)
        {
            var t = rootCanvas.transform.Find("DragLayer");
            if (t != null) dragLayer = t as RectTransform;
        }
    }

    private void SetDeleteVisible(bool visible)
    {
        if (deleteButton != null) deleteButton.SetActive(visible);
    }

    public void DeleteSelf()
    {
        EnsureRefs();

        var cmd = GetComponent<BlockCommand>();
        if (cmd != null && chainManager != null)
            chainManager.UnregisterBlock(cmd);

        Destroy(gameObject);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        EnsureRefs();
        if (rootCanvas == null || dragLayer == null) return;

        if (isPaletteSample)
        {
            if (paletteItem == null || paletteItem.blockUIPrefab == null) return;

            // создаём клон сразу в DragLayer
            cloneGO = Instantiate(paletteItem.blockUIPrefab, dragLayer);
            cloneGO.transform.SetAsLastSibling();

            var cloneDrag = cloneGO.GetComponent<DraggableBlock>();
            if (cloneDrag != null)
            {
                cloneDrag.isPaletteSample = false;
                cloneDrag.paletteItem = null;
                cloneDrag.rootCanvas = rootCanvas;
                cloneDrag.dragLayer = dragLayer;
                cloneDrag.chainManager = chainManager;
                cloneDrag.SetDeleteVisible(true);
            }

            cloneRT = cloneGO.GetComponent<RectTransform>();
            cloneCG = cloneGO.GetComponent<CanvasGroup>();
            if (cloneCG == null) cloneCG = cloneGO.AddComponent<CanvasGroup>();

            cloneCG.blocksRaycasts = false;
            cloneCG.alpha = 0.85f;

            SetRectToPointer(cloneRT, eventData);

            canvasGroup.blocksRaycasts = false;
            return;
        }

        // обычный блок
        if (myRT == null) myRT = GetComponent<RectTransform>();

        // отцепить от цепочки перед переносом
        var meCmd = GetComponent<BlockCommand>();
        if (meCmd != null && chainManager != null)
            chainManager.Detach(meCmd);

        originalParent = transform.parent;
        originalAnchoredPos = myRT.anchoredPosition;

        // переносим в DragLayer (а не в Canvas и не куда попало)
        transform.SetParent(dragLayer, true);
        transform.SetAsLastSibling();

        canvasGroup.blocksRaycasts = false;
        canvasGroup.alpha = 0.85f;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (isPaletteSample)
        {
            if (cloneRT != null) SetRectToPointer(cloneRT, eventData);
            return;
        }

        if (myRT != null) SetRectToPointer(myRT, eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        EnsureRefs();

        if (isPaletteSample)
        {
            canvasGroup.blocksRaycasts = true;
            if (cloneGO == null) return;

            var zone = FindDropZoneUnderPointer(eventData);
            if (zone != null)
            {
                cloneCG.blocksRaycasts = true;
                cloneCG.alpha = 1f;

                zone.Accept(cloneGO.transform);

                // ПОСЛЕ смены parent — выставим позицию корректно в локале workspaceContent
                var workspaceRT = zone.workspaceContent as RectTransform;
                if (workspaceRT != null)
                    SetRectToPointer(cloneRT, eventData, workspaceRT);

                var cmd = cloneGO.GetComponent<BlockCommand>();
                if (cmd != null && chainManager != null)
                {
                    chainManager.RegisterBlock(cmd);
                    chainManager.TrySnap(cmd);
                }
            }
            else
            {
                Destroy(cloneGO);
            }

            cloneGO = null;
            cloneRT = null;
            cloneCG = null;
            return;
        }

        // обычный блок
        canvasGroup.blocksRaycasts = true;
        canvasGroup.alpha = 1f;

        var dropZone = FindDropZoneUnderPointer(eventData);
        if (dropZone != null)
        {
            dropZone.Accept(transform);

            var workspaceRT = dropZone.workspaceContent as RectTransform;
            if (workspaceRT != null)
                SetRectToPointer(myRT, eventData, workspaceRT);

            var cmd = GetComponent<BlockCommand>();
            if (cmd != null && chainManager != null)
            {
                chainManager.RegisterBlock(cmd);
                chainManager.TrySnap(cmd);
            }
        }
        else
        {
            // вернуть назад
            if (originalParent != null)
            {
                transform.SetParent(originalParent, true);
                if (myRT != null) myRT.anchoredPosition = originalAnchoredPos;
            }
        }
    }

    private void SetRectToPointer(RectTransform target, PointerEventData eventData, RectTransform relativeTo = null)
    {
        if (target == null || rootCanvas == null) return;

        var reference = relativeTo != null ? relativeTo : (RectTransform)rootCanvas.transform;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            reference,
            eventData.position,
            eventData.pressEventCamera,
            out var localPoint
        );

        // если целевой RectTransform НЕ под тем же parent'ом, то ставим позицию через world
        if (target.parent == reference)
        {
            target.anchoredPosition = localPoint;
        }
        else
        {
            // перевод localPoint reference -> world -> target parent local
            Vector3 world = reference.TransformPoint(localPoint);
            Vector3 parentLocal = ((RectTransform)target.parent).InverseTransformPoint(world);
            target.anchoredPosition = parentLocal;
        }
    }

    private DropZone FindDropZoneUnderPointer(PointerEventData eventData)
    {
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        foreach (var r in results)
        {
            var zone = r.gameObject.GetComponentInParent<DropZone>();
            if (zone != null) return zone;
        }
        return null;
    }
}
