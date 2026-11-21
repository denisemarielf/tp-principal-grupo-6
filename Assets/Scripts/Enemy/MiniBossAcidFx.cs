using UnityEngine;

// Chorro de acido procedural (sin prefabs). Se crea como hijo del jefe en la boca y se autodestruye.
// Varias capas: chorro (manchones), gotas estiradas, burbujas, destello en la boca y salpicadura al chocar.
public static class MiniBossAcidFx
{
    private const float BlobLifetime = 0.9f;
    private const float MaxLifetime = 1.4f;   // vida maxima entre todas las capas (para el Destroy)

    private static Texture2D softCircle;
    private static Texture2D bubble;
    private static Texture2D droplet;

    private static Material blobMaterial;
    private static Material bubbleMaterial;
    private static Material dropletMaterial;

    public static void Spray(Transform boss, Vector3 localMouth, float duration, float range, float halfAngle, LayerMask blockMask)
    {
        if (boss == null)
            return;

        Material blobMat = GetMaterial(ref blobMaterial, GetSoftCircle());
        Material bubbleMat = GetMaterial(ref bubbleMaterial, GetBubble());
        Material dropletMat = GetMaterial(ref dropletMaterial, GetDroplet());
        if (blobMat == null || bubbleMat == null || dropletMat == null)
            return;

        float safeDuration = Mathf.Max(0.1f, duration);

        GameObject root = new GameObject("AcidSprayFx");
        root.transform.SetParent(boss, false);
        root.transform.localPosition = localMouth;
        root.transform.localRotation = Quaternion.identity;   // el cono sale hacia +Z (adelante del jefe)

        ParticleSystem blobs = BuildBlobs(root.transform, blobMat, safeDuration, range, halfAngle, blockMask);
        ParticleSystem drops = BuildDroplets(root.transform, dropletMat, safeDuration, range, halfAngle, blockMask);
        ParticleSystem fizz = BuildFizz(root.transform, bubbleMat, safeDuration, range, halfAngle, blockMask);
        ParticleSystem flash = BuildFlash(root.transform, blobMat);

        // Salpicadura: las gotas que chocan con algo sueltan un puñado de gotitas mas chicas.
        ParticleSystem splash = BuildSplash(drops.transform, dropletMat);
        ParticleSystem.SubEmittersModule subEmitters = drops.subEmitters;
        subEmitters.enabled = true;
        subEmitters.AddSubEmitter(splash, ParticleSystemSubEmitterType.Collision,
            ParticleSystemSubEmitterProperties.InheritNothing, 0.35f);

        // El splash NO se reproduce solo: lo dispara el sub-emisor al chocar.
        blobs.Play();
        drops.Play();
        fizz.Play();
        flash.Play();

        Object.Destroy(root, safeDuration + MaxLifetime + 0.5f);
    }

    // ---------------------------------------------------------------- capas

