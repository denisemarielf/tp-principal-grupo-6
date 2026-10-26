using System;
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

    public static string DefaultPath => Path.Combine(Application.persistentDataPath, FileName);

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
        path = path ?? DefaultPath;
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
}
