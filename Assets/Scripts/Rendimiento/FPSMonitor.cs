


using System;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public class FPSMonitor : MonoBehaviour
{
    private static FPSMonitor instance;

    private StreamWriter writer;

    private const float IntervalSeconds = 1f;
    private const float FrameLimitMs = 1000f / 30f;
    private const float LongPauseSeconds = 0.5f;

    private float intervalElapsed;
    private float sessionTime;
    private float measuredFrameTime;
    private float excludedTime;

    private int framesMeasured;
    private int slowFrames;
    private int longPauses;

    private string sessionId;
    private string intervalScene;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        sessionId = DateTime.Now.ToString(
            "MM_dd_HH_mm",
            CultureInfo.InvariantCulture
        );

        intervalScene = SceneManager.GetActiveScene().name;

        OpenLog();
    }

    private void OpenLog()
    {
        try
        {
            string baseName = $"FPS_{sessionId}";
            string path = Path.Combine(
                Application.persistentDataPath,
                baseName + ".csv"
            );

            int suffix = 1;

            while (File.Exists(path))
            {
                path = Path.Combine(
                    Application.persistentDataPath,
                    $"{baseName}_{suffix}.csv"
                );

                suffix++;
            }

            writer = new StreamWriter(path, false);

            writer.WriteLine(
                "FechaHora,TiempoDesdeInicioSeg,Escena," +
                "FPSEstimados,TiempoPromedioFrameMs," +
                "FramesLentos,FramesMedidos," +
                "PausasLargas,TiempoExcluidoMs"
            );

            writer.Flush();

            Debug.Log($"FPSMonitor: registro guardado en {path}");
        }
        catch (Exception e)
        {
            Debug.LogError(
                $"FPSMonitor: no se pudo crear el CSV: {e.Message}"
            );
        }
    }

    private void Update()
    {
        float frameTime = Time.unscaledDeltaTime;

        if (frameTime <= 0f)
            return;

        sessionTime += frameTime;

        string activeScene = SceneManager.GetActiveScene().name;

        // Cerrar el intervalo anterior antes de registrar
        // datos correspondientes a la nueva escena.
        if (activeScene != intervalScene)
        {
            WriteInterval();
            intervalScene = activeScene;

            // Excluir el frame de transición del cálculo normal.
            excludedTime += frameTime;

            if (frameTime > LongPauseSeconds)
                longPauses++;

            intervalElapsed += frameTime;

            if (intervalElapsed >= IntervalSeconds)
                WriteInterval();

            return;
        }

        intervalElapsed += frameTime;

        if (frameTime > LongPauseSeconds)
        {
            longPauses++;
            excludedTime += frameTime;
        }
        else
        {
            measuredFrameTime += frameTime;
            framesMeasured++;

            if (frameTime * 1000f > FrameLimitMs)
                slowFrames++;
        }

        if (intervalElapsed >= IntervalSeconds)
            WriteInterval();
    }

    private void WriteInterval()
    {
        if (writer == null)
        {
            ResetInterval();
            return;
        }

        if (framesMeasured == 0 && longPauses == 0
            && excludedTime == 0f)
        {
            ResetInterval();
            return;
        }

        float fps = measuredFrameTime > 0f
            ? framesMeasured / measuredFrameTime
            : 0f;

        float averageFrameTime = framesMeasured > 0
            ? measuredFrameTime / framesMeasured * 1000f
            : 0f;

        string row = string.Join(",",
            DateTime.Now.ToString(
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture
            ),
            sessionTime.ToString("F2", CultureInfo.InvariantCulture),
            intervalScene,
            fps.ToString("F2", CultureInfo.InvariantCulture),
            averageFrameTime.ToString("F2", CultureInfo.InvariantCulture),
            slowFrames,
            framesMeasured,
            longPauses,
            (excludedTime * 1000f).ToString(
                "F2", CultureInfo.InvariantCulture
            )
        );

        try
        {
            writer.WriteLine(row);
            writer.Flush();
        }
        catch (Exception e)
        {
            Debug.LogError(
                $"FPSMonitor: error al escribir el CSV: {e.Message}"
            );
        }

        ResetInterval();
    }

    private void ResetInterval()
    {
        intervalElapsed = 0f;
        measuredFrameTime = 0f;
        excludedTime = 0f;

        framesMeasured = 0;
        slowFrames = 0;
        longPauses = 0;
    }

    private void OnApplicationQuit()
    {
        CloseLog();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            CloseLog();
            instance = null;
        }
    }

    private void CloseLog()
    {
        if (writer == null)
            return;

        try
        {
            WriteInterval();
            writer.Dispose();
        }
        catch (Exception e)
        {
            Debug.LogError(
                $"FPSMonitor: error al cerrar el CSV: {e.Message}"
            );
        }
        finally
        {
            writer = null;
        }
    }
}
