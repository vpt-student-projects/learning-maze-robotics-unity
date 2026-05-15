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
    public RectTransform dragLayer;
    public BlockChainManager chainManager;

    [Header("Delete UI")]
    public GameObject deleteButton;

    private CanvasGroup canvasGroup;

    private GameObject cloneGO;
    private RectTransform cloneRT;
    private CanvasGroup cloneCG;

    private Transform originalParent;
    private Vector2 originalAnchoredPos;
    private RectTransform myRT;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        myRT = GetComponent<RectTransform>();

        if (rootCanvas == null)
            rootCanvas = GetComponentInParent<Canvas>();

        if (dragLayer == null && rootCanvas != null)
        {
            Transform t = rootCanvas.transform.Find("DragLayer");

            if (t != null)
                dragLayer = t as RectTransform;
        }

        SetDeleteVisible(!isPaletteSample);
    }

    private void EnsureRefs()
    {
        if (chainManager == null)
            chainManager = FindAnyObjectByType<BlockChainManager>();

        if (rootCanvas == null)
            rootCanvas = GetComponentInParent<Canvas>();

        if (dragLayer == null && rootCanvas != null)
        {
            Transform t = rootCanvas.transform.Find("DragLayer");

            if (t != null)
                dragLayer = t as RectTransform;
        }
    }

    private void SetDeleteVisible(bool visible)
    {
        if (deleteButton != null)
            deleteButton.SetActive(visible);
    }

    public void DeleteSelf()
    {
        EnsureRefs();

        BlockCommand cmd = GetComponent<BlockCommand>();

        if (cmd != null && chainManager != null)
            chainManager.UnregisterBlock(cmd);

        Destroy(gameObject);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        EnsureRefs();

        if (rootCanvas == null || dragLayer == null)
            return;

        if (isPaletteSample)
        {
            if (paletteItem == null || paletteItem.blockUIPrefab == null)
                return;

            cloneGO = Instantiate(paletteItem.blockUIPrefab, dragLayer);
            cloneGO.transform.SetAsLastSibling();

            DraggableBlock cloneDrag = cloneGO.GetComponent<DraggableBlock>();

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

            if (cloneCG == null)
                cloneCG = cloneGO.AddComponent<CanvasGroup>();

            cloneCG.blocksRaycasts = false;
            cloneCG.alpha = 0.85f;

            SetRectToPointer(cloneRT, eventData);

            canvasGroup.blocksRaycasts = false;
            return;
        }

        if (myRT == null)
            myRT = GetComponent<RectTransform>();

        BlockCommand meCmd = GetComponent<BlockCommand>();

        if (meCmd != null && chainManager != null)
            chainManager.Detach(meCmd);

        originalParent = transform.parent;
        originalAnchoredPos = myRT.anchoredPosition;

        transform.SetParent(dragLayer, true);
        transform.SetAsLastSibling();

        canvasGroup.blocksRaycasts = false;
        canvasGroup.alpha = 0.85f;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (isPaletteSample)
        {
            if (cloneRT != null)
                SetRectToPointer(cloneRT, eventData);

            return;
        }

        if (myRT != null)
            SetRectToPointer(myRT, eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        EnsureRefs();

        if (isPaletteSample)
        {
            canvasGroup.blocksRaycasts = true;

            if (cloneGO == null)
                return;

            DropZone zone = FindDropZoneUnderPointer(eventData);

            if (zone == null)
            {
                Destroy(cloneGO);
                ClearCloneRefs();
                return;
            }

            cloneCG.blocksRaycasts = true;
            cloneCG.alpha = 1f;

            zone.Accept(cloneGO.transform);

            BlockCommand cmd = cloneGO.GetComponent<BlockCommand>();

            if (cmd != null && chainManager != null)
            {
                chainManager.RegisterBlock(cmd);

                if (zone.zoneType == DropZoneType.Workspace)
                {
                    RectTransform workspaceRT = zone.workspaceContent as RectTransform;

                    if (workspaceRT != null)
                        SetRectToPointer(cloneRT, eventData, workspaceRT);

                    chainManager.TrySnap(cmd);
                }
                else
                {
                    PrepareBlockForBranch(cloneRT);
                    chainManager.RefreshIfElseBranches();
                }
            }

            ClearCloneRefs();
            return;
        }

        canvasGroup.blocksRaycasts = true;
        canvasGroup.alpha = 1f;

        DropZone dropZone = FindDropZoneUnderPointer(eventData);

        if (dropZone == null)
        {
            if (originalParent != null)
            {
                transform.SetParent(originalParent, true);

                if (myRT != null)
                    myRT.anchoredPosition = originalAnchoredPos;
            }

            return;
        }

        dropZone.Accept(transform);

        BlockCommand draggedCmd = GetComponent<BlockCommand>();

        if (draggedCmd != null && chainManager != null)
        {
            chainManager.RegisterBlock(draggedCmd);

            if (dropZone.zoneType == DropZoneType.Workspace)
            {
                RectTransform workspaceRT = dropZone.workspaceContent as RectTransform;

                if (workspaceRT != null)
                    SetRectToPointer(myRT, eventData, workspaceRT);

                chainManager.TrySnap(draggedCmd);
            }
            else
            {
                PrepareBlockForBranch(myRT);
                chainManager.RefreshIfElseBranches();
            }
        }
    }

    private void ClearCloneRefs()
    {
        cloneGO = null;
        cloneRT = null;
        cloneCG = null;
    }

    private void PrepareBlockForBranch(RectTransform rt)
    {
        if (rt == null) return;

        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
    }

    private void SetRectToPointer(RectTransform target, PointerEventData eventData, RectTransform relativeTo = null)
    {
        if (target == null || rootCanvas == null)
            return;

        RectTransform reference = relativeTo != null
            ? relativeTo
            : rootCanvas.transform as RectTransform;

        if (reference == null)
            return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            reference,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPoint
        );

        if (target.parent == reference)
        {
            target.anchoredPosition = localPoint;
        }
        else
        {
            Vector3 world = reference.TransformPoint(localPoint);

            RectTransform parentRT = target.parent as RectTransform;

            if (parentRT != null)
            {
                Vector3 parentLocal = parentRT.InverseTransformPoint(world);
                target.anchoredPosition = parentLocal;
            }
        }
    }

    private DropZone FindDropZoneUnderPointer(PointerEventData eventData)
    {
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        DropZone bestZone = null;
        int bestPriority = -1;

        foreach (RaycastResult result in results)
        {
            DropZone zone = result.gameObject.GetComponentInParent<DropZone>();

            if (zone == null)
                continue;

            int priority = 0;

            if (zone.zoneType == DropZoneType.Workspace)
                priority = 1;

            if (zone.zoneType == DropZoneType.IfBranch || zone.zoneType == DropZoneType.ElseBranch)
                priority = 10;

            if (priority > bestPriority)
            {
                bestPriority = priority;
                bestZone = zone;
            }
        }

        return bestZone;
    }
}