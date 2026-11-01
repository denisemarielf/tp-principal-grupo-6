using UnityEngine;
using UnityEngine.Rendering;

// Manchas de sangre en piso y paredes. Pool circular: las mas viejas se reciclan.
public class BloodDecal : MonoBehaviour
{
    private const int PoolSize = 120;
    private const float Lifetime = 25f;      // segundos que dura la mancha
    private const float FadeTime = 5f;       // ultimos segundos: se desvanece
    private const float SurfaceOffset = 0.015f;
    private const float MaxDistance = 3.5f;  // alcance de los rayos
    private const float Alpha = 0.92f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly RaycastHit[] hits = new RaycastHit[8];

    private static BloodDecal[] pool = new BloodDecal[PoolSize];
    private static int next;
    private static Transform root;
    private static Mesh quad;
    private static Material[] materials;

    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock block;
    private Color color;
    private float showAt, hideAt;

    // Chorro del impacto: manchas en la direccion del disparo y en el piso.
    public static void Spray(Vector3 point, Vector3 normal, float coneAngle, float scale)
    {
        if (normal.sqrMagnitude < 0.001f) normal = Vector3.up;
        normal.Normalize();
        Vector3 origin = point + normal * 0.05f;

        int sprays = Random.Range(2, 5);
        for (int i = 0; i < sprays; i++)
        {
            Vector3 perp = Vector3.Cross(normal, Random.onUnitSphere).normalized;
            Vector3 dir = Quaternion.AngleAxis(Random.Range(0f, coneAngle), perp) * normal;
            dir.y -= 0.35f; // la sangre cae un poco
            Place(origin, dir.normalized, scale * Random.Range(0.08f, 0.2f));
        }

        int floor = Random.Range(1, 3);
        for (int i = 0; i < floor; i++)
        {
            Vector3 start = point + new Vector3(Random.Range(-0.4f, 0.4f), 0.1f, Random.Range(-0.4f, 0.4f));
            Place(start, Vector3.down, scale * Random.Range(0.08f, 0.2f));
        }
    }

    // Gota chica desde la herida hacia el piso.
    public static void Drip(Vector3 from)
    {
        Place(from + Vector3.up * 0.05f, Vector3.down, Random.Range(0.12f, 0.28f));
    }

    private static void Place(Vector3 origin, Vector3 dir, float size)
    {
        int count = Physics.RaycastNonAlloc(origin, dir, hits, MaxDistance, ~0, QueryTriggerInteraction.Ignore);
        int best = -1;
        float bestDist = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            Collider c = hits[i].collider;
            // No manchar zombies, jugadores ni civiles.
            if (c.GetComponentInParent<EnemyHealth>() != null) continue;
            if (c.GetComponentInParent<PlayerHealth>() != null) continue;
            if (c.GetComponentInParent<RescuableNpc>() != null) continue;
            if (hits[i].distance < bestDist) { bestDist = hits[i].distance; best = i; }
        }
        if (best < 0) return;

