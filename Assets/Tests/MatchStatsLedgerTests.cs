using System.Collections.Generic;
using NUnit.Framework;

public class MatchStatsLedgerTests
{
    [Test]
    public void BuildRows_OrdenaPorBajas()
    {
        var ledger = new MatchStatsLedger();
        ledger.SetName(1, "Ana");
        ledger.SetName(2, "Beto");

        ledger.AddDeath(1);
        ledger.AddRescue(1);
        ledger.AddKill(2);
        ledger.AddDamage(2, 80);

        List<MatchPlayerRow> rows = ledger.BuildRows();

        Assert.AreEqual(2, rows.Count);

        Assert.AreEqual("Beto", rows[0].Name);
        Assert.AreEqual(1, rows[0].Kills);
        Assert.AreEqual(80, rows[0].Damage);
        Assert.AreEqual(0, rows[0].Deaths);
        Assert.AreEqual(0, rows[0].Rescues);

        Assert.AreEqual("Ana", rows[1].Name);
        Assert.AreEqual(0, rows[1].Kills);
        Assert.AreEqual(1, rows[1].Deaths);
        Assert.AreEqual(1, rows[1].Rescues);
        Assert.AreEqual(0, rows[1].Damage);
    }
}
