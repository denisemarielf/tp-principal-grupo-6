using UnityEngine;
using UnityEngine.Rendering;

// Gotas de sangre al pegarle a un zombie. Se arma en codigo para no depender de un prefab.
// Cada impacto elige una variante al azar y cada particula toma una forma de gota al azar.
public static class BloodSplatter
{
    // Cambiá este valor para agrandar o achicar toda la sangre (1 = tamaño original).
    private const float Scale = 4f;

    private struct Variant
    {
        public float angle, radius;
        public float speedMin, speedMax;
        public int countMin, countMax;
        public float sizeMin, sizeMax;
        public float lifeMin, lifeMax;
        public float gravity;
        public bool stretch; // gotas alargadas siguiendo la velocidad
    }

    private static readonly Variant[] Variants =
    {
        // 0 - Rocio (el original)
        new Variant { angle = 35f, radius = 0.04f, speedMin = 2.2f, speedMax = 4.8f, countMin = 14, countMax = 22,
                      sizeMin = 0.02f, sizeMax = 0.055f, lifeMin = 0.28f, lifeMax = 0.5f, gravity = 2.2f },
        // 1 - Explosion ancha
        new Variant { angle = 70f, radius = 0.08f, speedMin = 1.5f, speedMax = 3.5f, countMin = 20, countMax = 30,
                      sizeMin = 0.02f, sizeMax = 0.06f, lifeMin = 0.25f, lifeMax = 0.45f, gravity = 2.5f },
        // 2 - Chorro
        new Variant { angle = 12f, radius = 0.02f, speedMin = 4f, speedMax = 7f, countMin = 8, countMax = 12,
                      sizeMin = 0.025f, sizeMax = 0.05f, lifeMin = 0.3f, lifeMax = 0.5f, gravity = 3f, stretch = true },
        // 3 - Niebla fina
        new Variant { angle = 50f, radius = 0.06f, speedMin = 0.8f, speedMax = 2.2f, countMin = 25, countMax = 35,
                      sizeMin = 0.01f, sizeMax = 0.03f, lifeMin = 0.35f, lifeMax = 0.6f, gravity = 0.8f },
        // 4 - Gotas gordas
        new Variant { angle = 30f, radius = 0.05f, speedMin = 1.8f, speedMax = 3.5f, countMin = 5, countMax = 9,
                      sizeMin = 0.05f, sizeMax = 0.1f, lifeMin = 0.35f, lifeMax = 0.55f, gravity = 3.5f },
    };


    private static Material material;
    
    private static Texture2D atlasTexture;

    public static void Play(Vector3 point, Vector3 normal, Transform woundParent = null)
    {
        if (normal.sqrMagnitude < 0.001f)
            normal = Vector3.up;

        Variant v = Variants[Random.Range(0, Variants.Length)];

        GameObject splatter = new GameObject("BloodSplatter");
        splatter.transform.SetPositionAndRotation(point, Quaternion.LookRotation(normal.normalized));

        ParticleSystem particles = splatter.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particles.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.2f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(v.lifeMin, v.lifeMax);
        main.startSpeed = new ParticleSystem.MinMaxCurve(v.speedMin, v.speedMax);
        main.startSize = new ParticleSystem.MinMaxCurve(v.sizeMin * Scale, v.sizeMax * Scale);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.35f, 0.02f, 0.02f, 1f),
            new Color(0.72f, 0.06f, 0.04f, 1f));
        main.gravityModifier = v.gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 60;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, new ParticleSystem.MinMaxCurve(v.countMin, v.countMax))
        });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = v.angle;
        shape.radius = v.radius;

        // Cada particula elige al azar una de las 4 formas del atlas (2x2).
        ParticleSystem.TextureSheetAnimationModule sheet = particles.textureSheetAnimation;
        sheet.enabled = true;
        sheet.mode = ParticleSystemAnimationMode.Grid;
        sheet.numTilesX = 2;
        sheet.numTilesY = 2;
        sheet.animation = ParticleSystemAnimationType.WholeSheet;
        sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
        sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 0.99f);
        sheet.cycleCount = 1;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.65f, 0.85f),
            new Keyframe(1f, 0.25f)));

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(new Color(0.55f, 0.05f, 0.05f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 0.55f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = gradient;

        ParticleSystemRenderer renderer = splatter.GetComponent<ParticleSystemRenderer>();
        renderer.material = DropletMaterial();
        if (v.stretch)
        {
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.05f;
            renderer.lengthScale = 1.5f;
        }
        else
        {
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
        }
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        BloodDecal.Spray(point, normal, v.angle, Scale);
        if (woundParent != null)
            BloodWound.Begin(woundParent, point, normal, Scale);

        particles.Play();
        Object.Destroy(splatter, 1.2f);
    }

    public static Material DropletMaterial()
    {
        if (material != null)
            return material;

        material = BloodMaterials.Textured(DropletAtlas());
        return material;
    }

    // Atlas 2x2 con 4 formas de gota generadas por codigo.
    private static Texture2D DropletAtlas()
    {
        if (atlasTexture != null)
            return atlasTexture;

        const int cell = 32;
        const int size = cell * 2;
        atlasTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        atlasTexture.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int shapeIndex = (x / cell) + (y / cell) * 2;
                // coordenadas dentro de la celda, centradas: -1..1
                float u = ((x % cell) + 0.5f) / cell * 2f - 1f;
                float v = ((y % cell) + 0.5f) / cell * 2f - 1f;
                float alpha = ShapeAlpha(shapeIndex, u, v);
                atlasTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        atlasTexture.Apply();
        return atlasTexture;
    }

    private static float ShapeAlpha(int shape, float u, float v)
    {
        float d = Mathf.Sqrt(u * u + v * v);

        switch (shape)
        {
            case 0: // gota redonda y suave
                {
                    float a = Mathf.Clamp01(1f - d);
                    return a * a;
                }
            case 1: // mancha irregular
                {
                    float ang = Mathf.Atan2(v, u);
                    float r = 0.6f + 0.18f * Mathf.Sin(3f * ang + 1.3f) + 0.1f * Mathf.Sin(7f * ang + 0.4f);
                    return Mathf.Clamp01((r - d) / 0.2f);
                }
            case 2: // gota alargada
                {
                    float dx = u / 0.4f;
                    float dy = v / 0.9f;
                    float e = Mathf.Sqrt(dx * dx + dy * dy);
                    return Mathf.Clamp01((1f - e) / 0.3f);
                }
            default: // mancha central con gotitas satelite
                {
                    float main = Mathf.Clamp01((0.45f - d) / 0.15f);
                    float s1 = Mathf.Clamp01((0.14f - Vector2.Distance(new Vector2(u, v), new Vector2(0.65f, 0.4f))) / 0.06f);
                    float s2 = Mathf.Clamp01((0.12f - Vector2.Distance(new Vector2(u, v), new Vector2(-0.6f, 0.5f))) / 0.06f);
                    float s3 = Mathf.Clamp01((0.1f - Vector2.Distance(new Vector2(u, v), new Vector2(-0.2f, -0.7f))) / 0.05f);
                    return Mathf.Max(main, Mathf.Max(s1, Mathf.Max(s2, s3)));
                }
        }
    }
}