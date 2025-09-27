using UnityEngine;
using System.Collections;
using UnityEngine.Events;

// Hierarchie-Entry für Items mit eindeutigen Schlüsseln
[System.Serializable]
public class ItemHierarchyEntry
{
    public string hierarchyKey;        // z.B. "mirrow1", "mirrow2", "mirrow3"
    public string baseId;              // z.B. "mirrow"
    public string originalItemId;      // z.B. "mirrow_broken", "mirrow_fixed", "mirrow_face"
    public ItemInteractable item;      // Das tatsächliche Item
    public GameObject gameObject;      // Das dazugehörige GameObject
    public int minLoop;                // MinLoop Anforderung
    public System.Collections.Generic.List<string> requiredMemory; // Memory Anforderungen
    public int priority;               // Priorität (niedriger = bevorzugt bei gleichen Bedingungen)
    
    public ItemHierarchyEntry(string hierarchyKey, string baseId, string originalItemId, ItemInteractable item, GameObject gameObject, int minLoop, System.Collections.Generic.List<string> requiredMemory, int priority)
    {
        this.hierarchyKey = hierarchyKey;
        this.baseId = baseId;
        this.originalItemId = originalItemId;
        this.item = item;
        this.gameObject = gameObject;
        this.minLoop = minLoop;
        this.requiredMemory = requiredMemory ?? new System.Collections.Generic.List<string>();
        this.priority = priority;
    }
}

public class ItemDialogTrigger : MonoBehaviour
{
    [Header("Debug")]
    public bool showDebugLogs = true;
    
    [Header("Item Configuration")]
    public string itemId = ""; // Für WagonDoorWithDialog Kompatibilität
    
    [Header("Door Closed Inspector")]
    [Tooltip("Door Closed GameObject (wird für Tür-Dialog und Wechsel verwendet)")]
    public GameObject doorClosedObj;
    
    [Tooltip("Door 2 GameObject (wird bei Loop 10 + i_am_thomasen Memory aktiviert)")]
    public GameObject door2;
    
    [Header("Canvas Übergang System")]
    [Tooltip("Canvas Übergang GameObject (wird nach Door 2 Dialog dauerhaft aktiviert)")]
    public GameObject canvasUebergang;
    
    [Header("Spiegel Event")]
    public GameObject spiegelObjekt1; // Wird bei Loop 10 + fixed_mirrow aktiviert
    public GameObject spiegelObjekt2; // Wird bei Loop 10 + all_memorys_collected aktiviert
    public GameObject transitionCanvas; // Übergangs-Canvas

    [Header("Cup Window Crash")]
    public GameObject[] cupWindowActivate;
    public GameObject[] cupWindowDeactivate;

    [Header("Events")]
    public UnityEvent onDialogCompleted = new UnityEvent(); // Für WagonDoorWithDialog Kompatibilität
    
    [Header("WagonDoor System")]
    [Tooltip("WagonDoor Script, das beim Türwechsel aktiviert und ausgeführt werden soll")]
    public WagonDoor wagonDoorScript;

    private bool dialogActive = false;
    private bool doorDialogWasActive = false;
    private bool door2DialogWasActive = false; // Flag für Door 2 Dialog
    private string activeDoorDialogId = ""; // Welcher Door-Dialog aktiv ist
    private int lastCheckedLoop = -1;
    private int lastEvolutionLoop = -1; // Separater Tracker für Item Evolution
    private bool dialogWasShown = false; // Für WagonDoorWithDialog Kompatibilität
    private bool cacheInitialized = false; // Flag ob Cache bereits erstellt wurde
    private bool doorSystemChecked = false; // Flag um Door System nur einmal zu prüfen
    private bool canvasUebergangActivated = false; // Flag ob Canvas-Übergang bereits dauerhaft aktiviert wurde
    private bool spiegelEventTriggered = false;
    private bool spiegelDialogDone = false;
    private bool cupWindowCrashTriggered = false;
    private bool doorClicked = false;

    // Cache für Performance - wird nur einmal beim Start erstellt
    private System.Collections.Generic.List<ItemInteractable> cachedItemInteractables = null;
    private System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<ItemInteractable>> cachedItemGroups = null;
    
    private void Start()
    {
        // Koordiniere mit WagonManager für Cache-Erstellung
        StartCoroutine(InitializeCacheWithAllCanvases());
        
        // Registriere für Wagon Transition Complete Events
        RegisterForWagonTransitionEvents();
        
        if (doorClosedObj != null)
        {
            var btn = doorClosedObj.GetComponent<UnityEngine.UI.Button>();
            if (btn == null) btn = doorClosedObj.AddComponent<UnityEngine.UI.Button>();
            btn.onClick.AddListener(OnDoor1Clicked);
        }
    }
    
    private void OnDoor1Clicked()
    {
        doorClicked = true;
        if (showDebugLogs) Debug.Log("Door Closed wurde angeklickt. Wechsel-Flag gesetzt.");
    }
    
    // Registriere für WagonManager Events
    private void RegisterForWagonTransitionEvents()
    {
        var wagonManager = WagonManager.Instance;
        if (wagonManager != null)
        {
            wagonManager.OnTransitionCompleted += OnWagonTransitionCompleted;
            
            if (showDebugLogs)
            {
                Debug.Log("ItemDialogTrigger: Für Wagon Transition Events registriert");
            }
        }
        else
        {
            if (showDebugLogs)
            {
                Debug.LogWarning("ItemDialogTrigger: WagonManager nicht gefunden - kann sich nicht für Transition Events registrieren");
            }
        }
    }
    
    // Wird aufgerufen wenn Wagon-Wechsel complete ist
    private void OnWagonTransitionCompleted(int newWagon)
    {
        if (showDebugLogs)
        {
            Debug.Log($"ItemDialogTrigger: Wagon Transition Complete zu Wagon {newWagon} - führe Item-Evolution durch");
        }
        
        // Jetzt ist der perfekte Zeitpunkt für Item-Evolution:
        // - Neuer Wagon ist aktiviert
        // - Übergangs-Canvas ist noch aktiv (verdeckt den Tausch)
        // - Items können unsichtbar getauscht werden
        
        int currentLoop = GameManager.SafeGetCurrentLoop();
        
        // OPTIMIERT: Nur Items vom aktuellen Wagon bearbeiten
        UpdateItemVisibilityForSpecificWagon(newWagon, currentLoop);
        
        // Loop Tracker aktualisieren damit es nicht doppelt läuft
        lastEvolutionLoop = currentLoop;
        
        if (showDebugLogs)
        {
            Debug.Log($"ItemDialogTrigger: Item-Evolution nach Wagon-Wechsel abgeschlossen (unsichtbar für Spieler)");
        }
    }
    
    // Cleanup beim Destroy
    private void OnDestroy()
    {
        // Event-Registrierung aufräumen
        var wagonManager = WagonManager.Instance;
        if (wagonManager != null)
        {
            wagonManager.OnTransitionCompleted -= OnWagonTransitionCompleted;
        }
    }
    
    // Koordinierte Cache-Initialisierung mit WagonManager
    private System.Collections.IEnumerator InitializeCacheWithAllCanvases()
    {
        if (showDebugLogs)
        {
            Debug.Log("ItemDialogTrigger: Starte koordinierte Cache-Initialisierung");
        }
        
        // Warte einen Frame damit WagonManager bereit ist
        yield return null;
        
        // Aktiviere alle Canvases über WagonManager
        var wagonManager = WagonManager.Instance;
        if (wagonManager != null)
        {
            wagonManager.ActivateAllCanvasesForCaching();
            
            // Warte einen Frame damit alle GameObjects aktiviert sind
            yield return null;
            
            // Erstelle jetzt den Cache mit allen sichtbaren Items
            InitializeCache();
            
            // Stelle normale Canvas-Konfiguration wieder her
            wagonManager.RestoreNormalCanvasState();
            
            if (showDebugLogs)
            {
                Debug.Log("ItemDialogTrigger: Cache-Initialisierung abgeschlossen, normale Canvas-Konfiguration wiederhergestellt");
            }
        }
        else
        {
            Debug.LogWarning("ItemDialogTrigger: WagonManager nicht gefunden - verwende Fallback Cache-Erstellung");
            
            // Fallback: Cache ohne Canvas-Koordination erstellen
            InitializeCache();
        }
    }
    
