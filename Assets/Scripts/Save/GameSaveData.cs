using System;
using System.Collections.Generic;
using UnityEngine;

// Datos de una partida guardada (US 3.6). Son clases simples y serializables para que JsonUtility las
// convierta a JSON; no dependen de la red ni de la escena, asi se pueden probar sin levantar un host.

[Serializable]
public class InventorySlotSave
{
    public int itemId;   // -1 = vacio
    public int quantity;
}

[Serializable]
public class WeaponSave
{
    public int slot;
    public int weaponType;     // valor de WeaponType, -1 = slot vacio
    public int currentAmmo;    // balas en el cargador
    public int reserveAmmo;    // balas de reserva
}

[Serializable]
public class PlayerSaveData
{
    public string playerName;
    public ulong clientId;     // id de red de la sesion en que se guardo (no es estable entre sesiones)
    public bool isHost;
    public float health;
    public float maxHealth;
    public bool isDead;
    public List<InventorySlotSave> inventory = new List<InventorySlotSave>();
    public List<WeaponSave> weapons = new List<WeaponSave>();
}

[Serializable]
public class GameSaveData
{
    public int version = SaveSystem.CurrentVersion;
    public string savedAtUtc;  // ISO 8601
    public string sceneName;
    public string gameId;      // identifica la partida: cada guardado reemplaza al anterior de la misma partida
    public string mode = SaveSystem.ModeExtraction;
    public int difficulty = 1; // indice del selector del menu (0 facil, 1 normal, 2 dificil)
    public bool levelCompleted; // guardado automatico al completar el nivel: se continua en el siguiente
    public bool hasSafePoint;
    public Vector3 safePoint;  // donde estaba el host al guardar
    public List<PlayerSaveData> players = new List<PlayerSaveData>();
}

public class SavedGameInfo
{
    public string Path;
    public GameSaveData Data;  // null si el archivo esta danado
    public string Error;

    public bool IsValid => Data != null;
}
