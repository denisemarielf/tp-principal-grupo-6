using System;
using UnityEngine;

public static class CurrentGame
{
    public static string Id { get; private set; }
    public static int Difficulty { get; private set; } = 1;

    public static void StartNew(int difficulty)
    {
        Id = Guid.NewGuid().ToString("N");
        Difficulty = Mathf.Clamp(difficulty, 0, 2);
    }

    public static void Continue(GameSaveData save)
    {
        Id = save.gameId;
        Difficulty = Mathf.Clamp(save.difficulty, 0, 2);
    }

    // Para partidas que no pasaron por el menú (escenas de prueba).
    public static string EnsureId()
    {
        if (string.IsNullOrEmpty(Id))
            Id = Guid.NewGuid().ToString("N");

        return Id;
    }

    // Cliente: el host manda la dificultad al iniciar; no regenera el Id de la partida.
    public static void ApplyHostDifficulty(int difficulty)
    {
        Difficulty = Mathf.Clamp(difficulty, 0, 2);
    }

    public static void Clear()
    {
        Id = null;
        Difficulty = 1;
    }
}
