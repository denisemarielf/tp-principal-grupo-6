using UnityEngine;
using UnityEngine.Rendering.Universal;

// Activa el post-procesado en las camaras de esta escena.
// El jugador es un prefab compartido con otras escenas y se crea al iniciar la partida, asi que no se le puede
// dejar el post-procesado prendido en el prefab sin afectar a esos mapas. Esto lo prende solo aca.
public class ScenePostProcessing : MonoBehaviour
{
    [SerializeField] private float checkInterval = 0.5f;

    private float nextCheck;

    private void Update()
    {
        if (Time.time < nextCheck) return;
        nextCheck = Time.time + checkInterval;

        foreach (Camera cam in Camera.allCameras)
        {
            UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
            // Las camaras overlay (el arma) se dibujan sobre la camara base, que es la que aplica los efectos.
            if (data != null && data.renderType == CameraRenderType.Base && !data.renderPostProcessing)
                data.renderPostProcessing = true;
        }
    }
}
