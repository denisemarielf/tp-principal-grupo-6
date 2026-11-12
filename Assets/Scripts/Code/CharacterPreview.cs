using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class CharacterPreview : MonoBehaviour
{
    [Header("Prefabs de preview (mismo orden que el menu y que 'models' del binder)")]
    [SerializeField] private GameObject[] previewPrefabs;

    [Header("Escenario (se crea por codigo, lejos del menu)")]
    [SerializeField] private string previewLayer = "CharacterPreview";
    [SerializeField] private Vector3 stagePosition = new Vector3(0f, -500f, 0f);
    [SerializeField] private Vector2Int textureSize = new Vector2Int(512, 768);
    [SerializeField] private float fieldOfView = 30f;
    [SerializeField] private float cameraDistance = 3.5f;
    [SerializeField] private float lookHeight = 0.95f;
    [SerializeField] private float lightIntensity = 3f;
    [SerializeField] private float rotateSpeed = 25f;

    private Transform stage, anchor;
    private Camera cam;
    private RenderTexture rt;
    private GameObject[] instances;
  
    [SerializeField] private float frameMargin = 1.15f;
    private readonly List<RawImage> displays = new List<RawImage>();

    // compatible con tu codigo actual del panel Crear (characterPreview.Display = raw)
    public RawImage Display { set => AddDisplay(value); }

    public void AddDisplay(RawImage raw)
    {
        if (raw == null || displays.Contains(raw)) return;
        displays.Add(raw);
        if (rt != null) raw.texture = rt;
    }



    private void Awake()
    {
        int layer = LayerMask.NameToLayer(previewLayer);
        if (layer < 0)
        {
            Debug.LogError($"[Preview] Falta la layer '{previewLayer}' (Project Settings > Tags and Layers).", this);
            enabled = false;
            return;
        }

        stage = new GameObject("CharacterPreviewStage").transform;
        stage.position = stagePosition;

        anchor = new GameObject("Anchor").transform;
        anchor.SetParent(stage, false);
        
        // RenderTexture con alfa: el fondo queda transparente
        rt = new RenderTexture(textureSize.x, textureSize.y, 24, RenderTextureFormat.ARGB32);
        foreach (var d in displays) if (d != null) d.texture = rt;

        var camGO = new GameObject("PreviewCamera");
        camGO.transform.SetParent(stage, false);
        camGO.transform.localPosition = new Vector3(0f, lookHeight, cameraDistance);
        camGO.transform.LookAt(stage.position + Vector3.up * lookHeight);
        cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        cam.cullingMask = 1 << layer;
        cam.fieldOfView = fieldOfView;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 20f;
        cam.targetTexture = rt;
        cam.enabled = false;

        // Luz local (Point): su rango corto no afecta al resto del menu
        var lightGO = new GameObject("PreviewLight");
        lightGO.transform.SetParent(stage, false);
        lightGO.transform.localPosition = new Vector3(1.5f, 2.5f, 2.5f);
        var l = lightGO.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = 10f;
        l.intensity = lightIntensity;
        l.shadows = LightShadows.None;

        // la camara del menu no debe dibujar el escenario del preview
        if (Camera.main != null) Camera.main.cullingMask &= ~(1 << layer);

        instances = new GameObject[previewPrefabs.Length];
        for (int i = 0; i < previewPrefabs.Length; i++)
        {
            if (previewPrefabs[i] == null) continue;
            instances[i] = Instantiate(previewPrefabs[i], anchor);
            instances[i].transform.localPosition = Vector3.zero;
            instances[i].transform.localRotation = Quaternion.identity;
            SetLayerRecursively(instances[i], layer);
            foreach (var a in instances[i].GetComponentsInChildren<Animator>(true))
                a.updateMode = AnimatorUpdateMode.UnscaledTime;
            instances[i].SetActive(false);
        }
    }
    private void Frame(GameObject model)
    {
        bool has = false;
        Bounds b = new Bounds();

        // solo renderers activos: los de objetos inactivos devuelven bounds en el origen del mundo
        foreach (var r in model.GetComponentsInChildren<Renderer>(false))
        {
            if (!r.enabled || !(r is SkinnedMeshRenderer || r is MeshRenderer)) continue;
            if (!has) { b = r.bounds; has = true; }
            else b.Encapsulate(r.bounds);
        }
        if (!has || b.size.y < 0.1f) return;

        float centerY = b.center.y - stage.position.y;
        float dist = (b.size.y * 0.5f * frameMargin) / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);

        cam.transform.localPosition = new Vector3(0f, centerY, dist);
        cam.transform.LookAt(stage.position + Vector3.up * centerY);
        cam.farClipPlane = dist + b.size.y * 2f;   // el modelo nunca queda fuera del plano lejano

        Debug.Log($"[Preview] {model.name}: alto={b.size.y:F2} m, distancia={dist:F2} m");
    }


    private void Update()
    {
        if (cam == null) return;

        bool visible = false;
        foreach (var d in displays)
            if (d != null && d.gameObject.activeInHierarchy) { visible = true; break; }

        if (cam.enabled != visible) cam.enabled = visible;
        if (visible) anchor.Rotate(0f, rotateSpeed * Time.unscaledDeltaTime, 0f);
    }

    public void Show(int index)
    {
        if (instances == null) return;
        anchor.localRotation = Quaternion.identity;   // arranca de frente

        for (int i = 0; i < instances.Length; i++)
        {
            if (instances[i] == null) continue;
            bool on = i == index;
            instances[i].SetActive(on);
            if (on) Frame(instances[i]);   // tras activarlo, para que los bounds sean validos
        }
    }

    private void OnDestroy()
    {
        if (rt != null) { rt.Release(); Destroy(rt); }
        if (stage != null) Destroy(stage.gameObject);
    }

    private static void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursively(child.gameObject, layer);
    }
}