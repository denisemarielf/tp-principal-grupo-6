using UnityEngine;
using UnityEngine.Rendering;

// Gotas de sangre al pegarle a un zombie. Se arma en codigo para no depender de un prefab.
public static class BloodSplatter
{
    private static Material material;
    private static Texture2D dropletTexture;

    public static void Play(Vector3 point, Vector3 normal)
    {
        if (normal.sqrMagnitude < 0.001f)
            normal = Vector3.up;

        GameObject splatter = new GameObject("BloodSplatter");
        splatter.transform.SetPositionAndRotation(point, Quaternion.LookRotation(normal.normalized));

        ParticleSystem particles = splatter.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particles.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.2f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.6f, 3.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.055f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.35f, 0.02f, 0.02f, 1f),
            new Color(0.72f, 0.06f, 0.04f, 1f));
        main.gravityModifier = 2.2f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 24;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, new ParticleSystem.MinMaxCurve(8f, 14f))
        });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 28f;
        shape.radius = 0.02f;

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
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        particles.Play();
        Object.Destroy(splatter, 1.2f);
    }

    private static Material DropletMaterial()
    {
        if (material != null)
            return material;

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Particles/Standard Unlit");

        material = new Material(shader);
        material.mainTexture = DropletTexture();
        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", dropletTexture);
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
        return material;
    }

    private static Texture2D DropletTexture()
    {
        if (dropletTexture != null)
            return dropletTexture;

        const int size = 32;
        dropletTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        dropletTexture.wrapMode = TextureWrapMode.Clamp;
        float radius = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius)) / radius;
                float alpha = Mathf.Clamp01(1f - distance);
                alpha *= alpha;
                dropletTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        dropletTexture.Apply();
        return dropletTexture;
    }
}
