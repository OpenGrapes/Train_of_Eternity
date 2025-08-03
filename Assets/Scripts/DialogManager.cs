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
    private string currentSpeakerName; // Aktueller Sprecher für UI-Anzeige (Spieler)
    private string currentNPCName; // Name des aktuellen NPCs für Choice-Antworten
    private bool waitingForPlayerClick = false; // Warten auf Spieler-Klick
    private bool showingChoiceAnswer = false; // Zeigt gerade Choice-Antwort an

    // Neue Variable für Dialog-Status-Tracking
    // ENTFERNT: completedStartDialogs Variable
    // private HashSet<string> completedStartDialogs = new HashSet<string>();

    private void Start()
    {
        // Dialog-Panel initial deaktivieren
        if (dialogPanel != null)
        {
            dialogPanel.SetActive(false);
            // ENTFERNT: SetupDialogPanelButton(); - Diese Methode existiert nicht
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
                // Setup für umgekehrtes Hover-Verhalten
                SetupInvertedHoverColors(button);
            }
        }
    }
    
    // Neue Methode: Setup für transparente Buttons die beim Hover sichtbarer werden - NOCH SUBTILER
    private void SetupInvertedHoverColors(Button button)
    {
        if (button == null) return;
        
        // Hole die aktuellen Button-Colors
        var colors = button.colors;
        
        // Normal: Komplett transparent (unsichtbar)
        colors.normalColor = new Color(240f/255f, 240f/255f, 240f/255f, 0f); // Alpha 0 = komplett transparent
        
        // Hover: Noch subtiler - kaum wahrnehmbar
        colors.highlightedColor = new Color(240f/255f, 240f/255f, 240f/255f, 0.05f); // Alpha 0.05 = extrem dezent
        
        // Pressed: Etwas mehr beim Klick, aber immer noch sehr subtil
        colors.pressedColor = new Color(240f/255f, 240f/255f, 240f/255f, 0.1f); // Alpha 0.1 = subtil sichtbar
        
        // Selected: Falls Button ausgewählt bleibt
        colors.selectedColor = new Color(240f/255f, 240f/255f, 240f/255f, 0.08f); // Alpha 0.08 = sehr leicht sichtbar
        
        // Disabled: Komplett transparent wenn deaktiviert
        colors.disabledColor = new Color(240f/255f, 240f/255f, 240f/255f, 0f); // Alpha 0 = unsichtbar
        
        // Smooth Transition - etwas langsamer für noch subtileren Effekt
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.3f; // Noch langsamere Transition für sehr subtilen Effekt
        
        // Colors auf Button anwenden
        button.colors = colors;
        
        // Sicherstellen, dass Button korrekt konfiguriert ist
        button.transition = Selectable.Transition.ColorTint;
        var image = button.GetComponent<Image>();
        if (image != null)
        {
            button.targetGraphic = image;
            image.raycastTarget = true; // Wichtig für Hover-Detection
            
            // Image-Color auf die Basis-Farbe setzen (subtil)
            image.color = new Color(240f/255f, 240f/255f, 240f/255f, 1f); // Hellgrau als Basis
        }
        
        Debug.Log($"Button '{button.name}' setup: Normal=transparent, Hover=extrem subtil (Alpha 0.05)");
    }
    
    // Dialog starten mit NPC-ID
    // Dialog starten mit NPC-ID - VEREINFACHT
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
            
            Debug.Log($"Starte Dialog mit {npcName}");
            StartDialog(dialogs, npcName, onEnd);
        }
        else
        {
            Debug.LogError("GameManager nicht verfügbar!");
        }
    }

    // Dialog starten mit Item-ID
    public void StartDialogWithItem(string itemId, System.Action onEnd = null)
    {
        if (GameManager.Instance != null)
        {
            var dialogs = GameManager.Instance.GetDialogsForItem(itemId);
            // itemName entfernt - Items brauchen keinen Namen als Sprecher
            
            if (dialogPanel == null)
            {
                Debug.LogError("DialogPanel ist NULL! Bitte im Inspector zuweisen.");
                return;
            }
            
            Debug.Log($"Starte Dialog mit Item-ID: {itemId}");
            StartDialog(dialogs, itemId, onEnd); // itemId als Sprecher-Name verwenden
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

    // Hauptmethode für Dialog-Start mit Sprecher-Name - KORRIGIERT
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
        currentNPCName = speakerName; // NPC-Name für Choice-Antworten speichern
        
        // Erste Dialog-Zeile ist immer ein Gedanke des Spielers - Name vom GameManager holen
        if (dialog.Count > 0)
        {
            currentSpeakerName = GameManager.SafeGetPlayerName(); // Spieler-Name aus GameManager
        }
        else
        {
            currentSpeakerName = speakerName;
        }
        
        dialogIndex = 0;
        onDialogEnd = onEnd;
        
        // Reset states
        waitingForPlayerClick = false;
        showingChoiceAnswer = false;
        
        // WICHTIG: Zuerst normalen Dialog-Text anzeigen, dann Choices
        ShowNextDialogLine();
    }
    
    // Neue Methode: Zeigt den nächsten Dialog-Text an - VEREINFACHT
    private void ShowNextDialogLine()
    {
        if (currentDialog == null || dialogIndex >= currentDialog.Count)
        {
            // Keine normalen Dialog-Zeilen mehr - zeige Choices
            Debug.Log("Keine Dialog-Zeilen mehr - zeige Choices aus Warteschlange");
            UpdateSpeakerName("???");
            RefreshAndShowAvailableChoices();
            return;
        }
        
        // VEREINFACHT: Finde den neuesten verfügbaren Start-Dialog
        var newestStartDialog = FindNewestAvailableStartDialog();
        
        if (newestStartDialog != null)
        {
            // Zeige nur den neuesten Start-Dialog an
            Debug.Log($"Zeige neuesten Start-Dialog: '{newestStartDialog.text}' von {currentSpeakerName}");
            UpdateSpeakerName(currentSpeakerName);
            ShowTextOnly(newestStartDialog.text);
            
            // Verarbeite addMemory vom Start-Dialog (nach Anzeige des Textes)
            GameManager.SafeProcessDialogMemory(newestStartDialog);
            
            waitingForPlayerClick = true;
            
            // WICHTIG: Überspringe alle anderen Start-Dialoge und gehe direkt zu Choices
            dialogIndex = currentDialog.Count; // Setze Index ans Ende
            return;
        }
        
        // Fallback: Normaler Dialog-Ablauf (falls kein Start-Dialog gefunden)
        var currentLine = currentDialog[dialogIndex];
        
        // Prüfe ob diese Dialog-Zeile verfügbar ist
        if (!IsDialogLineAvailable(currentLine))
        {
            Debug.Log($"Dialog-Zeile {dialogIndex} ('{currentLine.memoryId}') nicht verfügbar - überspringe");
            dialogIndex++;
            ShowNextDialogLine(); // Rekursiv nächste Zeile prüfen
            return;
        }
        
        // Zeige Dialog-Text an (falls vorhanden)
        if (!string.IsNullOrEmpty(currentLine.text))
        {
            Debug.Log($"Zeige Dialog-Text: '{currentLine.text}' von {currentSpeakerName}");
            UpdateSpeakerName(currentSpeakerName);
            ShowTextOnly(currentLine.text);
            
            // Verarbeite addMemory vom Dialog (nach Anzeige des Textes)
            GameManager.SafeProcessDialogMemory(currentLine);
            
            waitingForPlayerClick = true;
            dialogIndex++;
        }
        else
        {
            // Keine Text in dieser Zeile - nächste Zeile
            Debug.Log($"Dialog-Zeile {dialogIndex} hat keinen Text - überspringe");
            dialogIndex++;
            ShowNextDialogLine();
        }
    }

    // VEREINFACHTE METHODE: Findet den neuesten verfügbaren Start-Dialog
    private DialogLine FindNewestAvailableStartDialog()
    {
        if (currentDialog == null) return null;
        
        DialogLine newestDialog = null;
        int mostRequirements = -1;
        
        // Durchlaufe alle Dialog-Zeilen und finde verfügbare Start-Dialoge
        foreach (var dialogLine in currentDialog)
        {
            // Prüfe ob es ein Start-Dialog ist (hat Text)
            if (string.IsNullOrEmpty(dialogLine.text)) continue;
            
            // Prüfe ob verfügbar
            if (!IsDialogLineAvailable(dialogLine)) continue;
            
            // Der Dialog mit den meisten Requirements ist der "neueste"
            int requirementCount = dialogLine.requiredMemory?.Count ?? 0;
            
            if (requirementCount > mostRequirements)
            {
                mostRequirements = requirementCount;
                newestDialog = dialogLine;
            }
        }
        
        if (newestDialog != null)
        {
            Debug.Log($"Neuester Start-Dialog gefunden: '{newestDialog.memoryId}' mit {mostRequirements} Requirements");
        }
        
        return newestDialog;
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
                // Nach Choice-Antwort - sofort neue Choices nachrücken lassen
                showingChoiceAnswer = false;
                UpdateSpeakerName("???");
                
                // KORRIGIERT: Nur einen Aufruf, nicht beide
                UpdateChoicesInstantly();
                // ENTFERNT: RefreshAndShowAvailableChoices(); - wird in UpdateChoicesInstantly() gemacht
                return;
            }
            else
            {
                // Nach normalem Dialog-Text - nächste Zeile oder Choices
                ShowNextDialogLine();
                return;
            }
        }
    }

    // Wählt eine Choice aus der Warteschlange (behält statische CSV-Logik) - MIT DIREKTEM NACHRÜCKEN FÜR ALLE POSITIONEN
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
        
        // Memory-Flags setzen - verwende das neue erweiterte Memory-System
        GameManager.SafeProcessChoiceMemory(choice);
        
        // DEBUG: Zeige Choice-Details
        Debug.Log($"=== CHOICE VERARBEITUNG ===");
        Debug.Log($"Choice Text: '{choice.choiceText}'");
        Debug.Log($"Answer Text: '{choice.answerText}'");
        Debug.Log($"AddMemory: {(choice.addMemory != null ? string.Join(", ", choice.addMemory) : "KEINE")}");
        Debug.Log($"Answer ist leer: {string.IsNullOrEmpty(choice.answerText)}");
        
        // Choice-Antwort anzeigen - aus der ursprünglichen CSV-Struktur
        if (!string.IsNullOrEmpty(choice.answerText))
        {
            Debug.Log($"Zeige Choice-Antwort: '{choice.answerText}' von '{currentNPCName}'");
            UpdateSpeakerName(currentNPCName); // NPC-Name für Antworten verwenden
            ShowTextOnly(choice.answerText);
            
            showingChoiceAnswer = true;
            waitingForPlayerClick = true;
        }
        else
        {
            Debug.LogWarning($"KEINE CHOICE-ANTWORT VERFÜGBAR für Choice: '{choice.choiceText}'");
            // Keine Antwort-Text - sofort neue Warteschlange anzeigen
            UpdateSpeakerName("???");
            RefreshAndShowAvailableChoices();
        }
        
        // WICHTIG: Nachrücken NACH der Antwort-Anzeige, nicht davor!
        // UpdateAndShiftChoicesInstantly(); // ENTFERNT - wird nach der Antwort gemacht
    }

    // NEUE METHODE: Sofortiges Nachrücken und Auffüllen der Choice-Buttons
    private void UpdateAndShiftChoicesInstantly()
    {
        Debug.Log("=== SOFORTIGES CHOICE NACHRÜCKEN ===");
        
        // 1. Aktualisiere die Dialog-Liste mit neuesten Daten vom GameManager
        if (GameManager.Instance != null)
        {
            var npcId = GetCurrentNPCId();
            var updatedDialogs = GameManager.Instance.GetDialogsForNPC(npcId);
            
            if (updatedDialogs != null && updatedDialogs.Count > 0)
            {
                currentDialog = updatedDialogs;
                Debug.Log($"Dialog-Liste aktualisiert: {currentDialog.Count} Dialoge verfügbar");
            }
        }
        
        // 2. Hole die komplette neue Warteschlange (ohne bereits gewählte Choices)
        var newWaitingQueue = GetAllAvailableChoicesWithWaitingQueue();
        
        Debug.Log($"Neue Warteschlange nach Choice-Auswahl: {newWaitingQueue.Count} verfügbare Choices");
        
        // 3. Prüfe ob aktuell Choice-Buttons angezeigt werden
        bool currentlyShowingChoices = AnyChoiceButtonsActive();
        
        if (currentlyShowingChoices)
        {
            // 4. Aktualisiere sofort alle angezeigten Choice-Buttons mit der neuen Warteschlange
            ShowChoicesFromWaitingQueue(newWaitingQueue);
            Debug.Log("Choice-Buttons sofort aktualisiert - alle Positionen neu gefüllt");
        }
        else if (newWaitingQueue.Count > 0)
        {
            // 5. Falls keine Buttons aktiv, aber Choices verfügbar - zeige sie an
            Debug.Log($"Keine aktiven Buttons - zeige neue Choices an: {newWaitingQueue.Count}");
            RefreshAndShowAvailableChoices();
        }
        else
        {
            // 6. Keine Choices mehr verfügbar - Dialog beenden
            Debug.Log("Keine Choices mehr in Warteschlange - Dialog beenden");
            ShowChoicesOrEnd();
        }
    }

    // NEUE METHODE: Sofortiges Update der Choices ohne Neustart - VEREINFACHT
    private void UpdateChoicesInstantly()
    {
        Debug.Log("=== SOFORTIGES CHOICE UPDATE (nach Answer) ===");
        
        // Verwende die gleiche Logik wie bei der Choice-Auswahl
        UpdateAndShiftChoicesInstantly();
    }

    // WARTESCHLANGEN-SYSTEM: Sammle alle verfügbaren Choices aus allen Dialog-IDs - OPTIMIERT
    private void RefreshAndShowAvailableChoices()
    {
        var waitingQueue = GetAllAvailableChoicesWithWaitingQueue();
        
        if (waitingQueue.Count > 0)
        {
            Debug.Log($"RefreshAndShowAvailableChoices: Zeige {waitingQueue.Count} Choices aus aktualisierter Warteschlange");
            UpdateSpeakerName("???");
            ShowChoicesFromWaitingQueue(waitingQueue);
        }
        else
        {
            // Wirklich keine Choices mehr verfügbar - Dialog kann beendet werden
            Debug.Log("Keine verfügbaren Choices in der Warteschlange gefunden - Dialog beenden");
            ShowChoicesOrEnd();
        }
    }
    
    // FEHLENDE METHODE: GetAllAvailableChoicesWithWaitingQueue hinzufügen
    private List<ChoiceReference> GetAllAvailableChoicesWithWaitingQueue()
    {
        var waitingQueue = new List<ChoiceReference>();
        
        if (currentDialog == null) return waitingQueue;
        
        // Durchlaufe alle Dialog-Zeilen und sammle verfügbare Choices
        foreach (var dialogLine in currentDialog)
        {
            // Prüfe ob diese Dialog-Zeile verfügbar ist
            if (!IsDialogLineAvailable(dialogLine)) continue;
            
            // Sammle alle verfügbaren Choices aus dieser Dialog-Zeile
            if (dialogLine.choices != null && dialogLine.choices.Count > 0)
            {
                for (int i = 0; i < dialogLine.choices.Count; i++)
                {
                    var choice = dialogLine.choices[i];
                    
                    // Prüfe ob diese Choice verfügbar und noch nicht gewählt ist
                    if (IsChoiceAvailable(choice) && !choice.wasChosen)
                    {
                        var choiceRef = new ChoiceReference(dialogLine, i, choice);
                        waitingQueue.Add(choiceRef);
                        
                        Debug.Log($"Warteschlange: Choice '{choice.choiceText}' von Dialog-ID '{dialogLine.memoryId}' hinzugefügt");
                    }
                }
            }
        }
        
        Debug.Log($"GetAllAvailableChoicesWithWaitingQueue: {waitingQueue.Count} Choices in Warteschlange");
        return waitingQueue;
    }

    // FEHLENDE METHODE: ShowChoicesFromWaitingQueue - ERWEITERT FÜR KOMPLETTES NACHRÜCKEN
    private void ShowChoicesFromWaitingQueue(List<ChoiceReference> waitingQueue)
    {
        ShowChoicesOnly();
        
        // Beschränke auf verfügbare Button-Slots
        int maxChoices = Mathf.Min(waitingQueue.Count, choiceButtons.Length);
        
        Debug.Log($"ShowChoicesFromWaitingQueue: Zeige {maxChoices} von {waitingQueue.Count} Choices (komplettes Nachrücken)");
        
        // WICHTIG: Alle Buttons erst deaktivieren, dann neu füllen
        HideAllChoiceButtons();
        
        // Aktiviere benötigte Buttons mit neuen Choices aus der Warteschlange
        for (int i = 0; i < maxChoices; i++)
        {
            var choiceRef = waitingQueue[i];
            var choice = choiceRef.choice;
            var button = choiceButtons[i];
            if (button == null) continue;
            
            button.gameObject.SetActive(true);
            button.interactable = true;
            
            // WICHTIG: Setup der transparenten Hover-Colors bei jedem Anzeigen
            SetupInvertedHoverColors(button);
            
            var buttonText = button.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = choice.choiceText;
                // Schrift bleibt normal sichtbar - nur der Hintergrund ist transparent → hell
            }
            
            Debug.Log($"  Position {i}: '{choice.choiceText}' (aus Dialog-ID '{choiceRef.sourceDialogLine.memoryId}')");
            
            // OnClick Event setzen - WICHTIG: Verwende lokale Closure für choiceRef
            button.onClick.RemoveAllListeners();
            var choiceToSelect = choiceRef; // Closure Variable
            button.onClick.AddListener(() => SelectChoiceFromWaitingQueue(choiceToSelect));
        }
        
        // Zeige Warteschlangen-Info
        if (waitingQueue.Count > maxChoices)
        {
            Debug.Log($"WARTESCHLANGE: {waitingQueue.Count - maxChoices} weitere Choices warten auf nachrücken");
        }
        else
        {
            Debug.Log("WARTESCHLANGE: Alle verfügbaren Choices werden angezeigt");
        }
    }

    // ENTFERNT: CheckForNewChoicesAndRestart() - wird nicht mehr benötigt
    // ENTFERNT: CountAvailableChoicesInDialogs() - wird nicht mehr benötigt

    // Hilfsmethode: Zählt verfügbare Choices in einer Dialog-Liste
    private int CountAvailableChoicesInDialogs(List<DialogLine> dialogs)
    {
        int count = 0;
        
        if (dialogs == null) return count;
        
        foreach (var dialogLine in dialogs)
        {
            if (!IsDialogLineAvailable(dialogLine)) continue;
            
            if (dialogLine.choices != null)
            {
                foreach (var choice in dialogLine.choices)
                {
                    if (IsChoiceAvailable(choice) && !choice.wasChosen)
                    {
                        count++;
                    }
                }
            }
        }
        
        return count;
    }
    
    // Hilfsmethode: Ermittelt die aktuelle NPC-ID
    private string GetCurrentNPCId()
    {
        // Versuche NPC-ID über den Sprecher-Namen zu finden
        if (GameManager.Instance != null && GameManager.Instance.npcs != null)
        {
            foreach (var npc in GameManager.Instance.npcs)
            {
                if (npc.npcName == currentSpeakerName)
                {
                    return npc.npcId;
                }
            }
        }
        
        // Fallback: Verwende den ersten verfügbaren NPC
        return "TestGrandma";
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
        // Verwende die zentrale Logik vom GameManager für konsistente Prüfungen
        return GameManager.SafeIsDialogLineAvailable(line);
    }

    private bool IsChoiceAvailable(DialogLine.ChoiceData choice)
    {
        // Verwende das neue erweiterte Memory-System vom GameManager
        return GameManager.SafeAreChoiceRequirementsMet(choice);
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

    // Debug-Methode - VEREINFACHT
    public void DebugCurrentState()
    {
        Debug.Log("=== DIALOG MANAGER DEBUG ===");
        Debug.Log($"GameObject: {gameObject.name}");
        Debug.Log($"DialogPanel aktiv: {dialogPanel?.activeInHierarchy ?? false}");
        Debug.Log($"WaitingForPlayerClick: {waitingForPlayerClick}");
        Debug.Log($"ShowingChoiceAnswer: {showingChoiceAnswer}");
        
        // Debug: Zeige verfügbare Start-Dialoge
        if (currentDialog != null)
        {
            Debug.Log("=== VERFÜGBARE START-DIALOGE ===");
            foreach (var dialogLine in currentDialog)
            {
                if (!string.IsNullOrEmpty(dialogLine.text) && IsDialogLineAvailable(dialogLine))
                {
                    int requirementCount = dialogLine.requiredMemory?.Count ?? 0;
                    Debug.Log($"  '{dialogLine.memoryId}': {requirementCount} Requirements - Text: '{dialogLine.text.Substring(0, Mathf.Min(50, dialogLine.text.Length))}...'");
                }
            }
            
            var newestDialog = FindNewestAvailableStartDialog();
            if (newestDialog != null)
            {
                Debug.Log($"GEWÄHLTER START-DIALOG: '{newestDialog.memoryId}'");
            }
        }
        
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
