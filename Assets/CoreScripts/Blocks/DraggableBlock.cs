using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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

    [Header("Debug")]
    public bool debugDropSearch = true;

    private CanvasGroup canvasGroup;

    private GameObject cloneGO;
    private RectTransform cloneRT;
    private CanvasGroup cloneCG;

    private Transform originalParent;
    private Vector2 originalAnchoredPos;
    private int originalSiblingIndex;
    private Vector3 originalTopWorld;
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
                cloneDrag.debugDropSearch = debugDropSearch;
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

        originalParent = transform.parent;
        originalAnchoredPos = myRT.anchoredPosition;
        originalSiblingIndex = transform.GetSiblingIndex();
        originalTopWorld = GetBlockTopWorld(gameObject);

        CalculatePointerOffset(myRT, eventData);

        if (meCmd != null && chainManager != null)
            chainManager.Detach(meCmd);

        transform.SetParent(dragLayer, true);
        transform.SetAsLastSibling();

        if (originalParent != null && originalSiblingIndex == 0)
            MoveNewFirstBlockToOldTop(originalParent, originalTopWorld);

        RefreshOldContainerAfterTake(originalParent);

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
                DebugDrop("[DROP DEBUG END PALETTE] zone=NULL -> destroy clone");
                Destroy(cloneGO);
                ClearCloneRefs();
                return;
            }

            NormalizeDropZone(zone);

            cloneCG.blocksRaycasts = true;
            cloneCG.alpha = 1f;

            if (IsIfElseBranchZone(zone))
            {
                DirectAcceptToBranch(cloneGO.transform, zone);
                ClearCloneRefs();
                return;
            }

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
            DebugDrop("[DROP DEBUG END EXISTING] zone=NULL -> return to old parent");

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

        if (IsIfElseBranchZone(dropZone))
        {
            DirectAcceptToBranch(transform, dropZone);
            return;
        }

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

    private void DirectAcceptToBranch(Transform blockTransform, DropZone zone)
    {
        if (blockTransform == null || zone == null)
            return;

        NormalizeDropZone(zone);

        RectTransform targetContent = zone.content as RectTransform;

        if (targetContent == null)
            targetContent = zone.workspaceContent as RectTransform;

        if (targetContent == null)
        {
            DebugDrop(
                $"[DROP DEBUG DIRECT BRANCH FAIL]" +
                $" zone={zone.name}" +
                $" zoneType={zone.zoneType}" +
                $" content=NULL"
            );
            return;
        }

        DebugDrop(
            $"[DROP DEBUG DIRECT BRANCH ACCEPT]" +
            $" block={blockTransform.name}" +
            $" zone={zone.name}" +
            $" zoneType={zone.zoneType}" +
            $" targetContent={GetTransformPath(targetContent)}" +
            $" ownerIfElse={(zone.ownerIfElse != null ? GetTransformPath(zone.ownerIfElse.transform) : "NULL")}" +
            $" ownerLoop={(zone.ownerLoop != null ? GetTransformPath(zone.ownerLoop.transform) : "NULL")}"
        );

        blockTransform.SetParent(targetContent, true);
        blockTransform.SetAsLastSibling();

        RectTransform blockRT = blockTransform as RectTransform;

        if (blockRT != null)
        {
            blockRT.localScale = Vector3.one;
            blockRT.localRotation = Quaternion.identity;
        }

        CanvasGroup cg = blockTransform.GetComponent<CanvasGroup>();

        if (cg == null)
            cg = blockTransform.gameObject.AddComponent<CanvasGroup>();

        cg.blocksRaycasts = true;
        cg.interactable = true;
        cg.alpha = 1f;

        LayoutElement layoutElement = blockTransform.GetComponent<LayoutElement>();

        if (layoutElement == null)
            layoutElement = blockTransform.gameObject.AddComponent<LayoutElement>();

        layoutElement.ignoreLayout = true;

        BlockCommand cmd = blockTransform.GetComponent<BlockCommand>();

        if (cmd != null && chainManager != null)
            chainManager.RegisterBlock(cmd);

        if (zone.ownerIfElse != null)
        {
            zone.ownerIfElse.RefreshSize();
            Canvas.ForceUpdateCanvases();
        }

        LoopBlockUI parentLoop = targetContent.GetComponentInParent<LoopBlockUI>();

        if (parentLoop != null)
        {
            parentLoop.RefreshSize();
            Canvas.ForceUpdateCanvases();
        }

        if (chainManager != null)
        {
            chainManager.RebuildAllChainsByHierarchy();
            chainManager.RefreshAllContainers();
        }

        if (zone.ownerIfElse != null)
        {
            zone.ownerIfElse.RefreshSize();
            Canvas.ForceUpdateCanvases();
        }

        if (parentLoop != null)
        {
            parentLoop.RefreshSize();
            Canvas.ForceUpdateCanvases();
        }

        if (chainManager != null)
            chainManager.RefreshAllContainers();
    }

    private void RefreshOldContainerAfterTake(Transform oldParent)
    {
        LoopBlockUI loop = oldParent != null ? oldParent.GetComponentInParent<LoopBlockUI>() : null;
        IfElseBlockUI ifElse = oldParent != null ? oldParent.GetComponentInParent<IfElseBlockUI>() : null;

        if (loop != null)
            loop.RefreshSize();

        if (ifElse != null)
            ifElse.RefreshSize();

        if (chainManager != null)
        {
            chainManager.RebuildAllChainsByHierarchy();
            chainManager.RefreshAllContainers();
        }
    }

    private void MoveNewFirstBlockToOldTop(Transform container, Vector3 oldTopWorld)
    {
        if (container == null)
            return;

        BlockCommand newFirst = FindFirstActiveBlockInContainer(container);

        if (newFirst == null)
            return;

        RectTransform firstRT = newFirst.transform as RectTransform;

        if (firstRT == null)
            return;

        Vector3 firstTopWorld = GetBlockTopWorld(newFirst.gameObject);
        Vector3 delta = oldTopWorld - firstTopWorld;

        firstRT.position += delta;

        Canvas.ForceUpdateCanvases();
    }

    private BlockCommand FindFirstActiveBlockInContainer(Transform container)
    {
        if (container == null)
            return null;

        for (int i = 0; i < container.childCount; i++)
        {
            Transform child = container.GetChild(i);

            if (child == null)
                continue;

            if (!child.gameObject.activeInHierarchy)
                continue;

            BlockCommand command = child.GetComponent<BlockCommand>();

            if (command != null)
                return command;
        }

        return null;
    }

    private Vector3 GetBlockTopWorld(GameObject blockObject)
    {
        if (blockObject == null)
            return Vector3.zero;

        BlockSnapPoints snapPoints = blockObject.GetComponent<BlockSnapPoints>();

        if (snapPoints != null)
            return snapPoints.TopWorld;

        RectTransform rectTransform = blockObject.transform as RectTransform;

        if (rectTransform != null)
            return rectTransform.position;

        return blockObject.transform.position;
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

    private bool IsIfElseBranchZone(DropZone zone)
    {
        if (zone == null)
            return false;

        NormalizeDropZone(zone);

        return zone.zoneType == DropZoneType.IfBranch ||
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

        if (zone.zoneType == DropZoneType.IfBranch ||
            zone.zoneType == DropZoneType.ElseBranch)
        {
            if (zone.ownerIfElse == null)
                zone.ownerIfElse = zone.GetComponentInParent<IfElseBlockUI>();

            if (zone.zoneType == DropZoneType.IfBranch)
            {
                if (zone.ownerIfElse != null && zone.ownerIfElse.ifContent != null)
                    zone.content = zone.ownerIfElse.ifContent;
            }

            if (zone.zoneType == DropZoneType.ElseBranch)
            {
                if (zone.ownerIfElse != null && zone.ownerIfElse.elseContent != null)
                    zone.content = zone.ownerIfElse.elseContent;
            }

            zone.workspaceContent = zone.content;

            /*
             * ownerLoop специально не заполняем тут.
             * Если нужен parent loop — найдём его отдельно после принятия блока.
             */
            zone.ownerLoop = null;
            return;
        }

        if (zone.ownerIfElse == null)
            zone.ownerIfElse = zone.GetComponentInParent<IfElseBlockUI>();

        if (zone.ownerLoop == null)
            zone.ownerLoop = zone.GetComponentInParent<LoopBlockUI>();

        if (zone.ownerLoop != null)
        {
            zone.zoneType = DropZoneType.LoopBranch;

            if (zone.ownerLoop.loopContent != null)
                zone.content = zone.ownerLoop.loopContent;

            zone.workspaceContent = zone.content;
            return;
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
        /*
         * Самый важный порядок:
         * 1. Сначала вручную ищем IF/ELSE.
         * 2. Если нашли — возвращаем IF/ELSE и цикл вообще не спрашиваем.
         * 3. Только потом ищем LOOP.
         */

        DropZone ifElseZone = FindIfElseDropZoneHard(eventData);

        if (ifElseZone != null)
        {
            NormalizeDropZone(ifElseZone);

            DebugDrop(
                $"[DROP DEBUG SELECT]" +
                $" selected=IFELSE" +
                $" zone={ifElseZone.name}" +
                $" zoneType={ifElseZone.zoneType}" +
                $" content={GetTransformPath(ifElseZone.content)}"
            );

            return ifElseZone;
        }

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        DropZone ifElseZoneByRaycast = FindIfElseDropZoneByRaycast(results);

        if (ifElseZoneByRaycast != null)
        {
            NormalizeDropZone(ifElseZoneByRaycast);

            DebugDrop(
                $"[DROP DEBUG SELECT]" +
                $" selected=IFELSE_RAYCAST" +
                $" zone={ifElseZoneByRaycast.name}" +
                $" zoneType={ifElseZoneByRaycast.zoneType}" +
                $" content={GetTransformPath(ifElseZoneByRaycast.content)}"
            );

            return ifElseZoneByRaycast;
        }

        DropZone loopZoneByGeometry = FindLoopDropZoneByPointerGeometry(eventData);

        if (loopZoneByGeometry != null)
        {
            NormalizeDropZone(loopZoneByGeometry);

            DebugDrop(
                $"[DROP DEBUG SELECT]" +
                $" selected=LOOP_GEOMETRY" +
                $" zone={loopZoneByGeometry.name}" +
                $" zoneType={loopZoneByGeometry.zoneType}" +
                $" content={GetTransformPath(loopZoneByGeometry.content)}"
            );

            return loopZoneByGeometry;
        }

        DropZone loopZoneByRaycast = FindLoopDropZoneByRaycast(results);

        if (loopZoneByRaycast != null)
        {
            NormalizeDropZone(loopZoneByRaycast);

            DebugDrop(
                $"[DROP DEBUG SELECT]" +
                $" selected=LOOP_RAYCAST" +
                $" zone={loopZoneByRaycast.name}" +
                $" zoneType={loopZoneByRaycast.zoneType}" +
                $" content={GetTransformPath(loopZoneByRaycast.content)}"
            );

            return loopZoneByRaycast;
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

            if (zone.zoneType == DropZoneType.LoopBranch)
                priority = 50;

            if (zone.zoneType == DropZoneType.IfBranch ||
                zone.zoneType == DropZoneType.ElseBranch)
            {
                priority = 100;
            }

            int depth = GetTransformDepth(zone.transform);
            int score = priority * 1000 + depth;

            if (score > bestScore)
            {
                bestScore = score;
                bestZone = zone;
            }
        }

        if (bestZone != null)
        {
            NormalizeDropZone(bestZone);

            DebugDrop(
                $"[DROP DEBUG SELECT]" +
                $" selected=BEST_RAYCAST" +
                $" zone={bestZone.name}" +
                $" zoneType={bestZone.zoneType}" +
                $" content={GetTransformPath(bestZone.content)}"
            );
        }
        else
        {
            DebugDrop("[DROP DEBUG SELECT] selected=NULL");
        }

        return bestZone;
    }

    private DropZone FindIfElseDropZoneHard(PointerEventData eventData)
    {
        if (eventData == null)
            return null;

        IfElseBlockUI[] ifBlocks = FindObjectsByType<IfElseBlockUI>(FindObjectsSortMode.None);

        IfElseBlockUI bestIfElse = null;
        RectTransform bestContent = null;
        DropZoneType bestType = DropZoneType.IfBranch;
        int bestDepth = -1;

        Camera cam = GetEventCamera(eventData);

        foreach (IfElseBlockUI ifElse in ifBlocks)
        {
            if (ifElse == null)
                continue;

            if (!ifElse.gameObject.activeInHierarchy)
                continue;

            if (!isPaletteSample && ifElse.gameObject == gameObject)
                continue;

            bool insideIf = BranchContainsPointer(ifElse.ifContent, eventData.position, cam);
            bool insideElse = BranchContainsPointer(ifElse.elseContent, eventData.position, cam);

            /*
             * Если конкретные ветки не поймались, но курсор внутри всего IfElseBlock,
             * выбираем ближайшую ветку. Это нужно для вложенности Loop -> IfElse.
             */
            if (!insideIf && !insideElse)
            {
                RectTransform ifElseRect = ifElse.transform as RectTransform;

                bool insideWholeIfElse = false;

                if (ifElseRect != null)
                {
                    insideWholeIfElse = RectTransformUtility.RectangleContainsScreenPoint(
                        ifElseRect,
                        eventData.position,
                        cam
                    );
                }

                if (insideWholeIfElse)
                {
                    float ifDistance = GetBranchScreenDistance(ifElse.ifContent, eventData.position, cam);
                    float elseDistance = GetBranchScreenDistance(ifElse.elseContent, eventData.position, cam);

                    if (ifDistance <= elseDistance)
                        insideIf = true;
                    else
                        insideElse = true;
                }
            }

            DebugDrop(
                $"[DROP DEBUG IFELSE CHECK]" +
                $" ifElse={GetTransformPath(ifElse.transform)}" +
                $" insideIf={insideIf}" +
                $" insideElse={insideElse}" +
                $" ifContent={(ifElse.ifContent != null ? GetTransformPath(ifElse.ifContent) : "NULL")}" +
                $" elseContent={(ifElse.elseContent != null ? GetTransformPath(ifElse.elseContent) : "NULL")}"
            );

            if (!insideIf && !insideElse)
                continue;

            int depth = GetTransformDepth(ifElse.transform);

            if (depth <= bestDepth)
                continue;

            bestDepth = depth;
            bestIfElse = ifElse;

            if (insideIf)
            {
                bestContent = ifElse.ifContent;
                bestType = DropZoneType.IfBranch;
            }
            else
            {
                bestContent = ifElse.elseContent;
                bestType = DropZoneType.ElseBranch;
            }
        }

        if (bestIfElse == null || bestContent == null)
            return null;

        return GetOrCreateBranchDropZone(bestContent, bestIfElse, bestType);
    }

    private bool BranchContainsPointer(RectTransform content, Vector2 screenPosition, Camera cam)
    {
        if (content == null)
            return false;

        if (RectTransformUtility.RectangleContainsScreenPoint(content, screenPosition, cam))
            return true;

        ScrollRect scrollRect = content.GetComponentInParent<ScrollRect>();

        if (scrollRect != null)
        {
            if (scrollRect.viewport != null &&
                RectTransformUtility.RectangleContainsScreenPoint(scrollRect.viewport, screenPosition, cam))
            {
                return true;
            }

            RectTransform scrollRT = scrollRect.transform as RectTransform;

            if (scrollRT != null &&
                RectTransformUtility.RectangleContainsScreenPoint(scrollRT, screenPosition, cam))
            {
                return true;
            }
        }

        RectTransform parentRT = content.parent as RectTransform;

        if (parentRT != null &&
            RectTransformUtility.RectangleContainsScreenPoint(parentRT, screenPosition, cam))
        {
            return true;
        }

        return false;
    }

    private float GetBranchScreenDistance(RectTransform content, Vector2 screenPosition, Camera cam)
    {
        if (content == null)
            return float.MaxValue;

        RectTransform basis = content;

        ScrollRect scrollRect = content.GetComponentInParent<ScrollRect>();

        if (scrollRect != null && scrollRect.viewport != null)
            basis = scrollRect.viewport;

        Vector3 worldCenter = basis.TransformPoint(basis.rect.center);
        Vector2 screenCenter = RectTransformUtility.WorldToScreenPoint(cam, worldCenter);

        return Vector2.SqrMagnitude(screenCenter - screenPosition);
    }

    private DropZone GetOrCreateBranchDropZone(
        RectTransform content,
        IfElseBlockUI owner,
        DropZoneType type
    )
    {
        if (content == null || owner == null)
            return null;

        /*
         * ВАЖНО:
         * DropZone берём/создаём именно на ifContent/elseContent.
         * НЕ ищем в родителях, потому что там может быть DropZone цикла.
         */
        DropZone zone = content.GetComponent<DropZone>();

        if (zone == null)
            zone = content.gameObject.AddComponent<DropZone>();

        zone.zoneType = type;
        zone.ownerIfElse = owner;
        zone.ownerLoop = null;
        zone.content = content;
        zone.workspaceContent = content;

        return zone;
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

            if (!loop.gameObject.activeInHierarchy)
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
        zone.ownerIfElse = null;
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

            if (!loop.gameObject.activeInHierarchy)
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
        zone.ownerIfElse = null;
        zone.zoneType = DropZoneType.LoopBranch;
        zone.content = bestLoop.loopContent;
        zone.workspaceContent = bestLoop.loopContent;

        return zone;
    }

    private DropZone FindIfElseDropZoneByRaycast(List<RaycastResult> results)
    {
        DropZone bestZone = null;
        int bestScore = int.MinValue;

        foreach (RaycastResult result in results)
        {
            IfElseBlockUI owner = result.gameObject.GetComponentInParent<IfElseBlockUI>();

            if (owner == null)
                continue;

            if (!owner.gameObject.activeInHierarchy)
                continue;

            if (!isPaletteSample && owner.gameObject == gameObject)
                continue;

            /*
             * Через raycast тоже не берём DropZone из родителей.
             * Выбираем ближайшую ветку по положению курсора.
             */
            Camera cam = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? rootCanvas.worldCamera
                : null;

            float ifDistance = GetBranchScreenDistance(owner.ifContent, Input.mousePosition, cam);
            float elseDistance = GetBranchScreenDistance(owner.elseContent, Input.mousePosition, cam);

            RectTransform content;
            DropZoneType type;

            if (ifDistance <= elseDistance)
            {
                content = owner.ifContent;
                type = DropZoneType.IfBranch;
            }
            else
            {
                content = owner.elseContent;
                type = DropZoneType.ElseBranch;
            }

            DropZone zone = GetOrCreateBranchDropZone(content, owner, type);

            if (zone == null)
                continue;

            int score = GetTransformDepth(owner.transform);

            if (score > bestScore)
            {
                bestScore = score;
                bestZone = zone;
            }
        }

        return bestZone;
    }

    private void DebugDrop(string message)
    {
        if (!debugDropSearch)
            return;

        Debug.Log(message);
    }

    private string GetTransformPath(Transform t)
    {
        if (t == null)
            return "NULL";

        string path = t.name;
        Transform current = t.parent;

        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
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