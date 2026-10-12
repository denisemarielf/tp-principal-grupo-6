using NUnit.Framework;

public class HitSlowTests
{
    [Test]
    public void CombineSpeed_MultiplicaBoostPorFrenado()
    {
        float resultado = PMovement.CombineSpeed(2f, 0.7f);

        Assert.AreEqual(1.4f, resultado);
    }

    [Test]
    public void ClampHitSlow_CeroNoCongelaAlJugador()
    {
        float resultado = PMovement.ClampHitSlow(0f);

        Assert.AreEqual(PMovement.MinHitSlow, resultado);
    }

    [Test]
    public void ClampHitSlow_MayorAUnoNoAceleraAlJugador()
    {
        float resultado = PMovement.ClampHitSlow(1.5f);

        Assert.AreEqual(1f, resultado);
    }

    [Test]
    public void ClampHitSlow_ValorValidoSeMantiene()
    {
        float resultado = PMovement.ClampHitSlow(0.7f);

        Assert.AreEqual(0.7f, resultado);
    }
}
