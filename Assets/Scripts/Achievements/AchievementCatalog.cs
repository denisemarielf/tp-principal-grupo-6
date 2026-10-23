using System.Collections.Generic;

public static class AchievementIds
{
    public const string FirstBlood = "primera_baja";
    public const string ZombieHunter = "cazador";
    public const string Exterminator = "exterminador";
    public const string ToughGuy = "duro_de_matar";
    public const string FirstRescue = "buen_samaritano";
    public const string LevelComplete = "fuera_de_peligro";
    public const string NoOneLeftBehind = "nadie_se_queda_atras";
}

public class AchievementDef
{
    public readonly string Id;
    public readonly string Title;
    public readonly string Description;

    public AchievementDef(string id, string title, string description)
    {
        Id = id;
        Title = title;
        Description = description;
    }
}

public static class AchievementCatalog
{
    public static readonly AchievementDef[] All =
    {
        new AchievementDef(AchievementIds.FirstBlood, "Primera baja", "Eliminá a tu primer infectado."),
        new AchievementDef(AchievementIds.ZombieHunter, "Cazador", $"Eliminá {AchievementRules.HunterKills} infectados en una partida."),
        new AchievementDef(AchievementIds.Exterminator, "Exterminador", $"Eliminá {AchievementRules.ExterminatorKills} infectados en una partida."),
        new AchievementDef(AchievementIds.ToughGuy, "Duro de matar", "Eliminá a un infectado resistente."),
        new AchievementDef(AchievementIds.FirstRescue, "Buen samaritano", "Escoltá a un civil hasta la zona segura."),
        new AchievementDef(AchievementIds.LevelComplete, "Fuera de peligro", "Completá un nivel de extracción."),
        new AchievementDef(AchievementIds.NoOneLeftBehind, "Nadie se queda atrás", "Completá el nivel con todos los civiles a salvo."),
    };

    public static AchievementDef Find(string id)
    {
        foreach (AchievementDef def in All)
            if (def.Id == id) return def;

        return null;
    }
}

public static class AchievementRules
{
    public const int HunterKills = 10;
    public const int ExterminatorKills = 30;

    public static void CollectKillUnlocks(int kills, bool wasToughEnemy, List<string> unlocks)
    {
        if (kills >= 1) unlocks.Add(AchievementIds.FirstBlood);
        if (kills >= HunterKills) unlocks.Add(AchievementIds.ZombieHunter);
        if (kills >= ExterminatorKills) unlocks.Add(AchievementIds.Exterminator);
        if (wasToughEnemy) unlocks.Add(AchievementIds.ToughGuy);
    }

    // 'total' incluye a los civiles que murieron.
    public static bool AllCiviliansSafe(int totalCivilians, int rescuedCivilians)
    {
        return totalCivilians > 0 && rescuedCivilians >= totalCivilians;
    }
}
