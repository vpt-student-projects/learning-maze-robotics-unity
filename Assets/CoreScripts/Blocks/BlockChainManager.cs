using System.Collections.Generic;
using UnityEngine;

public class BlockChainManager : MonoBehaviour
{
    [Header("Workspace")]
    public RectTransform workspaceRoot;

    [Header("Snap")]
    public float snapDistance = 40f;
    public float verticalGap = 8f;

    private readonly List<BlockCommand> blocks = new List<BlockCommand>();

    public int DebugCount => blocks.Count;

    public void RegisterBlock(BlockCommand block)
    {
        if (block == null) return;

        if (!blocks.Contains(block))
            blocks.Add(block);
    }

    public void UnregisterBlock(BlockCommand block)
    {
        if (block == null) return;

        Detach(block);
        blocks.Remove(block);

        RefreshIfElseBranches();
    }

    public void ClearAll()
    {
        blocks.Clear();
    }

    public void RebuildFromWorkspace(RectTransform root)
    {
        workspaceRoot = root;
        blocks.Clear();

        if (workspaceRoot == null)
        {
            Debug.LogWarning("CHAIN: workspaceRoot is null");
            return;
        }

        BlockCommand previous = null;

        for (int i = 0; i < workspaceRoot.childCount; i++)
        {
            Transform child = workspaceRoot.GetChild(i);

            BlockCommand cmd = child.GetComponent<BlockCommand>();
            if (cmd == null) continue;

            cmd.prev = null;
            cmd.next = null;

            blocks.Add(cmd);

            if (previous != null)
            {
                previous.next = cmd;
                cmd.prev = previous;
            }

            previous = cmd;
        }

        Debug.Log($"CHAIN REBUILD MAIN ONLY: count={blocks.Count}");
    }

    public void Detach(BlockCommand block)
    {
        if (block == null) return;

        if (block.prev != null)
            block.prev.next = block.next;

        if (block.next != null)
            block.next.prev = block.prev;

        block.prev = null;
        block.next = null;
    }

    public void TrySnap(BlockCommand moving)
    {
        if (moving == null) return;

        Detach(moving);

        BlockSnapPoints movingSnap = moving.GetComponent<BlockSnapPoints>();
        RectTransform movingRT = moving.GetComponent<RectTransform>();

        if (movingSnap == null || movingRT == null)
        {
            RefreshIfElseBranches();
            return;
        }

        Transform movingParent = moving.transform.parent;

        float bestDist = float.MaxValue;
        BlockCommand bestTarget = null;
        bool attachAsNext = true;

        foreach (BlockCommand other in blocks)
        {
            if (other == null || other == moving)
                continue;

            if (other.transform.parent != movingParent)
                continue;

            BlockSnapPoints otherSnap = other.GetComponent<BlockSnapPoints>();
            if (otherSnap == null)
                continue;

            float d1 = Vector3.Distance(otherSnap.BottomWorld, movingSnap.TopWorld);

            if (d1 < bestDist)
            {
                bestDist = d1;
                bestTarget = other;
                attachAsNext = true;
            }

            float d2 = Vector3.Distance(movingSnap.BottomWorld, otherSnap.TopWorld);

            if (d2 < bestDist)
            {
                bestDist = d2;
                bestTarget = other;
                attachAsNext = false;
            }
        }

        if (bestTarget == null || bestDist > snapDistance)
        {
            RefreshIfElseBranches();
            return;
        }

        if (attachAsNext)
        {
            BlockCommand oldNext = bestTarget.next;

            bestTarget.next = moving;
            moving.prev = bestTarget;

            if (oldNext != null && oldNext.transform.parent == movingParent)
            {
                moving.next = oldNext;
                oldNext.prev = moving;
            }
        }
        else
        {
            BlockCommand oldPrev = bestTarget.prev;

            bestTarget.prev = moving;
            moving.next = bestTarget;

            if (oldPrev != null && oldPrev.transform.parent == movingParent)
            {
                moving.prev = oldPrev;
                oldPrev.next = moving;
            }
        }

        LayoutFromChainRoot(moving);
        RefreshIfElseBranches();
    }

    private void LayoutFromChainRoot(BlockCommand anyInChain)
    {
        if (anyInChain == null) return;

        BlockCommand root = anyInChain;

        while (root.prev != null)
            root = root.prev;

        BlockCommand cur = root;

        while (cur != null)
        {
            RectTransform curRT = cur.GetComponent<RectTransform>();
            BlockSnapPoints curSnap = cur.GetComponent<BlockSnapPoints>();

            if (curRT == null || curSnap == null)
                break;

            if (cur.next != null)
            {
                RectTransform nextRT = cur.next.GetComponent<RectTransform>();
                BlockSnapPoints nextSnap = cur.next.GetComponent<BlockSnapPoints>();

                if (nextRT == null || nextSnap == null)
                    break;

                Vector3 desiredTop = curSnap.BottomWorld + Vector3.down * verticalGap;
                Vector3 delta = desiredTop - nextSnap.TopWorld;

                nextRT.position += delta;
            }

            cur = cur.next;
        }
    }

    public void RefreshIfElseBranches()
    {
        if (workspaceRoot == null) return;

        IfElseBlockUI[] ifBlocks = workspaceRoot.GetComponentsInChildren<IfElseBlockUI>(true);

        foreach (IfElseBlockUI ifUI in ifBlocks)
        {
            if (ifUI == null || ifUI.command == null)
                continue;

            ifUI.command.trueBranchStart = RebuildBranchChain(ifUI.ifContent);
            ifUI.command.falseBranchStart = RebuildBranchChain(ifUI.elseContent);

            Debug.Log(
                $"IF REFRESH: {ifUI.command.name} | TRUE=" +
                $"{(ifUI.command.trueBranchStart != null ? ifUI.command.trueBranchStart.name : "empty")} | ELSE=" +
                $"{(ifUI.command.falseBranchStart != null ? ifUI.command.falseBranchStart.name : "empty")}"
            );
        }
    }

    private BlockCommand RebuildBranchChain(RectTransform content)
    {
        if (content == null)
            return null;

        BlockCommand first = null;
        BlockCommand previous = null;

        for (int i = 0; i < content.childCount; i++)
        {
            Transform child = content.GetChild(i);

            BlockCommand cmd = child.GetComponent<BlockCommand>();
            if (cmd == null)
                continue;

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

    public BlockCommand FindProgramStart()
    {
        foreach (BlockCommand block in blocks)
        {
            if (block != null && block.type == BlockType.Start)
                return block;
        }

        foreach (BlockCommand block in blocks)
        {
            if (block != null && block.prev == null)
                return block;
        }

        return null;
    }
}