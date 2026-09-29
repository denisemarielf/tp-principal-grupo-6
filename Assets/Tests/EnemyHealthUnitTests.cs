using NUnit.Framework;

public class EnemyHealthUnitTests
{
    [Test]
    public void LaVidaInicialCorrespondeALaVarianteFragil()
    {
        float vida = EnemyHealth.HealthFor(EnemyVariant.Fragil);

        Assert.AreEqual(
            50f,
            vida,
            "La variante Frágil debería tener 50 de vida."
        );
    }

    [Test]
    public void LaVidaInicialCorrespondeALaVarianteNormal()
    {
        float vida = EnemyHealth.HealthFor(EnemyVariant.Normal);

        Assert.AreEqual(
            100f,
            vida,
            "La variante Normal debería tener 100 de vida."
        );
    }

    [Test]
    public void LaVidaInicialCorrespondeALaVarianteResistente()
    {
        float vida = EnemyHealth.HealthFor(EnemyVariant.Resistente);

        Assert.AreEqual(
            200f,
            vida,
            "La variante Resistente debería tener 200 de vida."
        );
    }
}
