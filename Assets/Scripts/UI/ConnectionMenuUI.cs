using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ConnectionMenuUI : MonoBehaviour
{
    [SerializeField] private int maxConnections = 3;
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private TMP_InputField joinCodeInputField;
    [SerializeField] private TMP_Text statusText;
    [Tooltip("Opcional: panel con los botones y el campo de codigo. Se oculta durante la partida; el Status Text tiene que quedar afuera para que se siga viendo el codigo de sala.")]
    [SerializeField] private GameObject menuPanel;

    private void Awake()
    {
        if (!ValidateReferences())
        {
            enabled = false;
            return;
        }

        hostButton.onClick.AddListener(OnHostClicked);
        joinButton.onClick.AddListener(OnJoinClicked);
        DisconnectionHandler.SessionEnded += OnSessionEnded;
    }

    private void OnDestroy()
    {
        DisconnectionHandler.SessionEnded -= OnSessionEnded;
    }

    private void OnSessionEnded()
    {
        statusText.text = "Desconectado.";
        SetInteractable(true);
        SetMenuVisible(true);
    }

    private bool ValidateReferences()
    {
        bool ok = true;

        if (hostButton == null) { Debug.LogError("ConnectionMenuUI: falta asignar 'Host Button' en el Inspector.", this); ok = false; }
        if (joinButton == null) { Debug.LogError("ConnectionMenuUI: falta asignar 'Join Button' en el Inspector.", this); ok = false; }
        if (joinCodeInputField == null) { Debug.LogError("ConnectionMenuUI: falta asignar 'Join Code Input Field' en el Inspector.", this); ok = false; }
        if (statusText == null) { Debug.LogError("ConnectionMenuUI: falta asignar 'Status Text' en el Inspector.", this); ok = false; }

        return ok;
    }

    private async void OnHostClicked()
    {
        SetInteractable(false);
        statusText.text = "Creando sala...";

        try
        {
            string joinCode = await RelayConnectionManager.StartHostAsync(maxConnections);
            statusText.text = $"Sala creada. Código: {joinCode}";
            Debug.Log($"ConnectionMenuUI: código de sala -> {joinCode}");
            SetMenuVisible(false);
        }
        catch (Exception e)
        {
            statusText.text = "No se pudo crear la sala.";
            Debug.LogException(e);
            SetInteractable(true);
        }
    }

    private async void OnJoinClicked()
    {
        string joinCode = joinCodeInputField.text.Trim();
        if (string.IsNullOrEmpty(joinCode))
        {
            statusText.text = "Ingresá un código de sala.";
            return;
        }

        SetInteractable(false);
        statusText.text = "Conectando...";

        try
        {
            await RelayConnectionManager.StartClientAsync(joinCode);
            statusText.text = "Conectado.";
            SetMenuVisible(false);
        }
        catch (Exception e)
        {
            statusText.text = "No se pudo conectar. Verificá el código.";
            Debug.LogException(e);
            SetInteractable(true);
        }
    }

    private void SetInteractable(bool interactable)
    {
        hostButton.interactable = interactable;
        joinButton.interactable = interactable;
    }

    private void SetMenuVisible(bool visible)
    {
        if (menuPanel != null)
        {
            menuPanel.SetActive(visible);
        }
    }
}