    // Chorro principal: manchones grandes y blandos que giran y se agrandan.
    private static ParticleSystem BuildBlobs(Transform parent, Material mat, float duration, float range, float halfAngle, LayerMask blockMask)
    {
        ParticleSystem ps = CreateLayer(parent, "Blobs", mat, duration);

        ParticleSystem.MainModule main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(BlobLifetime * 0.8f, BlobLifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(range / BlobLifetime * 0.75f, range / BlobLifetime); // llega a 'range'
        main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.85f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.65f, 1f, 0.2f), new Color(0.35f, 0.9f, 0.1f));
        main.gravityModifier = 0.3f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 500;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 130f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)15, (short)20) });   // golpe inicial al abrir la boca

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = halfAngle;
        shape.radius = 0.15f;

        // Turbulencia para que el chorro serpentee en vez de ser un cono perfecto.
        ParticleSystem.NoiseModule noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.5f;
        noise.frequency = 0.6f;
        noise.scrollSpeed = 0.5f;
        noise.damping = true;

        ParticleSystem.RotationOverLifetimeModule rotation = ps.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);

        ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
        color.enabled = true;
        color.color = MakeGradient(
            new Color(0.75f, 1f, 0.35f), new Color(0.25f, 0.65f, 0.05f),
            0.9f, 0.6f, 0.6f);

        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.7f));

        SetupCollision(ps, blockMask, 0.5f);
        return ps;
    }

    // Gotas rapidas y estiradas (se ven como lineas cortas siguiendo la trayectoria).
    private static ParticleSystem BuildDroplets(Transform parent, Material mat, float duration, float range, float halfAngle, LayerMask blockMask)
    {
        ParticleSystem ps = CreateLayer(parent, "Droplets", mat, duration);

        ParticleSystem.MainModule main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.0f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(range * 0.7f, range * 1.15f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.16f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.8f, 1f, 0.4f), new Color(0.4f, 0.95f, 0.1f));
        main.gravityModifier = 1.2f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 300;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 80f;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = halfAngle * 1.15f;
        shape.radius = 0.12f;

        ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
        color.enabled = true;
        color.color = MakeGradient(
            Color.white, new Color(0.5f, 0.85f, 0.2f),
            1f, 1f, 0.8f);

        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = 0.05f;
        renderer.lengthScale = 1.8f;
        renderer.cameraVelocityScale = 0f;

        SetupCollision(ps, blockMask, 0.5f);
        return ps;
    }

    // Burbujitas que flotan: dan la sensacion de acido que burbujea.
    private static ParticleSystem BuildFizz(Transform parent, Material mat, float duration, float range, float halfAngle, LayerMask blockMask)
    {
        ParticleSystem ps = CreateLayer(parent, "Fizz", mat, duration);

        ParticleSystem.MainModule main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(range * 0.25f, range * 0.55f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.85f, 1f, 0.55f), new Color(0.6f, 1f, 0.3f));
        main.gravityModifier = -0.12f;   // suben un poco
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 120;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 35f;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = halfAngle * 1.4f;
        shape.radius = 0.2f;

        ParticleSystem.NoiseModule noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.4f;
        noise.frequency = 0.9f;
        noise.damping = true;

        ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
        color.enabled = true;
        color.color = MakeGradient(
            Color.white, Color.white,
            0.85f, 0.7f, 0.6f);

        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.8f, 1f, 1.25f));

        SetupCollision(ps, blockMask, 0.5f);
        return ps;
    }

    // Destello verde breve en la boca al empezar el ataque.
    private static ParticleSystem BuildFlash(Transform parent, Material mat)
    {
        ParticleSystem ps = CreateLayer(parent, "Flash", mat, 0.3f);

        ParticleSystem.MainModule main = ps.main;
        main.startLifetime = 0.25f;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(1.6f, 2.2f);
        main.startColor = new Color(0.7f, 1f, 0.3f, 0.75f);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;   // se queda pegado a la boca
        main.maxParticles = 4;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)1, (short)1) });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = false;

        ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
        color.enabled = true;
        color.color = MakeGradient(Color.white, Color.white, 1f, 0.5f, 0.5f);

        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 1.5f));

        return ps;
    }

    // Sub-emisor: gotitas que saltan al chocar una gota contra pared/suelo.
    private static ParticleSystem BuildSplash(Transform parent, Material mat)
    {
        ParticleSystem ps = CreateLayer(parent, "Splash", mat, 0.2f);

        ParticleSystem.MainModule main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.22f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.7f, 1f, 0.3f), new Color(0.4f, 0.9f, 0.1f));
        main.gravityModifier = 1.5f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 300;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)2, (short)4) });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.03f;

        ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
        color.enabled = true;
        color.color = MakeGradient(Color.white, new Color(0.5f, 0.85f, 0.2f), 1f, 0.8f, 0.8f);

        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));

        return ps;
    }

    // ---------------------------------------------------------------- helpers

    // Crea un hijo con ParticleSystem apagado y con la config comun (hay que pararlo antes de tocar 'duration').
    private static ParticleSystem CreateLayer(Transform parent, string name, Material mat, float duration)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);   // se configura apagado

        ParticleSystem.MainModule main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = Mathf.Max(0.1f, duration);

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = mat;

        return ps;
    }

    // Las particulas mueren al chocar con paredes/suelo (visual coherente con el hitbox).
    private static void SetupCollision(ParticleSystem ps, LayerMask blockMask, float radiusScale)
    {
        ParticleSystem.CollisionModule collision = ps.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.mode = ParticleSystemCollisionMode.Collision3D;
        collision.collidesWith = blockMask;
        collision.dampen = 1f;
        collision.bounce = 0f;
        collision.lifetimeLoss = 1f;       // muere al primer choque
        collision.radiusScale = radiusScale;
        collision.sendCollisionMessages = false;
    }

    private static Gradient MakeGradient(Color start, Color end, float a0, float a1, float a1Time)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(start, 0f),
                new GradientColorKey(end, 1f)
            },
            new[]
            {
                new GradientAlphaKey(a0, 0f),
                new GradientAlphaKey(a1, a1Time),
                new GradientAlphaKey(0f, 1f)
            });
        return gradient;
    }

    private static Material GetMaterial(ref Material cache, Texture2D texture)
    {
        if (cache != null)
            return cache;

        // Sprites/Default viene incluido en los builds y funciona en Built-in y URP.
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null)
            return null;

        cache = new Material(shader) { mainTexture = texture };
        return cache;
    }

    // ---------------------------------------------------------------- texturas procedurales

    private static Texture2D GetSoftCircle()
    {
        if (softCircle != null)
            return softCircle;

        const int size = 32;
        softCircle = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };

        float center = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / center;
                float a = Mathf.Clamp01(1f - d);
                softCircle.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        }

        softCircle.Apply();
        return softCircle;
    }

    // Circulo casi duro, con borde suave: para gotas y salpicaduras.
    private static Texture2D GetDroplet()
    {
        if (droplet != null)
            return droplet;

        const int size = 32;
        droplet = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };

        float center = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / center;
                float a = Mathf.Clamp01((1f - d) * 3f);
                droplet.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }

        droplet.Apply();
        return droplet;
    }

    // Burbuja: borde brillante, relleno tenue y un reflejo arriba a la izquierda.
    private static Texture2D GetBubble()
    {
        if (bubble != null)
            return bubble;

        const int size = 64;
        bubble = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };

        float center = (size - 1) * 0.5f;
        Vector2 highlight = new Vector2(center * 0.65f, center * 1.4f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x, y);
                float d = Vector2.Distance(p, new Vector2(center, center)) / center;

                float a = 0f;
                if (d <= 1f)
                {
                    float rim = Mathf.Clamp01(1f - Mathf.Abs(d - 0.8f) / 0.15f);
                    float fill = 0.18f * (1f - d);
                    float hl = Mathf.Clamp01(1f - Vector2.Distance(p, highlight) / (size * 0.12f));
                    a = Mathf.Clamp01(rim * 0.9f + fill + hl * hl * 0.9f);
                }

                bubble.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }

        bubble.Apply();
        return bubble;
    }
}
