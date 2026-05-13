using TMPro;
using UnityEngine;

public class DecimalInputFilter : MonoBehaviour
{
    private TMP_InputField input;

    private void Awake()
    {
        input = GetComponent<TMP_InputField>();
        input.onValidateInput += ValidateChar;
    }

    private char ValidateChar(string text, int charIndex, char addedChar)
    {
        if (char.IsDigit(addedChar))
            return addedChar;

        if (addedChar == '.' || addedChar == ',')
        {
            if (text.Contains(".") || text.Contains(","))
                return '\0';

            return addedChar;
        }

        return '\0';
    }
}