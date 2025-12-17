using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class WaitBlock : CommandBlock
{
    [Header("Настройки ожидания")]
    public float waitTime = 1.0f;
    
    [Header("UI элементы")]
    public InputField timeInput;
    public Slider timeSlider;
    
    void Start()
    {
        base.Start();
        blockName = "Ожидание";
        description = $"Ждать {waitTime} секунд";
        blockColor = new Color(0.4f, 0.8f, 1f); // Голубой
        
        if (timeInput != null)
        {
            timeInput.onEndEdit.AddListener(OnTimeChanged);
            timeInput.text = waitTime.ToString("F1");
        }
        
        if (timeSlider != null)
        {
            timeSlider.onValueChanged.AddListener(OnSliderChanged);
            timeSlider.value = waitTime;
        }
    }
    
    public override IEnumerator Execute()
    {
        Highlight(true);
        yield return new WaitForSeconds(waitTime);
        Highlight(false);
    }
    
    void OnTimeChanged(string value)
    {
        if (float.TryParse(value, out float result))
        {
            waitTime = Mathf.Max(0.1f, result);
            
            if (timeSlider != null)
                timeSlider.value = waitTime;
            
            description = $"Ждать {waitTime:F1} секунд";
            
            if (descriptionText != null)
                descriptionText.text = description;
        }
    }
    
    void OnSliderChanged(float value)
    {
        waitTime = value;
        
        if (timeInput != null)
            timeInput.text = waitTime.ToString("F1");
        
        description = $"Ждать {waitTime:F1} секунд";
        
        if (descriptionText != null)
            descriptionText.text = description;
    }
}