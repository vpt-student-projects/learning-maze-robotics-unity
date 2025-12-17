using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class TurnBlock : CommandBlock
{
    public enum TurnDirection { Left, Right }

    [Header("Настройки поворота")]
    public TurnDirection turnDirection = TurnDirection.Left;

    [Header("UI элементы")]
    public Dropdown directionDropdown;

    [Header("Ссылки")]
    public CarController carController;

    void Start()
    {
        base.Start();
        blockName = "Поворот";
        description = "Поворачивает машинку";
        blockColor = new Color(0.2f, 0.6f, 1f); // Синий

        if (carController == null)
            carController = FindObjectOfType<CarController>();

        if (directionDropdown != null)
        {
            directionDropdown.onValueChanged.AddListener(OnDirectionChanged);
        }
    }

    public override IEnumerator Execute()
    {
        if (carController == null || !carController.IsCarReady())
        {
            Debug.LogError("CarController не готов!");
            yield break;
        }

        Highlight(true);

        // Выполняем поворот
        if (turnDirection == TurnDirection.Left)
        {
            carController.TurnLeft();
        }
        else
        {
            carController.TurnRight();
        }

        // Ждём окончания анимации поворота
        float waitTime = Mathf.Max(0.05f, carController.rotationAnimationTime + 0.02f);
        yield return new WaitForSeconds(waitTime);

        Highlight(false);
    }

    void OnDirectionChanged(int index)
    {
        turnDirection = (TurnDirection)index;

        description = turnDirection == TurnDirection.Left
            ? "Повернуть налево"
            : "Повернуть направо";

        if (descriptionText != null)
            descriptionText.text = description;
    }
}
