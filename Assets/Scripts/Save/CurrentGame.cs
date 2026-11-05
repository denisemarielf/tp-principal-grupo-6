using System;

public static class CurrentGame
{
    public static string Id { get; private set; }
    public static int Difficulty { get; private set; } = 1;

    public static void StartNew(int difficulty)
    {
        Id = Guid.NewGuid().ToString("N");
        Difficulty = difficulty;
    }

    public static void Continue(GameSaveData save)
    {
        Id = save.gameId;
        Difficulty = save.difficulty;
    }

    // Para partidas que no pasaron por el menú (escenas de prueba).
    public static string EnsureId()
    {
        if (string.IsNullOrEmpty(Id))
            Id = Guid.NewGuid().ToString("N");

        return Id;
    }

    public static void Clear()
    {
        Id = null;
        Difficulty = 1;
    }
}
