using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BlockChainManager : MonoBehaviour
{
    [Header("Workspace")]
    public RectTransform workspaceRoot;

    [Header("Snap")]
    public float snapDistance = 40f;
    public float verticalGap = 8f;

    [Header("Workspace Auto Size")]
    public bool autoResizeWorkspace = true;
    public float workspaceMinHeight = 900f;
    public float workspaceBottomPadding = 260f;

    [Header("Workspace Chain Centering")]
    public bool centerWorkspaceChainX = true;
    public float workspaceChainCenterOffsetX = 0f;

    [Header("Debug")]
    public bool debugChainMove = false;
    public bool debugWorkspaceSize = false;
    public bool debugWorkspaceCentering = false;

    private readonly List<BlockCommand> blocks = new List<BlockCommand>();

    public int DebugCount => blocks.Count;

    private bool isRefreshing;
    private bool delayedRefreshRequested;

    public void RegisterBlock(BlockCommand block)
    {
        if (block == null)
            return;

        MakeBlockIgnoreUnityLayout(block.transform);

        if (!blocks.Contains(block))
            blocks.Add(block);
    }

    public void UnregisterBlock(BlockCommand block)
    {
        if (block == null)
            return;

        Detach(block);

        blocks.Remove(block);

        BlockCommand[] nestedBlocks = block.GetComponentsInChildren<BlockCommand>(true);

        foreach (BlockCommand nested in nestedBlocks)
        {
            if (nested != null)
            {
                Detach(nested);
                blocks.Remove(nested);
            }
        }

        RefreshAllContainers();
    }

    public void ClearAll()
    {
        blocks.Clear();
    }

    public void RebuildFromWorkspace(RectTransform root)
    {
        workspaceRoot = root;

        PrepareWorkspaceRootForManualHeight();

        RebuildAllChainsByHierarchy();
        RefreshAllContainers();

        Debug.Log($"CHAIN REBUILD BY HIERARCHY: count={blocks.Count}");
    }

    public void Detach(BlockCommand block)
    {
        if (block == null)
            return;

        if (block.prev != null)
            block.prev.next = block.next;

        if (block.next != null)
            block.next.prev = block.prev;

        block.prev = null;
        block.next = null;
    }

    public void TrySnap(BlockCommand moving)
    {
        if (moving == null)
            return;

        MakeBlockIgnoreUnityLayout(moving.transform);

        ForceWorkspaceLayoutNow();

        RebuildAllChainsByHierarchy();
        RefreshLoopBlockSizes();

        Detach(moving);

        BlockSnapPoints movingSnap = moving.GetComponent<BlockSnapPoints>();
        RectTransform movingRT = moving.GetComponent<RectTransform>();

        if (movingSnap == null || movingRT == null)
        {
            RefreshAllContainers();
            return;
        }

        Transform movingParent = moving.transform.parent;

        Vector3 movingTop = movingSnap.TopWorld;
        Vector3 movingBottom = movingSnap.BottomWorld;

        float bestDist = float.MaxValue;
        BlockCommand bestTarget = null;
        bool attachAsNext = true;

        foreach (BlockCommand other in blocks)
        {
            if (other == null || other == moving)
                continue;

            if (!other.gameObject.activeInHierarchy)
                continue;

            if (other.transform.parent != movingParent)
                continue;

            MakeBlockIgnoreUnityLayout(other.transform);

            LoopBlockUI otherLoop = other.GetComponent<LoopBlockUI>();

            if (otherLoop != null)
                otherLoop.RefreshSize();

            IfElseBlockUI otherIfElse = other.GetComponent<IfElseBlockUI>();

            if (otherIfElse != null)
                otherIfElse.RefreshSize();

            BlockSnapPoints otherSnap = other.GetComponent<BlockSnapPoints>();

            if (otherSnap == null)
                continue;

            Vector3 otherTop = otherSnap.TopWorld;
            Vector3 otherBottom = otherSnap.BottomWorld;

            bool movingIsReallyBelowOther = movingTop.y < otherBottom.y;

            if (movingIsReallyBelowOther)
            {
                Vector3 desiredMovingTop = otherBottom + Vector3.down * verticalGap;
                float distanceOtherBottomToMovingTop = Vector3.Distance(desiredMovingTop, movingTop);

                if (distanceOtherBottomToMovingTop < bestDist)
                {
                    bestDist = distanceOtherBottomToMovingTop;
                    bestTarget = other;
                    attachAsNext = true;
                }
            }

            bool movingIsReallyAboveOther = movingBottom.y > otherTop.y;

            if (movingIsReallyAboveOther)
            {
                Vector3 desiredMovingBottom = otherTop + Vector3.up * verticalGap;
                float distanceMovingBottomToOtherTop = Vector3.Distance(movingBottom, desiredMovingBottom);

                if (distanceMovingBottomToOtherTop < bestDist)
                {
                    bestDist = distanceMovingBottomToOtherTop;
                    bestTarget = other;
                    attachAsNext = false;
                }
            }
        }

        if (bestTarget == null || bestDist > snapDistance)
        {
            RefreshAllContainers();
            return;
        }

        MakeBlockIgnoreUnityLayout(bestTarget.transform);
        MakeBlockIgnoreUnityLayout(moving.transform);

        if (attachAsNext)
        {
            int newIndex = bestTarget.transform.GetSiblingIndex() + 1;
            moving.transform.SetSiblingIndex(newIndex);
        }
        else
        {
            int newIndex = bestTarget.transform.GetSiblingIndex();
            moving.transform.SetSiblingIndex(newIndex);
        }

        RefreshAllContainers();
    }

    public void RefreshAllContainers()
    {
        if (isRefreshing)
        {
            RequestDelayedRefresh();
            return;
        }

        isRefreshing = true;

        PrepareWorkspaceRootForManualHeight();

        for (int pass = 0; pass < 4; pass++)
        {
            MakeAllBlocksIgnoreUnityLayout();

            RebuildAllChainsByHierarchy();

            RefreshIfElseBranches();
            RefreshLoopBranches();
            RefreshLoopBlockSizes();

            ForceWorkspaceLayoutNow();

            MakeAllBlocksIgnoreUnityLayout();

            RebuildAllChainsByHierarchy();

            RelayoutAllContainersBottomUp();

            UpdateWorkspaceContentHeight();

            ForceCanvasOnly();
        }

        isRefreshing = false;

        RequestDelayedRefresh();
    }

    public void RefreshAllContainersImmediate()
    {
        RefreshAllContainers();
    }

    private void RequestDelayedRefresh()
    {
        if (!isActiveAndEnabled)
            return;

        if (delayedRefreshRequested)
            return;

        delayedRefreshRequested = true;
        StartCoroutine(DelayedRefreshRoutine());
    }

    private IEnumerator DelayedRefreshRoutine()
    {
        yield return null;

        delayedRefreshRequested = false;

        if (!isRefreshing)
            RefreshAllContainersNoDelay();
    }

    private void RefreshAllContainersNoDelay()
    {
        if (isRefreshing)
            return;

        isRefreshing = true;

        PrepareWorkspaceRootForManualHeight();

        for (int pass = 0; pass < 3; pass++)
        {
            MakeAllBlocksIgnoreUnityLayout();

            RebuildAllChainsByHierarchy();

            RefreshIfElseBranches();
            RefreshLoopBranches();
            RefreshLoopBlockSizes();

            ForceWorkspaceLayoutNow();

            MakeAllBlocksIgnoreUnityLayout();

            RebuildAllChainsByHierarchy();

            RelayoutAllContainersBottomUp();

            UpdateWorkspaceContentHeight();

            ForceCanvasOnly();
        }

        isRefreshing = false;
    }

    private void ForceWorkspaceLayoutNow()
    {
        PrepareWorkspaceRootForManualHeight();

        Canvas.ForceUpdateCanvases();

        if (workspaceRoot != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(workspaceRoot);

        Canvas.ForceUpdateCanvases();
    }

    private void ForceCanvasOnly()
    {
        Canvas.ForceUpdateCanvases();
    }

    private void PrepareWorkspaceRootForManualHeight()
    {
        if (workspaceRoot == null)
            return;

        ContentSizeFitter fitter = workspaceRoot.GetComponent<ContentSizeFitter>();

        if (fitter != null)
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        VerticalLayoutGroup verticalLayout = workspaceRoot.GetComponent<VerticalLayoutGroup>();

        if (verticalLayout != null)
        {
            verticalLayout.childControlHeight = false;
            verticalLayout.childForceExpandHeight = false;
            verticalLayout.childControlWidth = false;
            verticalLayout.childForceExpandWidth = false;
        }

        HorizontalLayoutGroup horizontalLayout = workspaceRoot.GetComponent<HorizontalLayoutGroup>();

        if (horizontalLayout != null)
        {
            horizontalLayout.childControlHeight = false;
            horizontalLayout.childForceExpandHeight = false;
            horizontalLayout.childControlWidth = false;
            horizontalLayout.childForceExpandWidth = false;
        }

        GridLayoutGroup gridLayout = workspaceRoot.GetComponent<GridLayoutGroup>();

        if (gridLayout != null)
            gridLayout.enabled = false;

        LayoutElement rootLayoutElement = workspaceRoot.GetComponent<LayoutElement>();

        if (rootLayoutElement == null)
            rootLayoutElement = workspaceRoot.gameObject.AddComponent<LayoutElement>();

        rootLayoutElement.ignoreLayout = false;
        rootLayoutElement.flexibleHeight = 0f;
    }

    private void MakeAllBlocksIgnoreUnityLayout()
    {
        if (workspaceRoot == null)
            return;

        BlockCommand[] allBlocks = workspaceRoot.GetComponentsInChildren<BlockCommand>(true);

        foreach (BlockCommand block in allBlocks)
        {
            if (block == null)
                continue;

            if (!block.gameObject.activeInHierarchy)
                continue;

            MakeBlockIgnoreUnityLayout(block.transform);
        }
    }

    private void MakeBlockIgnoreUnityLayout(Transform blockTransform)
    {
        if (blockTransform == null)
            return;

        if (blockTransform.GetComponent<BlockCommand>() == null)
            return;

        LayoutElement layoutElement = blockTransform.GetComponent<LayoutElement>();

        if (layoutElement == null)
            layoutElement = blockTransform.gameObject.AddComponent<LayoutElement>();

        layoutElement.ignoreLayout = true;
    }

    public void RebuildAllChainsByHierarchy()
    {
        if (workspaceRoot == null)
            return;

        blocks.Clear();

        List<RectTransform> containers = GetAllBlockContainersBottomUp();

        if (!containers.Contains(workspaceRoot))
            containers.Add(workspaceRoot);

        foreach (RectTransform container in containers)
            RebuildContainerChainByHierarchy(container);
    }

    private BlockCommand RebuildContainerChainByHierarchy(RectTransform container)
    {
        if (container == null)
            return null;

        BlockCommand first = null;
        BlockCommand previous = null;

        for (int i = 0; i < container.childCount; i++)
        {
            Transform child = container.GetChild(i);

            if (child == null)
                continue;

            if (!child.gameObject.activeInHierarchy)
                continue;

            BlockCommand cmd = child.GetComponent<BlockCommand>();

            if (cmd == null)
                continue;

            MakeBlockIgnoreUnityLayout(child);

            RegisterBlock(cmd);

            cmd.prev = null;
            cmd.next = null;

            if (first == null)
                first = cmd;

            if (previous != null)
            {
                previous.next = cmd;
                cmd.prev = previous;
            }

            previous = cmd;
        }

        return first;
    }

    public void RefreshLoopBlockSizes()
    {
        List<LoopBlockUI> loops = GetLoopsBottomUp();

        foreach (LoopBlockUI loop in loops)
        {
            if (loop == null)
                continue;

            if (!loop.gameObject.activeInHierarchy)
                continue;

            MakeBlockIgnoreUnityLayout(loop.transform);
            loop.RefreshSize();
        }
    }

    private List<LoopBlockUI> GetLoopsBottomUp()
    {
        List<LoopBlockUI> result = new List<LoopBlockUI>();

        if (workspaceRoot == null)
            return result;

        LoopBlockUI[] loops = workspaceRoot.GetComponentsInChildren<LoopBlockUI>(true);

        foreach (LoopBlockUI loop in loops)
        {
            if (loop == null)
                continue;

            if (!loop.gameObject.activeInHierarchy)
                continue;

            result.Add(loop);
        }

        result.Sort((a, b) =>
            GetTransformDepth(b.transform).CompareTo(GetTransformDepth(a.transform))
        );

        return result;
    }

    private List<IfElseBlockUI> GetIfElseBlocksBottomUp()
    {
        List<IfElseBlockUI> result = new List<IfElseBlockUI>();

        if (workspaceRoot == null)
            return result;

        IfElseBlockUI[] ifBlocks = workspaceRoot.GetComponentsInChildren<IfElseBlockUI>(true);

        foreach (IfElseBlockUI ifUI in ifBlocks)
        {
            if (ifUI == null)
                continue;

            if (!ifUI.gameObject.activeInHierarchy)
                continue;

            result.Add(ifUI);
        }

        result.Sort((a, b) =>
            GetTransformDepth(b.transform).CompareTo(GetTransformDepth(a.transform))
        );

        return result;
    }

    private List<RectTransform> GetAllBlockContainersBottomUp()
    {
        List<RectTransform> containers = new List<RectTransform>();

        if (workspaceRoot == null)
            return containers;

        LoopBlockUI[] loops = workspaceRoot.GetComponentsInChildren<LoopBlockUI>(true);

        foreach (LoopBlockUI loop in loops)
        {
            if (loop == null || !loop.gameObject.activeInHierarchy)
                continue;

            if (loop.loopContent != null && !containers.Contains(loop.loopContent))
                containers.Add(loop.loopContent);
        }

        IfElseBlockUI[] ifBlocks = workspaceRoot.GetComponentsInChildren<IfElseBlockUI>(true);

        foreach (IfElseBlockUI ifUI in ifBlocks)
        {
            if (ifUI == null || !ifUI.gameObject.activeInHierarchy)
                continue;

            if (ifUI.ifContent != null && !containers.Contains(ifUI.ifContent))
                containers.Add(ifUI.ifContent);

            if (ifUI.elseContent != null && !containers.Contains(ifUI.elseContent))
                containers.Add(ifUI.elseContent);
        }

        containers.Sort((a, b) =>
            GetTransformDepth(b).CompareTo(GetTransformDepth(a))
        );

        return containers;
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

    private bool IsLoopContentContainer(RectTransform container)
    {
        if (container == null || workspaceRoot == null)
            return false;

        LoopBlockUI[] loops = workspaceRoot.GetComponentsInChildren<LoopBlockUI>(true);

        foreach (LoopBlockUI loop in loops)
        {
            if (loop == null || !loop.gameObject.activeInHierarchy)
                continue;

            if (loop.loopContent == container)
                return true;
        }

        return false;
    }

    private bool IsIfElseContentContainer(RectTransform container)
    {
        if (container == null || workspaceRoot == null)
            return false;

        IfElseBlockUI[] ifBlocks = workspaceRoot.GetComponentsInChildren<IfElseBlockUI>(true);

        foreach (IfElseBlockUI ifUI in ifBlocks)
        {
            if (ifUI == null || !ifUI.gameObject.activeInHierarchy)
                continue;

            if (ifUI.ifContent == container)
                return true;

            if (ifUI.elseContent == container)
                return true;
        }

        return false;
    }

    private void RelayoutAllContainersBottomUp()
    {
        if (workspaceRoot == null)
            return;

        List<RectTransform> containers = GetAllBlockContainersBottomUp();

        foreach (RectTransform container in containers)
        {
            if (container == null)
                continue;

            if (IsLoopContentContainer(container) || IsIfElseContentContainer(container))
            {
                RebuildContainerChainByHierarchy(container);
                continue;
            }

            RelayoutContainerByHierarchyChain(container);
            ForceCanvasOnly();
        }

        RelayoutContainerByHierarchyChain(workspaceRoot);
        ForceCanvasOnly();
    }

    private void RelayoutContainerByHierarchyChain(RectTransform container)
    {
        if (container == null)
            return;

        RebuildContainerChainByHierarchy(container);

        if (IsLoopContentContainer(container) || IsIfElseContentContainer(container))
            return;

        BlockCommand first = null;

        for (int i = 0; i < container.childCount; i++)
        {
            Transform child = container.GetChild(i);

            if (child == null)
                continue;

            if (!child.gameObject.activeInHierarchy)
                continue;

            BlockCommand cmd = child.GetComponent<BlockCommand>();

            if (cmd != null)
            {
                first = cmd;
                break;
            }
        }

        if (first == null)
            return;

        bool shouldCenterRootX = container == workspaceRoot && centerWorkspaceChainX;

        LayoutFromChainRoot(first, container, shouldCenterRootX);
    }

    private void LayoutFromChainRoot(
        BlockCommand anyInChain,
        RectTransform container,
        bool centerRootX
    )
    {
        if (anyInChain == null)
            return;

        if (!anyInChain.gameObject.activeInHierarchy)
            return;

        BlockCommand root = anyInChain;

        while (root.prev != null &&
               root.prev.gameObject.activeInHierarchy &&
               root.prev.transform.parent == root.transform.parent)
        {
            root = root.prev;
        }

        if (centerRootX)
            CenterChainRootX(root, container);

        BlockCommand cur = root;

        while (cur != null)
        {
            if (!cur.gameObject.activeInHierarchy)
                break;

            MakeBlockIgnoreUnityLayout(cur.transform);

            LoopBlockUI curLoop = cur.GetComponent<LoopBlockUI>();

            if (curLoop != null)
                curLoop.RefreshSize();

            IfElseBlockUI curIfElse = cur.GetComponent<IfElseBlockUI>();

            if (curIfElse != null)
                curIfElse.RefreshSize();

            BlockSnapPoints curSnap = cur.GetComponent<BlockSnapPoints>();

            if (curSnap == null)
                break;

            if (cur.next != null &&
                cur.next.gameObject.activeInHierarchy &&
                cur.next.transform.parent == cur.transform.parent)
            {
                MakeBlockIgnoreUnityLayout(cur.next.transform);

                LoopBlockUI nextLoop = cur.next.GetComponent<LoopBlockUI>();

                if (nextLoop != null)
                    nextLoop.RefreshSize();

                IfElseBlockUI nextIfElse = cur.next.GetComponent<IfElseBlockUI>();

                if (nextIfElse != null)
                    nextIfElse.RefreshSize();

                RectTransform nextRT = cur.next.GetComponent<RectTransform>();
                BlockSnapPoints nextSnap = cur.next.GetComponent<BlockSnapPoints>();

                if (nextRT == null || nextSnap == null)
                    break;

                Vector3 curBottom = curSnap.BottomWorld;
                Vector3 nextTopBefore = nextSnap.TopWorld;

                Vector3 desiredTop = curBottom + Vector3.down * verticalGap;
                Vector3 delta = desiredTop - nextTopBefore;

                if (debugChainMove)
                {
                    Debug.Log(
                        $"[CHAIN MOVE DEBUG]" +
                        $" parentPath={GetTransformPath(cur.transform.parent)}" +
                        $" cur={cur.name}" +
                        $" curID={cur.GetInstanceID()}" +
                        $" curPath={GetTransformPath(cur.transform)}" +
                        $" curIndex={cur.transform.GetSiblingIndex()}" +
                        $" curBottom={curBottom}" +
                        $" next={cur.next.name}" +
                        $" nextID={cur.next.GetInstanceID()}" +
                        $" nextPath={GetTransformPath(cur.next.transform)}" +
                        $" nextIndex={cur.next.transform.GetSiblingIndex()}" +
                        $" nextTopBefore={nextTopBefore}" +
                        $" desiredTop={desiredTop}" +
                        $" delta={delta}"
                    );
                }

                nextRT.position += delta;

                ForceCanvasOnly();

                Vector3 nextTopAfter = nextSnap.TopWorld;

                if (debugChainMove)
                {
                    Debug.Log(
                        $"[CHAIN MOVE AFTER DEBUG]" +
                        $" parentPath={GetTransformPath(cur.transform.parent)}" +
                        $" cur={cur.name}" +
                        $" curID={cur.GetInstanceID()}" +
                        $" next={cur.next.name}" +
                        $" nextID={cur.next.GetInstanceID()}" +
                        $" nextPath={GetTransformPath(cur.next.transform)}" +
                        $" nextTopAfter={nextTopAfter}" +
                        $" nextWorldPos={nextRT.position}" +
                        $" nextAnchored={nextRT.anchoredPosition}"
                    );
                }
            }

            if (cur.next != null && cur.next.transform.parent != cur.transform.parent)
                break;

            cur = cur.next;
        }
    }

    private void CenterChainRootX(BlockCommand root, RectTransform container)
    {
        if (root == null || container == null)
            return;

        RectTransform rootRT = root.transform as RectTransform;

        if (rootRT == null)
            return;

        BlockSnapPoints rootSnap = root.GetComponent<BlockSnapPoints>();

        Vector3 rootTopWorld;

        if (rootSnap != null)
            rootTopWorld = rootSnap.TopWorld;
        else
            rootTopWorld = rootRT.position;

        float containerCenterWorldX = GetContainerCenterWorldX(container);
        float offsetWorldX = container.TransformVector(new Vector3(workspaceChainCenterOffsetX, 0f, 0f)).x;

        Vector3 desiredTopWorld = rootTopWorld;
        desiredTopWorld.x = containerCenterWorldX + offsetWorldX;

        Vector3 delta = desiredTopWorld - rootTopWorld;
        delta.y = 0f;
        delta.z = 0f;

        rootRT.position += delta;

        ForceCanvasOnly();

        if (debugWorkspaceCentering)
        {
            Vector3 afterTopWorld;

            if (rootSnap != null)
                afterTopWorld = rootSnap.TopWorld;
            else
                afterTopWorld = rootRT.position;

            Debug.Log(
                $"[WORKSPACE CENTER DEBUG]" +
                $" root={root.name}" +
                $" container={GetTransformPath(container)}" +
                $" beforeTop={rootTopWorld}" +
                $" desiredTop={desiredTopWorld}" +
                $" afterTop={afterTopWorld}" +
                $" delta={delta}"
            );
        }
    }

    private float GetContainerCenterWorldX(RectTransform container)
    {
        if (container == null)
            return 0f;

        Vector3 localCenter = new Vector3(
            container.rect.center.x,
            container.rect.center.y,
            0f
        );

        Vector3 worldCenter = container.TransformPoint(localCenter);

        return worldCenter.x;
    }

    private void UpdateWorkspaceContentHeight()
    {
        if (!autoResizeWorkspace)
            return;

        if (workspaceRoot == null)
            return;

        PrepareWorkspaceRootForManualHeight();

        float lowestY = 0f;
        bool foundAnyBlock = false;

        BlockCommand[] workspaceBlocks = workspaceRoot.GetComponentsInChildren<BlockCommand>(true);

        foreach (BlockCommand block in workspaceBlocks)
        {
            if (block == null)
                continue;

            if (!block.gameObject.activeInHierarchy)
                continue;

            if (block.transform.parent != workspaceRoot)
                continue;

            RectTransform blockRT = block.transform as RectTransform;

            if (blockRT == null)
                continue;

            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                workspaceRoot,
                blockRT
            );

            if (!foundAnyBlock)
            {
                lowestY = bounds.min.y;
                foundAnyBlock = true;
            }
            else
            {
                lowestY = Mathf.Min(lowestY, bounds.min.y);
            }
        }

        float neededHeight = workspaceMinHeight;

        if (foundAnyBlock)
            neededHeight = Mathf.Abs(lowestY) + workspaceBottomPadding;

        neededHeight = Mathf.Max(neededHeight, workspaceMinHeight);

        float currentHeight = workspaceRoot.rect.height;

        LayoutElement rootLayoutElement = workspaceRoot.GetComponent<LayoutElement>();

        if (rootLayoutElement == null)
            rootLayoutElement = workspaceRoot.gameObject.AddComponent<LayoutElement>();

        rootLayoutElement.ignoreLayout = false;
        rootLayoutElement.minHeight = neededHeight;
        rootLayoutElement.preferredHeight = neededHeight;
        rootLayoutElement.flexibleHeight = 0f;

        workspaceRoot.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            neededHeight
        );

        Vector2 sizeDelta = workspaceRoot.sizeDelta;
        sizeDelta.y = neededHeight;
        workspaceRoot.sizeDelta = sizeDelta;

        Canvas.ForceUpdateCanvases();

        RectTransform parentRT = workspaceRoot.parent as RectTransform;

        if (parentRT != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(parentRT);

        Canvas.ForceUpdateCanvases();

        workspaceRoot.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            neededHeight
        );

        sizeDelta = workspaceRoot.sizeDelta;
        sizeDelta.y = neededHeight;
        workspaceRoot.sizeDelta = sizeDelta;

        Canvas.ForceUpdateCanvases();

        if (debugWorkspaceSize)
        {
            Debug.Log(
                $"[WORKSPACE SIZE DEBUG FIXED]" +
                $" neededHeight={neededHeight}" +
                $" oldHeight={currentHeight}" +
                $" newRectHeight={workspaceRoot.rect.height}" +
                $" sizeDeltaY={workspaceRoot.sizeDelta.y}" +
                $" lowestY={lowestY}" +
                $" foundAnyBlock={foundAnyBlock}" +
                $" childCount={workspaceRoot.childCount}" +
                $" path={GetTransformPath(workspaceRoot)}"
            );
        }
    }

    public void RefreshIfElseBranches()
    {
        if (workspaceRoot == null)
            return;

        List<IfElseBlockUI> ifBlocks = GetIfElseBlocksBottomUp();

        foreach (IfElseBlockUI ifUI in ifBlocks)
        {
            if (ifUI == null || ifUI.command == null)
                continue;

            MakeBlockIgnoreUnityLayout(ifUI.transform);

            ifUI.command.trueBranchStart = RebuildContainerChainByHierarchy(ifUI.ifContent);
            ifUI.command.falseBranchStart = RebuildContainerChainByHierarchy(ifUI.elseContent);
            ifUI.RefreshSize();
        }
    }

    public void RefreshLoopBranches()
    {
        List<LoopBlockUI> loopBlocks = GetLoopsBottomUp();

        foreach (LoopBlockUI loopUI in loopBlocks)
        {
            if (loopUI == null || loopUI.command == null)
                continue;

            MakeBlockIgnoreUnityLayout(loopUI.transform);

            loopUI.ApplyToCommand();
            loopUI.command.loopBranchStart = RebuildContainerChainByHierarchy(loopUI.loopContent);
            loopUI.RefreshSize();
        }
    }

    public BlockCommand FindProgramStart()
    {
        if (workspaceRoot == null)
            return null;

        RebuildAllChainsByHierarchy();
        RefreshLoopBranches();
        RefreshIfElseBranches();

        foreach (BlockCommand block in blocks)
        {
            if (block != null &&
                block.gameObject.activeInHierarchy &&
                block.type == BlockType.Start &&
                block.transform.parent == workspaceRoot)
            {
                return block;
            }
        }

        foreach (BlockCommand block in blocks)
        {
            if (block != null &&
                block.gameObject.activeInHierarchy &&
                block.prev == null &&
                block.transform.parent == workspaceRoot)
            {
                return block;
            }
        }

        return null;
    }

    private int CountCommandChildren(RectTransform container)
    {
        if (container == null)
            return 0;

        int count = 0;

        for (int i = 0; i < container.childCount; i++)
        {
            Transform child = container.GetChild(i);

            if (child == null)
                continue;

            if (!child.gameObject.activeInHierarchy)
                continue;

            if (child.GetComponent<BlockCommand>() != null)
                count++;
        }

        return count;
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
}