    // Cache einmalig beim Start erstellen
    private void InitializeCache()
    {
        if (!cacheInitialized)
        {
            if (showDebugLogs)
            {
                Debug.Log("ItemDialogTrigger: Initialisiere Cache beim Spielstart - alle Canvases sind jetzt aktiv");
            }
            
            // EINFACHES SYSTEM: Nur die grundlegenden Caches erstellen
            cachedItemInteractables = GetAllItemInteractables();
            cachedItemGroups = GroupItemsByBaseId(cachedItemInteractables);
            
            cacheInitialized = true;
            
            if (showDebugLogs)
            {
                Debug.Log($"ItemDialogTrigger: Cache erstellt - {cachedItemInteractables.Count} Items in {cachedItemGroups.Count} Gruppen");
                
                // Debug: Zeige gefundene Items
                foreach (var item in cachedItemInteractables)
                {
                    Debug.Log($"  Gefunden: '{item.itemId}' auf GameObject '{item.name}' (Canvas: {GetCanvasName(item)})");
                }
                
                // Debug: Zeige Gruppen
                foreach (var group in cachedItemGroups)
                {
                    Debug.Log($"Gruppe '{group.Key}': {group.Value.Count} Items");
                    foreach (var item in group.Value)
                    {
                        Debug.Log($"  - '{item.itemId}' auf GameObject '{item.name}'");
                    }
                }
            }
        }
    }
    
    // Erstelle hierarchische Item-Schlüssel für jede Basis-ID
    private System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<ItemHierarchyEntry>> CreateItemHierarchy(System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<ItemInteractable>> itemGroups)
    {
        var hierarchy = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<ItemHierarchyEntry>>();
        
        foreach (var group in itemGroups)
        {
            string baseId = group.Key;
            var itemsInGroup = group.Value;
            
            if (showDebugLogs)
            {
                Debug.Log($"\n=== ERSTELLE HIERARCHIE FÜR BASIS-ID '{baseId}' ({itemsInGroup.Count} Items) ===");
                Debug.Log($"Gefundene GameObjects mit ItemInteractable in CanvasWagon:");
                foreach (var item in itemsInGroup)
                {
                    Debug.Log($"  - GameObject '{item.name}' → itemId '{item.itemId}' (Canvas: {GetCanvasName(item)})");
                }
            }
            
            // Sammle alle Items mit ihren tatsächlichen itemIds und Dialog-Daten
            var itemsWithData = new System.Collections.Generic.List<System.Tuple<ItemInteractable, string, int, System.Collections.Generic.List<string>>>();
            
            foreach (var item in itemsInGroup)
            {
                string actualItemId = item.itemId; // itemId vom ItemInteractable Script
                
                // Für evolutionäre Items (mit _) erwarten wir oft keine Dialoge
                bool isEvolutionaryItem = actualItemId.Contains("_");
                var dialogs = GameManager.SafeGetDialogsForItem(actualItemId);
                
                if (dialogs != null && dialogs.Count > 0)
                {
                    var dialog = dialogs[0];
                    itemsWithData.Add(new System.Tuple<ItemInteractable, string, int, System.Collections.Generic.List<string>>(
                        item, actualItemId, dialog.minLoop, dialog.requiredMemory ?? new System.Collections.Generic.List<string>()));
                    
                    if (showDebugLogs)
                    {
                        Debug.Log($"  GameObject '{item.name}' → itemId '{actualItemId}': minLoop={dialog.minLoop}, requiredMemory=[{string.Join(",", dialog.requiredMemory ?? new System.Collections.Generic.List<string>())}]");
                    }
                }
                else
                {
                    // Item ohne Dialog - das ist normal für evolutionäre Items
                    // Diese Items haben nur visuelle Funktionen, keine eigenen Dialoge
                    itemsWithData.Add(new System.Tuple<ItemInteractable, string, int, System.Collections.Generic.List<string>>(
                        item, actualItemId, 0, new System.Collections.Generic.List<string>()));
                    
                    if (showDebugLogs)
                    {
                        string itemType = isEvolutionaryItem ? "evolutionäres Item" : "Basis-Item";
                        Debug.Log($"  GameObject '{item.name}' → itemId '{actualItemId}': KEIN DIALOG ({itemType}) - minLoop=0, requiredMemory=[]");
                    }
                }
            }
            
            // Sortiere Items nach minLoop (aufsteigend), dann nach Anzahl requiredMemory (aufsteigend)
            itemsWithData.Sort((a, b) => 
            {
                int minLoopComparison = a.Item3.CompareTo(b.Item3);
                if (minLoopComparison != 0)
                    return minLoopComparison;
                
                // Bei gleichem minLoop: weniger requiredMemory = höhere Priorität
                return a.Item4.Count.CompareTo(b.Item4.Count);
            });
            
            // Erstelle hierarchische Schlüssel basierend auf itemId → GameObject Mapping
            var hierarchyEntries = new System.Collections.Generic.List<ItemHierarchyEntry>();
            
            for (int i = 0; i < itemsWithData.Count; i++)
            {
                var itemData = itemsWithData[i];
                string hierarchyKey = $"{baseId}{i + 1}"; // z.B. "mirrow1", "mirrow2", "mirrow3"
                
                var entry = new ItemHierarchyEntry(
                    hierarchyKey,
                    baseId,
                    itemData.Item2, // originalItemId (die echte itemId)
                    itemData.Item1, // ItemInteractable
                    itemData.Item1.gameObject, // GameObject das diese itemId hat
                    itemData.Item3, // minLoop
                    itemData.Item4, // requiredMemory
                    i + 1 // Priorität: 1 = höchste Priorität
                );
                
                hierarchyEntries.Add(entry);
                
                if (showDebugLogs)
                {
                    Debug.Log($"  ➤ SCHLÜSSEL '{hierarchyKey}': itemId '{entry.originalItemId}' → GameObject '{entry.gameObject.name}' (minLoop={entry.minLoop}, Memory=[{string.Join(",", entry.requiredMemory)}], Priorität={entry.priority})");
                }
            }
            
            hierarchy[baseId] = hierarchyEntries;
            
            if (showDebugLogs)
            {
                Debug.Log($"=== HIERARCHIE FÜR BASIS-ID '{baseId}' ERSTELLT: {hierarchyEntries.Count} Schlüssel ===");
                Debug.Log($"itemId → GameObject Mapping für Basis-ID '{baseId}':");
                foreach (var entry in hierarchyEntries)
                {
                    Debug.Log($"  {entry.originalItemId} → GameObject '{entry.gameObject.name}' (Schlüssel: {entry.hierarchyKey})");
                }
            }
        }
        
        return hierarchy;
    }
    
    // Erstelle Wagon-spezifische Item-Zuordnung
    private System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<ItemHierarchyEntry>> CreateWagonSpecificMapping(System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<ItemHierarchyEntry>> itemHierarchy)
    {
        var wagonMapping = new System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<ItemHierarchyEntry>>();
        
        if (showDebugLogs)
        {
            Debug.Log($"\n=== ERSTELLE WAGON-SPEZIFISCHE ITEM-ZUORDNUNG ===");
        }
        
        // Gehe durch alle Hierarchie-Einträge und ordne sie Wagons zu
        foreach (var hierarchy in itemHierarchy)
        {
            string baseId = hierarchy.Key;
            var entries = hierarchy.Value;
            
            foreach (var entry in entries)
            {
                // Ermittle Wagon-Nummer basierend auf Canvas-Namen
                int wagonNumber = GetWagonNumberFromGameObject(entry.gameObject);
                
                if (wagonNumber > 0) // Nur gültige Wagon-Nummern
                {
                    if (!wagonMapping.ContainsKey(wagonNumber))
                    {
                        wagonMapping[wagonNumber] = new System.Collections.Generic.List<ItemHierarchyEntry>();
                    }
                    
                    wagonMapping[wagonNumber].Add(entry);
                    
                    if (showDebugLogs)
                    {
                        Debug.Log($"  Wagon {wagonNumber}: Schlüssel '{entry.hierarchyKey}' (itemId: '{entry.originalItemId}', GameObject: '{entry.gameObject.name}')");
                    }
                }
                else if (showDebugLogs)
                {
                    Debug.LogWarning($"  UNBEKANNTER WAGON: Schlüssel '{entry.hierarchyKey}' (itemId: '{entry.originalItemId}', GameObject: '{entry.gameObject.name}') - kann nicht zugeordnet werden");
                }
            }
        }
        
        if (showDebugLogs)
        {
            Debug.Log($"=== WAGON-ZUORDNUNG ERSTELLT: {wagonMapping.Count} Wagons ===");
            foreach (var wagon in wagonMapping)
            {
                Debug.Log($"  Wagon {wagon.Key}: {wagon.Value.Count} Items");
            }
        }
        
        return wagonMapping;
    }
    
