using System.Collections.Generic;
using UnityEngine;

public class BlockChainManager : MonoBehaviour
{
    [Header("Workspace")]
    public RectTransform workspaceRoot; // WorkspaceContent (куда складываются блоки)

    [Header("Snap")]
    public float snapDistance = 40f;    // насколько близко нужно отпустить, чтобы "прилипло"
    public float verticalGap = 8f;      // зазор между блоками в цепочке

    private readonly List<BlockCommand> blocks = new();

    public int DebugCount => blocks.Count;

    // -------- Registry --------

    public void RegisterBlock(BlockCommand b)
    {
        if (b == null) return;
        if (!blocks.Contains(b)) blocks.Add(b);
    }

    public void UnregisterBlock(BlockCommand b)
    {
        if (b == null) return;
        blocks.Remove(b);
        Detach(b);
    }

    public void ClearAll()
    {
        blocks.Clear();
    }

    /// <summary>
    /// На случай, если регистрация слетает — пересобираем список blocks из детей workspaceContent.
    /// Связи prev/next не создаём заново, но хотя бы blocks будет не пустой.
    /// </summary>
    public void RebuildFromWorkspace(Transform workspaceContent)
    {
        blocks.Clear();
        if (workspaceContent == null) return;

        var cmds = workspaceContent.GetComponentsInChildren<BlockCommand>(true);
        foreach (var c in cmds)
        {
            if (c == null) continue;
            // prev/next НЕ трогаем тут намеренно — цепочка могла уже быть построена снапом
            if (!blocks.Contains(c)) blocks.Add(c);
        }
    }

    // -------- Links --------

    public void Detach(BlockCommand b)
    {
        if (b == null) return;

        if (b.prev != null) b.prev.next = b.next;
        if (b.next != null) b.next.prev = b.prev;

        b.prev = null;
        b.next = null;
    }

    // -------- Snapping --------

    public void TrySnap(BlockCommand moving)
    {
        if (moving == null) return;

        // перед попыткой снапа — выкидываем из цепи
        Detach(moving);

        var movingSnap = moving.GetComponent<BlockSnapPoints>();
        var movingRT = moving.GetComponent<RectTransform>();
        if (movingSnap == null || movingRT == null) return;

        float bestDist = float.MaxValue;
        BlockCommand bestTarget = null;

        // attachAsNext:
        // true  => target.next = moving (движимый встанет НИЖЕ target)
        // false => moving.next = target (движимый встанет ВЫШЕ target)
        bool attachAsNext = true;

        foreach (var other in blocks)
        {
            if (other == null || other == moving) continue;

            var otherSnap = other.GetComponent<BlockSnapPoints>();
            if (otherSnap == null) continue;

            // other.bottom -> moving.top (moving ниже other)
            float d1 = Vector3.Distance(otherSnap.BottomWorld, movingSnap.TopWorld);
            if (d1 < bestDist)
            {
                bestDist = d1;
                bestTarget = other;
                attachAsNext = true;
            }

            // moving.bottom -> other.top (moving выше other)
            float d2 = Vector3.Distance(movingSnap.BottomWorld, otherSnap.TopWorld);
            if (d2 < bestDist)
            {
                bestDist = d2;
                bestTarget = other;
                attachAsNext = false;
            }
        }

        if (bestTarget == null) return;
        if (bestDist > snapDistance) return;

        // Линкуем (с учётом вставки в середину)
        if (attachAsNext)
        {
            // bestTarget -> moving -> oldNext
            var oldNext = bestTarget.next;

            bestTarget.next = moving;
            moving.prev = bestTarget;

            if (oldNext != null)
            {
                moving.next = oldNext;
                oldNext.prev = moving;
            }
        }
        else
        {
            // oldPrev -> moving -> bestTarget
            var oldPrev = bestTarget.prev;

            bestTarget.prev = moving;
            moving.next = bestTarget;

            if (oldPrev != null)
            {
                moving.prev = oldPrev;
                oldPrev.next = moving;
            }
        }

        // Выравниваем позиции по цепи
        LayoutFromChainRoot(moving);
    }

    private void LayoutFromChainRoot(BlockCommand anyInChain)
    {
        if (anyInChain == null) return;

        // найти корень (самый верхний)
        BlockCommand root = anyInChain;
        while (root.prev != null) root = root.prev;

        // идём вниз и ставим блоки ровно один под другим
        var cur = root;
        while (cur != null)
        {
            var curRT = cur.GetComponent<RectTransform>();
            var curSnap = cur.GetComponent<BlockSnapPoints>();
            if (curRT == null || curSnap == null) break;

            if (cur.next != null)
            {
                var nextRT = cur.next.GetComponent<RectTransform>();
                var nextSnap = cur.next.GetComponent<BlockSnapPoints>();
                if (nextRT == null || nextSnap == null) break;

                // хотим: next.top == cur.bottom - gap
                Vector3 desiredTop = curSnap.BottomWorld + Vector3.down * verticalGap;
                Vector3 delta = desiredTop - nextSnap.TopWorld;
                nextRT.position += delta;
            }

            cur = cur.next;
        }
    }

    // -------- Start detection --------

    public BlockCommand FindProgramStart()
    {
        // 1) если есть Start блок — берём его цепочку
        foreach (var b0 in blocks)
        {
            if (b0 == null) continue;
            if (b0.type != BlockType.Start) continue;

            var b = b0; // копия переменной, чтобы не ломать foreach
            while (b.prev != null) b = b.prev;
            return b;
        }

        // 2) иначе берём самый верхний по Y (для случая без Start)
        BlockCommand top = null;
        float bestY = float.NegativeInfinity;

        foreach (var b in blocks)
        {
            if (b == null) continue;
            var rt = b.GetComponent<RectTransform>();
            if (rt == null) continue;

            if (rt.position.y > bestY)
            {
                bestY = rt.position.y;
                top = b;
            }
        }

        if (top == null) return null;
        while (top.prev != null) top = top.prev;
        return top;
    }
}
