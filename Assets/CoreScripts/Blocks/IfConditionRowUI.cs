using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;

public class IfConditionRowUI : MonoBehaviour
{
    public TMP_Dropdown logicDropdown;
    public TMP_Dropdown sideDropdown;
    public TMP_Dropdown compareDropdown;
    public TMP_InputField distanceInput;

    public GameObject deleteButton;

    private IfElseBlockUI owner;

    public void Init(IfElseBlockUI owner, bool isFirst)
    {
        this.owner = owner;

        if (logicDropdown != null)
            logicDropdown.gameObject.SetActive(!isFirst);

        if (deleteButton != null)
            deleteButton.SetActive(true);

        FillDropdowns();

        RemoveListeners();

        if (logicDropdown != null)
            logicDropdown.onValueChanged.AddListener(_ => owner.ApplyConditionsToCommand());

        if (sideDropdown != null)
            sideDropdown.onValueChanged.AddListener(_ => owner.ApplyConditionsToCommand());

        if (compareDropdown != null)
            compareDropdown.onValueChanged.AddListener(_ => owner.ApplyConditionsToCommand());

        if (distanceInput != null)
            distanceInput.onValueChanged.AddListener(_ => owner.ApplyConditionsToCommand());
    }

    private void FillDropdowns()
    {
        if (logicDropdown != null)
        {
            logicDropdown.ClearOptions();
            logicDropdown.AddOptions(new List<string>
            {
                "И",
                "ИЛИ"
            });
        }

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

    private void RemoveListeners()
    {
        if (logicDropdown != null)
            logicDropdown.onValueChanged.RemoveAllListeners();

        if (sideDropdown != null)
            sideDropdown.onValueChanged.RemoveAllListeners();

        if (compareDropdown != null)
            compareDropdown.onValueChanged.RemoveAllListeners();

        if (distanceInput != null)
            distanceInput.onValueChanged.RemoveAllListeners();
    }

    public void SetData(BlockCommand.IfConditionData data)
    {
        if (data == null) return;

        if (logicDropdown != null)
            logicDropdown.SetValueWithoutNotify(LogicToDropdown(data.logic));

        if (sideDropdown != null)
            sideDropdown.SetValueWithoutNotify(SideToDropdown(data.side));

        if (compareDropdown != null)
            compareDropdown.SetValueWithoutNotify(CompareToDropdown(data.compare));

        if (distanceInput != null)
            distanceInput.SetTextWithoutNotify(data.distanceMeters.ToString("0.##", CultureInfo.InvariantCulture));
    }

    public BlockCommand.IfConditionData GetData()
    {
        BlockCommand.IfConditionData data = new BlockCommand.IfConditionData();

        if (logicDropdown != null)
            data.logic = DropdownToLogic(logicDropdown.value);

        if (sideDropdown != null)
            data.side = DropdownToSide(sideDropdown.value);

        if (compareDropdown != null)
            data.compare = DropdownToCompare(compareDropdown.value);

        if (distanceInput != null)
        {
            string text = distanceInput.text.Replace(",", ".");

            if (float.TryParse(
                    text,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float value))
            {
                data.distanceMeters = value;
            }
        }

        return data;
    }

    public void DeleteThisCondition()
    {
        if (owner != null)
            owner.DeleteConditionRow(this);
    }

    private int LogicToDropdown(LogicOperator logic)
    {
        switch (logic)
        {
            case LogicOperator.And: return 0;
            case LogicOperator.Or: return 1;
            default: return 0;
        }
    }

    private LogicOperator DropdownToLogic(int value)
    {
        switch (value)
        {
            case 0: return LogicOperator.And;
            case 1: return LogicOperator.Or;
            default: return LogicOperator.And;
        }
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