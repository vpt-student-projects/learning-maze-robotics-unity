using UnityEngine;

public class BackgroundEnforcer : MonoBehaviour
{
    [Header("Background Settings")]
    public bool runInBackground = true;
    public int targetFrameRate = 60;
    public bool preventSleep = true;

    [Header("Monitoring")]
    public bool logStatusChanges = true;

    void Start()
    {
        ApplyBackgroundSettings();

        // Дублируем настройки на случай если другие системы их меняют
        InvokeRepeating("EnforceBackgroundSettings", 1f, 5f);
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (logStatusChanges)
        {
            Debug.Log($"🎯 Application focus: {hasFocus}");
        }

        // При потере фокуса усиливаем настройки фона
        if (!hasFocus)
        {
            ApplyBackgroundSettings();
        }
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (logStatusChanges)
        {
            Debug.Log($"⏸️ Application pause: {pauseStatus}");
        }
    }

    private void ApplyBackgroundSettings()
    {
        Application.runInBackground = runInBackground;
        Application.targetFrameRate = targetFrameRate;

        if (preventSleep)
        {
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }

        if (logStatusChanges)
        {
            Debug.Log($"🔧 Background settings enforced: " +
                     $"RunInBackground={Application.runInBackground}, " +
                     $"TargetFPS={Application.targetFrameRate}, " +
                     $"SleepTimeout={Screen.sleepTimeout}");
        }
    }

    private void EnforceBackgroundSettings()
    {
        // Постоянно применяем настройки чтобы другие системы их не меняли
        if (!Application.runInBackground && runInBackground)
        {
            Debug.LogWarning("⚠️ runInBackground was disabled - re-enforcing");
            Application.runInBackground = true;
        }

        if (Application.targetFrameRate != targetFrameRate)
        {
            Application.targetFrameRate = targetFrameRate;
        }
    }

    [ContextMenu("Print Current Settings")]
    private void PrintCurrentSettings()
    {
        Debug.Log($"Current Settings - RunInBackground: {Application.runInBackground}, " +
                 $"TargetFPS: {Application.targetFrameRate}, " +
                 $"SleepTimeout: {Screen.sleepTimeout}, " +
                 $"HasFocus: {Application.isFocused}");
    }
}