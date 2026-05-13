using TMPro;
using UnityEngine;

public class IfElseBlockUI : MonoBehaviour
{
    public BlockCommand command;

    [Header("Branch containers")]
    public RectTransform ifContent;
    public RectTransform elseContent;

    [Header("Condition UI")]
    public TMP_Dropdown sideDropdown;
    public TMP_Dropdown compareDropdown;
    public TMP_InputField distanceInput;

    private void Awake()
    {
        if (command == null)
            command = GetComponent<BlockCommand>();
    }

    private void Start()
    {
        SetupDropdowns();
        LoadFromCommand();

        sideDropdown.onValueChanged.AddListener(OnSideChanged);
        compareDropdown.onValueChanged.AddListener(OnCompareChanged);
        distanceInput.onValueChanged.AddListener(OnDistanceChanged);
    }

    private void SetupDropdowns()
    {
        sideDropdown.ClearOptions();
        sideDropdown.AddOptions(new System.Collections.Generic.List<string>
        {
            "Впереди",
            "Справа",
            "Сзади",
            "Слева"
        });

        compareDropdown.ClearOptions();
        compareDropdown.AddOptions(new System.Collections.Generic.List<string>
        {
            "<",
            "<=",
            ">",
            ">=",
            "="
        });
    }

    private void LoadFromCommand()
    {
        if (command == null) return;

        sideDropdown.value = (int)command.lidarSide;
        compareDropdown.value = (int)command.compare;

        distanceInput.text = command.distanceMeters.ToString("0.##").Replace(",", ".");
    }

    private void OnSideChanged(int value)
    {
        if (command == null) return;
        command.lidarSide = (LidarSide)value;
    }

    private void OnCompareChanged(int value)
    {
        if (command == null) return;
        command.compare = (CompareOperator)value;
    }

    private void OnDistanceChanged(string text)
    {
        if (command == null) return;

        string fixedText = text.Replace(",", ".");

        if (float.TryParse(
                fixedText,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out float value))
        {
            command.distanceMeters = value;
        }
    }
}