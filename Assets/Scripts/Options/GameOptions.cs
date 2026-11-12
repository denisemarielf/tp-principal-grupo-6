using System;
using System.IO;
using UnityEngine;

// Ajustes del jugador (US 5.15). Van a options.json, aparte de la partida guardada.
[Serializable]
public class GameOptionsData
{
    public float master = 1f;
    public float music = 1f;
    public float sfx = 1f;
    public float sensitivity = 2f;
    public int qualityLevel;
    public bool fullScreen = true;
    public int width;
    public int height;
    public int vSync = 1;
    public int targetFrameRate = -1;

    public void Clamp()
    {
        master = Mathf.Clamp01(master);
        music = Mathf.Clamp01(music);
        sfx = Mathf.Clamp01(sfx);
        sensitivity = Mathf.Clamp(sensitivity, GameOptions.MinSensitivity, GameOptions.MaxSensitivity);

        int levels = QualitySettings.names != null ? QualitySettings.names.Length : 0;
        if (levels > 0)
            qualityLevel = Mathf.Clamp(qualityLevel, 0, levels - 1);
    }
}

public static class GameOptions
{
    public const string FileName = "options.json";
    public const float MinSensitivity = 0.5f;
    public const float MaxSensitivity = 5f;

    public static event Action Changed;

    private static GameOptionsData current = new GameOptionsData();

    public static float Master => current.master;
    public static float Music => current.music;
    public static float Sfx => current.sfx;
    public static float Sensitivity => current.sensitivity;
    public static int Quality => current.qualityLevel;
    public static bool FullScreen => current.fullScreen;
    public static int Width => current.width;
    public static int Height => current.height;
    public static int VSync => current.vSync;
    public static int FrameRate => current.targetFrameRate;

    // La música no pasa por el listener: si no, el slider de efectos también la bajaría.
    public static float MusicGain => current.master * current.music;
    public static float ListenerVolume => current.master * current.sfx;

    public static string DefaultPath => Path.Combine(Application.persistentDataPath, FileName);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        GameOptionsData loaded = TryRead(DefaultPath);
        if (loaded != null)
        {
            current = loaded;
            ApplyAudio();
            ApplyVideo();
            return;
        }

        current = CreateDefault();
        ApplyAudio();
    }

    public static GameOptionsData CreateDefault()
    {
        var data = new GameOptionsData
        {
            qualityLevel = QualitySettings.GetQualityLevel(),
            fullScreen = Screen.fullScreen,
            width = Screen.width,
            height = Screen.height,
            vSync = QualitySettings.vSyncCount > 0 ? 1 : 0,
            targetFrameRate = Application.targetFrameRate
        };
        data.Clamp();
        return data;
    }

    public static void SetMaster(float value)
    {
        current.master = Mathf.Clamp01(value);
        ApplyAudio();
        SaveAndNotify();
    }

    public static void SetMusic(float value)
    {
        current.music = Mathf.Clamp01(value);
        ApplyAudio();
        SaveAndNotify();
    }

    public static void SetSfx(float value)
    {
        current.sfx = Mathf.Clamp01(value);
        ApplyAudio();
        SaveAndNotify();
    }

    public static void SetSensitivity(float value)
    {
        current.sensitivity = Mathf.Clamp(value, MinSensitivity, MaxSensitivity);
        SaveAndNotify();
    }

    public static void SetQuality(int level)
    {
        int count = QualitySettings.names != null ? QualitySettings.names.Length : 0;
        if (count <= 0)
            return;

        current.qualityLevel = Mathf.Clamp(level, 0, count - 1);
        QualitySettings.SetQualityLevel(current.qualityLevel, true);
        SaveAndNotify();
    }

    public static void SetFullscreen(bool enabled)
    {
        current.fullScreen = enabled;
        Screen.fullScreenMode = enabled ? FullScreenMode.ExclusiveFullScreen : FullScreenMode.Windowed;
        Screen.fullScreen = enabled;
        SaveAndNotify();
    }

    public static void SetResolution(int width, int height)
    {
        current.width = width;
        current.height = height;
        if (width >= 640 && height >= 480)
            Screen.SetResolution(width, height, Screen.fullScreenMode);
        SaveAndNotify();
    }

    public static void SetVSync(int count)
    {
        current.vSync = count > 0 ? 1 : 0;
        QualitySettings.vSyncCount = current.vSync;
        SaveAndNotify();
    }

    public static void SetFrameRate(int frameRate)
    {
        current.targetFrameRate = frameRate;
        Application.targetFrameRate = frameRate;
        SaveAndNotify();
    }

    // Pantalla, resolución, gráficos, vsync y FPS se confirman juntos.
    public static void CommitVideo(bool fullScreen, int width, int height, int quality, int vSync, int frameRate)
    {
        current.fullScreen = fullScreen;
        current.width = width;
        current.height = height;
        current.vSync = vSync > 0 ? 1 : 0;
        current.targetFrameRate = frameRate;

        int count = QualitySettings.names != null ? QualitySettings.names.Length : 0;
        if (count > 0)
            current.qualityLevel = Mathf.Clamp(quality, 0, count - 1);

        ApplyVideo();
        SaveAndNotify();
    }

    public static void ApplyAudio()
    {
        AudioListener.volume = ListenerVolume;
    }

    public static void ApplyVideo()
    {
        int count = QualitySettings.names != null ? QualitySettings.names.Length : 0;
        if (count > 0)
            QualitySettings.SetQualityLevel(Mathf.Clamp(current.qualityLevel, 0, count - 1), true);

        QualitySettings.vSyncCount = current.vSync > 0 ? 1 : 0;
        Application.targetFrameRate = current.targetFrameRate;

        if (current.width >= 640 && current.height >= 480)
        {
            FullScreenMode mode = current.fullScreen
                ? FullScreenMode.ExclusiveFullScreen
                : FullScreenMode.Windowed;
            Screen.SetResolution(current.width, current.height, mode);
        }
    }

    public static bool TryWrite(GameOptionsData data, string path)
    {
        if (data == null || string.IsNullOrEmpty(path))
            return false;

        data.Clamp();
        string temp = path + ".tmp";

        try
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(temp, JsonUtility.ToJson(data, true));
            if (File.Exists(path))
                File.Delete(path);
            File.Move(temp, path);
            return true;
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static GameOptionsData TryRead(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return null;

        try
        {
            GameOptionsData data = JsonUtility.FromJson<GameOptionsData>(File.ReadAllText(path));
            if (data == null)
                return null;

            data.Clamp();
            return data;
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
        {
            return null;
        }
    }

    private static void SaveAndNotify()
    {
        TryWrite(current, DefaultPath);
        Changed?.Invoke();
    }
}
