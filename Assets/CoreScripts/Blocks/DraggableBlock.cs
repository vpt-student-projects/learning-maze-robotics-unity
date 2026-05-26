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

        LoopBlockUI parentLoop = GetComponentInParent<LoopBlockUI>();

        BlockCommand cmd = GetComponent<BlockCommand>();

        if (cmd != null && chainManager != null)
            chainManager.UnregisterBlock(cmd);

        Destroy(gameObject);

        if (chainManager != null)
            chainManager.RefreshAllContainers();

        if (parentLoop != null)
            parentLoop.RefreshSize();
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

            /*
             * ВАЖНО:
             * Здесь больше НЕ вызываем chainManager.RefreshAllContainers().
             *
             * Когда мы только берём блок из палитры, workspace трогать нельзя.
             * Иначе BlockChainManager может пересчитать цепочку в момент,
             * когда Canvas/Layout ещё не стабилен, увидеть цикл пустым
             * и вернуть блок после цикла к старому BottomSnap.
             */

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
                /*
                 * ВАЖНО:
                 * Если взяли блок из палитры и передумали ставить,
                 * просто удаляем временный clone.
                 *
                 * НЕ вызываем RefreshAllContainers().
                 * Существующий алгоритм не должен пересчитываться,
                 * потому что фактически пользователь ничего не добавил.
                 */
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
                        SetRectToPointer(cloneRT, eventData, workspaceRT);

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

            /*
             * Для уже существующего блока refresh нужен,
             * потому что он мог быть Detach() в OnBeginDrag.
             */
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
    }

    private void ClearCloneRefs()
    {
        cloneGO = null;
        cloneRT = null;
        cloneCG = null;
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
        DropZone loopZoneByGeometry = FindLoopDropZoneByPointerGeometry(eventData);

        if (loopZoneByGeometry != null)
        {
            NormalizeDropZone(loopZoneByGeometry);
            return loopZoneByGeometry;
        }

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        DropZone loopZoneByRaycast = FindLoopDropZoneByRaycast(results);

        if (loopZoneByRaycast != null)
        {
            NormalizeDropZone(loopZoneByRaycast);
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