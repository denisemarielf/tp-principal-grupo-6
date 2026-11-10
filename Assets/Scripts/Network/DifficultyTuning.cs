using UnityEngine;

// Multiplicadores de enemigos segun la dificultad elegida por el host (0 Facil, 1 Normal, 2 Dificil).
public static class DifficultyTuning
{
    public static float HealthMul => HealthFor(CurrentGame.Difficulty);
    public static float SpeedMul => SpeedFor(CurrentGame.Difficulty);
    public static float DamageMul => DamageFor(CurrentGame.Difficulty);
    public static float CountMul => CountFor(CurrentGame.Difficulty);

    // Supervivencia: cantidad de rondas que hay que superar para ganar.
    public static int RoundsToWin => RoundsFor(CurrentGame.Difficulty);

    public static int ScaleCount(int baseCount)
    {
        return Mathf.Max(1, Mathf.RoundToInt(baseCount * CountMul));
    }

    private static int RoundsFor(int difficulty)
    {
        switch (Clamp(difficulty))
        {
            case 0: return 3;
            case 2: return 7;
            default: return 5;
        }
    }

    private static float HealthFor(int difficulty)
    {
        switch (Clamp(difficulty))
        {
            case 0: return 0.75f;
            case 2: return 2f;
            default: return 1f;
        }
    }

    private static float SpeedFor(int difficulty)
    {
        switch (Clamp(difficulty))
        {
            case 0: return 0.85f;
            case 2: return 2f;
            default: return 1f;
        }
    }

    private static float DamageFor(int difficulty)
    {
        switch (Clamp(difficulty))
        {
            case 0: return 0.75f;
            case 2: return 2f;
            default: return 1f;
        }
    }

    private static float CountFor(int difficulty)
    {
        switch (Clamp(difficulty))
        {
            case 0: return 0.7f;
            case 2: return 2f;
            default: return 1f;
        }
    }

    private static int Clamp(int difficulty)
    {
        return Mathf.Clamp(difficulty, 0, 2);
    }
}
