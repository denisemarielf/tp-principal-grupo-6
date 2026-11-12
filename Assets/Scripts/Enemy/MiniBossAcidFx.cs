using UnityEngine;

// Chorro de acido procedural (sin prefabs). Se crea como hijo del jefe en la boca y se autodestruye.
public static class MiniBossAcidFx
{
    private static Texture2D softCircle;
    private static Material material;

    public static void Spray(Transform boss, Vector3 localMouth, float duration, float range, float halfAngle, LayerMask blockMask)
    {
        if (boss == null)
            return;

        Material mat = GetMaterial();
        if (mat == null)
            return;

        const float lifetime = 0.9f;

        GameObject go = new GameObject("AcidSprayFx");
        go.transform.SetParent(boss, false);
        go.transform.localPosition = localMouth;
        go.transform.localRotation = Quaternion.identity;   // el cono sale hacia +Z (adelante del jefe)

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);   // se configura apagado

        ParticleSystem.MainModule main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = Mathf.Max(0.1f, duration);
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.8f, lifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(range / lifetime * 0.75f, range / lifetime); // llega a 'range'
        main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
        main.gravityModifier = 0.35f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 400;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 140f;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = halfAngle;
        shape.radius = 0.15f;

        ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
        color.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.55f, 1f, 0.15f), 0f),
                new GradientColorKey(new Color(0.3f, 0.7f, 0.05f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.9f, 0f),
                new GradientAlphaKey(0.6f, 0.6f),
                new GradientAlphaKey(0f, 1f)
            });
        color.color = gradient;

        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));

        // Las particulas mueren al chocar con paredes/suelo (visual coherente con el hitbox).
        ParticleSystem.CollisionModule collision = ps.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.mode = ParticleSystemCollisionMode.Collision3D;
        collision.collidesWith = blockMask;
        collision.dampen = 1f;
        collision.bounce = 0f;
        collision.lifetimeLoss = 1f;       // muere al primer choque
        collision.radiusScale = 0.5f;
        collision.sendCollisionMessages = false;

        ParticleSystemRenderer rendererComponent = go.GetComponent<ParticleSystemRenderer>();
        rendererComponent.renderMode = ParticleSystemRenderMode.Billboard;
        rendererComponent.sharedMaterial = mat;

        ps.Play();
        Object.Destroy(go, duration + lifetime + 0.5f);
    }

    private static Material GetMaterial()
    {
        if (material != null)
            return material;

        // Sprites/Default viene incluido en los builds y funciona en Built-in y URP.
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null)
            return null;

        material = new Material(shader) { mainTexture = GetSoftCircle() };
        return material;
    }

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
}