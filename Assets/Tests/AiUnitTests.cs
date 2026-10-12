using NUnit.Framework;
using UnityEngine;
using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

public class AiUnitTests
{
    [Test]
    public void LaVelocidadCorrespondeALaVarianteFragil()
    {
        float velocidad = Ai.SpeedFor(EnemyVariant.Fragil);

        Assert.AreEqual(
            7f,
            velocidad,
            "La variante Frágil debería tener una velocidad de 7."
        );
    }

    [Test]
    public void LaVelocidadCorrespondeALaVarianteNormal()
    {
        float velocidad = Ai.SpeedFor(EnemyVariant.Normal);

        Assert.AreEqual(
            4f,
            velocidad,
            "La variante Normal debería tener una velocidad de 4."
        );
    }

    [Test]
    public void LaVelocidadCorrespondeALaVarianteResistente()
    {
        float velocidad = Ai.SpeedFor(EnemyVariant.Resistente);

        Assert.AreEqual(
            3f,
            velocidad,
            "La variante Resistente debería tener una velocidad de 3."
        );
    }

    [Test]
    public void AlEstablecerUnObjetivoDeAggroSeConvierteEnElObjetivoActual()
    {
        GameObject enemigo = new GameObject("Enemigo");
        GameObject atacante = new GameObject("Atacante");

        Ai ai = enemigo.AddComponent<Ai>();

        ai.SetAggroTarget(atacante.transform);

        Assert.AreEqual(
            atacante.transform,
            ai.CurrentPlayer,
            "El atacante debería convertirse en el objetivo actual durante el aggro."
        );

        Object.DestroyImmediate(enemigo);
        Object.DestroyImmediate(atacante);
    }

    
}
