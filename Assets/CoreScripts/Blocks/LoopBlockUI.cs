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
    public bool debugLoopSize = true;
    public bool debugOnlyWhenHasChildren = true;

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

    private float lastStableGrow = 0f;
    private int lastStableBlockChildCount = 0;

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
            repeatInput.onValueChanged.AddListener(_ => ApplyToCommand());

        if (modeDropdown != null)
            modeDropdown.onValueChanged.AddListener(_ => ApplyToCommand());

        RefreshSize();
    }

    private void OnEnable()
    {
        DisableUnityAutoLayoutOnLoopContent();
        SyncSnapPointReferences();
    }

    private void LateUpdate()
    {
        RefreshSize();
    }

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
            baseBodyHeight = bodyRoot.rect.height;
            baseBodyPos = bodyRoot.anchoredPosition;
        }

        if (loopContent != null)
        {
            baseContentHeight = loopContent.rect.height;
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

        lastStableGrow = 0f;
        lastStableBlockChildCount = CountActiveBlockChildren();

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

        float childrenHeight = LayoutChildrenByRealVisualBounds();

        float neededContentHeight = paddingTop + childrenHeight + paddingBottom;
        float calculatedGrow = Mathf.Max(0f, neededContentHeight - baseContentHeight);

        int blockChildCount = CountActiveBlockChildren();

        float grow = calculatedGrow;

        if (blockChildCount == 0)
        {
            lastStableGrow = 0f;
            grow = 0f;
        }
        else
        {
            bool childWasRemoved = blockChildCount < lastStableBlockChildCount;

            if (childWasRemoved)
            {
                lastStableGrow = calculatedGrow;
                grow = calculatedGrow;
            }
            else
            {
                if (calculatedGrow < lastStableGrow)
                    grow = lastStableGrow;
                else
                    lastStableGrow = calculatedGrow;
            }
        }

        lastStableBlockChildCount = blockChildCount;

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
            bool shouldLog = true;

            if (debugOnlyWhenHasChildren)
                shouldLog = blockChildCount > 0 || loopContent.childCount > 0;

            if (shouldLog)
            {
                Debug.Log(
                    $"[LOOP SIZE DEBUG]" +
                    $" name={name}" +
                    $" id={GetInstanceID()}" +
                    $" path={GetTransformPath(transform)}" +
                    $" rootRectPath={(rootRect != null ? GetTransformPath(rootRect) : "NULL")}" +
                    $" bodyRootPath={(bodyRoot != null ? GetTransformPath(bodyRoot) : "NULL")}" +
                    $" loopContentPath={(loopContent != null ? GetTransformPath(loopContent) : "NULL")}" +
                    $" footerPath={(footer != null ? GetTransformPath(footer) : "NULL")}" +
                    $" bottomSnapPath={(bottomSnap != null ? GetTransformPath(bottomSnap) : "NULL")}" +
                    $" commandChildren={blockChildCount}" +
                    $" rawContentChildren={(loopContent != null ? loopContent.childCount.ToString() : "NULL")}" +
                    $" childList={GetContentChildrenDebug()}" +
                    $" childrenHeight={childrenHeight}" +
                    $" neededContentHeight={neededContentHeight}" +
                    $" baseContentHeight={baseContentHeight}" +
                    $" calculatedGrow={calculatedGrow}" +
                    $" usedGrow={grow}" +
                    $" visualHeight={currentVisualHeight}" +
                    $" topWorld={GetTopSnapWorldForDebug()}" +
                    $" realBottomWorld={GetRealBottomSnapWorld()}" +
                    $" bottomLocal={(bottomSnap != null ? bottomSnap.anchoredPosition.ToString() : "NULL")}" +
                    $" bottomWorld={(bottomSnap != null ? bottomSnap.position.ToString() : "NULL")}" +
                    $" footerLocal={(footer != null ? footer.anchoredPosition.ToString() : "NULL")}"
                );
            }
        }

        refreshing = false;
    }

    private Vector3 GetTopSnapWorldForDebug()
    {
        BlockSnapPoints snap = GetComponent<BlockSnapPoints>();

        if (snap != null && snap.topSnap != null)
            return snap.topSnap.position;

        if (rootRect != null)
            return rootRect.position;

        return transform.position;
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
            LoopBlockUI childLoop = loopContent.GetChild(i).GetComponent<LoopBlockUI>();

            if (childLoop != null)
                childLoop.RefreshSize();
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

            child.anchoredPosition = new Vector2(childX, child.anchoredPosition.y);

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

    private string GetContentChildrenDebug()
    {
        if (loopContent == null)
            return "NULL";

        if (loopContent.childCount == 0)
            return "EMPTY";

        string result = "";

        for (int i = 0; i < loopContent.childCount; i++)
        {
            Transform child = loopContent.GetChild(i);

            if (child == null)
                continue;

            BlockCommand cmd = child.GetComponent<BlockCommand>();

            if (result.Length > 0)
                result += " | ";

            result +=
                $"#{i}:{child.name}" +
                $" id={child.GetInstanceID()}" +
                $" hasCmd={(cmd != null)}" +
                $" path={GetTransformPath(child)}";
        }

        return result;
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