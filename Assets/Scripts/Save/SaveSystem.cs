using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

// Motivos por los que no se puede guardar. None = se puede.
public enum SaveBlockReason
{
    None,
    NoSession,        // no hay partida en red en curso
    NotHost,          // CA3: solo el host guarda
    NotExtraction,    // CA2: la escena no es de Extraccion (no tiene zona segura)
    NotInSafeZone     // CA2/CA5: el host no esta dentro de la zona segura
}

// Reglas y escritura del guardado (US 3.6). Todo estatico y sin red.
public static class SaveSystem
{
    public const int CurrentVersion = 1;
    public const string FileName = "savegame.json";
    public const string ModeExtraction = "Extraccion";
    public const string LegacyGameId = "legacy";

    private const string FolderName = "Saves";
    private const string FilePrefix = "partida_";

    public static string DefaultPath => Path.Combine(Application.persistentDataPath, FileName);
    public static string FolderPath => Path.Combine(Application.persistentDataPath, FolderName);

    // La partida guardada con el formato anterior (un solo archivo) sigue siendo valida.
    public static string PathFor(string gameId)
    {
        if (string.IsNullOrEmpty(gameId) || gameId == LegacyGameId)
            return DefaultPath;

        return Path.Combine(FolderPath, FilePrefix + gameId + ".json");
    }

    public static string ModeLabel(string mode)
    {
        return mode == ModeExtraction ? "EXTRACCIÓN" : (mode ?? "").ToUpperInvariant();
    }

    // CA2, CA3 y CA5: solo el host, solo en una escena de Extraccion y solo dentro de la zona segura.
    public static SaveBlockReason Evaluate(bool hasSession, bool isHost, bool sceneHasSafeZone, bool hostInsideSafeZone)
    {
        if (!hasSession) return SaveBlockReason.NoSession;
        if (!isHost) return SaveBlockReason.NotHost;
        if (!sceneHasSafeZone) return SaveBlockReason.NotExtraction;
        if (!hostInsideSafeZone) return SaveBlockReason.NotInSafeZone;
        return SaveBlockReason.None;
    }

    public static string Describe(SaveBlockReason reason)
    {
        switch (reason)
        {
            case SaveBlockReason.NoSession: return "No hay una partida en curso.";
            case SaveBlockReason.NotHost: return "Solo el host puede guardar la partida.";
            case SaveBlockReason.NotExtraction: return "Solo se puede guardar en el modo Extraccion.";
            case SaveBlockReason.NotInSafeZone: return "El host tiene que estar en una zona segura para guardar.";
            default: return string.Empty;
        }
    }

    public static string ToJson(GameSaveData data)
    {
        return JsonUtility.ToJson(data, true);
    }

    // Escribe a un archivo temporal y despues lo mueve, para no dejar un guardado a medias si se cierra el juego.
    public static bool TryWrite(GameSaveData data, out string error, string path = null)
    {
        error = null;
        path = path ?? PathFor(data.gameId);
        string temp = path + ".tmp";

        try
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(temp, ToJson(data));

            if (File.Exists(path))
                File.Delete(path);
            File.Move(temp, path);
            return true;
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            error = e.Message;
            return false;
        }
    }

    // true si hay al menos un archivo de guardado, este sano o no.
    public static bool AnySaveFile()
    {
        if (File.Exists(DefaultPath))
            return true;

        try
        {
            return Directory.Exists(FolderPath)
                && Directory.GetFiles(FolderPath, FilePrefix + "*.json").Length > 0;
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            return false;
        }
    }

    // Todas las partidas guardadas, la mas reciente primero. Los archivos danados van al final.
    public static List<SavedGameInfo> ListSaves()
    {
        List<string> paths = new List<string>();

        if (File.Exists(DefaultPath))
            paths.Add(DefaultPath);

        try
        {
            if (Directory.Exists(FolderPath))
                paths.AddRange(Directory.GetFiles(FolderPath, FilePrefix + "*.json"));
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            Debug.LogWarning("SaveSystem: no se pudo leer la carpeta de guardados: " + e.Message);
        }

        List<SavedGameInfo> saves = new List<SavedGameInfo>();
        foreach (string path in paths)
        {
            SavedGameInfo info = new SavedGameInfo { Path = path };
            if (TryRead(path, out GameSaveData data, out string error))
                info.Data = data;
            else
                info.Error = error;

            saves.Add(info);
        }

        saves.Sort(CompareByDate);
        return saves;
    }

    public static bool TryRead(string path, out GameSaveData data, out string error)
    {
        data = null;
        error = null;

        try
        {
            if (!File.Exists(path))
            {
                error = "El archivo de guardado no existe.";
                return false;
            }

            data = JsonUtility.FromJson<GameSaveData>(File.ReadAllText(path));
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
        {
            data = null;
            error = e.Message;
            return false;
        }

        if (data == null
            || data.version != CurrentVersion
            || string.IsNullOrEmpty(data.sceneName)
            || data.players == null
            || data.players.Count == 0
            || !TryGetSavedAtLocal(data, out _))
        {
            data = null;
            error = "El archivo de guardado está dañado.";
            return false;
        }

        if (string.IsNullOrEmpty(data.gameId))
        {
            if (path != DefaultPath)
            {
                data = null;
                error = "El archivo de guardado está dañado.";
                return false;
            }

            data.gameId = LegacyGameId;
        }

        return true;
    }

    public static bool TryGetSavedAtLocal(GameSaveData data, out DateTime local)
    {
        local = default;
        if (data == null || !DateTime.TryParse(data.savedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime utc))
            return false;

        local = utc.ToLocalTime();
        return true;
    }

    private static int CompareByDate(SavedGameInfo a, SavedGameInfo b)
    {
        if (a.IsValid != b.IsValid)
            return a.IsValid ? -1 : 1;

        if (!a.IsValid)
            return 0;

        TryGetSavedAtLocal(a.Data, out DateTime dateA);
        TryGetSavedAtLocal(b.Data, out DateTime dateB);
        return dateB.CompareTo(dateA);
    }
}
