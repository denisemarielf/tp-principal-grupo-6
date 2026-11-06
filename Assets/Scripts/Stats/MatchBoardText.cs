using System.Text;

// Texto del tablero. Lo comparten la pantalla de fin y el panel de nivel completado.
public static class MatchBoardText
{
    public static string Format(MatchBoard board)
    {
        if (board == null)
            return "Cargando estadísticas...";

        var text = new StringBuilder();
        text.Append("<size=78%><color=#F24026><cspace=0.15em>EQUIPO</cspace></color></size>\n");
        text.Append(string.IsNullOrEmpty(board.Result) ? "PARTIDA" : board.Result.ToUpperInvariant());
        text.Append("\n<color=#ADB5B2>PUNTOS</color>  ").Append(board.Score);

        if (!string.IsNullOrEmpty(board.Civilians))
            text.Append("\n<color=#ADB5B2>").Append(board.Civilians).Append("</color>");

        if (!string.IsNullOrEmpty(board.Extra))
            text.Append("\n<color=#ADB5B2>").Append(board.Extra).Append("</color>");

        text.Append("\n\n<size=78%><color=#F24026><cspace=0.15em>JUGADORES</cspace></color></size>");

        if (board.Players == null || board.Players.Count == 0)
        {
            text.Append("\n<color=#ADB5B2>Sin datos individuales</color>");
            return text.ToString();
        }

        foreach (MatchPlayerRow row in board.Players)
        {
            text.Append("\n<b>").Append(Safe(row.Name)).Append("</b>");
            text.Append("\n<color=#ADB5B2>BAJAS</color>  ").Append(row.Kills);
            text.Append("    <color=#ADB5B2>MUERTES</color>  ").Append(row.Deaths);
            text.Append("    <color=#ADB5B2>DAÑO</color>  ").Append(row.Damage);
            text.Append("    <color=#ADB5B2>CIVILES</color>  ").Append(row.Rescues);
        }

        return text.ToString();
    }

    private static string Safe(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Jugador";

        return name.Replace("<", "").Replace(">", "");
    }
}
