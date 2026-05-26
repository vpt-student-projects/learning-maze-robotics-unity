using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LoopBlockUI : MonoBehaviour
{
    [Header("Command")]
    public BlockCommand command;

    [Header("Mode UI")]
    public TMP_Dropdown modeDropdown;

    [Header("Repeat UI")]
    public TMP_InputField repeatInput;
    public GameObject repeatInputRoot;
    public TMP_Text repeatSuffixText;

    [Header("Center Mode UI")]
    public GameObject untilCenterLabelRoot;

    [Header("DO NOT TOUCH BY SCRIPT")]
    public RectTransform rootRect;
    public RectTransform header;

    [Header("ONLY THESE ARE CHANGED")]
    public RectTransform bodyRoot;
    public RectTransform loopContent;
    public RectTransform footer;
    public RectTransform bottomSnap;

    [Header("Layout")]
    public float paddingTop = 8f;
    public float paddingBottom = 8f;
    public float spacing = 8f;

    [Header("Child Position Inside Content")]
    public float childX = 0f;

    [Header("Fallback")]
    public float defaultBlockHeight = 60f;

    [Header("Debug")]
    public bool debugLoopSize = false;

    private bool initialized;
    private bool refreshing;

    private float baseBodyHeight;
    private float baseContentHeight;
    private float baseVisualHeight;
    private float currentVisualHeight;

    private Vector2 baseBodyPos;
    private Vector2 baseContentPos;
    private Vector2 baseFooterPos;
    private Vector2 baseBottomSnapPos;

    private int lastBlockChildCount = -1;
    private float lastUsedGrow = -1f;

    public float CurrentVisualHeight
    {
        get
        {
            if (!initialized)
                CacheBaseValues();

            return Mathf.Max(currentVisualHeight, baseVisualHeight, defaultBlockHeight);
        }
    }

    private void Awake()
    {
        if (command == null)
            command = GetComponent<BlockCommand>();

        if (rootRect == null)
            rootRect = GetComponent<RectTransform>();

        DisableUnityAutoLayoutOnLoopContent();
        SyncSnapPointReferences();
    }

    private void Start()
    {
        CacheBaseValues();
        LoadFromCommand();

        if (repeatInput != null)
            repeatInput.onValueChanged.AddListener(_ =>
            {
                ApplyToCommand();
                RefreshSize();
            });

        if (modeDropdown != null)
            modeDropdown.onValueChanged.AddListener(_ =>
            {
                ApplyToCommand();
                RefreshSize();
            });

        RefreshSize();
    }

    private void OnEnable()
    {
        DisableUnityAutoLayoutOnLoopContent();
        SyncSnapPointReferences();

        if (initialized)
            RefreshSize();
    }

    /*
     * ВАЖНО:
     * Здесь больше НЕТ LateUpdate() с постоянным RefreshSize().
     *
     * Раньше цикл пересчитывался каждый кадр.
     * Когда ты скроллил WorkPanel колесиком/двумя пальцами,
     * Unity на кадр могла дать другие bounds, и цикл думал,
     * что внутри стало больше места/блоков.
     *
     * Теперь размер цикла обновляется только когда его реально зовут:
     * BlockChainManager, Drop/Drag, DeleteBlock или UI самого цикла.
     */

    private void DisableUnityAutoLayoutOnLoopContent()
    {
        if (loopContent == null)
            return;

        LayoutGroup[] layoutGroups = loopContent.GetComponents<LayoutGroup>();

        foreach (LayoutGroup layoutGroup in layoutGroups)
        {
            if (layoutGroup != null)
                layoutGroup.enabled = false;
        }

        ContentSizeFitter fitter = loopContent.GetComponent<ContentSizeFitter>();

        if (fitter != null)
            fitter.enabled = false;
    }

    private void CacheBaseValues()
    {
        if (initialized)
            return;

        if (rootRect == null)
            rootRect = GetComponent<RectTransform>();

        DisableUnityAutoLayoutOnLoopContent();
        SyncSnapPointReferences();
        Canvas.ForceUpdateCanvases();

        if (bodyRoot != null)
        {
            baseBodyHeight = Mathf.Max(bodyRoot.rect.height, Mathf.Abs(bodyRoot.sizeDelta.y));
            baseBodyPos = bodyRoot.anchoredPosition;
        }

        if (loopContent != null)
        {
            baseContentHeight = Mathf.Max(loopContent.rect.height, Mathf.Abs(loopContent.sizeDelta.y));
            baseContentPos = loopContent.anchoredPosition;
        }

        if (footer != null)
            baseFooterPos = footer.anchoredPosition;

        if (bottomSnap != null)
            baseBottomSnapPos = bottomSnap.anchoredPosition;

        baseVisualHeight = CalculateBaseVisualHeight();

        if (baseVisualHeight <= 0.01f && rootRect != null && rootRect.rect.height > 0f)
            baseVisualHeight = rootRect.rect.height;

        if (baseVisualHeight <= 0.01f)
            baseVisualHeight = defaultBlockHeight;

        currentVisualHeight = baseVisualHeight;

        lastBlockChildCount = CountActiveBlockChildren();
        lastUsedGrow = 0f;

        initialized = true;
    }

    private float CalculateBaseVisualHeight()
    {
        BlockSnapPoints snap = GetComponent<BlockSnapPoints>();

        if (rootRect != null && snap != null && snap.topSnap != null && bottomSnap != null)
        {
            float value = Mathf.Abs(
                rootRect.InverseTransformPoint(snap.topSnap.position).y -
                rootRect.InverseTransformPoint(bottomSnap.position).y
            );

            if (value > 0.01f)
                return value;
        }

        if (rootRect != null && rootRect.rect.height > 0f)
            return rootRect.rect.height;

        return defaultBlockHeight;
    }

    private void SyncSnapPointReferences()
    {
        BlockSnapPoints snapPoints = GetComponent<BlockSnapPoints>();

        if (snapPoints == null)
            return;

        if (bottomSnap != null)
            snapPoints.bottomSnap = bottomSnap;
    }

    public void LoadFromCommand()
    {
        if (command == null)
            return;

        if (repeatInput != null)
            repeatInput.SetTextWithoutNotify(command.repeat.ToString());

        if (modeDropdown != null)
            modeDropdown.SetValueWithoutNotify((int)command.loopMode);

        RefreshModeView();
    }

    public void ApplyToCommand()
    {
        if (command == null)
            return;

        if (modeDropdown != null)
        {
            command.loopMode = modeDropdown.value == 0
                ? LoopExecutionMode.RepeatCount
                : LoopExecutionMode.UntilCarInCenter;
        }

        if (repeatInput != null)
        {
            if (int.TryParse(repeatInput.text, out int value))
                command.repeat = Mathf.Max(1, value);
            else
                command.repeat = 1;
        }

        RefreshModeView();
    }

    private void RefreshModeView()
    {
        if (command == null)
            return;

        bool repeatMode = command.loopMode == LoopExecutionMode.RepeatCount;

        if (repeatInputRoot != null)
            repeatInputRoot.SetActive(repeatMode);

        if (repeatInput != null)
            repeatInput.gameObject.SetActive(repeatMode);

        if (repeatSuffixText != null)
            repeatSuffixText.gameObject.SetActive(repeatMode);

        if (untilCenterLabelRoot != null)
            untilCenterLabelRoot.SetActive(!repeatMode);
    }

    public void RefreshSize()
    {
        if (refreshing)
            return;

        if (!initialized)
            CacheBaseValues();

        if (bodyRoot == null || loopContent == null)
            return;

        refreshing = true;

        DisableUnityAutoLayoutOnLoopContent();
        SyncSnapPointReferences();
        ApplyToCommand();

        RefreshChildLoopsFirst();

        int blockChildCount = CountActiveBlockChildren();

        float childrenHeight = LayoutChildrenByRealVisualBounds();

        float neededContentHeight = paddingTop + childrenHeight + paddingBottom;
        float grow = Mathf.Max(0f, neededContentHeight - baseContentHeight);

        /*
         * ВАЖНО:
         * Раньше тут была логика lastStableGrow, которая не давала циклу уменьшаться.
         * Из-за неё случайное увеличение во время скролла могло запомниться навсегда.
         *
         * Теперь grow всегда считается заново по реальным активным детям.
         */
        if (blockChildCount == 0)
            grow = 0f;

        bodyRoot.anchoredPosition = baseBodyPos;
        SetHeightOnly(bodyRoot, baseBodyHeight + grow);

        loopContent.anchoredPosition = baseContentPos;
        SetHeightOnly(loopContent, baseContentHeight + grow);

        if (footer != null)
        {
            footer.anchoredPosition = new Vector2(
                baseFooterPos.x,
                baseFooterPos.y - grow
            );
        }

        if (bottomSnap != null)
        {
            bottomSnap.anchoredPosition = new Vector2(
                baseBottomSnapPos.x,
                baseBottomSnapPos.y - grow
            );
        }

        currentVisualHeight = baseVisualHeight + grow;

        ForceOwnLayoutWithoutTouchingChildren();
        ForceBottomSnapToRealWorldPosition();
        SyncSnapPointReferences();

        if (debugLoopSize)
        {
            bool changed =
                blockChildCount != lastBlockChildCount ||
                Mathf.Abs(grow - lastUsedGrow) > 0.5f;

            if (changed)
            {
                Debug.Log(
                    $"[LOOP SIZE DEBUG]" +
                    $" name={name}" +
                    $" id={GetInstanceID()}" +
                    $" path={GetTransformPath(transform)}" +
                    $" commandChildren={blockChildCount}" +
                    $" rawContentChildren={(loopContent != null ? loopContent.childCount.ToString() : "NULL")}" +
                    $" childrenHeight={childrenHeight}" +
                    $" neededContentHeight={neededContentHeight}" +
                    $" baseContentHeight={baseContentHeight}" +
                    $" usedGrow={grow}" +
                    $" visualHeight={currentVisualHeight}" +
                    $" bottomLocal={(bottomSnap != null ? bottomSnap.anchoredPosition.ToString() : "NULL")}" +
                    $" bottomWorld={(bottomSnap != null ? bottomSnap.position.ToString() : "NULL")}"
                );
            }
        }

        lastBlockChildCount = blockChildCount;
        lastUsedGrow = grow;

        refreshing = false;
    }

    private int CountActiveBlockChildren()
    {
        if (loopContent == null)
            return 0;

        int count = 0;

        for (int i = 0; i < loopContent.childCount; i++)
        {
            Transform child = loopContent.GetChild(i);

            if (child == null)
                continue;

            if (!child.gameObject.activeInHierarchy)
                continue;

            if (child.GetComponent<BlockCommand>() != null)
                count++;
        }

        return count;
    }

    private void RefreshChildLoopsFirst()
    {
        if (loopContent == null)
            return;

        for (int i = 0; i < loopContent.childCount; i++)
        {
            Transform child = loopContent.GetChild(i);

            if (child == null)
                continue;

            if (!child.gameObject.activeInHierarchy)
                continue;

            LoopBlockUI childLoop = child.GetComponent<LoopBlockUI>();

            if (childLoop != null)
                childLoop.RefreshSize();

            IfElseBlockUI childIfElse = child.GetComponent<IfElseBlockUI>();

            if (childIfElse != null)
                childIfElse.RefreshSize();
        }
    }

    private float LayoutChildrenByRealVisualBounds()
    {
        if (loopContent == null)
            return 0f;

        float y = paddingTop;
        int count = 0;

        for (int i = 0; i < loopContent.childCount; i++)
        {
            RectTransform child = loopContent.GetChild(i) as RectTransform;

            if (child == null)
                continue;

            if (!child.gameObject.activeInHierarchy)
                continue;

            if (child.GetComponent<BlockCommand>() == null)
                continue;

            PrepareChildTransform(child);

            float desiredVisualTopY = -y;

            child.anchoredPosition = new Vector2(childX, desiredVisualTopY);
            Canvas.ForceUpdateCanvases();

            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(loopContent, child);

            float correctionY = desiredVisualTopY - bounds.max.y;
            child.anchoredPosition = new Vector2(childX, child.anchoredPosition.y + correctionY);

            Canvas.ForceUpdateCanvases();

            bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(loopContent, child);

            float childHeight = Mathf.Max(bounds.size.y, GetFallbackChildHeight(child), defaultBlockHeight);

            y += childHeight + spacing;
            count++;
        }

        if (count > 0)
            y -= spacing;

        return Mathf.Max(0f, y - paddingTop);
    }

    private void PrepareChildTransform(RectTransform child)
    {
        if (child == null)
            return;

        child.anchorMin = new Vector2(0f, 1f);
        child.anchorMax = new Vector2(0f, 1f);
        child.pivot = new Vector2(0f, 1f);

        child.localScale = Vector3.one;
        child.localRotation = Quaternion.identity;

        LayoutElement layoutElement = child.GetComponent<LayoutElement>();

        if (layoutElement == null)
            layoutElement = child.gameObject.AddComponent<LayoutElement>();

        layoutElement.ignoreLayout = true;
    }

    private float GetFallbackChildHeight(RectTransform child)
    {
        if (child == null)
            return defaultBlockHeight;

        LoopBlockUI childLoop = child.GetComponent<LoopBlockUI>();

        if (childLoop != null)
        {
            childLoop.RefreshSize();
            return Mathf.Max(childLoop.CurrentVisualHeight, defaultBlockHeight);
        }

        IfElseBlockUI childIfElse = child.GetComponent<IfElseBlockUI>();

        if (childIfElse != null)
        {
            childIfElse.RefreshSize();

            RectTransform childIfElseRT = childIfElse.transform as RectTransform;

            if (childIfElseRT != null)
                return Mathf.Max(childIfElseRT.rect.height, Mathf.Abs(childIfElseRT.sizeDelta.y), defaultBlockHeight);
        }

        float height = 0f;

        if (child.rect.height > height)
            height = child.rect.height;

        if (Mathf.Abs(child.sizeDelta.y) > height)
            height = Mathf.Abs(child.sizeDelta.y);

        LayoutElement layoutElement = child.GetComponent<LayoutElement>();

        if (layoutElement != null)
        {
            if (layoutElement.preferredHeight > height)
                height = layoutElement.preferredHeight;

            if (layoutElement.minHeight > height)
                height = layoutElement.minHeight;
        }

        BlockSnapPoints snap = child.GetComponent<BlockSnapPoints>();

        if (snap != null && snap.topSnap != null && snap.bottomSnap != null)
        {
            float localSnapHeight = Mathf.Abs(
                snap.topSnap.anchoredPosition.y -
                snap.bottomSnap.anchoredPosition.y
            );

            if (localSnapHeight > height)
                height = localSnapHeight;
        }

        return Mathf.Max(height, defaultBlockHeight);
    }

    public Vector3 GetRealBottomSnapWorld()
    {
        if (!initialized)
            CacheBaseValues();

        BlockSnapPoints snap = GetComponent<BlockSnapPoints>();

        Vector3 topWorld = transform.position;

        if (snap != null && snap.topSnap != null)
            topWorld = snap.topSnap.position;
        else if (rootRect != null)
            topWorld = rootRect.position;

        RectTransform basis = rootRect != null ? rootRect : transform as RectTransform;

        if (basis == null)
            return bottomSnap != null ? bottomSnap.position : transform.position;

        Vector3 downOffset = basis.TransformVector(new Vector3(0f, -CurrentVisualHeight, 0f));

        return topWorld + downOffset;
    }

    private void ForceBottomSnapToRealWorldPosition()
    {
        if (bottomSnap == null)
            return;

        Vector3 realWorld = GetRealBottomSnapWorld();

        bottomSnap.position = realWorld;

        Canvas.ForceUpdateCanvases();
    }

    private void ForceOwnLayoutWithoutTouchingChildren()
    {
        Canvas.ForceUpdateCanvases();

        if (bodyRoot != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(bodyRoot);

        if (rootRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);

        Canvas.ForceUpdateCanvases();
    }

    public float GetCurrentVisualHeight()
    {
        RefreshSize();
        return CurrentVisualHeight;
    }

    private void SetHeightOnly(RectTransform rect, float height)
    {
        if (rect == null)
            return;

        rect.pivot = new Vector2(rect.pivot.x, 1f);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
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