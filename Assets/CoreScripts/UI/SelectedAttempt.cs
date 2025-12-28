public static class SelectedAttempt
{
    public static bool HasValue;

    public static int AttemptId;
    public static int Seed;
    public static int Width;
    public static int Height;

    // ✅ новые поля (галочки)
    public static bool CreateFinishArea;
    public static bool CreateFinishAreaInCorner;

    public static void Set(int attemptId, int seed, int width, int height, bool createFinishArea, bool createFinishAreaInCorner)
    {
        HasValue = true;

        AttemptId = attemptId;
        Seed = seed;
        Width = width;
        Height = height;

        CreateFinishArea = createFinishArea;
        CreateFinishAreaInCorner = createFinishAreaInCorner;
    }

    public static void Clear()
    {
        HasValue = false;

        AttemptId = 0;
        Seed = 0;
        Width = 0;
        Height = 0;

        CreateFinishArea = false;
        CreateFinishAreaInCorner = false;
    }
}
