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

    [Header("Drag View")]
    public bool centerPaletteCloneUnderCursor = true;

    private CanvasGroup canvasGroup;

    private GameObject cloneGO;
    private RectTransform cloneRT;
    private CanvasGroup cloneCG;

    private Transform originalParent;
    private Vector2 originalAnchoredPos;
    private RectTransform myRT;

    private Vector2 dragPointerOffset;
    private bool hasDragPointerOffset;

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

    private Camera GetEventCamera(PointerEventData eventData)
    {
        if (eventData != null && eventData.pressEventCamera != null)
            return eventData.pressEventCamera;

        if (rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            return rootCanvas.worldCamera;

        return null;
    }

    private void SetDeleteVisible(bool visible)
    {
        if (deleteButton != null)
            deleteButton.SetActive(visible);
    }

    public void DeleteSelf()
    {
        EnsureRefs();

        if (isPaletteSample)
            return;

        BlockCommand cmd = GetComponent<BlockCommand>();
        LoopBlockUI parentLoop = GetComponentInParent<LoopBlockUI>();
        IfElseBlockUI parentIfElse = GetComponentInParent<IfElseBlockUI>();

        gameObject.SetActive(false);

        if (chainManager != null && cmd != null)
            chainManager.UnregisterBlock(cmd);

        if (parentLoop != null)
            parentLoop.RefreshSize();

        if (parentIfElse != null)
            parentIfElse.RefreshSize();

        if (chainManager != null)
            chainManager.RefreshAllContainers();

        Destroy(gameObject);

        if (parentLoop != null)
            parentLoop.RefreshSize();

        if (parentIfElse != null)
            parentIfElse.RefreshSize();

        if (chainManager != null)
            chainManager.RefreshAllContainers();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        EnsureRefs();

        if (rootCanvas == null || dragLayer == null)
            return;

        hasDragPointerOffset = false;
        dragPointerOffset = Vector2.zero;

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

            PrepareDraggedClone(cloneRT);

            if (centerPaletteCloneUnderCursor)
                SetRectCenterToPointer(cloneRT, eventData);
            else
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

        CalculatePointerOffset(myRT, eventData);

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
            {
                if (centerPaletteCloneUnderCursor)
                    SetRectCenterToPointer(cloneRT, eventData);
                else
                    SetRectToPointer(cloneRT, eventData);
            }

            return;
        }

        if (myRT != null)
        {
            if (hasDragPointerOffset)
                SetRectToPointerWithOffset(myRT, eventData, dragPointerOffset);
            else
                SetRectToPointer(myRT, eventData);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        EnsureRefs();

        hasDragPointerOffset = false;
        dragPointerOffset = Vector2.zero;

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

            NormalizeDropZone(zone);

            cloneCG.blocksRaycasts = true;
            cloneCG.alpha = 1f;

            zone.Accept(cloneGO.transform);

            BlockCommand cmd = cloneGO.GetComponent<BlockCommand>();

            if (cmd != null && chainManager != null)
                chainManager.RegisterBlock(cmd);

            if (IsWorkspaceZone(zone))
            {
                if (cmd != null && chainManager != null)
                {
                    RectTransform workspaceRT = zone.workspaceContent as RectTransform;

                    if (workspaceRT != null)
                        SetRectCenterToPointer(cloneRT, eventData, workspaceRT);

                    chainManager.TrySnap(cmd);
                }

                ClearCloneRefs();
                return;
            }

            FinalizeBranchDrop(zone);

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

            if (chainManager != null)
                chainManager.RefreshAllContainers();

            return;
        }

        NormalizeDropZone(dropZone);

        dropZone.Accept(transform);

        BlockCommand draggedCmd = GetComponent<BlockCommand>();

        if (draggedCmd != null && chainManager != null)
            chainManager.RegisterBlock(draggedCmd);

        if (IsWorkspaceZone(dropZone))
        {
            if (draggedCmd != null && chainManager != null)
            {
                RectTransform workspaceRT = dropZone.workspaceContent as RectTransform;

                if (workspaceRT != null)
                    SetRectToPointer(myRT, eventData, workspaceRT);

                chainManager.TrySnap(draggedCmd);
            }

            return;
        }

        FinalizeBranchDrop(dropZone);
    }

    private void PrepareDraggedClone(RectTransform rt)
    {
        if (rt == null)
            return;

        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
    }

    private void FinalizeBranchDrop(DropZone zone)
    {
        Canvas.ForceUpdateCanvases();

        if (zone != null)
            NormalizeDropZone(zone);

        if (zone != null && zone.ownerLoop != null)
        {
            zone.ownerLoop.RefreshSize();
            Canvas.ForceUpdateCanvases();
        }

        if (zone != null && zone.ownerIfElse != null)
        {
            zone.ownerIfElse.RefreshSize();
            Canvas.ForceUpdateCanvases();
        }

        if (chainManager != null)
        {
            chainManager.RebuildAllChainsByHierarchy();
            chainManager.RefreshAllContainers();
        }

        if (zone != null && zone.ownerLoop != null)
        {
            zone.ownerLoop.RefreshSize();
            Canvas.ForceUpdateCanvases();
        }

        if (zone != null && zone.ownerIfElse != null)
        {
            zone.ownerIfElse.RefreshSize();
            Canvas.ForceUpdateCanvases();
        }

        if (chainManager != null)
            chainManager.RefreshAllContainers();
    }

    private bool IsWorkspaceZone(DropZone zone)
    {
        if (zone == null)
            return false;

        NormalizeDropZone(zone);

        return zone.zoneType == DropZoneType.Workspace;
    }

    private bool IsBranchZone(DropZone zone)
    {
        if (zone == null)
            return false;

        NormalizeDropZone(zone);

        return zone.zoneType == DropZoneType.LoopBranch ||
               zone.zoneType == DropZoneType.IfBranch ||
               zone.zoneType == DropZoneType.ElseBranch;
    }

    private void NormalizeDropZone(DropZone zone)
    {
        if (zone == null)
            return;

        if (zone.content == null)
            zone.content = zone.transform as RectTransform;

        if (zone.workspaceContent == null)
            zone.workspaceContent = zone.content;

        if (zone.ownerLoop == null)
            zone.ownerLoop = zone.GetComponentInParent<LoopBlockUI>();

        if (zone.ownerIfElse == null)
            zone.ownerIfElse = zone.GetComponentInParent<IfElseBlockUI>();

        if (zone.ownerLoop != null)
        {
            zone.zoneType = DropZoneType.LoopBranch;

            if (zone.ownerLoop.loopContent != null)
                zone.content = zone.ownerLoop.loopContent;

            zone.workspaceContent = zone.content;
            return;
        }

        if (zone.ownerIfElse != null)
        {
            if (zone.zoneType == DropZoneType.IfBranch ||
                zone.zoneType == DropZoneType.If ||
                zone.zoneType == DropZoneType.IfContent ||
                zone.zoneType == DropZoneType.IfTrue)
            {
                if (zone.ownerIfElse.ifContent != null)
                    zone.content = zone.ownerIfElse.ifContent;

                zone.workspaceContent = zone.content;
                return;
            }

            if (zone.zoneType == DropZoneType.ElseBranch ||
                zone.zoneType == DropZoneType.Else ||
                zone.zoneType == DropZoneType.ElseContent ||
                zone.zoneType == DropZoneType.IfFalse)
            {
                if (zone.ownerIfElse.elseContent != null)
                    zone.content = zone.ownerIfElse.elseContent;

                zone.workspaceContent = zone.content;
                return;
            }
        }
    }

    private void ClearCloneRefs()
    {
        cloneGO = null;
        cloneRT = null;
        cloneCG = null;
    }

    private void CalculatePointerOffset(RectTransform target, PointerEventData eventData)
    {
        if (target == null || rootCanvas == null)
            return;

        RectTransform reference = rootCanvas.transform as RectTransform;

        if (reference == null)
            return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            reference,
            eventData.position,
            GetEventCamera(eventData),
            out Vector2 pointerLocal
        );

        Vector3 targetWorld = target.position;
        Vector3 targetLocal3 = reference.InverseTransformPoint(targetWorld);
        Vector2 targetLocal = new Vector2(targetLocal3.x, targetLocal3.y);

        dragPointerOffset = targetLocal - pointerLocal;
        hasDragPointerOffset = true;
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
            GetEventCamera(eventData),
            out Vector2 localPoint
        );

        SetRectPivotToLocalPoint(target, reference, localPoint);
    }

    private void SetRectToPointerWithOffset(RectTransform target, PointerEventData eventData, Vector2 offset, RectTransform relativeTo = null)
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
            GetEventCamera(eventData),
            out Vector2 localPoint
        );

        SetRectPivotToLocalPoint(target, reference, localPoint + offset);
    }

    private void SetRectCenterToPointer(RectTransform target, PointerEventData eventData, RectTransform relativeTo = null)
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
            GetEventCamera(eventData),
            out Vector2 pointerLocal
        );

        Vector2 size = target.rect.size;
        Vector2 pivot = target.pivot;

        Vector2 centerOffsetFromPivot = new Vector2(
            (0.5f - pivot.x) * size.x,
            (0.5f - pivot.y) * size.y
        );

        Vector2 wantedPivotLocal = pointerLocal - centerOffsetFromPivot;

        SetRectPivotToLocalPoint(target, reference, wantedPivotLocal);
    }

    private void SetRectPivotToLocalPoint(RectTransform target, RectTransform reference, Vector2 localPoint)
    {
        if (target == null || reference == null)
            return;

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
                target.anchoredPosition = new Vector2(parentLocal.x, parentLocal.y);
            }
        }
    }

    private DropZone FindDropZoneUnderPointer(PointerEventData eventData)
    {
        DropZone loopZoneByGeometry = FindLoopDropZoneByPointerGeometry(eventData);

        if (loopZoneByGeometry != null)
        {
            NormalizeDropZone(loopZoneByGeometry);
            return loopZoneByGeometry;
        }

        DropZone ifElseZoneByGeometry = FindIfElseDropZoneByPointerGeometry(eventData);

        if (ifElseZoneByGeometry != null)
        {
            NormalizeDropZone(ifElseZoneByGeometry);
            return ifElseZoneByGeometry;
        }

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        DropZone loopZoneByRaycast = FindLoopDropZoneByRaycast(results);

        if (loopZoneByRaycast != null)
        {
            NormalizeDropZone(loopZoneByRaycast);
            return loopZoneByRaycast;
        }

        DropZone ifElseZoneByRaycast = FindIfElseDropZoneByRaycast(results);

        if (ifElseZoneByRaycast != null)
        {
            NormalizeDropZone(ifElseZoneByRaycast);
            return ifElseZoneByRaycast;
        }

        DropZone bestZone = null;
        int bestScore = int.MinValue;

        foreach (RaycastResult result in results)
        {
            DropZone zone = result.gameObject.GetComponentInParent<DropZone>();

            if (zone == null)
                continue;

            NormalizeDropZone(zone);

            int priority = 0;

            if (IsWorkspaceZone(zone))
                priority = 1;

            if (IsBranchZone(zone))
                priority = 100;

            int depth = GetTransformDepth(zone.transform);
            int score = priority * 1000 + depth;

            if (score > bestScore)
            {
                bestScore = score;
                bestZone = zone;
            }
        }

        if (bestZone != null)
            NormalizeDropZone(bestZone);

        return bestZone;
    }

    private DropZone FindLoopDropZoneByPointerGeometry(PointerEventData eventData)
    {
        if (eventData == null)
            return null;

        LoopBlockUI[] loops = FindObjectsByType<LoopBlockUI>(FindObjectsSortMode.None);

        LoopBlockUI bestLoop = null;
        int bestDepth = -1;

        Camera cam = GetEventCamera(eventData);

        foreach (LoopBlockUI loop in loops)
        {
            if (loop == null)
                continue;

            if (loop.loopContent == null)
                continue;

            if (!isPaletteSample && loop.gameObject == gameObject)
                continue;

            bool insideContent = RectTransformUtility.RectangleContainsScreenPoint(
                loop.loopContent,
                eventData.position,
                cam
            );

            bool insideBody = false;

            if (loop.bodyRoot != null)
            {
                insideBody = RectTransformUtility.RectangleContainsScreenPoint(
                    loop.bodyRoot,
                    eventData.position,
                    cam
                );
            }

            bool insideWholeLoop = false;

            RectTransform loopRect = loop.rootRect != null
                ? loop.rootRect
                : loop.GetComponent<RectTransform>();

            if (loopRect != null)
            {
                insideWholeLoop = RectTransformUtility.RectangleContainsScreenPoint(
                    loopRect,
                    eventData.position,
                    cam
                );
            }

            if (!insideContent && !insideBody && !insideWholeLoop)
                continue;

            int depth = GetTransformDepth(loop.transform);

            if (depth > bestDepth)
            {
                bestDepth = depth;
                bestLoop = loop;
            }
        }

        if (bestLoop == null)
            return null;

        DropZone zone = bestLoop.GetComponentInChildren<DropZone>(true);

        if (zone == null)
            return null;

        zone.ownerLoop = bestLoop;
        zone.zoneType = DropZoneType.LoopBranch;
        zone.content = bestLoop.loopContent;
        zone.workspaceContent = bestLoop.loopContent;

        return zone;
    }

    private DropZone FindLoopDropZoneByRaycast(List<RaycastResult> results)
    {
        LoopBlockUI bestLoop = null;
        int bestDepth = -1;

        foreach (RaycastResult result in results)
        {
            LoopBlockUI loop = result.gameObject.GetComponentInParent<LoopBlockUI>();

            if (loop == null)
                continue;

            if (loop.loopContent == null)
                continue;

            if (!isPaletteSample && loop.gameObject == gameObject)
                continue;

            int depth = GetTransformDepth(loop.transform);

            if (depth > bestDepth)
            {
                bestDepth = depth;
                bestLoop = loop;
            }
        }

        if (bestLoop == null)
            return null;

        DropZone zone = bestLoop.GetComponentInChildren<DropZone>(true);

        if (zone == null)
            return null;

        zone.ownerLoop = bestLoop;
        zone.zoneType = DropZoneType.LoopBranch;
        zone.content = bestLoop.loopContent;
        zone.workspaceContent = bestLoop.loopContent;

        return zone;
    }

    private DropZone FindIfElseDropZoneByPointerGeometry(PointerEventData eventData)
    {
        if (eventData == null)
            return null;

        IfElseBlockUI[] ifBlocks = FindObjectsByType<IfElseBlockUI>(FindObjectsSortMode.None);

        IfElseBlockUI bestIfElse = null;
        DropZone bestZone = null;
        int bestDepth = -1;

        Camera cam = GetEventCamera(eventData);

        foreach (IfElseBlockUI ifElse in ifBlocks)
        {
            if (ifElse == null)
                continue;

            if (!isPaletteSample && ifElse.gameObject == gameObject)
                continue;

            if (ifElse.ifContent != null &&
                RectTransformUtility.RectangleContainsScreenPoint(ifElse.ifContent, eventData.position, cam))
            {
                int depth = GetTransformDepth(ifElse.transform);

                if (depth > bestDepth)
                {
                    bestDepth = depth;
                    bestIfElse = ifElse;
                    bestZone = FindDropZoneForContent(ifElse.ifContent, DropZoneType.IfBranch);
                }
            }

            if (ifElse.elseContent != null &&
                RectTransformUtility.RectangleContainsScreenPoint(ifElse.elseContent, eventData.position, cam))
            {
                int depth = GetTransformDepth(ifElse.transform);

                if (depth > bestDepth)
                {
                    bestDepth = depth;
                    bestIfElse = ifElse;
                    bestZone = FindDropZoneForContent(ifElse.elseContent, DropZoneType.ElseBranch);
                }
            }
        }

        if (bestIfElse == null || bestZone == null)
            return null;

        bestZone.ownerIfElse = bestIfElse;

        if (bestZone.zoneType == DropZoneType.IfBranch ||
            bestZone.zoneType == DropZoneType.If ||
            bestZone.zoneType == DropZoneType.IfContent ||
            bestZone.zoneType == DropZoneType.IfTrue)
        {
            bestZone.content = bestIfElse.ifContent;
            bestZone.workspaceContent = bestIfElse.ifContent;
        }
        else
        {
            bestZone.content = bestIfElse.elseContent;
            bestZone.workspaceContent = bestIfElse.elseContent;
        }

        return bestZone;
    }

    private DropZone FindIfElseDropZoneByRaycast(List<RaycastResult> results)
    {
        DropZone bestZone = null;
        int bestScore = int.MinValue;

        foreach (RaycastResult result in results)
        {
            DropZone zone = result.gameObject.GetComponentInParent<DropZone>();

            if (zone == null)
                continue;

            IfElseBlockUI owner = zone.GetComponentInParent<IfElseBlockUI>();

            if (owner == null)
                continue;

            if (!isPaletteSample && owner.gameObject == gameObject)
                continue;

            zone.ownerIfElse = owner;
            NormalizeDropZone(zone);

            if (!IsBranchZone(zone))
                continue;

            int score = GetTransformDepth(zone.transform);

            if (score > bestScore)
            {
                bestScore = score;
                bestZone = zone;
            }
        }

        return bestZone;
    }

    private DropZone FindDropZoneForContent(RectTransform content, DropZoneType fallbackType)
    {
        if (content == null)
            return null;

        DropZone zone = content.GetComponent<DropZone>();

        if (zone == null)
            zone = content.GetComponentInParent<DropZone>();

        if (zone == null)
            zone = content.GetComponentInChildren<DropZone>(true);

        if (zone != null)
            zone.zoneType = fallbackType;

        return zone;
    }

    private int GetTransformDepth(Transform t)
    {
        int depth = 0;

        while (t != null)
        {
            depth++;
            t = t.parent;
        }

        return depth;
    }
}