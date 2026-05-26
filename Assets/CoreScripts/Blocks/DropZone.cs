using UnityEngine;
using UnityEngine.UI;

public enum DropZoneType
{
    Workspace = 0,
    IfBranch = 1,
    ElseBranch = 2,
    LoopBranch = 3,

    // Алиасы для совместимости со старым кодом/инспектором.
    Loop = 3,
    LoopContent = 3,

    If = 1,
    IfContent = 1,
    IfTrue = 1,

    Else = 2,
    ElseContent = 2,
    IfFalse = 2
}

public class DropZone : MonoBehaviour
{
    [Header("Zone Type")]
    public DropZoneType zoneType = DropZoneType.Workspace;

    [Header("Workspace Content")]
    public RectTransform workspaceContent;

    [Header("Content where blocks will be placed")]
    public RectTransform content;

    [Header("Optional owner loop")]
    public LoopBlockUI ownerLoop;

    [Header("Optional owner if/else")]
    public IfElseBlockUI ownerIfElse;

    private void Awake()
    {
        Init();
    }

    private void OnEnable()
    {
        Init();
    }

    private void Reset()
    {
        Init();
    }

    private void Init()
    {
        if (content == null)
            content = transform as RectTransform;

        if (workspaceContent == null)
            workspaceContent = content;

        if (ownerLoop == null)
            ownerLoop = GetComponentInParent<LoopBlockUI>();

        if (ownerIfElse == null)
            ownerIfElse = GetComponentInParent<IfElseBlockUI>();

        /*
         * Если DropZone лежит внутри цикла, она должна принимать блоки
         * именно в loopContent и иметь тип LoopBranch.
         */
        if (ownerLoop != null)
        {
            zoneType = DropZoneType.LoopBranch;

            if (ownerLoop.loopContent != null)
                content = ownerLoop.loopContent;

            if (workspaceContent == null)
                workspaceContent = content;
        }
    }

    public bool CanAccept(Transform blockTransform)
    {
        if (blockTransform == null)
            return false;

        if (content == null)
            Init();

        if (content == null)
            return false;

        BlockCommand cmd = blockTransform.GetComponent<BlockCommand>();

        if (cmd == null)
            return false;

        return true;
    }

    public void Accept(Transform blockTransform)
    {
        if (blockTransform == null)
            return;

        Init();

        if (content == null)
            return;

        RectTransform rt = blockTransform as RectTransform;

        if (rt == null)
            return;

        BlockCommand cmd = blockTransform.GetComponent<BlockCommand>();

        BlockChainManager chainManager = FindObjectOfType<BlockChainManager>();

        if (chainManager != null && cmd != null)
            chainManager.Detach(cmd);

        /*
         * DropZone теперь ТОЛЬКО кладёт блок в нужный контейнер.
         * Она больше НЕ вызывает chainManager.RefreshAllContainers(),
         * потому что именно этот вызов по логам запускал LayoutFromChainRoot
         * слишком рано и ломал красивое соединение.
         */
        blockTransform.SetParent(content, false);

        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
        rt.anchoredPosition = Vector2.zero;

        blockTransform.SetAsLastSibling();

        Canvas.ForceUpdateCanvases();

        /*
         * Для цикла разрешаем только локальный RefreshSize.
         * Глобальный RefreshAllContainers тут НЕ вызываем.
         * Глобальный пересчёт/снап решает DraggableBlock после Accept().
         */
        if (ownerLoop != null)
        {
            ownerLoop.RefreshSize();
            Canvas.ForceUpdateCanvases();
        }
    }
}