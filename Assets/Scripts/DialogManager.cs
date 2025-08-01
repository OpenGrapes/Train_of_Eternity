using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

public class DialogManager : MonoBehaviour
{
    // Erweiterte Choice-Struktur für Warteschlangen-System
    [System.Serializable]
    public class ChoiceReference
    {
        public DialogLine sourceDialogLine;  // Ursprüngliche DialogLine
        public int choiceIndex;              // Index in der ursprünglichen DialogLine
        public DialogLine.ChoiceData choice; // Die tatsächliche Choice
        
        public ChoiceReference(DialogLine dialogLine, int index, DialogLine.ChoiceData choiceData)
        {
            sourceDialogLine = dialogLine;
            choiceIndex = index;
            choice = choiceData;
        }
    }

    [Header("UI References")]
    public GameObject dialogPanel;
    public TextMeshProUGUI dialogText;
    public TextMeshProUGUI sprecherName; // UI-Element für Sprecher-Name oben rechts
    public Button[] choiceButtons; // Array für Antwortbuttons (direkt im DialogPanel)

    private List<DialogLine> currentDialog;
    private int dialogIndex = 0;
    private System.Action onDialogEnd;
    private string currentSpeakerName; // Aktueller Sprecher für UI-Anzeige
    private bool waitingForPlayerClick = false; // Warten auf Spieler-Klick
    private bool showingChoiceAnswer = false; // Zeigt gerade Choice-Antwort an

    private void Start()
    {
        // Dialog-Panel initial deaktivieren
        if (dialogPanel != null)
        {
            dialogPanel.SetActive(false);
            SetupDialogPanelButton();
        }
        
        // Choice-Buttons mit Events ausstatten
        SetupChoiceButtons();
    }
    
