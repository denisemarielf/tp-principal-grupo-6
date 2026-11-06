using System;
using System.Collections.Generic;

// Fila de un jugador al cierre de la partida. La arma el host y la leen todos.
public struct MatchPlayerRow
{
    public ulong ClientId;
    public string Name;
    public int Kills;
    public int Deaths;
    public int Damage;
    public int Rescues;
}

// Datos de equipo que acompañan a las filas (resultado, puntos, civiles).
public struct MatchTeamSummary
{
    public string Result;
    public int Score;
    public string Civilians;
    public string Extra;
}

// Tablero ya cerrado: equipo + filas ordenadas. Es lo que viaja a los clientes.
public sealed class MatchBoard
{
    public string Result;
    public int Score;
    public string Civilians;
    public string Extra;
    public List<MatchPlayerRow> Players = new List<MatchPlayerRow>();

    public static MatchBoard From(MatchTeamSummary team, List<MatchPlayerRow> players)
    {
        return new MatchBoard
        {
            Result = team.Result ?? "",
            Score = team.Score,
            Civilians = team.Civilians ?? "",
            Extra = team.Extra ?? "",
            Players = players ?? new List<MatchPlayerRow>()
        };
    }
}

// Acumulador puro de la partida. No conoce la red: solo suma y ordena.
public sealed class MatchStatsLedger
{
    private readonly Dictionary<ulong, MatchPlayerRow> rows = new Dictionary<ulong, MatchPlayerRow>();

    public static string FallbackName(ulong clientId)
    {
        return "Jugador " + (clientId + 1);
    }

    public void Clear()
    {
        rows.Clear();
    }

    public void SetName(ulong clientId, string name)
    {
        MatchPlayerRow row = GetOrCreate(clientId);
        row.Name = string.IsNullOrWhiteSpace(name) ? FallbackName(clientId) : name.Trim();
        rows[clientId] = row;
    }

    public void AddKill(ulong clientId)
    {
        MatchPlayerRow row = GetOrCreate(clientId);
        row.Kills++;
        rows[clientId] = row;
    }

    public void AddDeath(ulong clientId)
    {
        MatchPlayerRow row = GetOrCreate(clientId);
        row.Deaths++;
        rows[clientId] = row;
    }

    public void AddDamage(ulong clientId, int amount)
    {
        if (amount <= 0)
            return;

        MatchPlayerRow row = GetOrCreate(clientId);
        row.Damage += amount;
        rows[clientId] = row;
    }

    public void AddRescue(ulong clientId)
    {
        MatchPlayerRow row = GetOrCreate(clientId);
        row.Rescues++;
        rows[clientId] = row;
    }

    // Más bajas primero. Si empatan, más daño. Si siguen empatando, por nombre.
    public List<MatchPlayerRow> BuildRows()
    {
        var list = new List<MatchPlayerRow>(rows.Values);
        list.Sort(Compare);
        return list;
    }

    private static int Compare(MatchPlayerRow a, MatchPlayerRow b)
    {
        int kills = b.Kills.CompareTo(a.Kills);
        if (kills != 0)
            return kills;

        int damage = b.Damage.CompareTo(a.Damage);
        if (damage != 0)
            return damage;

        return string.Compare(a.Name, b.Name, StringComparison.Ordinal);
    }

    private MatchPlayerRow GetOrCreate(ulong clientId)
    {
        if (!rows.TryGetValue(clientId, out MatchPlayerRow row))
        {
            row = new MatchPlayerRow
            {
                ClientId = clientId,
                Name = FallbackName(clientId)
            };
        }

        return row;
    }
}
