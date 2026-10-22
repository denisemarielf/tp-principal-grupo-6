
using System;
using System.IO;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class SaveSystemTests
{
    private string testDirectory;

    [SetUp]
    public void SetUp()
    {
        testDirectory = Path.Combine(
            Path.GetTempPath(),
            "SaveSystemTests_" + Guid.NewGuid().ToString("N")
        );

        Directory.CreateDirectory(testDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(testDirectory))
        {
            Directory.Delete(testDirectory, true);
        }
    }

    // ==========================================
    // 1. PRUEBAS DE EVALUATE
    // ==========================================

    [Test]
    public void Evaluate_SinSesion_BloqueaGuardado()
    {
        var resultado = SaveSystem.Evaluate(
            hasSession: false,
            isHost: true,
            sceneHasSafeZone: true,
            hostInsideSafeZone: true
        );

        Assert.AreEqual(SaveBlockReason.NoSession, resultado);
    }

    [Test]
    public void Evaluate_SiNoEsHost_BloqueaGuardado()
    {
        var resultado = SaveSystem.Evaluate(
            hasSession: true,
            isHost: false,
            sceneHasSafeZone: true,
            hostInsideSafeZone: true
        );

        Assert.AreEqual(SaveBlockReason.NotHost, resultado);
    }

    [Test]
    public void Evaluate_SiNoEstaEnExtraccion_BloqueaGuardado()
    {
        var resultado = SaveSystem.Evaluate(
            hasSession: true,
            isHost: true,
            sceneHasSafeZone: false,
            hostInsideSafeZone: true
        );

        Assert.AreEqual(SaveBlockReason.NotExtraction, resultado);
    }

    [Test]
    public void Evaluate_SiHostNoEstaEnZonaSegura_BloqueaGuardado()
    {
        var resultado = SaveSystem.Evaluate(
            hasSession: true,
            isHost: true,
            sceneHasSafeZone: true,
            hostInsideSafeZone: false
        );

        Assert.AreEqual(SaveBlockReason.NotInSafeZone, resultado);
    }

    [Test]
    public void Evaluate_SiSeCumplenTodasLasCondiciones_PermiteGuardado()
    {
        var resultado = SaveSystem.Evaluate(
            hasSession: true,
            isHost: true,
            sceneHasSafeZone: true,
            hostInsideSafeZone: true
        );

        Assert.AreEqual(SaveBlockReason.None, resultado);
    }

    // ==========================================
    // 2. PRUEBA DE DESCRIBE
    // ==========================================

    [TestCase(
        SaveBlockReason.NoSession,
        "No hay una partida en curso."
    )]
    [TestCase(
        SaveBlockReason.NotHost,
        "Solo el host puede guardar la partida."
    )]
    [TestCase(
        SaveBlockReason.NotExtraction,
        "Solo se puede guardar en el modo Extraccion."
    )]
    [TestCase(
        SaveBlockReason.NotInSafeZone,
        "El host tiene que estar en una zona segura para guardar."
    )]
    public void Describe_DevuelveMensajeCorrecto(
        SaveBlockReason reason,
        string mensajeEsperado)
    {
        string resultado = SaveSystem.Describe(reason);

        Assert.AreEqual(mensajeEsperado, resultado);
    }

    // ==========================================
    // 3. PRUEBA DE TOJSON
    // ==========================================

    [Test]
    public void ToJson_ConvierteLosDatosDePartidaAJson()
    {
        var data = new GameSaveData
        {
            sceneName = "SampleScene",
            savedAtUtc = "2026-10-22T12:00:00Z",
            players = new List<PlayerSaveData>
            {
                new PlayerSaveData
                {
                    playerName = "JugadorPrueba",
                    clientId = 1,
                    isHost = true,
                    health = 75,
                    maxHealth = 100,
                    isDead = false,

                    inventory = new List<InventorySlotSave>
                    {
                        new InventorySlotSave
                        {
                            itemId = 10,
                            quantity = 3
                        }
                    },

                    weapons = new List<WeaponSave>
                    {
                        new WeaponSave
                        {
                            slot = 0,
                            weaponType = (int)WeaponType.rifleAKH,
                            currentAmmo = 15,
                            reserveAmmo = 45
                        }
                    }
                }
            }
        };

        string json = SaveSystem.ToJson(data);

        Assert.IsNotNull(json);
        Assert.IsNotEmpty(json);

        // Se recuperan los datos dentro del test para comprobar
        // que la conversión a JSON conservó sus valores.
        GameSaveData recuperado =
            JsonUtility.FromJson<GameSaveData>(json);

        Assert.IsNotNull(recuperado);
        Assert.AreEqual("SampleScene", recuperado.sceneName);
        Assert.AreEqual(1, recuperado.players.Count);

        PlayerSaveData jugador = recuperado.players[0];

        Assert.AreEqual("JugadorPrueba", jugador.playerName);
        Assert.AreEqual(1, jugador.clientId);
        Assert.IsTrue(jugador.isHost);
        Assert.AreEqual(75, jugador.health);
        Assert.AreEqual(100, jugador.maxHealth);
        Assert.IsFalse(jugador.isDead);

        Assert.AreEqual(1, jugador.inventory.Count);
        Assert.AreEqual(10, jugador.inventory[0].itemId);
        Assert.AreEqual(3, jugador.inventory[0].quantity);

        Assert.AreEqual(1, jugador.weapons.Count);
        Assert.AreEqual(
            (int)WeaponType.rifleAKH,
            jugador.weapons[0].weaponType
        );
        Assert.AreEqual(15, jugador.weapons[0].currentAmmo);
        Assert.AreEqual(45, jugador.weapons[0].reserveAmmo);
    }

    // ==========================================
    // 4. PRUEBA DE TRYWRITE
    // ==========================================

    [Test]
    public void TryWrite_EscribeElArchivoCorrectamente()
    {
        string path = Path.Combine(
            testDirectory,
            "savegame.json"
        );

        var data = new GameSaveData
        {
            sceneName = "SampleScene",
            players = new List<PlayerSaveData>()
        };

        bool resultado = SaveSystem.TryWrite(
            data,
            out string error,
            path
        );

        Assert.IsTrue(
            resultado,
            "El guardado debería ser exitoso. Error: " + error
        );

        Assert.IsTrue(
            File.Exists(path),
            "El archivo de guardado debería existir."
        );

        string contenido = File.ReadAllText(path);

        Assert.IsNotEmpty(contenido);

        GameSaveData recuperado =
            JsonUtility.FromJson<GameSaveData>(contenido);

        Assert.IsNotNull(recuperado);
        Assert.AreEqual("SampleScene", recuperado.sceneName);
    }
}