    // Ermittle Wagon-Nummer aus GameObject (über Canvas-Namen)
    private int GetWagonNumberFromGameObject(GameObject gameObject)
    {
        Transform current = gameObject.transform;
        while (current != null)
        {
            Canvas canvas = current.GetComponent<Canvas>();
            if (canvas != null)
            {
                string canvasName = canvas.name;
                
                // Parse Wagon-Nummer aus Canvas-Namen (z.B. "CanvasWagon1" → 1)
                if (canvasName.StartsWith("CanvasWagon"))
                {
                    string numberPart = canvasName.Substring("CanvasWagon".Length);
                    if (int.TryParse(numberPart, out int wagonNumber))
                    {
                        return wagonNumber;
                    }
                }
                
                // Fallback: Andere Canvas-Namen-Patterns
                if (canvasName.Contains("Wagon"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(canvasName, @"(\d+)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int wagonNumber))
                    {
                        return wagonNumber;
                    }
                }
            }
            current = current.parent;
        }
        
        return -1; // Wagon-Nummer nicht gefunden
    }
    
    // Hilfsmethode: Finde den Canvas-Namen eines Items
    private string GetCanvasName(ItemInteractable item)
    {
        Transform current = item.transform;
        while (current != null)
        {
            Canvas canvas = current.GetComponent<Canvas>();
            if (canvas != null)
            {
                return canvas.name;
            }
            current = current.parent;
        }
        return "Unbekannter Canvas";
    }
    
    private void Update()
    {
        bool currentlyActive = IsDialogActive();
        // Dialog wurde gerade beendet
        if (dialogActive && !currentlyActive)
        {
            // Wechsel nur, wenn Tür angeklickt wurde
            if (doorClicked)
            {
                if (showDebugLogs)
                {
                    Debug.Log("ItemDialogTrigger: Tür-Dialog beendet und Tür wurde angeklickt - triggere WagonDoor (ID egal)");
                }
                TriggerWagonDoor();
                doorClicked = false;
            }
            
            // Prüfe ob es ein Door 2 Dialog war (für Canvas-Übergang und Musik)
            if (door2DialogWasActive)
            {
                if (showDebugLogs)
                {
                    Debug.Log($"ItemDialogTrigger: Door 2 Dialog '{activeDoorDialogId}' beendet - aktiviere Canvas-Übergang und stoppe Musik");
                }
                
                HandleDoor2DialogCompleted();
                door2DialogWasActive = false;
                activeDoorDialogId = "";
            }
            
            // Triggere onDialogCompleted Event für WagonDoorWithDialog Kompatibilität
            if (onDialogCompleted != null)
            {
                onDialogCompleted.Invoke();
                if (showDebugLogs)
                {
                    Debug.Log("ItemDialogTrigger: onDialogCompleted Event ausgelöst");
                }
            }
        }
        
        // Dialog wurde gerade gestartet
        if (!dialogActive && currentlyActive)
        {
            // Prüfe ob es ein door_closed Dialog ist
            doorDialogWasActive = CheckIfDoorDialog();
            
            // Prüfe ob es ein Door 2 Dialog ist
            door2DialogWasActive = CheckIfDoor2Dialog();
            
            if (showDebugLogs && doorDialogWasActive)
            {
                Debug.Log("ItemDialogTrigger: door_closed Dialog gestartet");
            }
            
            if (showDebugLogs && door2DialogWasActive)
            {
                Debug.Log($"ItemDialogTrigger: Door 2 Dialog '{activeDoorDialogId}' gestartet");
            }
        }
        
        dialogActive = currentlyActive;
        
        // Prüfe Loop-basierte Dialoge (nur wenn kein Dialog aktiv ist)
        if (!currentlyActive)
        {
            CheckLoopDialogs();
            
            // Prüfe Door Management System ab Loop 10 (jeden Frame)
            CheckDoorManagementSystem();
            
            // Item-Evolution läuft jetzt hauptsächlich über Wagon Transition Events
            // Nur als Fallback für den ersten Start prüfen
            CheckItemEvolutionOptimizedFallback();
        }

        // Spiegel Event: Erstes Objekt aktivieren
        if (!spiegelEventTriggered && GameManager.SafeGetCurrentLoop() == 10 && GameManager.SafeHasMemory("fixed_mirrow"))
        {
            if (spiegelObjekt1 != null)
            {
                spiegelObjekt1.SetActive(true);
                spiegelEventTriggered = true;
                Debug.Log("SpiegelObjekt1 aktiviert (Loop 10 + fixed_mirrow)");
            }
        }

        // Zweites Objekt aktivieren, wenn Voraussetzungen erfüllt
        if (GameManager.SafeGetCurrentLoop() == 10 && GameManager.SafeHasMemory("all_memorys_collected"))
        {
            if (spiegelObjekt2 != null && !spiegelObjekt2.activeSelf)
            {
                spiegelObjekt2.SetActive(true);
                Debug.Log("SpiegelObjekt2 aktiviert (Loop 10 + all_memorys_collected)");
            }
        }

        // Cup Window Crash Event
        if (!cupWindowCrashTriggered && GameManager.SafeGetCurrentLoop() == 8)
        {
            foreach (var obj in cupWindowActivate)
            {
                if (obj != null) obj.SetActive(true);
            }
            foreach (var obj in cupWindowDeactivate)
            {
                if (obj != null) obj.SetActive(false);
            }
            cupWindowCrashTriggered = true;
            Debug.Log("Cup Window Crash Event: 3 Objekte aktiviert, 3 deaktiviert (Loop 8)");
        }
    }
    
    // Prüfe ob ein Dialog aktiv ist
    private bool IsDialogActive()
    {
        var dialogManager = FindFirstObjectByType<DialogManager>();
        return dialogManager != null && dialogManager.dialogPanel != null && dialogManager.dialogPanel.activeInHierarchy;
    }
    
    // Prüfe ob es ein door_closed Dialog ist (vereinfacht)
    private bool CheckIfDoorDialog()
    {
        // Prüfe, ob der aktuell angezeigte Dialog wirklich zu door_closed gehört
        var dialogManager = FindFirstObjectByType<DialogManager>();
        if (dialogManager == null)
            return false;

        // Prüfe, ob ein Dialog aktiv ist
        if (!dialogManager.dialogPanel || !dialogManager.dialogPanel.activeInHierarchy)
            return false;

        // Prüfe, ob die aktuelle Dialog-Liste existiert und DialogIndex gültig ist
        var currentDialog = typeof(DialogManager).GetField("currentDialog", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(dialogManager) as System.Collections.Generic.List<DialogLine>;
        var dialogIndexField = typeof(DialogManager).GetField("dialogIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        int dialogIndex = dialogIndexField != null ? (int)dialogIndexField.GetValue(dialogManager) : 0;

        if (currentDialog == null || dialogIndex < 0 || dialogIndex >= currentDialog.Count)
            return false;

        // Prüfe, ob die aktuelle Dialogzeile zu door_closed gehört
        var currentLine = currentDialog[dialogIndex];
        // Annahme: DialogLine hat eine itemId oder memoryId, die "door_closed" entspricht
        if (currentLine != null && currentLine.memoryId == "door_closed")
            return true;

        return false;
    }
    
    // Prüfe ob es ein Door 2 Dialog ist (basierend auf Door 2 GameObject)
    private bool CheckIfDoor2Dialog()
    {
        // Prüfe ob Door 2 zugewiesen ist und aktiv ist
        if (door2 == null || !door2.activeInHierarchy)
            return false;
        
        // Suche ItemInteractable auf Door 2 GameObject
        var itemInteractable = door2.GetComponent<ItemInteractable>();
        if (itemInteractable == null)
            return false;
        
        // Prüfe ob dieser Dialog für diese itemId verfügbar ist
        var dialogs = GameManager.SafeGetDialogsForItem(itemInteractable.itemId);
        if (dialogs != null && dialogs.Count > 0)
        {
            activeDoorDialogId = itemInteractable.itemId;
            return true;
        }
        
        return false;
    }
    
    // Behandle Door 2 Dialog-Abschluss: Canvas-Übergang + Musik stoppen
    private void HandleDoor2DialogCompleted()
    {
        if (showDebugLogs)
        {
            Debug.Log($"🚪 DOOR 2 DIALOG ABGESCHLOSSEN: Aktiviere Canvas-Übergang dauerhaft und stoppe Musik");
        }
        
        // 1. Canvas-Übergang dauerhaft aktivieren (über Inspector zugewiesenes GameObject)
        if (canvasUebergang != null && !canvasUebergangActivated)
        {
            canvasUebergang.SetActive(true);
            canvasUebergangActivated = true; // Markiere als dauerhaft aktiviert
            
            if (showDebugLogs)
            {
                Debug.Log($"✅ CANVAS-ÜBERGANG DAUERHAFT AKTIVIERT: {canvasUebergang.name}");
            }
        }
        else if (canvasUebergang == null)
        {
            if (showDebugLogs)
            {
                Debug.LogWarning("Canvas-Übergang nicht im Inspector zugewiesen - kann nicht aktiviert werden");
            }
        }
        else if (canvasUebergangActivated)
        {
            if (showDebugLogs)
            {
                Debug.Log("Canvas-Übergang bereits dauerhaft aktiviert");
            }
        }
        
        // 2. Musik stoppen über GameManager
        if (GameManager.Instance != null)
        {
            // Stoppe alle Musik-AudioSources
            StopAllMusicFromGameManager();
        }
        else
        {
            if (showDebugLogs)
            {
                Debug.LogWarning("GameManager nicht gefunden - kann Musik nicht stoppen");
            }
        }
    }
    
    // Stoppe alle Musik-AudioSources im GameManager
    private void StopAllMusicFromGameManager()
    {
        var gameManager = GameManager.Instance;
        
        // Verwende Reflection um auf private AudioSources zuzugreifen
        var gameManagerType = gameManager.GetType();
        
        // Suche nach AudioSource-Feldern für Musik
        var fields = gameManagerType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        foreach (var field in fields)
        {
            if (field.FieldType == typeof(AudioSource))
            {
                var audioSource = field.GetValue(gameManager) as AudioSource;
                if (audioSource != null && audioSource.isPlaying)
                {
                    // Stoppe nur Musik-AudioSources (erkennbar am Namen oder am Loop-Flag)
                    if (field.Name.ToLower().Contains("music") || field.Name.ToLower().Contains("background") || audioSource.loop)
                    {
                        audioSource.Stop();
                        
                        if (showDebugLogs)
                        {
                            Debug.Log($"🎵 MUSIK GESTOPPT: {field.Name} (AudioClip: {(audioSource.clip != null ? audioSource.clip.name : "null")})");
                        }
                    }
                }
            }
        }
    }
    
    // Triggere WagonDoor Script
    private void TriggerWagonDoor()
    {
        // Debug: Zeige ob Inspector-Zuweisung vorhanden ist
        if (showDebugLogs)
        {
            Debug.Log($"TriggerWagonDoor: wagonDoorScript Inspector-Zuweisung: {(wagonDoorScript != null ? "JA" : "NEIN")}");
            Debug.Log($"TriggerWagonDoor: doorClosedObj Inspector-Zuweisung: {(doorClosedObj != null ? "JA" : "NEIN")}");
        }
        // 1. Direktes Inspector-Feld
        if (wagonDoorScript != null)
        {
            Debug.Log("TriggerWagonDoor: Aktiviere und rufe WagonDoor direkt über Inspector-Feld auf!");
            wagonDoorScript.enabled = true;
            wagonDoorScript.OnWagonDoorClicked();
            StartCoroutine(DeactivateWagonDoorAfterDelaySeconds(wagonDoorScript, 3f));
            return;
        }
        // 2. Fallback: Suche auf doorClosedObj
        if (doorClosedObj != null)
        {
            var allComponents = doorClosedObj.GetComponents<MonoBehaviour>();
            foreach (var component in allComponents)
            {
                if (component.GetType().Name == "WagonDoor")
                {
                    Debug.Log("TriggerWagonDoor: Aktiviere und rufe WagonDoor über doorClosedObj auf!");
                    component.enabled = true;
                    CallWagonDoorMethod(component);
                    StartCoroutine(DeactivateWagonDoorAfterDelaySeconds(component, 3f));
                    return;
                }
            }
            Debug.LogWarning($"TriggerWagonDoor: Kein WagonDoor Script auf GameObject '{doorClosedObj.name}' gefunden (Inspector)");
        }
        Debug.LogWarning("TriggerWagonDoor: Kein WagonDoor Script gefunden! Weder Inspector noch Tür-Objekt.");
    }
    
    // Deaktiviere WagonDoor Script nach Ausführung
    private System.Collections.IEnumerator DeactivateWagonDoorAfterDelay(MonoBehaviour script)
    {
        yield return new WaitForSeconds(0.1f); // Kurz warten nach Ausführung
        
        script.enabled = false;
        
        if (showDebugLogs)
        {
            Debug.Log($"ItemDialogTrigger: WagonDoor Script wieder deaktiviert");
        }
    }
    
    // Deaktiviere WagonDoor Script nach Ausführung (mit Sekunden-Delay)
    private System.Collections.IEnumerator DeactivateWagonDoorAfterDelaySeconds(MonoBehaviour script, float seconds)
    {
        yield return new WaitForSeconds(seconds);
        script.enabled = false;
        if (showDebugLogs)
        {
            Debug.Log($"ItemDialogTrigger: WagonDoor Script nach {seconds} Sekunden wieder deaktiviert");
        }
    }
    
    // Rufe WagonDoor Methode nach kurzer Verzögerung auf
    private System.Collections.IEnumerator CallWagonDoorAfterDelay(MonoBehaviour script)
    {
        yield return new WaitForSeconds(0.2f); // Etwas länger warten für Start()
        
        if (showDebugLogs)
        {
            Debug.Log($"ItemDialogTrigger: Warten beendet, rufe jetzt OnWagonDoorClicked auf");
        }
        
        CallWagonDoorMethod(script);
    }
    
    // Rufe die WagonDoor Methode auf
    private void CallWagonDoorMethod(MonoBehaviour script)
    {
        var method = script.GetType().GetMethod("OnWagonDoorClicked");
        if (method != null)
        {
            if (showDebugLogs)
            {
                Debug.Log($"ItemDialogTrigger: Rufe {script.GetType().Name}.OnWagonDoorClicked() auf GameObject '{script.name}' auf");
            }
            
            try
            {
                method.Invoke(script, null);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"ItemDialogTrigger: Fehler beim Aufrufen von OnWagonDoorClicked(): {e.Message}");
            }
        }
        else
        {
            if (showDebugLogs)
            {
                Debug.LogWarning($"ItemDialogTrigger: OnWagonDoorClicked() Methode nicht gefunden in {script.GetType().Name}");
            }
        }
    }
    
    // Prüfe Loop-basierte Dialoge - DEAKTIVIERT, da jetzt über WagonManager Sound-System gesteuert
    private void CheckLoopDialogs()
    {
        // ALTE VERSION: Loop-Dialoge werden jetzt über WagonManager nach Loop-Sound gestartet
        // Diese Methode bleibt für Kompatibilität, macht aber nichts mehr
        
        if (showDebugLogs)
        {
            // Debug-Info nur einmal pro Loop anzeigen
            int currentLoop = GameManager.SafeGetCurrentLoop();
            if (currentLoop != lastCheckedLoop)
            {
                lastCheckedLoop = currentLoop;
                Debug.Log($"ItemDialogTrigger: Loop Count geändert auf {currentLoop} - Loop-Dialoge werden jetzt über WagonManager Sound-System gesteuert");
            }
        }
    }
    
    // NEUE VERSION: Loop-Dialog nach Sound-Ende vom WagonManager aufgerufen
    public void TriggerLoopDialogAfterSound(int loopCount)
    {
        if (showDebugLogs)
        {
            Debug.Log($"ItemDialogTrigger: Loop-Dialog angefordert nach Sound-Ende für Loop {loopCount}");
        }
        
        // Prüfe ob ein Dialog gerade aktiv ist
        if (IsDialogActive())
        {
            if (showDebugLogs)
            {
                Debug.Log($"ItemDialogTrigger: Dialog ist aktiv - Loop-Dialog für Loop {loopCount} wird übersprungen");
            }
            return;
        }
        
        // Kurze Verzögerung um sicherzustellen dass alles stabilisiert ist
        StartCoroutine(TriggerLoopDialogAfterDelay(loopCount));
    }
    
    // Door Management System - prüft Loop Count und Memory für Door-Wechsel
    private void CheckDoorManagementSystem()
    {
        // Nur prüfen wenn beide Doors zugewiesen sind
        if (doorClosedObj == null || door2 == null)
            return;
        
        int currentLoop = GameManager.SafeGetCurrentLoop();
        
        // OPTIMIERUNG: Erst ab Loop 10 prüfen - davor ist es sinnlos
        if (currentLoop < 10)
            return;
        
        // Ab Loop 10: Jeden Frame prüfen ob Door-Zustand korrekt ist
        bool shouldDoor1BeActive = true;
        bool shouldDoor2BeActive = false;
        
        // Prüfe ob Loop Count 10 erreicht wurde UND i_am_thomasen Memory vorhanden ist
        if (currentLoop >= 10 && GameManager.SafeHasMemory("i_am_thomasen"))
        {
            shouldDoor1BeActive = false;
            shouldDoor2BeActive = true;
        }
        
        // Prüfe aktuellen Zustand der Doors
        bool door1CurrentlyActive = doorClosedObj.activeInHierarchy;
        bool door2CurrentlyActive = door2.activeInHierarchy;
        
        // Korrigiere Door-Zustand falls nötig
        bool needsCorrection = false;
        
        if (door1CurrentlyActive != shouldDoor1BeActive)
        {
            doorClosedObj.SetActive(shouldDoor1BeActive);
            needsCorrection = true;
            
            if (showDebugLogs)
            {
                Debug.Log($"🚪 KORREKTUR Door Closed: {(door1CurrentlyActive ? "aktiv" : "inaktiv")} → {(shouldDoor1BeActive ? "aktiv" : "inaktiv")}");
            }
        }
        
        if (door2CurrentlyActive != shouldDoor2BeActive)
        {
            door2.SetActive(shouldDoor2BeActive);
            needsCorrection = true;
            
            if (showDebugLogs)
            {
                Debug.Log($"🚪 KORREKTUR Door 2: {(door2CurrentlyActive ? "aktiv" : "inaktiv")} → {(shouldDoor2BeActive ? "aktiv" : "inaktiv")}");
            }
        }
        
        // Debug-Output bei Änderungen oder beim ersten Mal
        if (needsCorrection || !doorSystemChecked)
        {
            if (showDebugLogs)
            {
                string reason = currentLoop >= 10 && GameManager.SafeHasMemory("i_am_thomasen") 
                    ? "Loop 10+ und Memory 'i_am_thomasen' verfügbar" 
                    : $"Loop {currentLoop} erreicht, aber Memory 'i_am_thomasen' noch nicht verfügbar";
                
                Debug.Log($"🚪 DOOR MANAGEMENT SYSTEM (Loop {currentLoop}): {reason}");
                Debug.Log($"Door Closed: {(shouldDoor1BeActive ? "aktiv" : "inaktiv")}, Door 2: {(shouldDoor2BeActive ? "aktiv" : "inaktiv")}");
                
                if (needsCorrection)
                {
                    Debug.Log($"⚠️ Door-Zustand wurde korrigiert!");
                }
            }
            
            doorSystemChecked = true; // Markiere als geprüft für Debug-Output
        }
    }
    
    // Triggere Loop Dialog nach Delay
    private System.Collections.IEnumerator TriggerLoopDialogAfterDelay(int loopCount)
    {
        yield return new WaitForSeconds(0.5f);
        
        // Bestimme welcher Loop-Dialog basierend auf dem aktuellen Loop Count ausgelöst werden soll
        string targetItemId = null;
        
        if (loopCount == 2)
        {
            targetItemId = "loop_intro";
        }
        else if (loopCount == 3)
        {
            targetItemId = "loop_two";
        }
        else if (loopCount >= 9)
        {
            targetItemId = "loop_last";
        }
        
        if (!string.IsNullOrEmpty(targetItemId))
        {
            // Prüfe ob dieser Dialog verfügbar ist
            var dialogs = GameManager.SafeGetDialogsForItem(targetItemId);
            if (dialogs != null && dialogs.Count > 0)
            {
                if (showDebugLogs)
                {
                    Debug.Log($"ItemDialogTrigger: Triggere Loop-Dialog für '{targetItemId}' (Loop {loopCount})");
                }
                
                // Triggere den spezifischen Dialog
                TriggerItemDialog(targetItemId);
            }
        }
        else
        {
            if (showDebugLogs)
            {
                Debug.Log($"ItemDialogTrigger: Kein Loop-Dialog für Loop {loopCount} definiert");
            }
        }
    }
    
    // Triggere Item Dialog direkt
    private void TriggerItemDialog(string itemId)
    {
        // Finde das ItemInteractable mit der passenden itemId
        var itemInteractables = FindObjectsByType<ItemInteractable>(FindObjectsSortMode.None);
        
        foreach (var item in itemInteractables)
        {
            if (item.itemId == itemId)
            {
                if (showDebugLogs)
                {
                    Debug.Log($"ItemDialogTrigger: ItemInteractable mit ID '{itemId}' gefunden auf GameObject '{item.name}' - triggere OnClick");
                }
                
                // Rufe OnButtonClick() direkt auf wie bei einem normalen Item-Klick
                item.OnButtonClick();
                return;
            }
        }
        
        if (showDebugLogs)
        {
            Debug.LogWarning($"ItemDialogTrigger: Kein ItemInteractable mit ID '{itemId}' gefunden");
        }
    }
    
    // Fallback Version: Nur für ersten Start wenn noch kein Wagon-Wechsel stattgefunden hat
    private void CheckItemEvolutionOptimizedFallback()
    {
        int currentLoop = GameManager.SafeGetCurrentLoop();
        
        // Nur beim ersten Start (lastEvolutionLoop == -1) prüfen
        // Nach dem ersten Wagon-Wechsel übernehmen die Transition Events
        if (lastEvolutionLoop == -1)
        {
            if (showDebugLogs)
            {
                Debug.Log($"ItemDialogTrigger: Erste Item Evolution beim Start (Loop {currentLoop}) - Fallback");
            }
            
            lastEvolutionLoop = currentLoop;
            
            // Beim ersten Start: Alle Items aktualisieren (Fallback)
            UpdateItemVisibilityForCurrentLoop(currentLoop);
        }
    }
    
    // Original Methode als Backup behalten
    private void CheckItemEvolutionOptimized()
    {
        int currentLoop = GameManager.SafeGetCurrentLoop();
        
        // Beim ersten Start oder wenn Loop Count sich geändert hat
        if (lastEvolutionLoop == -1 || currentLoop != lastEvolutionLoop)
        {
            if (showDebugLogs)
            {
                if (lastEvolutionLoop == -1)
                {
                    Debug.Log($"ItemDialogTrigger: Erste Item Evolution beim Start (Loop {currentLoop})");
                }
                else
                {
                    Debug.Log($"ItemDialogTrigger: Loop Count für Evolution geändert von {lastEvolutionLoop} auf {currentLoop} - aktualisiere Item Sichtbarkeit");
                }
            }
            
            lastEvolutionLoop = currentLoop;
            
            // Sichtbarkeit aktualisieren (OHNE Cache zu invalidieren)
            UpdateItemVisibilityForCurrentLoop(currentLoop);
        }
    }
    
    // VEREINFACHTE FALLBACK METHODE
    private void UpdateItemVisibilityForCurrentLoop(int currentLoop)
    {
        if (!cacheInitialized) return;
        
        if (showDebugLogs)
        {
            Debug.Log($"=== ITEM EVOLUTION FÜR ALLE ITEMS (Loop {currentLoop}) ===");
        }
        
        // EINFACHES SYSTEM: Verwende die alten, funktionierenden Methoden
        // Gruppiere alle Items nach Basis-ID
        var itemGroups = GetItemGroupsCached(cachedItemInteractables);
        
        // Für jede Gruppe: Finde bestes Item und aktiviere es
        foreach (var group in itemGroups)
        {
            string baseId = group.Key;
            var itemsInGroup = group.Value;
            
            if (showDebugLogs)
            {
                Debug.Log($"Evolution für Item-Gruppe '{baseId}' mit {itemsInGroup.Count} Items");
            }
            
            // Verwende die alte, funktionierende Methode
            ItemInteractable activeItem = FindCorrectItemForCurrentState(itemsInGroup, currentLoop);
            
            // Aktiviere das richtige Item und deaktiviere die anderen
            UpdateItemGroupVisibility(itemsInGroup, activeItem);
        }
        
        if (showDebugLogs)
        {
            Debug.Log($"=== ITEM EVOLUTION FÜR ALLE ITEMS ABGESCHLOSSEN ===");
        }
    }
    
    // VEREINFACHTE WAGON-SPEZIFISCHE ITEM-EVOLUTION
    private void UpdateItemVisibilityForSpecificWagon(int wagonNumber, int currentLoop)
    {
        if (!cacheInitialized) return;
        
        if (showDebugLogs)
        {
            Debug.Log($"=== ITEM EVOLUTION FÜR WAGON {wagonNumber} (Loop {currentLoop}) ===");
        }
        
        // NEUES EINFACHES SYSTEM: Verwende die alten, funktionierenden Methoden
        // Hole alle Items für diesen Wagon
        var wagonItems = GetItemsForWagon(wagonNumber);
        
        // Gruppiere nach Basis-ID (wie im alten System)
        var itemGroups = GroupItemsByBaseId(wagonItems);
        
        // Für jede Gruppe: Finde bestes Item und aktiviere es
        foreach (var group in itemGroups)
        {
            string baseId = group.Key;
            var itemsInGroup = group.Value;
            
            if (showDebugLogs)
            {
                Debug.Log($"Evolution für Item-Gruppe '{baseId}' mit {itemsInGroup.Count} Items auf Wagon {wagonNumber}");
            }
            
            // Verwende die alte, funktionierende Methode
            ItemInteractable activeItem = FindCorrectItemForCurrentState(itemsInGroup, currentLoop);
            
            // Aktiviere das richtige Item und deaktiviere die anderen
            UpdateItemGroupVisibility(itemsInGroup, activeItem);
        }
        
        if (showDebugLogs)
        {
            Debug.Log($"=== ITEM EVOLUTION FÜR WAGON {wagonNumber} ABGESCHLOSSEN ===");
        }
    }
    
    // Neue Methode: Hole alle Items für einen bestimmten Wagon
    private System.Collections.Generic.List<ItemInteractable> GetItemsForWagon(int wagonNumber)
    {
        var wagonItems = new System.Collections.Generic.List<ItemInteractable>();
        
        if (cachedItemInteractables == null) return wagonItems;
        
        foreach (var item in cachedItemInteractables)
        {
            int itemWagonNumber = GetWagonNumberFromGameObject(item.gameObject);
            if (itemWagonNumber == wagonNumber)
            {
                wagonItems.Add(item);
            }
        }
        
        if (showDebugLogs)
        {
            Debug.Log($"Gefunden: {wagonItems.Count} Items auf Wagon {wagonNumber}");
            foreach (var item in wagonItems)
            {
                Debug.Log($"  - '{item.itemId}' auf GameObject '{item.name}'");
            }
        }
        
        return wagonItems;
    }
    
    // Hilfsmethode: Erstes Zeichen groß schreiben (z.B. "mirrow1" → "Mirrow1")
    private string CapitalizeFirstLetter(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;
        
        if (input.Length == 1)
            return input.ToUpper();
        
        return input.Substring(0, 1).ToUpper() + input.Substring(1);
    }
    
    // Führe Item Evolution durch (wird nur bei Loop Count Änderung aufgerufen)
    private void PerformItemEvolution(int currentLoop)
    {
        // Hole alle verfügbaren Items (mit Caching)
        var allItems = GetAllItemInteractablesCached();
        
        // Gruppiere Items nach Basis-ID (mit Caching)
        var itemGroups = GetItemGroupsCached(allItems);
        
        foreach (var group in itemGroups)
        {
            string baseId = group.Key;
            var itemsInGroup = group.Value;
            
            if (showDebugLogs)
            {
                Debug.Log($"ItemDialogTrigger: Evolution für Item-Gruppe '{baseId}' mit {itemsInGroup.Count} Items (Loop {currentLoop})");
            }
            
            // Finde das richtige Item für den aktuellen Zustand
            ItemInteractable activeItem = FindCorrectItemForCurrentState(itemsInGroup, currentLoop);
            
            // Aktiviere das richtige Item und deaktiviere die anderen
            UpdateItemGroupVisibility(itemsInGroup, activeItem);
        }
    }
    
    // Gecachte Version von GetAllItemInteractables - verwendet den Cache vom Start
    private System.Collections.Generic.List<ItemInteractable> GetAllItemInteractablesCached()
    {
        // Stelle sicher dass Cache initialisiert ist
        if (!cacheInitialized)
        {
            InitializeCache();
        }
        
        return cachedItemInteractables;
    }
    
    // Gecachte Version von GroupItemsByBaseId - verwendet den Cache vom Start
    private System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<ItemInteractable>> GetItemGroupsCached(System.Collections.Generic.List<ItemInteractable> items)
    {
        // Stelle sicher dass Cache initialisiert ist
        if (!cacheInitialized)
        {
            InitializeCache();
        }
        
        return cachedItemGroups;
    }
    
    // Hole alle ItemInteractable Components
    private System.Collections.Generic.List<ItemInteractable> GetAllItemInteractables()
    {
        var allItems = new System.Collections.Generic.List<ItemInteractable>();
        var itemInteractables = FindObjectsByType<ItemInteractable>(FindObjectsSortMode.None);
        
        // Erst alle Items mit _ sammeln und ihre Basis-IDs ermitteln
        var baseIdsWithEvolution = new System.Collections.Generic.HashSet<string>();
        
        foreach (var item in itemInteractables)
        {
            // Ignoriere Loop-Items
            if (item.itemId.StartsWith("loop"))
                continue;
                
            // Sammle Basis-IDs von Items mit _
            if (item.itemId.Contains("_"))
            {
                string baseId = GetBaseId(item.itemId);
                baseIdsWithEvolution.Add(baseId);
            }
        }
        
        // Jetzt alle Items sammeln, die entweder _ haben oder deren Basis-ID evolutionäre Verwandte hat
        foreach (var item in itemInteractables)
        {
            // Ignoriere Loop-Items
            if (item.itemId.StartsWith("loop"))
                continue;
                
            if (item.itemId.Contains("_"))
            {
                // Items mit _ immer hinzufügen
                allItems.Add(item);
            }
            else
            {
                // Items ohne _ nur hinzufügen, wenn es evolutionäre Verwandte gibt
                if (baseIdsWithEvolution.Contains(item.itemId))
                {
                    allItems.Add(item);
                    if (showDebugLogs)
                    {
                        Debug.Log($"ItemDialogTrigger: Item '{item.itemId}' ohne _ hinzugefügt, da evolutionäre Verwandte existieren");
                    }
                }
            }
        }
        
        return allItems;
    }
    
    // Gruppiere Items nach Basis-ID
    private System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<ItemInteractable>> GroupItemsByBaseId(System.Collections.Generic.List<ItemInteractable> items)
    {
        var groups = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<ItemInteractable>>();
        
        foreach (var item in items)
        {
            string baseId = GetBaseId(item.itemId);
            
            if (!groups.ContainsKey(baseId))
            {
                groups[baseId] = new System.Collections.Generic.List<ItemInteractable>();
            }
            
            groups[baseId].Add(item);
        }
        
        return groups;
    }
    
    // Extrahiere Basis-ID (alles vor dem ersten _ oder die ganze ID falls kein _)
    private string GetBaseId(string itemId)
    {
        int underscoreIndex = itemId.IndexOf('_');
        if (underscoreIndex > 0)
        {
            return itemId.Substring(0, underscoreIndex);
        }
        return itemId; // Falls kein _ gefunden, verwende die ganze ID als Basis-ID
    }
    
    // Finde das richtige Item für den aktuellen Zustand
    private ItemInteractable FindCorrectItemForCurrentState(System.Collections.Generic.List<ItemInteractable> itemsInGroup, int currentLoop)
    {
        ItemInteractable bestMatch = null;
        int highestValidMinLoop = -1; // Starte mit -1 um Items ohne Dialog (minLoop 0) zu berücksichtigen
        
        if (showDebugLogs)
        {
            Debug.Log($"=== Finde korrektes Item für Gruppe (CurrentLoop: {currentLoop}) ===");
            foreach (var item in itemsInGroup)
            {
                Debug.Log($"  Item: '{item.itemId}' auf GameObject '{item.name}'");
            }
        }
        
        foreach (var item in itemsInGroup)
        {
            var dialogs = GameManager.SafeGetDialogsForItem(item.itemId);
            
            if (dialogs != null && dialogs.Count > 0)
            {
                var dialog = dialogs[0];
                
                if (showDebugLogs)
                {
                    Debug.Log($"  Prüfe '{item.itemId}': minLoop={dialog.minLoop}, requiredMemory={string.Join(",", dialog.requiredMemory ?? new System.Collections.Generic.List<string>())}");
                }
                
                // Prüfe minLoop Bedingung: Item ist verfügbar wenn currentLoop >= minLoop
                if (dialog.minLoop <= currentLoop)
                {
                    // Prüfe requiredMemory Bedingungen
                    if (CheckMemoryRequirements(dialog.requiredMemory))
                    {
                        // Dieses Item erfüllt alle Bedingungen
                        // Wähle das Item mit dem HÖCHSTEN minLoop das noch <= currentLoop ist
                        if (dialog.minLoop > highestValidMinLoop)
                        {
                            bestMatch = item;
                            highestValidMinLoop = dialog.minLoop;
                            
                            if (showDebugLogs)
                            {
                                Debug.Log($"    ✅ NEUE BESTE WAHL: '{item.itemId}' (minLoop: {dialog.minLoop})");
                            }
                        }
                        else if (showDebugLogs)
                        {
                            Debug.Log($"    ⚪ Gültig aber nicht besser: '{item.itemId}' (minLoop: {dialog.minLoop} <= aktuell beste: {highestValidMinLoop})");
                        }
                    }
                    else if (showDebugLogs)
                    {
                        Debug.Log($"    ❌ Memory-Anforderungen nicht erfüllt: '{item.itemId}'");
                    }
                }
                else if (showDebugLogs)
                {
                    Debug.Log($"    ❌ minLoop zu hoch: '{item.itemId}' (braucht {dialog.minLoop}, aktuell {currentLoop})");
                }
            }
            else
            {
                // Item ohne Dialog - verwende als Fallback wenn kein anderes Item gefunden wird
                if (bestMatch == null && highestValidMinLoop == -1)
                {
                    bestMatch = item;
                    highestValidMinLoop = 0; // Fallback hat Priorität 0
                    
                    if (showDebugLogs)
                    {
                        Debug.Log($"    ⚪ FALLBACK (kein Dialog): '{item.itemId}'");
                    }
                }
                else if (showDebugLogs)
                {
                    Debug.Log($"    ⚪ Kein Dialog, aber bereits bessere Wahl vorhanden: '{item.itemId}'");
                }
            }
        }
        
        if (showDebugLogs)
        {
            if (bestMatch != null)
            {
                Debug.Log($"=== FINALE WAHL: '{bestMatch.itemId}' (minLoop: {highestValidMinLoop}) ===");
            }
            else
            {
                Debug.Log($"=== KEINE GÜLTIGE WAHL GEFUNDEN ===");
            }
        }
        
        return bestMatch;
    }
    
    // VEREINFACHTE Memory-Prüfung
    private bool CheckMemoryRequirements(System.Collections.Generic.List<string> requiredMemory)
    {
        if (requiredMemory == null || requiredMemory.Count == 0) return true;
        
        foreach (string memory in requiredMemory)
        {
            if (!GameManager.SafeHasMemory(memory)) return false;
        }
        
        return true;
    }
    
    // Update Visibility der Item-Gruppe
    private void UpdateItemGroupVisibility(System.Collections.Generic.List<ItemInteractable> itemsInGroup, ItemInteractable activeItem)
    {
        if (showDebugLogs)
        {
            string activeItemName = activeItem != null ? activeItem.itemId : "KEIN ITEM";
            Debug.Log($"=== UPDATE SICHTBARKEIT für {itemsInGroup.Count} Items ===");
            Debug.Log($"Aktives Item: {activeItemName}");
        }
        
        foreach (var item in itemsInGroup)
        {
            var image = item.GetComponent<UnityEngine.UI.Image>();
            if (image != null)
            {
                bool shouldBeActive = (item == activeItem);
                bool currentlyActive = image.enabled;
                
                if (showDebugLogs)
                {
                    Debug.Log($"  Item '{item.itemId}': aktuell={currentlyActive}, soll={shouldBeActive}");
                }
                
                // Nur ändern wenn nötig
                if (currentlyActive != shouldBeActive)
                {
                    image.enabled = shouldBeActive;
                    
                    if (showDebugLogs)
                    {
                        if (shouldBeActive)
                        {
                            Debug.Log($"    ✅ AKTIVIERT: '{item.itemId}' Image auf GameObject '{item.name}'");
                        }
                        else
                        {
                            Debug.Log($"    ❌ DEAKTIVIERT: '{item.itemId}' Image auf GameObject '{item.name}'");
                        }
                    }
                }
                else if (showDebugLogs)
                {
                    Debug.Log($"    ⚪ UNVERÄNDERT: '{item.itemId}' bleibt {(currentlyActive ? "aktiv" : "inaktiv")}");
                }
            }
            else
            {
                if (showDebugLogs)
                {
                    Debug.LogWarning($"    ⚠️ KEIN IMAGE: Item '{item.itemId}' auf GameObject '{item.name}' hat keine Image-Component!");
                }
            }
        }
        
        if (showDebugLogs)
        {
            Debug.Log($"=== SICHTBARKEITS-UPDATE ABGESCHLOSSEN ===");
        }
    }
    
    // === KOMPATIBILITÄTS-METHODEN FÜR WagonDoorWithDialog ===
    
    // Cache invalidieren (falls Items zur Laufzeit hinzugefügt/entfernt werden)
    public void InvalidateCache()
    {
        cachedItemInteractables = null;
        cachedItemGroups = null;
        cacheInitialized = false;
        
        if (showDebugLogs)
        {
            Debug.Log("ItemDialogTrigger: Cache invalidiert - wird bei nächster Nutzung neu erstellt");
        }
    }
    
    // Cache komplett neu erstellen (mit Canvas-Koordination)
    public void RecreateCache()
    {
        if (showDebugLogs)
        {
            Debug.Log("ItemDialogTrigger: Starte komplette Cache-Neuerstellung");
        }
        
        StartCoroutine(RecreateeCacheWithAllCanvases());
    }
    
    // Cache neu erstellen mit Canvas-Koordination
    private System.Collections.IEnumerator RecreateeCacheWithAllCanvases()
    {
        // Cache invalidieren
        InvalidateCache();
        
        // Koordinierte Neuerstellung wie beim Start
        var wagonManager = WagonManager.Instance;
        if (wagonManager != null)
        {
            wagonManager.ActivateAllCanvasesForCaching();
            yield return null;
            
            InitializeCache();
            
            wagonManager.RestoreNormalCanvasState();
            
            if (showDebugLogs)
            {
                Debug.Log("ItemDialogTrigger: Cache erfolgreich neu erstellt");
            }
        }
        else
        {
            // Fallback ohne Canvas-Koordination
            InitializeCache();
        }
    }
    
    // Manuell Item Evolution forcieren (für Debugging/Fallback)
    public void ForceItemEvolution()
    {
        int currentLoop = GameManager.SafeGetCurrentLoop();
        
        if (showDebugLogs)
        {
            Debug.Log($"ItemDialogTrigger: MANUELL Item Evolution forciert (Loop {currentLoop}) - Bypass Wagon Transition System");
        }
        
        // Aktualisiere Sichtbarkeit ohne Cache zu invalidieren
        UpdateItemVisibilityForCurrentLoop(currentLoop);
        
        // Loop Tracker aktualisieren
        lastEvolutionLoop = currentLoop;
    }
    
    // Zeige aktuelles System-Status für Debugging
    public void DebugShowEvolutionSystem()
    {
        Debug.Log("=== ITEM EVOLUTION SYSTEM STATUS ===");
        Debug.Log($"System: Event-basiert (Wagon Transition Complete)");
        Debug.Log($"Performance: Nur bei Wagon-Wechsel, nicht jeden Frame");
        Debug.Log($"Sichtbarkeit: Items werden während Übergangs-Canvas getauscht (unsichtbar)");
        Debug.Log($"Cache: Einmalig beim Start mit allen Canvases erstellt");
        Debug.Log($"lastEvolutionLoop: {lastEvolutionLoop}");
        Debug.Log($"Aktueller Loop: {GameManager.SafeGetCurrentLoop()}");
        
        var wagonManager = WagonManager.Instance;
        if (wagonManager != null)
        {
            Debug.Log($"WagonManager: Verbunden, Event-System aktiv");
            Debug.Log($"Aktueller Wagon: {wagonManager.GetCurrentWagon()}");
            Debug.Log($"Transition aktiv: {wagonManager.IsTransitioning()}");
        }
        else
        {
            Debug.Log($"WagonManager: NICHT VERBUNDEN - Fallback-System aktiv");
        }
    }
    
    // Debug: Zeige verfügbare Items (VEREINFACHT)
    public void DebugShowHierarchy()
    {
        Debug.Log($"=== VERFÜGBARE GAMEOBJECTS MIT ITEMINTERACTABLE ===");
        var allItemInteractables = FindObjectsByType<ItemInteractable>(FindObjectsSortMode.None);
        foreach (var item in allItemInteractables)
        {
            Debug.Log($"  GameObject '{item.gameObject.name}' → itemId '{item.itemId}' (Canvas: {GetCanvasName(item)})");
        }
        
        if (cachedItemGroups != null)
        {
            Debug.Log($"\n=== ITEM GRUPPEN ({cachedItemGroups.Count} Basis-IDs) ===");
            foreach (var group in cachedItemGroups)
            {
                Debug.Log($"Gruppe '{group.Key}': {group.Value.Count} Items");
                foreach (var item in group.Value)
                {
                    Debug.Log($"  - '{item.itemId}' auf GameObject '{item.name}'");
                }
            }
        }
    }
    
    // Debug: Zeige Door Management System Status
    public void DebugShowDoorManagementSystem()
    {
        Debug.Log("=== DOOR MANAGEMENT SYSTEM STATUS ===");
        Debug.Log($"Door Closed zugewiesen: {doorClosedObj != null}");
        Debug.Log($"Door 2 zugewiesen: {door2 != null}");
        Debug.Log($"Canvas Übergang zugewiesen: {canvasUebergang != null}");
        
        if (doorClosedObj != null)
        {
            Debug.Log($"Door Closed Name: '{doorClosedObj.name}'");
            Debug.Log($"Door Closed Status: {(doorClosedObj.activeInHierarchy ? "aktiv" : "inaktiv")}");
        }
        
        if (door2 != null)
        {
            Debug.Log($"Door 2 Name: '{door2.name}'");
            Debug.Log($"Door 2 Status: {(door2.activeInHierarchy ? "aktiv" : "inaktiv")}");
        }
        
        if (canvasUebergang != null)
        {
            Debug.Log($"Canvas Übergang Name: '{canvasUebergang.name}'");
            Debug.Log($"Canvas Übergang Status: {(canvasUebergang.activeInHierarchy ? "aktiv" : "inaktiv")}");
            Debug.Log($"Canvas Übergang dauerhaft aktiviert: {canvasUebergangActivated}");
        }
        
        int currentLoop = GameManager.SafeGetCurrentLoop();
        bool hasThomasisMemory = GameManager.SafeHasMemory("i_am_thomasen");
        
        Debug.Log($"Aktueller Loop: {currentLoop}");
        Debug.Log($"Memory 'i_am_thomasen': {(hasThomasisMemory ? "verfügbar" : "nicht verfügbar")}");
        Debug.Log($"Door System bereits geprüft: {doorSystemChecked}");
        Debug.Log($"Bedingungen erfüllt: {(currentLoop >= 10 && hasThomasisMemory ? "JA" : "NEIN")}");
        Debug.Log($"Door Management System läuft ab Loop 10: {(currentLoop >= 10 ? "JA (jeden Frame)" : "NEIN")}");
        
        if (currentLoop >= 10 && hasThomasisMemory && !doorSystemChecked)
        {
            Debug.Log("⚠️ BEDINGUNGEN ERFÜLLT - Door System sollte beim nächsten Update aktiviert werden!");
        }
        else if (currentLoop < 10)
        {
            Debug.Log($"⏳ Door System inaktiv - Warte auf Loop 10 (aktuell: {currentLoop})");
        }
        else if (!hasThomasisMemory)
        {
            Debug.Log($"⏳ Warte auf Memory 'i_am_thomasen'");
        }
        else if (doorSystemChecked)
        {
            Debug.Log($"✅ Door System läuft kontinuierlich ab Loop 10");
        }
    }
    
    // Manuell Door Management System ausführen (für Debugging)
    public void ForceDoorManagementSystem()
    {
        if (doorClosedObj == null || door2 == null)
        {
            Debug.LogWarning("ItemDialogTrigger: Kann Door Management System nicht forcieren - Door Closed oder Door 2 nicht zugewiesen!");
            return;
        }
        
        if (showDebugLogs)
        {
            Debug.Log("ItemDialogTrigger: MANUELL Door Management System forciert (Bypass-Bedingungen)");
        }
        
        doorClosedObj.SetActive(false);
        door2.SetActive(true);
        doorSystemChecked = true;
        
        if (showDebugLogs)
        {
            Debug.Log($"🚪 FORCIERT: Door Closed deaktiviert, Door 2 aktiviert");
        }
    }
    
    // Reset Door Management System (für Debugging)
    public void ResetDoorManagementSystem()
    {
        doorSystemChecked = false;
        
        if (showDebugLogs)
        {
            Debug.Log("ItemDialogTrigger: Door Management System zurückgesetzt - wird bei nächster Prüfung neu evaluiert");
        }
    }
    
    // Debug: Zeige Cache-Inhalt
    public void DebugShowCache()
    {
        Debug.Log($"ItemDialogTrigger: DEBUG - Cache Status:");
        Debug.Log($"  cacheInitialized: {cacheInitialized}");
        Debug.Log($"  lastEvolutionLoop: {lastEvolutionLoop}");
        Debug.Log($"  cachedItemInteractables: {(cachedItemInteractables != null ? cachedItemInteractables.Count.ToString() : "null")}");
        Debug.Log($"  cachedItemGroups: {(cachedItemGroups != null ? cachedItemGroups.Count.ToString() : "null")}");
        Debug.Log($"  Aktueller Loop: {GameManager.SafeGetCurrentLoop()}");
        
        if (cachedItemInteractables != null)
        {
            foreach (var item in cachedItemInteractables)
            {
                Debug.Log($"    Cached Item: {item.itemId} auf GameObject '{item.name}'");
            }
        }
        
        if (cachedItemGroups != null)
        {
            foreach (var group in cachedItemGroups)
            {
                Debug.Log($"    Gruppe '{group.Key}': {group.Value.Count} Items");
                foreach (var item in group.Value)
                {
                    Debug.Log($"      - '{item.itemId}' auf GameObject '{item.name}'");
                }
            }
        }
    }
    
    // Für WagonDoorWithDialog Kompatibilität
    public void OnItemClicked()
    {
        if (showDebugLogs)
        {
            Debug.Log($"ItemDialogTrigger: OnItemClicked() aufgerufen für itemId '{itemId}'");
        }
        
        if (!string.IsNullOrEmpty(itemId))
        {
            TriggerItemDialog(itemId);
            dialogWasShown = true;
        }
    }
    
    // Für WagonDoorWithDialog Kompatibilität
    public bool WasDialogShown()
    {
        return dialogWasShown;
    }
    
    // Für WagonDoorWithDialog Kompatibilität
    public void ResetDialogState()
    {
        dialogWasShown = false;
        if (showDebugLogs)
        {
            Debug.Log($"ItemDialogTrigger: Dialog-Status zurückgesetzt für itemId '{itemId}'");
        }
    }

    // Diese Methode nach Dialog-Ende aufrufen
    public void StartSpiegelTransition()
    {
        if (transitionCanvas != null)
        {
            StartCoroutine(SpiegelTransitionCoroutine());
        }
    }

    private System.Collections.IEnumerator SpiegelTransitionCoroutine()
    {
        transitionCanvas.SetActive(true);
        Debug.Log("Übergangs-Canvas aktiviert (Spiegel Event)");
        yield return new WaitForSeconds(Random.Range(1f, 3f));
        transitionCanvas.SetActive(false);
        Debug.Log("Übergangs-Canvas deaktiviert (Spiegel Event)");
    }
}
