using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// Cuando el dueño muere, la cámara deja el cuerpo y sigue a un compañero vivo.
// Q cambia de objetivo. Muestra el tiempo restante para reaparecer.
public class SpectatorMode : MonoBehaviour
{
    private const float FollowDistance = 3.2f;
    private const float Shoulder = 0.45f;
    private const float EyeHeight = 1.6f;

    private PlayerHealth health;
    private readonly List<PlayerHealth> living = new List<PlayerHealth>();
    private int index;
    private bool following;
    private GameObject canvas;
    private TextMeshProUGUI label;
    private Camera[] weaponCameras;

    private void Awake()
    {
        health = GetComponentInParent<PlayerHealth>();
        weaponCameras = transform.root.GetComponentsInChildren<Camera>(true);
    }

    private void LateUpdate()
    {
        if (!OwnerIsDead())
        {
            HideLabel();
            return;
        }

        DisableWeaponCameras();

        if (MatchEnded())
        {
            HideLabel();
            return;
        }

        RefreshTargets();
        if (living.Count == 0)
        {
            ShowWaitingOnly();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }

        if (!PauseMenuUI.IsOpen && Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
            index = (index + 1) % living.Count;

        if (index >= living.Count)
            index = 0;

        Follow(living[index]);
        following = true;
        ShowLabel(living[index]);
    }

    private void OnDisable()
    {
        HideLabel();
    }

    private bool OwnerIsDead()
    {
        return health != null && health.IsSpawned && health.IsOwner && health.IsDead;
    }

    private static bool MatchEnded()
    {
        return GameOverUI.IsShown || (SafeZone.Current != null && SafeZone.Current.IsCompleted);
    }

    private void RefreshTargets()
    {
        ulong currentId = ulong.MaxValue;
        if (following && index >= 0 && index < living.Count && living[index] != null)
            currentId = living[index].OwnerClientId;

        living.Clear();
        foreach (PlayerHealth candidate in FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None))
        {
            if (candidate == null || candidate == health)
                continue;
            if (!candidate.IsSpawned || candidate.IsDead)
                continue;
            living.Add(candidate);
        }

        living.Sort((a, b) => a.OwnerClientId.CompareTo(b.OwnerClientId));

        index = 0;
        for (int i = 0; i < living.Count; i++)
        {
            if (living[i].OwnerClientId == currentId)
            {
                index = i;
                break;
            }
        }
    }

    private void Follow(PlayerHealth target)
    {
        Transform body = target.transform;
        Vector3 focus = body.position + Vector3.up * EyeHeight;
        Vector3 forward = body.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.forward;
        forward.Normalize();

        Vector3 desired = focus - forward * FollowDistance + body.right * Shoulder + Vector3.up * 0.35f;
        desired = PullInFrontOfWalls(focus, desired, body);
        transform.position = desired;

        Vector3 look = focus - desired;
        if (look.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(look);
    }

    private Vector3 PullInFrontOfWalls(Vector3 origin, Vector3 desired, Transform targetBody)
    {
        Vector3 delta = desired - origin;
        float distance = delta.magnitude;
        if (distance < 0.05f)
            return desired;

        Vector3 direction = delta / distance;
        RaycastHit[] hits = Physics.RaycastAll(origin, direction, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float closest = distance;
        Transform self = transform.root;
        foreach (RaycastHit hit in hits)
        {
            if (hit.transform == self || hit.transform.IsChildOf(self))
                continue;
            if (hit.transform == targetBody || hit.transform.IsChildOf(targetBody))
                continue;
            if (hit.distance < closest)
                closest = hit.distance;
        }

        if (closest >= distance)
            return desired;

        closest = Mathf.Max(0.2f, closest - 0.15f);
        return origin + direction * closest;
    }

    private void DisableWeaponCameras()
    {
        if (weaponCameras == null)
            return;

        foreach (Camera cam in weaponCameras)
        {
            if (cam != null && cam.gameObject.name == "WeaponCamera")
                cam.enabled = false;
        }
    }

    private void ShowLabel(PlayerHealth target)
    {
        if (label == null)
            BuildLabel();

        Character character = target.GetComponent<Character>();
        string name = character != null ? character.PlayerName : "Jugador";
        label.text = "Espectando a " + name + "\nQ para cambiar" + FormatRespawnCountdownSuffix();
        if (canvas != null)
            canvas.SetActive(true);
    }

    private void ShowWaitingOnly()
    {
        following = false;
        if (label == null)
            BuildLabel();

        string countdown = FormatRespawnCountdownSuffix();
        label.text = string.IsNullOrEmpty(countdown)
            ? "Sin compañeros vivos"
            : "Sin compañeros vivos" + countdown;
        if (canvas != null)
            canvas.SetActive(true);
    }

    // Solo en multi hay timer de revive; en solo no se muestra.
    private string FormatRespawnCountdownSuffix()
    {
        float remaining = health != null ? health.RespawnSecondsRemaining : 0f;
        if (remaining <= 0f)
            return string.Empty;

        int total = Mathf.CeilToInt(remaining);
        int minutes = total / 60;
        int seconds = total % 60;
        return "\nReaparición en " + minutes + ":" + seconds.ToString("00");
    }

    private void HideLabel()
    {
        following = false;
        if (canvas != null)
            canvas.SetActive(false);
    }

    private void BuildLabel()
    {
        canvas = new GameObject("SpectatorHUD");
        Canvas overlay = canvas.AddComponent<Canvas>();
        overlay.renderMode = RenderMode.ScreenSpaceOverlay;
        overlay.sortingOrder = 40;

        GameObject textObject = new GameObject("Label");
        textObject.transform.SetParent(canvas.transform, false);
        label = textObject.AddComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset;
        label.fontSize = 28f;
        label.alignment = TextAlignmentOptions.Bottom;
        label.color = new Color(0.93f, 0.94f, 0.92f, 1f);

        RectTransform rect = label.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 48f);
        rect.sizeDelta = new Vector2(900f, 120f);
    }
}
