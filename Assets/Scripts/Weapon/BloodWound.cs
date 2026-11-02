using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Herida que sigue sangrando unos segundos despues del impacto y deja gotas en el piso.
public class BloodWound : MonoBehaviour
{
    private const float Duration = 3f;   // cuanto sangra
    private const int MaxPerTarget = 4;  // heridas activas a la vez por zombie (escopeta = muchos perdigones)

    private static readonly List<BloodWound> active = new List<BloodWound>();

    private float endTime;
    private float nextDrip;

    public static void Begin(Transform parent, Vector3 point, Vector3 normal, float scale)
    {
        parent.GetComponentsInChildren(false, active);
        if (active.Count >= MaxPerTarget) return;

        if (normal.sqrMagnitude < 0.001f) normal = Vector3.up;

        GameObject go = new GameObject("BloodWound");
        go.transform.SetPositionAndRotation(point, Quaternion.LookRotation(normal.normalized));
        go.transform.SetParent(parent, true); // sigue al zombie mientras se mueve

        BloodWound wound = go.AddComponent<BloodWound>();
        wound.endTime = Time.time + Duration;
        wound.nextDrip = Time.time + 0.4f;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = Duration;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 1.1f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.012f * scale, 0.03f * scale);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.35f, 0.02f, 0.02f, 1f),
            new Color(0.65f, 0.05f, 0.04f, 1f));
        main.gravityModifier = 1.8f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 80;

        // Empieza fuerte y se va apagando.
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(28f, new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.35f, 0.7f),
            new Keyframe(1f, 0.08f)));

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 25f;
        shape.radius = 0.01f;

        ParticleSystem.TextureSheetAnimationModule sheet = ps.textureSheetAnimation;
        sheet.enabled = true;
        sheet.mode = ParticleSystemAnimationMode.Grid;
        sheet.numTilesX = 2;
        sheet.numTilesY = 2;
        sheet.animation = ParticleSystemAnimationType.WholeSheet;
        sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
        sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 0.99f);
        sheet.cycleCount = 1;

        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(1f, 0.3f)));

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = BloodSplatter.DropletMaterial();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        ps.Play();
        Destroy(go, Duration + 1.5f);
    }

    private void Update()
    {
        if (Time.time >= endTime || Time.time < nextDrip) return;

        nextDrip = Time.time + Random.Range(0.3f, 0.6f);
        BloodDecal.Drip(transform.position);
    }
}