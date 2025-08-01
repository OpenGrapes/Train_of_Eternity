using UnityEngine;
using UnityEngine.UI;

public class NPCInteractable : MonoBehaviour
{
    [Header("NPC Settings")]
    public string npcId = "TestGrandma"; // Name der CSV-Datei ohne .csv
    public string npcName = "Oma Gertrude"; // Anzeigename für UI
    
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
            Debug.LogWarning($"DialogManager für NPC '{npcName}' nicht gefunden!");
        }
        
        // Setup für UI-Button (Image auf Canvas)
        SetupUIButton();
        
        if (showDebugInfo)
        {
            Debug.Log($"NPC '{npcName}' (ID: {npcId}) bereit für UI-Interaktion");
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
                Debug.Log($"Button-Component zu Canvas-NPC '{npcName}' hinzugefügt");
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
            Debug.Log($"Canvas-NPC '{npcName}' wurde angeklickt!");
        }
        StartDialog();
    }
    
    // Manuelle Interaktion (falls vom Code ausgelöst)
    public void OnInteract()
    {
        if (showDebugInfo)
        {
            Debug.Log($"'{npcName}' OnInteract() manuell aufgerufen");
        }
        StartDialog();
    }
    
    private void StartDialog()
    {
        if (dialogManager != null)
        {
            if (showDebugInfo)
            {
                Debug.Log($"Starte Dialog mit {npcName} (NPC-ID: {npcId})");
            }
            
            // Zusätzliche Debug-Checks
            if (GameManager.Instance == null)
            {
                Debug.LogError("GameManager.Instance ist NULL!");
                return;
            }
            
            if (showDebugInfo)
            {
                Debug.Log($"GameManager gefunden, starte Dialog mit NPC...");
            }
            
            // Verwende StartDialogWithNPC statt StartDialogWithCSV
            dialogManager.StartDialogWithNPC(npcId);
        }
        else
        {
            Debug.LogError($"DialogManager nicht verfügbar für {npcName}!");
        }
    }
    
    // Für Button-Text oder Hover-Info
    public string GetNPCName()
    {
        return npcName;
    }
    
    // Prüfe ob dieser NPC Dialoge verfügbar hat
    public bool HasAvailableDialogs()
    {
        if (GameManager.Instance != null)
        {
            var dialogs = GameManager.Instance.GetAllDialogsFromCSV(npcId);
            return dialogs.Count > 0;
        }
        return false;
    }
}
