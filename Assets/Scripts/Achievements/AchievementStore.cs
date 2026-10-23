using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

[Serializable]
public class UnlockedAchievement
{
    public string id;
    public string unlockedAtUtc; // ISO 8601
}

[Serializable]
public class AchievementSaveData
{
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;
    public List<UnlockedAchievement> unlocked = new List<UnlockedAchievement>();
}

public static class AchievementStore
{
    private const string FileName = "achievements.json";

    private static AchievementSaveData cache;

    public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    // Con "Enter Play Mode" sin recargar dominio la cache sobreviviria.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache()
    {
        cache = null;
    }

    public static bool IsUnlocked(string id)
    {
        foreach (UnlockedAchievement item in Load().unlocked)
            if (item.id == id) return true;

        return false;
    }

    public static bool TryUnlock(string id)
    {
        if (IsUnlocked(id)) return false;

        AchievementSaveData data = Load();
        data.unlocked.Add(new UnlockedAchievement { id = id, unlockedAtUtc = DateTime.UtcNow.ToString("o") });

        if (!TryWrite(data, out string error))
            Debug.LogError($"AchievementStore: no se pudo guardar '{FilePath}': {error}");

        return true;
    }

    public static IReadOnlyList<UnlockedAchievement> Unlocked => Load().unlocked;

    private static AchievementSaveData Load()
    {
        if (cache != null) return cache;

        cache = new AchievementSaveData();
        if (!File.Exists(FilePath)) return cache;

        try
        {
            AchievementSaveData read = JsonUtility.FromJson<AchievementSaveData>(File.ReadAllText(FilePath));
            if (read != null && read.version == AchievementSaveData.CurrentVersion && read.unlocked != null)
                cache = read;
            else
                KeepAsCorrupt();
        }
        catch (Exception exception) when (exception is ArgumentException || exception is IOException || exception is UnauthorizedAccessException)
        {
            KeepAsCorrupt();
        }

        return cache;
    }

    private static void KeepAsCorrupt()
    {
        Debug.LogWarning($"AchievementStore: '{FilePath}' no se pudo leer, se aparta como .corrupt y se empieza de cero.");

        try
        {
            File.Copy(FilePath, FilePath + ".corrupt", true);
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
        {
            // Se sigue con la lista vacia.
        }
    }

    // File.Replace es atomico: si el juego se cierra a mitad de la escritura, el archivo bueno no se pierde.
    private static bool TryWrite(AchievementSaveData data, out string error)
    {
        try
        {
            string directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string temp = FilePath + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(data, true), new UTF8Encoding(false));

            if (File.Exists(FilePath))
                File.Replace(temp, FilePath, FilePath + ".bak");
            else
                File.Move(temp, FilePath);

            error = null;
            return true;
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
        {
            error = exception.Message;
            return false;
        }
    }
}
