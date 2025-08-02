using UnityEngine;
using UnityEngine.UI;

public class ItemInteractable : MonoBehaviour
{
    [Header("Item Settings")]
    public string itemId = "mirrow_broken"; // Item-ID aus dem GameManager (= memoryId aus CSV)
    // itemName entfernt - Items brauchen keinen Namen als Sprecher
    
    [Header("References")]
    public DialogManager dialogManager; // Referenz zum DialogManager
    
    [Header("Debug")]
    public bool showDebugInfo = true; // Debug-Ausgaben anzeigen
    
    private void Start()
    {
        // Automatisch DialogManager finden, falls nicht zugewiesen
        if (dialogManager == null)
        {
            dialogManager = FindFirstObjectByType<DialogManager>();
        }
        
        if (dialogManager == null)
        {
            Debug.LogWarning($"DialogManager für Item-ID '{itemId}' nicht gefunden!");
        }
        
        // Setup für UI-Button (Image auf Canvas)
        SetupUIButton();
        
        if (showDebugInfo)
        {
            Debug.Log($"Item-ID '{itemId}' bereit für UI-Interaktion");
        }
    }
    
    private void SetupUIButton()
    {
        // Button-Component hinzufügen für Canvas UI-Interaktion
        Button button = GetComponent<Button>();
        if (button == null)
        {
            button = gameObject.AddComponent<Button>();
            if (showDebugInfo)
            {
                Debug.Log($"Button-Component zu Item-ID '{itemId}' hinzugefügt");
            }
        }
        
        // OnClick-Event für UI-Interaktion einrichten
        button.onClick.RemoveAllListeners(); // Alte Events entfernen
        button.onClick.AddListener(OnButtonClick);
        
        // Raycast Target aktivieren (essentiell für Canvas UI-Clicks)
        Image image = GetComponent<Image>();
        if (image != null)
        {
            image.raycastTarget = true;
        }
    }
    
    // Canvas UI-Button Click Handler
    public void OnButtonClick()
    {
        if (showDebugInfo)
        {
            Debug.Log($"Item-ID '{itemId}' wurde angeklickt!");
        }
        StartItemDialog();
    }
    
    // Manuelle Interaktion (falls vom Code ausgelöst)
    public void OnInteract()
    {
        if (showDebugInfo)
        {
            Debug.Log($"Item-ID '{itemId}' OnInteract() manuell aufgerufen");
        }
        StartItemDialog();
    }
    
    private void StartItemDialog()
    {
        if (dialogManager != null)
        {
            if (showDebugInfo)
            {
                Debug.Log($"Starte Item-Dialog mit ID: {itemId}");
            }
            
            // Zusätzliche Debug-Checks
            if (GameManager.Instance == null)
            {
                Debug.LogError("GameManager.Instance ist NULL!");
                return;
            }
            
            if (showDebugInfo)
            {
                Debug.Log($"GameManager gefunden, starte Dialog mit Item...");
            }
            
            // Verwende StartDialogWithItem (neue Methode im DialogManager)
            dialogManager.StartDialogWithItem(itemId); 
        }
        else
        {
            Debug.LogError($"DialogManager nicht verfügbar für Item-ID: {itemId}!");
        }
    }
    
    // Prüfe ob dieses Item verfügbare Dialoge hat
    public bool HasAvailableDialogs()
    {
        if (GameManager.Instance != null)
        {
            var dialogs = GameManager.Instance.GetDialogsForItem(itemId);
            return dialogs.Count > 0;
        }
        return false;
    }
}
