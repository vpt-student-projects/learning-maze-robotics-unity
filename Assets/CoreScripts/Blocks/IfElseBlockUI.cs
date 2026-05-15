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

    private readonly List<IfConditionRowUI> rows = new List<IfConditionRowUI>();

    private bool initialized;

    private void Awake()
    {
        if (command == null)
            command = GetComponent<BlockCommand>();
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
        if (command == null) return;

        if (sideDropdown != null)
            sideDropdown.SetValueWithoutNotify(SideToDropdown(command.lidarSide));

        if (compareDropdown != null)
            compareDropdown.SetValueWithoutNotify(CompareToDropdown(command.compare));

        if (distanceInput != null)
            distanceInput.SetTextWithoutNotify(command.distanceMeters.ToString("0.##", CultureInfo.InvariantCulture));
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
        if (row == null) return;

        rows.Remove(row);
        Destroy(row.gameObject);

        ApplyConditionsToCommand();
    }

    public void ApplyConditionsToCommand()
    {
        if (command == null) return;

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
            if (row == null) continue;
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
            case LidarSide.Forward: return 0;
            case LidarSide.Right: return 1;
            case LidarSide.Backward: return 2;
            case LidarSide.Left: return 3;
            default: return 1;
        }
    }

    private LidarSide DropdownToSide(int value)
    {
        switch (value)
        {
            case 0: return LidarSide.Forward;
            case 1: return LidarSide.Right;
            case 2: return LidarSide.Backward;
            case 3: return LidarSide.Left;
            default: return LidarSide.Right;
        }
    }

    private int CompareToDropdown(CompareOperator compare)
    {
        switch (compare)
        {
            case CompareOperator.Less: return 0;
            case CompareOperator.LessOrEqual: return 1;
            case CompareOperator.Greater: return 2;
            case CompareOperator.GreaterOrEqual: return 3;
            case CompareOperator.Equal: return 4;
            default: return 1;
        }
    }

    private CompareOperator DropdownToCompare(int value)
    {
        switch (value)
        {
            case 0: return CompareOperator.Less;
            case 1: return CompareOperator.LessOrEqual;
            case 2: return CompareOperator.Greater;
            case 3: return CompareOperator.GreaterOrEqual;
            case 4: return CompareOperator.Equal;
            default: return CompareOperator.LessOrEqual;
        }
    }
}