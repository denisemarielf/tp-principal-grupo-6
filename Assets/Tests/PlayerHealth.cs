using NUnit.Framework;

public class PlayerHealthTests
{
    [Test]
    public void ApplyDamage_ReduceLaVidaCorrectamente()
    {
        float resultado = PlayerHealth.ApplyDamage(100f, 25f);

        Assert.AreEqual(75f, resultado);
    }

    [Test]
    public void ApplyDamage_NoPermiteVidaNegativa()
    {
        float resultado = PlayerHealth.ApplyDamage(20f, 50f);

        Assert.AreEqual(0f, resultado);
    }

    [Test]
    public void ApplyDamage_DanioNegativo_NoModificaLaVida()
    {
        float resultado = PlayerHealth.ApplyDamage(100f, -20f);

        Assert.AreEqual(100f, resultado);
    }

    [Test]
    public void ApplyHeal_AumentaLaVidaCorrectamente()
    {
        float resultado = PlayerHealth.ApplyHeal(50f, 25f, 100f);

        Assert.AreEqual(75f, resultado);
    }

    [Test]
    public void ApplyHeal_NoSuperaLaVidaMaxima()
    {
        float resultado = PlayerHealth.ApplyHeal(90f, 30f, 100f);

        Assert.AreEqual(100f, resultado);
    }

    [Test]
    public void ApplyHeal_CuracionNegativa_NoModificaLaVida()
    {
        float resultado = PlayerHealth.ApplyHeal(50f, -20f, 100f);

        Assert.AreEqual(50f, resultado);
    }
}