        RaycastHit hit = hits[best];
        Spawn(hit.point, hit.normal, size, hit.distance / 7f);
    }

    private static void Spawn(Vector3 point, Vector3 surfaceNormal, float size, float delay)
    {
        EnsureResources();

        BloodDecal decal = pool[next];
        if (decal == null)
        {
            decal = Create();
            pool[next] = decal;
        }
        next = (next + 1) % PoolSize;

        Quaternion rot = Quaternion.LookRotation(-surfaceNormal) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        decal.Show(point + surfaceNormal * SurfaceOffset, rot, size, delay);
    }

    private void Show(Vector3 pos, Quaternion rot, float size, float delay)
    {
        transform.SetPositionAndRotation(pos, rot);
        transform.localScale = Vector3.one * size;
        meshRenderer.sharedMaterial = materials[Random.Range(0, materials.Length)];
        color = Color.Lerp(new Color(0.28f, 0.01f, 0.01f), new Color(0.55f, 0.03f, 0.02f), Random.value);
        SetAlpha(Alpha);

        showAt = Time.time + delay;
        hideAt = showAt + Lifetime;
        meshRenderer.enabled = false; // aparece cuando termina el delay
        enabled = true;
    }

    private void Update()
    {
        float t = Time.time;
        if (!meshRenderer.enabled)
        {
            if (t < showAt) return;
            meshRenderer.enabled = true;
        }

        float remaining = hideAt - t;
        if (remaining <= 0f)
        {
            meshRenderer.enabled = false;
            enabled = false; // sin Update mientras esta libre en el pool
            return;
        }
        if (remaining < FadeTime)
            SetAlpha(Alpha * remaining / FadeTime);
    }

    private void SetAlpha(float a)
    {
        Color c = color;
        c.a = a;
        block.SetColor(BaseColorId, c);
        block.SetColor(ColorId, c);
        meshRenderer.SetPropertyBlock(block);
    }

    // ---------- Recursos generados por codigo ----------

    private static BloodDecal Create()
    {
        GameObject go = new GameObject("BloodDecal");
        go.transform.SetParent(root, false);
        go.AddComponent<MeshFilter>().sharedMesh = quad;

        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;

        BloodDecal decal = go.AddComponent<BloodDecal>();
        decal.meshRenderer = mr;
        decal.block = new MaterialPropertyBlock();
        return decal;
    }

    private static void EnsureResources()
    {
        if (root == null)
        {
            root = new GameObject("BloodDecals").transform;
            pool = new BloodDecal[PoolSize];
            next = 0;
        }
        if (quad == null)
            quad = MakeQuad();
        if (materials == null || materials[0] == null)
        {
            materials = new Material[4];
            for (int i = 0; i < materials.Length; i++)
                materials[i] = MakeMaterial(MakeStain(i + 1));
        }
    }

    private static Mesh MakeQuad()
    {
        Mesh m = new Mesh();
        m.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f)
        };
        m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
        m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        m.RecalculateNormals();
        return m;
    }

    private static Material MakeMaterial(Texture2D tex)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Particles/Standard Unlit");

        Material m = new Material(shader);
        m.mainTexture = tex;
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
        if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
        if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f); // se ve de ambos lados
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
        return m;
    }

    // Mancha irregular con borde ondulado y gotitas alrededor. Una textura distinta por semilla.
    private static Texture2D MakeStain(int seed)
    {
        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        tex.wrapMode = TextureWrapMode.Clamp;

        System.Random rng = new System.Random(seed * 7919);
        int dots = 4 + rng.Next(4);
        float[] sx = new float[dots], sy = new float[dots], sr = new float[dots];
        for (int i = 0; i < dots; i++)
        {
            float a = (float)(rng.NextDouble() * Mathf.PI * 2.0);
            float d = 0.55f + (float)rng.NextDouble() * 0.3f;
            sx[i] = Mathf.Cos(a) * d;
            sy[i] = Mathf.Sin(a) * d;
            sr[i] = 0.04f + (float)rng.NextDouble() * 0.07f;
        }

        float off = seed * 17.3f;
        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f;
                float v = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Sqrt(u * u + v * v);
                float ang = Mathf.Atan2(v, u);

                float n = Mathf.PerlinNoise(off + Mathf.Cos(ang) * 1.4f + 2f, off + Mathf.Sin(ang) * 1.4f + 2f);
                float r = 0.25f + n * 0.35f;
                float alpha = Mathf.Clamp01((r - d) / 0.1f);

                for (int i = 0; i < dots; i++)
                {
                    float dist = Mathf.Sqrt((u - sx[i]) * (u - sx[i]) + (v - sy[i]) * (v - sy[i]));
                    alpha = Mathf.Max(alpha, Mathf.Clamp01((sr[i] - dist) / 0.03f));
                }

                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }
}