    private void SetupChoiceButtons()
    {
        if (choiceButtons == null) return;
        
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            var button = choiceButtons[i];
            if (button != null)
            {
                button.gameObject.SetActive(false);
            }
        }
    }
    
    private void SetupDialogPanelButton()
    {
        // DialogPanel klickbar machen für Weiterklicken
    }

    // Dialog starten mit NPC-ID
    public void StartDialogWithNPC(string npcId, System.Action onEnd = null)
    {
        if (GameManager.Instance != null)
        {
            var dialogs = GameManager.Instance.GetDialogsForNPC(npcId);
            var npcName = GameManager.Instance.GetNPCName(npcId);
            
            if (dialogPanel == null)
            {
                Debug.LogError("DialogPanel ist NULL! Bitte im Inspector zuweisen.");
                return;
            }
            
            Debug.Log("Starte Dialog...");
            StartDialog(dialogs, npcName, onEnd);
        }
        else
        {
            Debug.LogError("GameManager nicht verfügbar!");
        }
    }

    // Dialog starten mit CSV-Dateiname
    public void StartDialogWithCSV(string csvFileName, System.Action onEnd = null)
    {
        if (GameManager.Instance != null)
        {
            var dialogs = GameManager.Instance.GetAllDialogsFromCSV(csvFileName);
            
            if (dialogPanel == null)
            {
                Debug.LogError("DialogPanel ist NULL! Bitte im Inspector zuweisen.");
                return;
            }
            
            Debug.Log("Starte Dialog...");
            StartDialog(dialogs, "Unbekannt", onEnd);
        }
        else
        {
            Debug.LogError("GameManager nicht verfügbar!");
        }
    }

    // Hauptmethode für Dialog-Start mit Sprecher-Name
    public void StartDialog(List<DialogLine> dialog, string speakerName, System.Action onEnd = null)
    {
        Debug.Log($"StartDialog aufgerufen mit {dialog.Count} Dialogen für {speakerName}");
        
        if (dialog == null || dialog.Count == 0) 
        {
            Debug.LogWarning("Keine Dialoge verfügbar!");
            return;
        }

        if (dialogPanel == null)
        {
            Debug.LogError("DialogPanel ist NULL!");
            return;
        }

        Debug.Log("Aktiviere DialogPanel...");
        dialogPanel.SetActive(true);
        
        HideAllChoiceButtons();
        currentDialog = dialog;
        currentSpeakerName = speakerName;
        dialogIndex = 0;
        onDialogEnd = onEnd;
        
        // Reset states
        waitingForPlayerClick = false;
        showingChoiceAnswer = false;
        
        // Zeige sofort die Warteschlange mit verfügbaren Choices
        UpdateSpeakerName("???");
        RefreshAndShowAvailableChoices();
    }

    // Legacy-Support
    public void StartDialog(List<DialogLine> dialog, System.Action onEnd = null)
    {
        StartDialog(dialog, "Unbekannt", onEnd);
    }

    private void Update()
    {
        // Auf jeden Mausklick reagieren, wenn Dialog aktiv ist UND keine Choice-Buttons angezeigt werden
        if (dialogPanel.activeInHierarchy && Input.GetMouseButtonDown(0) && !AnyChoiceButtonsActive())
        {
            OnAnyMouseClick();
        }
    }
    
    public void OnAnyMouseClick()
    {
        if (waitingForPlayerClick)
        {
            waitingForPlayerClick = false;
            
            if (showingChoiceAnswer)
            {
                // Nach Choice-Antwort - zurück zur Warteschlange
                showingChoiceAnswer = false;
                UpdateSpeakerName("???");
                RefreshAndShowAvailableChoices();
                return;
            }
        }
    }

    // WARTESCHLANGEN-SYSTEM: Sammle alle verfügbaren Choices aus allen Dialog-IDs
    private void RefreshAndShowAvailableChoices()
    {
        var waitingQueue = GetAllAvailableChoicesWithWaitingQueue();
        
        if (waitingQueue.Count > 0)
        {
            Debug.Log($"Zeige {waitingQueue.Count} Choices aus Warteschlange");
            UpdateSpeakerName("???");
            ShowChoicesFromWaitingQueue(waitingQueue);
        }
        else
        {
            // Wirklich keine Choices mehr verfügbar - Dialog kann beendet werden
            Debug.Log("Keine verfügbaren Choices in der Warteschlange gefunden");
            ShowChoicesOrEnd();
        }
    }
    
    // WARTESCHLANGEN-LOGIK: Sammle alle verfügbaren Choices aus allen Dialog-IDs
    private List<ChoiceReference> GetAllAvailableChoicesWithWaitingQueue()
    {
        var waitingQueue = new List<ChoiceReference>();
        
        if (currentDialog == null) return waitingQueue;
        
        Debug.Log("=== PRÜFE ALLE DIALOG-IDs ===");
        
        // Durchsuche ALLE Dialog-IDs nach verfügbaren Choices
        for (int dialogIdx = 0; dialogIdx < currentDialog.Count; dialogIdx++)
        {
            var dialogLine = currentDialog[dialogIdx];
            
            Debug.Log($"Prüfe Dialog-ID '{dialogLine.memoryId}':");
            
            // Prüfe Dialog-Level Requirements
            bool dialogAvailable = IsDialogLineAvailable(dialogLine);
            Debug.Log($"  Dialog-Requirements erfüllt: {dialogAvailable}");
            
            if (!dialogAvailable) continue;
            
            // Sammle verfügbare Choices aus dieser Dialog-ID
            if (dialogLine.choices != null && dialogLine.choices.Count > 0)
            {
                for (int choiceIdx = 0; choiceIdx < dialogLine.choices.Count; choiceIdx++)
                {
                    var choice = dialogLine.choices[choiceIdx];
                    
                    // Prüfe Choice-Level Requirements und ob bereits gewählt
                    bool choiceAvailable = IsChoiceAvailable(choice);
                    bool notChosen = !choice.wasChosen;
                    
                    Debug.Log($"    Choice {choiceIdx}: '{choice.choiceText}' - Requirements: {choiceAvailable}, Not Chosen: {notChosen}");
                    
                    if (choiceAvailable && notChosen)
                    {
                        var choiceRef = new ChoiceReference(dialogLine, choiceIdx, choice);
                        waitingQueue.Add(choiceRef);
                        Debug.Log($"      -> In Warteschlange hinzugefügt");
                    }
                }
            }
        }
        
        // SORTIERUNG: Dialog-Index, dann Choice-Index für stabile Reihenfolge
        waitingQueue.Sort((a, b) => {
            int dialogComparison = currentDialog.IndexOf(a.sourceDialogLine).CompareTo(
                                  currentDialog.IndexOf(b.sourceDialogLine));
            if (dialogComparison != 0) return dialogComparison;
            return a.choiceIndex.CompareTo(b.choiceIndex);
        });
        
        Debug.Log($"=== WARTESCHLANGE: {waitingQueue.Count} Choices verfügbar ===");
        for (int i = 0; i < waitingQueue.Count; i++)
        {
            var choiceRef = waitingQueue[i];
            Debug.Log($"  Position {i}: '{choiceRef.choice.choiceText}' aus Dialog-ID '{choiceRef.sourceDialogLine.memoryId}'");
        }
        
        return waitingQueue;
    }
    
    // Zeigt Choices aus der Warteschlange an (max. 3 gleichzeitig)
    private void ShowChoicesFromWaitingQueue(List<ChoiceReference> waitingQueue)
    {
        ShowChoicesOnly();
        
        // Beschränke auf verfügbare Button-Slots
        int maxChoices = Mathf.Min(waitingQueue.Count, choiceButtons.Length);
        
        Debug.Log($"ShowChoicesFromWaitingQueue: Zeige {maxChoices} von {waitingQueue.Count} Choices");
        
        // Aktiviere benötigte Buttons
        for (int i = 0; i < maxChoices; i++)
        {
            var choiceRef = waitingQueue[i];
            var choice = choiceRef.choice;
            var button = choiceButtons[i];
            if (button == null) continue;
            
            button.gameObject.SetActive(true);
            button.interactable = true;
            
            var buttonText = button.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = choice.choiceText;
                buttonText.color = Color.white;
            }
            
            // OnClick Event setzen - WICHTIG: Verwende lokale Closure für choiceRef
            button.onClick.RemoveAllListeners();
            var choiceToSelect = choiceRef; // Closure Variable
            button.onClick.AddListener(() => SelectChoiceFromWaitingQueue(choiceToSelect));
            
            Debug.Log($"  Button {i}: '{choice.choiceText}' aus Dialog-ID '{choiceRef.sourceDialogLine.memoryId}'");
        }
        
        // Verstecke übrige Buttons
        for (int i = maxChoices; i < choiceButtons.Length; i++)
        {
            choiceButtons[i]?.gameObject.SetActive(false);
        }
        
        // Zeige Warteschlangen-Info
        if (waitingQueue.Count > maxChoices)
        {
            Debug.Log($"WARTESCHLANGE: {waitingQueue.Count - maxChoices} weitere Choices warten");
        }
    }
    
    // Wählt eine Choice aus der Warteschlange (behält statische CSV-Logik)
    private void SelectChoiceFromWaitingQueue(ChoiceReference choiceRef)
    {
        if (choiceRef == null || choiceRef.choice == null || choiceRef.choice.wasChosen) 
        {
            Debug.LogWarning("ChoiceReference ist null oder bereits gewählt!");
            return;
        }
        
        var choice = choiceRef.choice;
        var sourceDialog = choiceRef.sourceDialogLine;
        
        Debug.Log($"SelectChoiceFromWaitingQueue: '{choice.choiceText}' aus Dialog-ID '{sourceDialog.memoryId}' (Index {choiceRef.choiceIndex})");
        
        // Choice als gewählt markieren - WICHTIG: In der ursprünglichen DialogLine!
        choice.wasChosen = true;
        
        // Memory-Flags setzen - aus der ursprünglichen CSV-Struktur
        if (choice.addMemory != null && choice.addMemory.Count > 0)
        {
            foreach (string memory in choice.addMemory)
            {
                if (GameManager.Instance != null && !string.IsNullOrEmpty(memory))
                {
                    GameManager.Instance.AddMemory(memory);
                    Debug.Log($"Memory gesetzt: {memory} (von Choice {choiceRef.choiceIndex} aus Dialog-ID '{sourceDialog.memoryId}')");
                }
            }
        }
        
        // Choice-Antwort anzeigen - aus der ursprünglichen CSV-Struktur
        if (!string.IsNullOrEmpty(choice.answerText))
        {
            UpdateSpeakerName(currentSpeakerName);
            ShowTextOnly(choice.answerText);
            
            showingChoiceAnswer = true;
            waitingForPlayerClick = true;
        }
        else
        {
            // Keine Antwort-Text - sofort Warteschlange neu laden
            UpdateSpeakerName("???");
            RefreshAndShowAvailableChoices();
        }
    }

    // Öffentliche Methoden für Unity Inspector OnClick-Events
    public void OnChoice0Selected() { SelectChoiceByIndex(0); }
    public void OnChoice1Selected() { SelectChoiceByIndex(1); }
    public void OnChoice2Selected() { SelectChoiceByIndex(2); }
    
    public void SelectChoiceByIndex(int index)
    {
        // WARTESCHLANGEN-LOGIK: Hole alle verfügbaren Choices aus der Warteschlange
        var waitingQueue = GetAllAvailableChoicesWithWaitingQueue();
        
        Debug.Log($"SelectChoiceByIndex({index}): {waitingQueue.Count} verfügbare Choices in Warteschlange");
        
        if (index >= 0 && index < waitingQueue.Count)
        {
            var selectedChoiceRef = waitingQueue[index];
            Debug.Log($"Gewählte Choice: '{selectedChoiceRef.choice.choiceText}' aus Dialog-ID '{selectedChoiceRef.sourceDialogLine.memoryId}' (Index {selectedChoiceRef.choiceIndex})");
            SelectChoiceFromWaitingQueue(selectedChoiceRef);
        }
        else
        {
            Debug.LogWarning($"Choice Index {index} ist ungültig. Verfügbare Choices: {waitingQueue.Count}");
        }
    }

    // Hilfsmethoden
    private bool IsDialogLineAvailable(DialogLine line)
    {
        if (line.requiredMemory == null || line.requiredMemory.Count == 0) 
            return true;
        
        foreach (var requirement in line.requiredMemory)
        {
            if (GameManager.Instance == null || !GameManager.Instance.HasMemory(requirement))
            {
                return false;
            }
        }
        return true;
    }

    private bool IsChoiceAvailable(DialogLine.ChoiceData choice)
    {
        if (choice.requiredMemory == null || choice.requiredMemory.Count == 0) 
            return true;
        
        foreach (var requirement in choice.requiredMemory)
        {
            if (GameManager.Instance == null || !GameManager.Instance.HasMemory(requirement))
            {
                return false;
            }
        }
        return true;
    }

    private void ShowChoicesOrEnd()
    {
        Debug.Log("ShowChoicesOrEnd: Keine Choices mehr verfügbar - Dialog beenden");
        
        if (dialogPanel != null)
        {
            dialogPanel.SetActive(false);
        }
        
        onDialogEnd?.Invoke();
    }

    // Öffentliche Methode um Dialog von außen zu beenden
    public void ForceEndDialog()
    {
        ShowChoicesOrEnd();
    }

    // UI-Hilfsmethoden
    private void UpdateSpeakerName(string name)
    {
        if (sprecherName != null)
        {
            sprecherName.text = name;
        }
    }

    private void HideAllChoiceButtons()
    {
        if (choiceButtons == null) return;
        
        foreach (var button in choiceButtons)
        {
            button?.gameObject.SetActive(false);
        }
    }

    private bool AnyChoiceButtonsActive()
    {
        if (choiceButtons == null) return false;
        
        return System.Array.Exists(choiceButtons, button => 
            button != null && button.gameObject.activeInHierarchy);
    }

    private void ShowTextOnly(string text)
    {
        if (dialogText != null)
        {
            dialogText.text = text;
        }
        HideAllChoiceButtons();
    }

    private void ShowChoicesOnly()
    {
        if (dialogText != null)
        {
            dialogText.text = "";
        }
    }

    // Debug-Methode
    public void DebugCurrentState()
    {
        Debug.Log("=== DIALOG MANAGER DEBUG ===");
        Debug.Log($"GameObject: {gameObject.name}");
        Debug.Log($"DialogPanel aktiv: {dialogPanel?.activeInHierarchy ?? false}");
        Debug.Log($"WaitingForPlayerClick: {waitingForPlayerClick}");
        Debug.Log($"ShowingChoiceAnswer: {showingChoiceAnswer}");
        
        var waitingQueue = GetAllAvailableChoicesWithWaitingQueue();
        Debug.Log($"Warteschlange: {waitingQueue.Count} verfügbare Choices");
        
        for (int i = 0; i < waitingQueue.Count; i++)
        {
            var choiceRef = waitingQueue[i];
            Debug.Log($"  Choice {i}: '{choiceRef.choice.choiceText}' aus Dialog-ID '{choiceRef.sourceDialogLine.memoryId}'");
        }
        
        if (choiceButtons != null)
        {
            int activeButtons = 0;
            foreach (var btn in choiceButtons)
            {
                if (btn?.gameObject.activeInHierarchy == true) activeButtons++;
            }
            Debug.Log($"Choice Buttons aktiv: {activeButtons}/{choiceButtons.Length}");
        }
    }
}
