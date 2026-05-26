using UnityEngine;

public class DeleteBlock : MonoBehaviour
{
    [Header("Debug")]
    public bool debugDelete = true;

    public void DeleteSelf()
    {
        GameObject blockObject = GetBlockObject();

        if (blockObject == null)
        {
            Debug.LogWarning("[DELETE DEBUG] blockObject=NULL");
            return;
        }

        BlockCommand command = blockObject.GetComponent<BlockCommand>();

        if (command == null)
        {
            Debug.LogWarning($"[DELETE DEBUG] BlockCommand not found on {blockObject.name}");
            return;
        }

        Transform originalParent = blockObject.transform.parent;
        int originalSiblingIndex = blockObject.transform.GetSiblingIndex();

        Vector3 removedTopWorld = GetBlockTopWorld(blockObject);

        LoopBlockUI parentLoop = blockObject.GetComponentInParent<LoopBlockUI>();
        IfElseBlockUI parentIfElse = blockObject.GetComponentInParent<IfElseBlockUI>();

        BlockChainManager chainManager = FindAnyObjectByType<BlockChainManager>();

        if (debugDelete)
        {
            Debug.Log(
                $"[DELETE DEBUG BEFORE]" +
                $" block={blockObject.name}" +
                $" blockID={blockObject.GetInstanceID()}" +
                $" originalParent={(originalParent != null ? GetTransformPath(originalParent) : "NULL")}" +
                $" siblingIndex={originalSiblingIndex}" +
                $" removedTopWorld={removedTopWorld}" +
                $" hasChainManager={(chainManager != null)}" +
                $" parentLoop={(parentLoop != null ? parentLoop.name : "NULL")}" +
                $" parentIfElse={(parentIfElse != null ? parentIfElse.name : "NULL")}"
            );
        }

        /*
         * Destroy удаляет объект только в конце кадра.
         * Поэтому сначала переносим удаляемый блок во временный контейнер
         * вне WorkPanel / loopContent / ifContent.
         */
        Transform trashRoot = GetOrCreateTrashRoot();

        blockObject.transform.SetParent(trashRoot, true);

        CanvasGroup canvasGroup = blockObject.GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = blockObject.AddComponent<CanvasGroup>();

        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        if (chainManager != null)
            chainManager.Detach(command);

        if (chainManager != null)
            chainManager.UnregisterBlock(command);

        /*
         * ГЛАВНЫЙ ФИКС:
         * Если удалили самый первый блок в контейнере, следующий блок становится новым root.
         * BlockChainManager не знает, куда ставить root, поэтому он остаётся ниже.
         * Мы вручную переносим новый первый блок туда, где был TopSnap удалённого блока.
         */
        if (originalSiblingIndex == 0 && originalParent != null)
            MoveNewFirstBlockToRemovedTop(originalParent, removedTopWorld);

        if (parentLoop != null)
            parentLoop.RefreshSize();

        if (parentIfElse != null)
            parentIfElse.RefreshSize();

        if (chainManager != null)
        {
            chainManager.RebuildAllChainsByHierarchy();
            chainManager.RefreshAllContainers();
        }

        if (debugDelete)
        {
            Debug.Log(
                $"[DELETE DEBUG AFTER MOVE]" +
                $" block={blockObject.name}" +
                $" newParent={GetTransformPath(blockObject.transform.parent)}" +
                $" originalParentChildCount={(originalParent != null ? originalParent.childCount.ToString() : "NULL")}"
            );
        }

        Destroy(blockObject);

        if (parentLoop != null)
            parentLoop.RefreshSize();

        if (parentIfElse != null)
            parentIfElse.RefreshSize();

        if (chainManager != null)
        {
            chainManager.RebuildAllChainsByHierarchy();
            chainManager.RefreshAllContainers();
        }
    }

    private void MoveNewFirstBlockToRemovedTop(Transform originalParent, Vector3 removedTopWorld)
    {
        if (originalParent == null)
            return;

        BlockCommand newFirst = FindFirstActiveBlockInContainer(originalParent);

        if (newFirst == null)
        {
            if (debugDelete)
            {
                Debug.Log(
                    $"[DELETE DEBUG COMPACT]" +
                    $" parent={GetTransformPath(originalParent)}" +
                    $" result=NO_FIRST_BLOCK"
                );
            }

            return;
        }

        RectTransform firstRT = newFirst.transform as RectTransform;

        if (firstRT == null)
            return;

        Vector3 firstTopWorld = GetBlockTopWorld(newFirst.gameObject);
        Vector3 delta = removedTopWorld - firstTopWorld;

        firstRT.position += delta;

        Canvas.ForceUpdateCanvases();

        if (debugDelete)
        {
            Debug.Log(
                $"[DELETE DEBUG COMPACT]" +
                $" parent={GetTransformPath(originalParent)}" +
                $" newFirst={newFirst.name}" +
                $" removedTopWorld={removedTopWorld}" +
                $" firstTopBefore={firstTopWorld}" +
                $" delta={delta}" +
                $" firstTopAfter={GetBlockTopWorld(newFirst.gameObject)}"
            );
        }
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

    private GameObject GetBlockObject()
    {
        /*
         * Обычно DeleteBlock висит на кнопке Delete,
         * а сам блок — это parent кнопки.
         */
        if (transform.parent != null && transform.parent.GetComponent<BlockCommand>() != null)
            return transform.parent.gameObject;

        /*
         * Если кнопка лежит глубже, ищем первый BlockCommand выше.
         */
        BlockCommand commandInParent = GetComponentInParent<BlockCommand>();

        if (commandInParent != null)
            return commandInParent.gameObject;

        return null;
    }

    private Transform GetOrCreateTrashRoot()
    {
        Canvas canvas = GetComponentInParent<Canvas>();

        Transform root = canvas != null
            ? canvas.transform
            : transform.root;

        Transform existing = root.Find("__DeletedBlocksTrash");

        if (existing != null)
            return existing;

        GameObject trash = new GameObject("__DeletedBlocksTrash", typeof(RectTransform));

        RectTransform trashRT = trash.GetComponent<RectTransform>();

        trashRT.SetParent(root, false);
        trashRT.anchorMin = new Vector2(0f, 0f);
        trashRT.anchorMax = new Vector2(0f, 0f);
        trashRT.pivot = new Vector2(0f, 0f);
        trashRT.anchoredPosition = new Vector2(-10000f, -10000f);
        trashRT.sizeDelta = Vector2.zero;
        trashRT.localScale = Vector3.one;

        return trashRT;
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