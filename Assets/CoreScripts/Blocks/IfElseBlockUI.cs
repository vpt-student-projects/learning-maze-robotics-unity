using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class IfElseBlockUI : MonoBehaviour
{
    [Header("Command")]
    public BlockCommand command;

    [Header("Branch containers")]
    public RectTransform ifContent;
    public RectTransform elseContent;

    [Header("Main condition UI")]
    public TMP_Dropdown sideDropdown;
    public TMP_Dropdown compareDropdown;
    public TMP_InputField distanceInput;

    [Header("Extra conditions UI")]
    public RectTransform conditionsContent;
    public IfConditionRowUI conditionRowPrefab;
    public Button addConditionButton;

    [Header("Branch Scroll Auto Size")]
    public bool autoResizeBranchContents = true;
    public float branchMinHeight = 160f;
    public float branchBottomPadding = 80f;
    public bool debugBranchSize = true;

    private readonly List<IfConditionRowUI> rows = new List<IfConditionRowUI>();

    private bool initialized;

    private void Awake()
    {
        if (command == null)
            command = GetComponent<BlockCommand>();

        PrepareBranchContent(ifContent);
        PrepareBranchContent(elseContent);
    }

    private void Start()
    {
        SetupMainDropdowns();
        LoadMainFromCommand();

        if (sideDropdown != null)
            sideDropdown.onValueChanged.AddListener(_ => ApplyConditionsToCommand());

        if (compareDropdown != null)
            compareDropdown.onValueChanged.AddListener(_ => ApplyConditionsToCommand());

        if (distanceInput != null)
            distanceInput.onValueChanged.AddListener(_ => ApplyConditionsToCommand());

        if (addConditionButton != null)
            addConditionButton.onClick.AddListener(AddConditionRow);

        BuildExtraRowsFromCommand();

        initialized = true;

        ApplyConditionsToCommand();
        RefreshSize();
    }

    private void OnEnable()
    {
        PrepareBranchContent(ifContent);
        PrepareBranchContent(elseContent);
        RefreshSize();
    }

    private void LateUpdate()
    {
        RefreshSize();
    }

    public void RefreshSize()
    {
        if (!autoResizeBranchContents)
            return;

        PrepareBranchContent(ifContent);
        PrepareBranchContent(elseContent);

        UpdateBranchContentHeight(ifContent, "IF");
        UpdateBranchContentHeight(elseContent, "ELSE");
    }

    private void PrepareBranchContent(RectTransform content)
    {
        if (content == null)
            return;
        
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(0f, 1f);
        content.pivot = new Vector2(0f, 1f);
        content.localScale = Vector3.one;
        content.localRotation = Quaternion.identity;

        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();

        if (fitter != null)
        {
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        }

        VerticalLayoutGroup verticalLayout = content.GetComponent<VerticalLayoutGroup>();

        if (verticalLayout != null)
        {
            verticalLayout.padding.left = 0;
            verticalLayout.padding.right = 0;
            verticalLayout.padding.top = 0;
            verticalLayout.padding.bottom = 0;

            verticalLayout.childAlignment = TextAnchor.UpperLeft;

            verticalLayout.childControlWidth = false;
            verticalLayout.childControlHeight = false;

            verticalLayout.childForceExpandWidth = false;
            verticalLayout.childForceExpandHeight = false;
        }

        HorizontalLayoutGroup horizontalLayout = content.GetComponent<HorizontalLayoutGroup>();

        if (horizontalLayout != null)
        {
            horizontalLayout.padding.left = 0;
            horizontalLayout.padding.right = 0;
            horizontalLayout.padding.top = 0;
            horizontalLayout.padding.bottom = 0;

            horizontalLayout.childAlignment = TextAnchor.UpperLeft;

            horizontalLayout.childControlWidth = false;
            horizontalLayout.childControlHeight = false;

            horizontalLayout.childForceExpandWidth = false;
            horizontalLayout.childForceExpandHeight = false;
        }

        GridLayoutGroup gridLayout = content.GetComponent<GridLayoutGroup>();

        if (gridLayout != null)
            gridLayout.enabled = false;

        ScrollRect scrollRect = content.GetComponentInParent<ScrollRect>();

        if (scrollRect != null)
        {
            scrollRect.content = content;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;

            if (scrollRect.viewport == null)
            {
                RectTransform viewport = content.parent as RectTransform;

                if (viewport != null)
                    scrollRect.viewport = viewport;
            }
        }
    }

    private void UpdateBranchContentHeight(RectTransform content, string label)
    {
        if (content == null)
            return;

        float viewportHeight = GetViewportHeight(content);
        float minHeight = Mathf.Max(branchMinHeight, viewportHeight);

        float lowestY = 0f;
        bool foundAnyBlock = false;

        for (int i = 0; i < content.childCount; i++)
        {
            Transform child = content.GetChild(i);

            if (child == null)
                continue;

            if (!child.gameObject.activeInHierarchy)
                continue;

            if (child.GetComponent<BlockCommand>() == null)
                continue;

            RectTransform childRT = child as RectTransform;

            if (childRT == null)
                continue;

            PrepareBranchChild(childRT);

            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                content,
                childRT
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

        float neededHeight = minHeight;

        if (foundAnyBlock)
            neededHeight = Mathf.Abs(lowestY) + branchBottomPadding;

        neededHeight = Mathf.Max(neededHeight, minHeight);

        float oldHeight = content.rect.height;

        LayoutElement layoutElement = content.GetComponent<LayoutElement>();

        if (layoutElement == null)
            layoutElement = content.gameObject.AddComponent<LayoutElement>();

        layoutElement.ignoreLayout = false;
        layoutElement.minHeight = neededHeight;
        layoutElement.preferredHeight = neededHeight;
        layoutElement.flexibleHeight = 0f;

        content.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            neededHeight
        );

        Vector2 sizeDelta = content.sizeDelta;
        sizeDelta.y = neededHeight;
        content.sizeDelta = sizeDelta;

        Canvas.ForceUpdateCanvases();

        ScrollRect scrollRect = content.GetComponentInParent<ScrollRect>();

        if (scrollRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);

            RectTransform viewport = scrollRect.viewport;

            if (viewport != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(viewport);

            RectTransform scrollRectRT = scrollRect.transform as RectTransform;

            if (scrollRectRT != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRectRT);

            Canvas.ForceUpdateCanvases();

            content.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                neededHeight
            );

            sizeDelta = content.sizeDelta;
            sizeDelta.y = neededHeight;
            content.sizeDelta = sizeDelta;

            Canvas.ForceUpdateCanvases();
        }

        if (debugBranchSize && Mathf.Abs(oldHeight - neededHeight) > 0.5f)
        {

        }
    }

    private void PrepareBranchChild(RectTransform child)
    {
        if (child == null)
            return;

        if (child.GetComponent<BlockCommand>() == null)
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

    private float GetViewportHeight(RectTransform content)
    {
        if (content == null)
            return branchMinHeight;

        ScrollRect scrollRect = content.GetComponentInParent<ScrollRect>();

        if (scrollRect != null && scrollRect.viewport != null)
            return Mathf.Max(scrollRect.viewport.rect.height, branchMinHeight);

        RectTransform parent = content.parent as RectTransform;

        if (parent != null)
            return Mathf.Max(parent.rect.height, branchMinHeight);

        return branchMinHeight;
    }

    private void SetupMainDropdowns()
    {
        if (sideDropdown != null)
        {
            sideDropdown.ClearOptions();
            sideDropdown.AddOptions(new List<string>
            {
                "Спереди",
                "Справа",
                "Сзади",
                "Слева"
            });
        }

        if (compareDropdown != null)
        {
            compareDropdown.ClearOptions();
            compareDropdown.AddOptions(new List<string>
            {
                "<",
                "<=",
                ">",
                ">=",
                "="
            });
        }
    }

    private void LoadMainFromCommand()
    {
        if (command == null)
            return;

        if (sideDropdown != null)
            sideDropdown.SetValueWithoutNotify(SideToDropdown(command.lidarSide));

        if (compareDropdown != null)
            compareDropdown.SetValueWithoutNotify(CompareToDropdown(command.compare));

        if (distanceInput != null)
            distanceInput.SetTextWithoutNotify(
                command.distanceMeters.ToString("0.##", CultureInfo.InvariantCulture)
            );
    }

    private void BuildExtraRowsFromCommand()
    {
        ClearRows();

        if (command == null)
            return;

        if (command.conditions == null)
            command.conditions = new List<BlockCommand.IfConditionData>();

        for (int i = 0; i < command.conditions.Count; i++)
            CreateRow(command.conditions[i]);
    }

    public void AddConditionRow()
    {
        BlockCommand.IfConditionData data = new BlockCommand.IfConditionData
        {
            logic = LogicOperator.And,
            side = LidarSide.Right,
            compare = CompareOperator.LessOrEqual,
            distanceMeters = 0.04f
        };

        CreateRow(data);
        ApplyConditionsToCommand();
    }

    private void CreateRow(BlockCommand.IfConditionData data)
    {
        if (conditionRowPrefab == null || conditionsContent == null)
        {
            Debug.LogError("IF UI ERROR: conditionRowPrefab или conditionsContent не назначены!");
            return;
        }

        IfConditionRowUI row = Instantiate(conditionRowPrefab, conditionsContent);
        rows.Add(row);

        row.Init(this, false);
        row.SetData(data);
    }

    public void DeleteConditionRow(IfConditionRowUI row)
    {
        if (row == null)
            return;

        rows.Remove(row);
        Destroy(row.gameObject);

        ApplyConditionsToCommand();
    }

    public void ApplyConditionsToCommand()
    {
        if (command == null)
            return;

        if (sideDropdown != null)
            command.lidarSide = DropdownToSide(sideDropdown.value);

        if (compareDropdown != null)
            command.compare = DropdownToCompare(compareDropdown.value);

        if (distanceInput != null)
        {
            string text = distanceInput.text.Replace(",", ".");

            if (float.TryParse(
                    text,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float value))
            {
                command.distanceMeters = value;
            }
        }

        if (command.conditions == null)
            command.conditions = new List<BlockCommand.IfConditionData>();

        command.conditions.Clear();

        foreach (IfConditionRowUI row in rows)
        {
            if (row == null)
                continue;

            command.conditions.Add(row.GetData());
        }

        if (initialized)
        {
            Debug.Log(
                $"IF UI APPLY MAIN: side={command.lidarSide}, " +
                $"compare={command.compare}, distance={command.distanceMeters}, " +
                $"extraCount={command.conditions.Count}"
            );
        }
    }

    private void ClearRows()
    {
        foreach (IfConditionRowUI row in rows)
        {
            if (row != null)
                Destroy(row.gameObject);
        }

        rows.Clear();
    }

    private int SideToDropdown(LidarSide side)
    {
        switch (side)
        {
            case LidarSide.Forward:
                return 0;

            case LidarSide.Right:
                return 1;

            case LidarSide.Backward:
                return 2;

            case LidarSide.Left:
                return 3;

            default:
                return 1;
        }
    }

    private LidarSide DropdownToSide(int value)
    {
        switch (value)
        {
            case 0:
                return LidarSide.Forward;

            case 1:
                return LidarSide.Right;

            case 2:
                return LidarSide.Backward;

            case 3:
                return LidarSide.Left;

            default:
                return LidarSide.Right;
        }
    }

    private int CompareToDropdown(CompareOperator compare)
    {
        switch (compare)
        {
            case CompareOperator.Less:
                return 0;

            case CompareOperator.LessOrEqual:
                return 1;

            case CompareOperator.Greater:
                return 2;

            case CompareOperator.GreaterOrEqual:
                return 3;

            case CompareOperator.Equal:
                return 4;

            default:
                return 1;
        }
    }

    private CompareOperator DropdownToCompare(int value)
    {
        switch (value)
        {
            case 0:
                return CompareOperator.Less;

            case 1:
                return CompareOperator.LessOrEqual;

            case 2:
                return CompareOperator.Greater;

            case 3:
                return CompareOperator.GreaterOrEqual;

            case 4:
                return CompareOperator.Equal;

            default:
                return CompareOperator.LessOrEqual;
        }
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