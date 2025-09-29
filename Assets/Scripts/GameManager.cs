using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
using System.Collections;

// Memory-Mapping-Datenstrukturen
[System.Serializable]
public class MemorySource
{
    public string memoryId;        // Welches Memory wird hinzugefügt
    public string csvFileName;     // In welcher CSV-Datei
    public string dialogMemoryId;  // MemoryId des Dialogs
    public int minLoop;           // Ab welchem Loop verfügbar
    public SourceType sourceType; // Dialog oder Choice
    public int choiceIndex;       // Bei Choices: welche Choice (1, 2, 3)
    
    public enum SourceType
    {
        Dialog,
        Choice
    }
    
    public override string ToString()
    {
        if (sourceType == SourceType.Choice)
            return $"CSV:{csvFileName} | Dialog:{dialogMemoryId} | Choice{choiceIndex} | Loop:{minLoop}";
        else
            return $"CSV:{csvFileName} | Dialog:{dialogMemoryId} | Loop:{minLoop}";
    }
}

[System.Serializable]
public class MemoryUsage
{
    public string requiredMemory;  // Welches Memory wird benötigt
    public string csvFileName;     // In welcher CSV-Datei
    public string dialogMemoryId;  // MemoryId des Dialogs
    public int minLoop;           // Ab welchem Loop verfügbar
    public UsageType usageType;   // Dialog oder Choice
    public int choiceIndex;       // Bei Choices: welche Choice (1, 2, 3)
    
    public enum UsageType
    {
        Dialog,
        Choice
    }
    
    public override string ToString()
    {
        if (usageType == UsageType.Choice)
            return $"CSV:{csvFileName} | Dialog:{dialogMemoryId} | Choice{choiceIndex} | Loop:{minLoop}";
        else
            return $"CSV:{csvFileName} | Dialog:{dialogMemoryId} | Loop:{minLoop}";
    }
}

[System.Serializable]
public class NPCData
{
    public string npcId;           // Eindeutige NPC-ID
    public string npcName;         // Anzeigename
    public int csvFileIndex;       // Index im dialogCSVFiles Array (0, 1, 2, ...)
}

[System.Serializable]
public class ItemData
{
    public string itemId;          // Eindeutige Item-ID = memoryId aus CSV (z.B. "mirrow_broken")
    // itemName entfernt - Items brauchen keinen Namen als Sprecher
    public int csvFileIndex;       // Index im dialogCSVFiles Array (0, 1, 2, ...)
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    
    [Header("Notebook Image Change")]
    [Tooltip("GameObject, dessen Bild in Loop 2 gewechselt wird")]
    public GameObject notebookImageTarget;
    [Tooltip("Neues Sprite für das Notebook-Objekt in Loop 2")]
    public Sprite notebookNewSprite;

    [Header("Game State")]
    public int currentLoopCount = 1; // Aktueller Spielloop

        [Header("Sternschnuppen-Nacht System")]
        [Tooltip("GameObject für Sternschnuppen-Nacht (im Inspector zuweisen)")]
        public GameObject sternschnuppenNacht;
        [Tooltip("Min Loop für Sternschnuppen-Nacht")]
        public int minLoop = 1;
        [Tooltip("Max Loop für Sternschnuppen-Nacht")]
        public int maxLoop = 3;

        private int triggerLoop = -1;
        private bool sternschnuppenNachtTriggered = false;
        private bool sternschnuppenNachtDeactivated = false;
    
    [Header("Player Settings")]
    // === PLAYER DATA ===
    public string playerName = "Reisender"; // Name des Spielers (wird später in der Story enthüllt)
    
    [Header("Dialog System")]
    public TextAsset[] dialogCSVFiles; // CSV-Dateien aus Assets/DialogeCSV im Inspector zuweisen
    
    [Header("NPC System")]
    public NPCData[] npcs; // NPCs und ihre CSV-Zuordnungen im Inspector definieren
    
    [Header("Item System")]
    public ItemData[] items; // Items und ihre CSV-Zuordnungen im Inspector definieren
    
    [Header("Window2 Image System")]
    [Tooltip("Window2 GameObject (muss Image Component haben)")]
    public GameObject window2GameObject;
    
    [Tooltip("Canvas Wagon 3 GameObject (zum Prüfen ob Wagon 3 aktiv ist)")]
    public GameObject canvasWagon3;
    
    [Tooltip("Canvas Übergang GameObject (Countdown startet erst wenn NICHT aktiv)")]
    public GameObject canvasUebergang;
    
    [Tooltip("Das neue Sprite das angezeigt werden soll")]
    public GameObject newWindow2ImageObject;
    
    [Tooltip("Zeit in Sekunden bis das Image gewechselt wird")]
    public float window2SwitchDelay = 3f;
    
    [Tooltip("Soll das EventSystem während der Wartezeit deaktiviert werden?")]
    public bool disableEventSystemDuringWindow2Switch = true;
    
    [Tooltip("AudioSource mit Countdown-Sound (optional - AudioClip direkt in der AudioSource setzen)")]
    public AudioSource window2CountdownAudioSource;
    
    [Tooltip("AudioSource für Hintergrundmusik VOR Window2-System (Play on Awake + Loop, stoppt bei Countdown-Start)")]
    public AudioSource preWindow2BackgroundMusicAudioSource;
    
    [Tooltip("AudioSource für Hintergrundmusik nach Countdown (loopt dauerhaft)")]
    public AudioSource backgroundMusicAudioSource;
    
    [Tooltip("Canvas Hauptmenü GameObject (Hintergrundmusik stoppt wenn aktiv)")]
    public GameObject canvasHauptmenu;
    
    [Header("Auto Dialog System")]
    [Tooltip("ItemInteractable GameObject das nach Window2-Wechsel automatisch angeklickt werden soll")]
    public GameObject autoClickItemAfterWindow2;
    [Tooltip("Verzögerung in Sekunden nach EventSystem-Aktivierung bevor Auto-Click ausgeführt wird")]
    public float autoClickDelay = 0.5f;
    
    [Header("Debug")]
    public bool showDebugLogs = true;
    
    // Memory-System
    private HashSet<string> memoryFlags = new HashSet<string>();
    
    // Loop-Progression-System
    private HashSet<string> memoriesFoundInCurrentLoop = new HashSet<string>(); // Memories die in diesem Loop gefunden wurden
    private Dictionary<int, HashSet<string>> requiredMemoriesPerLoop = new Dictionary<int, HashSet<string>>(); // Welche Memories pro Loop verfügbar sind
    
    // Memory-Mapping-System
    private HashSet<string> allAvailableMemories = new HashSet<string>(); // Alle verfügbaren AddMemories aus allen CSVs
    private HashSet<string> allRequiredMemories = new HashSet<string>(); // Alle RequiredMemories aus allen CSVs
    private Dictionary<string, HashSet<string>> memoryDependencies = new Dictionary<string, HashSet<string>>(); // RequiredMemory -> welche AddMemories es erfüllen können
    private Dictionary<string, List<MemorySource>> addMemorySources = new Dictionary<string, List<MemorySource>>(); // AddMemory -> wo es gefunden werden kann
    private Dictionary<string, List<MemoryUsage>> requiredMemoryUsages = new Dictionary<string, List<MemoryUsage>>(); // RequiredMemory -> wo es verwendet wird
    
    // Loop-Progression-spezifische Sammlungen
    private HashSet<string> notebookOnlyMemories = new HashSet<string>(); // RequiredMemories die nur im Notizbuch verwendet werden
    private HashSet<string> loopRelevantAddMemories = new HashSet<string>(); // AddMemories die für Loop-Progression wichtig sind
    
    // Dialog-System
    private Dictionary<string, List<DialogLine>> allDialogs = new Dictionary<string, List<DialogLine>>();
    
    // NPC-System
    private Dictionary<string, NPCData> npcRegistry = new Dictionary<string, NPCData>();
    
    // Item-System
    private Dictionary<string, ItemData> itemRegistry = new Dictionary<string, ItemData>();
    
    // Window2 Image System  
    private Sprite originalWindow2Sprite;
    private EventSystem eventSystem;
    private bool hasWindow2Switched = false;
    private bool isWindow2Switching = false;
    private bool window2SystemInitialized = false;
    private bool backgroundMusicStarted = false;

    // Inspector-Variablen für das Bildwechsel-Feature
    [Header("StrangerChange")]
    public Image targetImageObject;
    public Sprite imageForLoop6;
    public Sprite imageForLoop9;
    
    [Header("Loop-abhängige Objekte deaktivieren")]
    public GameObject objectToDeactivateInLoop6;
    public GameObject objectToDeactivateInLoop11;
    
    private void Awake()
    {
        // Singleton Pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadAllDialogCSVs();
            RegisterAllNPCs();
            RegisterAllItems();
            InitializeLoopProgressionSystem();
            InitializeWindow2System();
                InitializeSternschnuppenNacht();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    // NPC-Registry aufbauen
    private void RegisterAllNPCs()
    {
        if (npcs == null) return;
        
        foreach (var npc in npcs)
        {
            if (!string.IsNullOrEmpty(npc.npcId))
            {
                npcRegistry[npc.npcId] = npc;
                string csvName = GetCSVFileNameByIndex(npc.csvFileIndex);
                Debug.Log($"NPC registriert: {npc.npcName} ({npc.npcId}) -> CSV Index {npc.csvFileIndex} ({csvName})");
            }
        }
    }
    
    // Item-Registry aufbauen
    private void RegisterAllItems()
    {
        if (items == null) return;
        
        foreach (var item in items)
        {
            if (!string.IsNullOrEmpty(item.itemId))
            {
                itemRegistry[item.itemId] = item;
                string csvName = GetCSVFileNameByIndex(item.csvFileIndex);
                Debug.Log($"Item registriert: {item.itemId} -> CSV Index {item.csvFileIndex} ({csvName})");
            }
        }
    }
    
    // Window2 Image System initialisieren
    private void InitializeWindow2System()
    {
        if (showDebugLogs)
        {
            Debug.Log("=== INITIALISIERE WINDOW2 IMAGE SYSTEM ===");
        }
        
        // Validierung
        if (window2GameObject == null)
        {
            if (showDebugLogs)
            {
                Debug.LogWarning("Window2 System: Kein Window2 GameObject zugewiesen!");
            }
            return;
        }
        
        if (canvasWagon3 == null)
        {
            if (showDebugLogs)
            {
                Debug.LogWarning("Window2 System: Kein CanvasWagon3 GameObject zugewiesen!");
            }
            return;
        }
        
        if (canvasUebergang == null)
        {
            if (showDebugLogs)
            {
                Debug.LogWarning("Window2 System: Kein CanvasÜbergang GameObject zugewiesen!");
            }
            return;
        }
        
        if (newWindow2ImageObject == null)
        {
            if (showDebugLogs)
            {
                Debug.LogWarning("Window2 System: Kein neues Image-Objekt zugewiesen!");
            }
            return;
        }
        
        // Image Component vom Window2 GameObject holen
        var window2Image = window2GameObject.GetComponent<Image>();
        if (window2Image == null)
        {
            if (showDebugLogs)
            {
                Debug.LogWarning("Window2 System: Window2 GameObject hat keine Image Component!");
            }
            return;
        }
        
        // Original Sprite speichern
        originalWindow2Sprite = window2Image.sprite;
        
        // EventSystem finden
        eventSystem = EventSystem.current;
        
        window2SystemInitialized = true;
        
        if (showDebugLogs)
        {
            Debug.Log($"✅ WINDOW2 SYSTEM INITIALISIERT");
            Debug.Log($"Window2 GameObject: {window2GameObject.name}");
            Debug.Log($"CanvasWagon3: {canvasWagon3.name}");
            Debug.Log($"CanvasÜbergang: {canvasUebergang.name}");
            Debug.Log($"Original Sprite: {(originalWindow2Sprite != null ? originalWindow2Sprite.name : "null")}");
            Debug.Log($"Neues Sprite: {newWindow2ImageObject.name}");
            Debug.Log($"Switch Delay: {window2SwitchDelay} Sekunden");
            Debug.Log($"EventSystem deaktivieren: {disableEventSystemDuringWindow2Switch}");
            Debug.Log($"Countdown AudioSource: {(window2CountdownAudioSource != null ? window2CountdownAudioSource.name : "nicht zugewiesen")}");
            Debug.Log($"Pre-Window2 Background Music AudioSource: {(preWindow2BackgroundMusicAudioSource != null ? preWindow2BackgroundMusicAudioSource.name : "nicht zugewiesen")}");
            Debug.Log($"Background Music AudioSource: {(backgroundMusicAudioSource != null ? backgroundMusicAudioSource.name : "nicht zugewiesen")}");
            Debug.Log($"Canvas Hauptmenü: {(canvasHauptmenu != null ? canvasHauptmenu.name : "nicht zugewiesen")}");
            Debug.Log($"Auto-Click Item: {(autoClickItemAfterWindow2 != null ? autoClickItemAfterWindow2.name : "nicht zugewiesen")}");
            Debug.Log($"Auto-Click Delay: {autoClickDelay} Sekunden");
        }
    }
    
    private void Update()
    {
        // Window2 Image System überwachen
        if (window2SystemInitialized)
        {
            CheckWindow2ImageSwitch();
            CheckBackgroundMusicState();
        }

            CheckSternschnuppenNacht();
    }
    // Initialisiert das Sternschnuppen-Nacht-System
    private void InitializeSternschnuppenNacht()
    {
        sternschnuppenNachtTriggered = false;
        sternschnuppenNachtDeactivated = false;
        if (sternschnuppenNacht != null)
        {
            sternschnuppenNacht.SetActive(false);
        }
        // Zufallszahl für Trigger bestimmen
        triggerLoop = UnityEngine.Random.Range(minLoop, maxLoop + 1);
        if (showDebugLogs)
        {
            Debug.Log($"[SternschnuppenNacht] TriggerLoop gewählt: {triggerLoop} (min: {minLoop}, max: {maxLoop})");
        }
    }

    // Überwacht und steuert die Aktivierung/Deaktivierung der Sternschnuppen-Nacht
    private void CheckSternschnuppenNacht()
    {
        if (sternschnuppenNacht == null || sternschnuppenNachtDeactivated) return;

        if (!sternschnuppenNachtTriggered && currentLoopCount == triggerLoop)
        {
            sternschnuppenNacht.SetActive(true);
            sternschnuppenNachtTriggered = true;
            if (showDebugLogs)
            {
                Debug.Log($"[SternschnuppenNacht] Aktiviert bei Loop {currentLoopCount}");
            }
        }
        else if (sternschnuppenNachtTriggered && currentLoopCount > triggerLoop)
        {
            sternschnuppenNacht.SetActive(false);
            sternschnuppenNachtDeactivated = true;
            if (showDebugLogs)
            {
                Debug.Log($"[SternschnuppenNacht] Deaktiviert bei Loop {currentLoopCount}");
            }
        }
    }
    
    // Prüfe ob Window2 Image gewechselt werden soll
    private void CheckWindow2ImageSwitch()
    {
        // Bereits gewechselt?
        if (hasWindow2Switched)
            return;
            
        // Ist CanvasÜbergang aktiv? (Übergang muss aktiv sein)
        if (canvasUebergang == null || !canvasUebergang.activeInHierarchy)
            return;
            
        // Ist CanvasWagon3 aktiv? (Spieler muss in Wagon 3 sein)
        if (canvasWagon3 == null || !canvasWagon3.activeInHierarchy)
            return;
            
        // Ist Window2 GameObject aktiv?
        if (window2GameObject == null || !window2GameObject.activeInHierarchy)
            return;
            
        // Hat Window2 eine Image Component und ist diese aktiviert?
        var window2Image = window2GameObject.GetComponent<Image>();
        if (window2Image == null || !window2Image.enabled)
            return;
        
        // Alle Bedingungen erfüllt - starte Image Wechsel
        if (showDebugLogs)
        {
            Debug.Log($"🎯 WINDOW2 IMAGE WECHSEL AKTIVIERT: Übergang aktiv, CanvasWagon3 aktiv, Window2 Image aktiviert");
        }
        
        // Pre-Window2 Hintergrundmusik sofort stoppen (falls sie läuft)
        if (preWindow2BackgroundMusicAudioSource != null && preWindow2BackgroundMusicAudioSource.isPlaying)
        {
            preWindow2BackgroundMusicAudioSource.Stop();
            
            if (showDebugLogs)
            {
                Debug.Log($"🎵 Pre-Window2 Hintergrundmusik gestoppt: {(preWindow2BackgroundMusicAudioSource.clip != null ? preWindow2BackgroundMusicAudioSource.clip.name : "unknown")}");
            }
        }
        
        StartCoroutine(SwitchWindow2ImageAfterDelay());
    }
    
    // Coroutine für Window2 Image Wechsel mit Countdown
    private IEnumerator SwitchWindow2ImageAfterDelay()
    {
        if (hasWindow2Switched || isWindow2Switching) yield break;
        
        isWindow2Switching = true;
        
        if (showDebugLogs)
        {
            Debug.Log($"Window2 Timer läuft für {window2SwitchDelay} Sekunden");
        }
        
        // Countdown-Sound starten (optional)
        bool soundWasStarted = false;
        if (window2CountdownAudioSource != null && window2CountdownAudioSource.clip != null)
        {
            window2CountdownAudioSource.loop = false; // Nur einmal abspielen
            window2CountdownAudioSource.Play();
            soundWasStarted = true;
            
            if (showDebugLogs)
            {
                Debug.Log($"Countdown-Sound gestartet: {window2CountdownAudioSource.clip.name} (einmalig)");
            }
        }
        
        // EventSystem deaktivieren (optional)
        bool eventSystemWasEnabled = false;
        if (disableEventSystemDuringWindow2Switch && eventSystem != null)
        {
            eventSystemWasEnabled = eventSystem.enabled;
            eventSystem.enabled = false;
            
            if (showDebugLogs)
            {
                Debug.Log($"EventSystem deaktiviert für {window2SwitchDelay} Sekunden");
            }
        }
        
        // Warte 2 Sekunden, dann starte Hintergrundmusik
        yield return new WaitForSeconds(2f);
        
        // Hintergrundmusik nach 2 Sekunden starten
        StartBackgroundMusic();
        
        // Warte die restliche Zeit bis zum Image-Wechsel
        float remainingDelay = window2SwitchDelay - 2f;
        if (remainingDelay > 0)
        {
            yield return new WaitForSeconds(remainingDelay);
        }
        
        // Image wechseln
        var window2Image = window2GameObject.GetComponent<Image>();
        if (window2Image != null && newWindow2ImageObject != null)
        {
            window2GameObject.SetActive(false);
            newWindow2ImageObject.SetActive(true);
            hasWindow2Switched = true;
            
            if (showDebugLogs)
            {
                Debug.Log($"✅ WINDOW2 IMAGE-OBJEKT AKTIVIERT: {newWindow2ImageObject.name}, altes deaktiviert: {window2GameObject.name}");
            }
        }
        
        // Countdown-Sound stoppen (optional) - nur falls er noch läuft
        if (soundWasStarted && window2CountdownAudioSource != null && window2CountdownAudioSource.isPlaying)
        {
            window2CountdownAudioSource.Stop();
            
            if (showDebugLogs)
            {
                Debug.Log($"Countdown-Sound gestoppt (war noch am Laufen)");
            }
        }
        else if (soundWasStarted && showDebugLogs)
        {
            Debug.Log($"Countdown-Sound war bereits beendet");
        }
        
        // EventSystem wieder aktivieren (falls es deaktiviert wurde)
        if (disableEventSystemDuringWindow2Switch && eventSystem != null && eventSystemWasEnabled)
        {
            eventSystem.enabled = true;
            
            if (showDebugLogs)
            {
                Debug.Log($"EventSystem wieder aktiviert nach Window2 Image-Wechsel");
            }
            
            // Auto-Click für ItemInteractable nach kurzer Verzögerung
            if (autoClickItemAfterWindow2 != null)
            {
                StartCoroutine(AutoClickItemAfterDelay());
            }
        }
        
        if (showDebugLogs)
        {
            Debug.Log($"Window2 Image-Wechsel abgeschlossen");
        }
        
        isWindow2Switching = false;
    }
    
    // Auto-Click für ItemInteractable nach Window2-Wechsel
    private IEnumerator AutoClickItemAfterDelay()
    {
        if (showDebugLogs)
        {
            Debug.Log($"🎯 AUTO-CLICK: Warte {autoClickDelay} Sekunden bevor ItemInteractable angeklickt wird...");
        }
        
        // Kurze Verzögerung um sicherzustellen dass EventSystem vollständig aktiviert ist
        yield return new WaitForSeconds(autoClickDelay);
        
        if (autoClickItemAfterWindow2 == null)
        {
            if (showDebugLogs)
            {
                Debug.LogWarning("AUTO-CLICK: Kein ItemInteractable GameObject zugewiesen!");
            }
            yield break;
        }
        
        // Prüfe ob das GameObject aktiv ist
        if (!autoClickItemAfterWindow2.activeInHierarchy)
        {
            if (showDebugLogs)
            {
                Debug.LogWarning($"AUTO-CLICK: ItemInteractable GameObject '{autoClickItemAfterWindow2.name}' ist nicht aktiv!");
            }
            yield break;
        }
        
        // Suche nach ItemInteractable Component
        var itemInteractable = autoClickItemAfterWindow2.GetComponent<ItemInteractable>();
        if (itemInteractable == null)
        {
            if (showDebugLogs)
            {
                Debug.LogError($"AUTO-CLICK: GameObject '{autoClickItemAfterWindow2.name}' hat keine ItemInteractable Component!");
            }
            yield break;
        }
        
        // Führe OnButtonClick() Methode aus
        try
        {
            itemInteractable.OnButtonClick();
            
            if (showDebugLogs)
            {
                Debug.Log($"✅ AUTO-CLICK ERFOLGREICH: ItemInteractable '{autoClickItemAfterWindow2.name}' wurde automatisch angeklickt - Dialog sollte starten");
            }
        }
        catch (System.Exception e)
        {
            if (showDebugLogs)
            {
                Debug.LogError($"AUTO-CLICK FEHLER: Konnte ItemInteractable '{autoClickItemAfterWindow2.name}' nicht anklicken - {e.Message}");
            }
        }
    }
    
    // Hintergrundmusik nach Window2-Wechsel starten
    private void StartBackgroundMusic()
    {
        if (backgroundMusicStarted) return; // Bereits gestartet
        
        if (backgroundMusicAudioSource != null && backgroundMusicAudioSource.clip != null)
        {
            // Prüfe ob Hauptmenü aktiv ist
            if (canvasHauptmenu != null && canvasHauptmenu.activeInHierarchy)
            {
                if (showDebugLogs)
                {
                    Debug.Log("Hintergrundmusik nicht gestartet - Hauptmenü ist aktiv");
                }
                return;
            }
            
            backgroundMusicAudioSource.loop = true; // Dauerhaft loopen
            backgroundMusicAudioSource.Play();
            backgroundMusicStarted = true;
            
            if (showDebugLogs)
            {
                Debug.Log($"🎵 HINTERGRUNDMUSIK GESTARTET: {backgroundMusicAudioSource.clip.name} (dauerhaft geloopt)");
            }
        }
        else if (showDebugLogs)
        {
            Debug.LogWarning("Hintergrundmusik konnte nicht gestartet werden - AudioSource oder AudioClip fehlt");
        }
    }
    
    // Überwache Hintergrundmusik-Status basierend auf Hauptmenü
    private void CheckBackgroundMusicState()
    {
        if (!backgroundMusicStarted || backgroundMusicAudioSource == null) return;
        
        bool hauptmenuActive = canvasHauptmenu != null && canvasHauptmenu.activeInHierarchy;
        bool musicPlaying = backgroundMusicAudioSource.isPlaying;
        
        if (hauptmenuActive && musicPlaying)
        {
            // Hauptmenü aktiv -> Musik stoppen
            backgroundMusicAudioSource.Stop();
            
            if (showDebugLogs)
            {
                Debug.Log("🎵 Hintergrundmusik gestoppt - Hauptmenü aktiv");
            }
        }
        else if (!hauptmenuActive && !musicPlaying && backgroundMusicStarted)
        {
            // Hauptmenü nicht aktiv und Musik läuft nicht -> Musik starten
            backgroundMusicAudioSource.Play();
            
            if (showDebugLogs)
            {
                Debug.Log("🎵 Hintergrundmusik fortgesetzt - Hauptmenü verlassen");
            }
        }
    }
    
    // Debug-Methode für Window2 System
    [ContextMenu("Debug Window2 Image System")]
    public void DebugWindow2ImageSystem()
    {
        Debug.Log("=== WINDOW2 IMAGE SYSTEM DEBUG ===");
        Debug.Log($"System initialisiert: {window2SystemInitialized}");
        Debug.Log($"Window2 GameObject zugewiesen: {window2GameObject != null}");
        Debug.Log($"CanvasWagon3 zugewiesen: {canvasWagon3 != null}");
        Debug.Log($"CanvasÜbergang zugewiesen: {canvasUebergang != null}");
        Debug.Log($"New Sprite zugewiesen: {newWindow2ImageObject != null}");
        Debug.Log($"Switch Delay: {window2SwitchDelay} Sekunden");
        Debug.Log($"Switching in Progress: {isWindow2Switching}");
        Debug.Log($"Bereits gewechselt: {hasWindow2Switched}");
        Debug.Log($"AudioSource zugewiesen: {window2CountdownAudioSource != null}");
        Debug.Log($"AudioClip in AudioSource: {(window2CountdownAudioSource != null && window2CountdownAudioSource.clip != null)}");
        Debug.Log($"Pre-Window2 Background Music AudioSource zugewiesen: {preWindow2BackgroundMusicAudioSource != null}");
        Debug.Log($"Pre-Window2 Background Music AudioClip: {(preWindow2BackgroundMusicAudioSource != null && preWindow2BackgroundMusicAudioSource.clip != null)}");
        Debug.Log($"Pre-Window2 Background Music läuft: {(preWindow2BackgroundMusicAudioSource != null && preWindow2BackgroundMusicAudioSource.isPlaying)}");
        Debug.Log($"Background Music AudioSource zugewiesen: {backgroundMusicAudioSource != null}");
        Debug.Log($"Background Music AudioClip: {(backgroundMusicAudioSource != null && backgroundMusicAudioSource.clip != null)}");
        Debug.Log($"Background Music gestartet: {backgroundMusicStarted}");
        Debug.Log($"Background Music läuft: {(backgroundMusicAudioSource != null && backgroundMusicAudioSource.isPlaying)}");
        Debug.Log($"Canvas Hauptmenü zugewiesen: {canvasHauptmenu != null}");
        
        if (backgroundMusicAudioSource != null && backgroundMusicAudioSource.clip != null)
        {
            Debug.Log($"  - Background Music Clip Name: {backgroundMusicAudioSource.clip.name}");
        }
        
        if (preWindow2BackgroundMusicAudioSource != null && preWindow2BackgroundMusicAudioSource.clip != null)
        {
            Debug.Log($"  - Pre-Window2 Background Music Clip Name: {preWindow2BackgroundMusicAudioSource.clip.name}");
        }
        
        if (canvasUebergang != null)
        {
            Debug.Log($"  - CanvasÜbergang aktiv: {canvasUebergang.activeInHierarchy} (Countdown braucht: aktiv)");
        }
        
        if (canvasWagon3 != null)
        {
            Debug.Log($"  - CanvasWagon3 aktiv: {canvasWagon3.activeInHierarchy} (Countdown braucht: aktiv)");
        }
        
        if (canvasHauptmenu != null)
        {
            Debug.Log($"  - Canvas Hauptmenü aktiv: {canvasHauptmenu.activeInHierarchy} (Hintergrundmusik stoppt wenn aktiv)");
        }
        
        // Auto-Click System Info
        Debug.Log($"Auto-Click System:");
        Debug.Log($"  - Auto-Click Item zugewiesen: {autoClickItemAfterWindow2 != null}");
        if (autoClickItemAfterWindow2 != null)
        {
            Debug.Log($"  - Auto-Click Item GameObject: {autoClickItemAfterWindow2.name}");
            Debug.Log($"  - Auto-Click Item aktiv: {autoClickItemAfterWindow2.activeInHierarchy}");
            var itemComponent = autoClickItemAfterWindow2.GetComponent<ItemInteractable>();
            Debug.Log($"  - ItemInteractable Component vorhanden: {itemComponent != null}");
            if (itemComponent != null)
            {
                Debug.Log($"  - ItemInteractable Item-ID: {itemComponent.itemId}");
                Debug.Log($"  - ItemInteractable hat verfügbare Dialoge: {itemComponent.HasAvailableDialogs()}");
            }
        }
        Debug.Log($"  - Auto-Click Delay: {autoClickDelay} Sekunden");
        
        if (window2GameObject != null)
        {
            Debug.Log($"  - Window2 GameObject aktiv: {window2GameObject.activeInHierarchy}");
            var window2Image = window2GameObject.GetComponent<Image>();
            if (window2Image != null)
            {
                Debug.Log($"  - Window2 Image aktiviert: {window2Image.enabled}");
                Debug.Log($"  - Aktuelle Sprite: {(window2Image.sprite != null ? window2Image.sprite.name : "NULL")}");
            }
            else
            {
                Debug.Log($"  - Window2 hat keine Image Component!");
            }
        }
        else
        {
            Debug.Log("  - Kein Window2 GameObject zugewiesen!");
        }
    }
    
    // Manueller Test für Window2 System
    [ContextMenu("Test Window2 Switch Now")]
    public void TestWindow2SwitchNow()
    {
        if (window2SystemInitialized && !hasWindow2Switched)
        {
            Debug.Log("Window2 Switch manuell gestartet (Test)");
            StartCoroutine(SwitchWindow2ImageAfterDelay());
        }
        else if (hasWindow2Switched)
        {
            Debug.Log("Window2 bereits gewechselt");
        }
        else
        {
            Debug.Log("Window2 System nicht initialisiert");
        }
    }
    
    // Loop-Progression-System initialisieren
    private void InitializeLoopProgressionSystem()
    {
        Debug.Log("=== INITIALISIERE LOOP-PROGRESSION-SYSTEM ===");
        
        // 1. Analysiere alle Memory-Abhängigkeiten
        AnalyzeAllMemoryDependencies();
        
        // 2. Sammle verfügbare Memories pro Loop (nur für Loop-Progression)
        CollectRequiredMemoriesPerLoop();
        
        Debug.Log($"Loop-Progression-System initialisiert für {requiredMemoriesPerLoop.Count} Loops");
        foreach (var loop in requiredMemoriesPerLoop)
        {
            Debug.Log($"  Loop {loop.Key}: {loop.Value.Count} verfügbare Memories: [{string.Join(", ", loop.Value)}]");
        }
        
        // 3. Debug-Ausgabe des Memory-Mappings
        PrintMemoryMappingStats();
    }
    
    // === MEMORY-MAPPING-SYSTEM ===
    
    // Analysiere alle Memory-Abhängigkeiten aus allen CSVs
    private void AnalyzeAllMemoryDependencies()
    {
        Debug.Log("=== ANALYSIERE ALLE MEMORY-ABHÄNGIGKEITEN ===");
        
        // Reset
        allAvailableMemories.Clear();
        allRequiredMemories.Clear();
        memoryDependencies.Clear();
        addMemorySources.Clear();
        requiredMemoryUsages.Clear();
        
        foreach (var dialogFile in allDialogs)
        {
            string csvFileName = dialogFile.Key;
            
            foreach (var dialog in dialogFile.Value)
            {
                // 1. Analysiere Dialog AddMemory
                AnalyzeDialogAddMemory(dialog, csvFileName);
                
                // 2. Analysiere Dialog RequiredMemory
                AnalyzeDialogRequiredMemory(dialog, csvFileName);
                
                // 3. Analysiere Choice AddMemory und RequiredMemory
                AnalyzeChoiceMemories(dialog, csvFileName);
            }
        }
        
        // 4. Erstelle Memory-Verknüpfungen
        CreateMemoryDependencies();
        
        // 5. Identifiziere Notizbuch-spezifische Memories
        IdentifyNotebookOnlyMemories();
        
        // 6. Bestimme loop-relevante AddMemories
        DetermineLoopRelevantAddMemories();
        
        Debug.Log($"Memory-Analyse abgeschlossen:");
        Debug.Log($"  Gefundene AddMemories: {allAvailableMemories.Count}");
        Debug.Log($"  Gefundene RequiredMemories: {allRequiredMemories.Count}");
        Debug.Log($"  Memory-Verknüpfungen: {memoryDependencies.Count}");
        Debug.Log($"  Notizbuch-spezifische RequiredMemories: {notebookOnlyMemories.Count}");
        Debug.Log($"  Loop-relevante AddMemories: {loopRelevantAddMemories.Count}");
    }
    
    private void AnalyzeDialogAddMemory(DialogLine dialog, string csvFileName)
    {
        if (dialog.addMemory != null && dialog.addMemory.Count > 0)
        {
            foreach (var memory in dialog.addMemory)
            {
                if (!string.IsNullOrEmpty(memory))
                {
                    allAvailableMemories.Add(memory);
                    
                    // Speichere Quelle
                    if (!addMemorySources.ContainsKey(memory))
                    {
                        addMemorySources[memory] = new List<MemorySource>();
                    }
                    
                    addMemorySources[memory].Add(new MemorySource
                    {
                        memoryId = memory,
                        csvFileName = csvFileName,
                        dialogMemoryId = dialog.memoryId,
                        minLoop = dialog.minLoop,
                        sourceType = MemorySource.SourceType.Dialog
                    });
                    
                    Debug.Log($"AddMemory gefunden: '{memory}' (Dialog: {dialog.memoryId}, CSV: {csvFileName}, Loop: {dialog.minLoop})");
                }
            }
        }
    }
    
    private void AnalyzeDialogRequiredMemory(DialogLine dialog, string csvFileName)
    {
        if (dialog.requiredMemory != null && dialog.requiredMemory.Count > 0)
        {
            foreach (var memory in dialog.requiredMemory)
            {
                if (!string.IsNullOrEmpty(memory))
                {
                    allRequiredMemories.Add(memory);
                    
                    // Speichere Verwendung
                    if (!requiredMemoryUsages.ContainsKey(memory))
                    {
                        requiredMemoryUsages[memory] = new List<MemoryUsage>();
                    }
                    
                    requiredMemoryUsages[memory].Add(new MemoryUsage
                    {
                        requiredMemory = memory,
                        csvFileName = csvFileName,
                        dialogMemoryId = dialog.memoryId,
                        minLoop = dialog.minLoop,
                        usageType = MemoryUsage.UsageType.Dialog
                    });
                    
                    Debug.Log($"RequiredMemory gefunden: '{memory}' (Dialog: {dialog.memoryId}, CSV: {csvFileName}, Loop: {dialog.minLoop})");
                }
            }
        }
    }
    
    private void AnalyzeChoiceMemories(DialogLine dialog, string csvFileName)
    {
        if (dialog.choices != null)
        {
            for (int i = 0; i < dialog.choices.Count; i++)
            {
                var choice = dialog.choices[i];
                int choiceIndex = i + 1;
                
                // Choice AddMemory
                if (choice.addMemory != null && choice.addMemory.Count > 0)
                {
                    foreach (var memory in choice.addMemory)
                    {
                        if (!string.IsNullOrEmpty(memory))
                        {
                            allAvailableMemories.Add(memory);
                            
                            if (!addMemorySources.ContainsKey(memory))
                            {
                                addMemorySources[memory] = new List<MemorySource>();
                            }
                            
                            addMemorySources[memory].Add(new MemorySource
                            {
                                memoryId = memory,
                                csvFileName = csvFileName,
                                dialogMemoryId = dialog.memoryId,
                                minLoop = dialog.minLoop,
                                sourceType = MemorySource.SourceType.Choice,
                                choiceIndex = choiceIndex
                            });
                            
                            Debug.Log($"Choice AddMemory gefunden: '{memory}' (Dialog: {dialog.memoryId}, Choice: {choiceIndex}, CSV: {csvFileName}, Loop: {dialog.minLoop})");
                        }
                    }
                }
                
                // Choice RequiredMemory
                if (choice.requiredMemory != null && choice.requiredMemory.Count > 0)
                {
                    foreach (var memory in choice.requiredMemory)
                    {
                        if (!string.IsNullOrEmpty(memory))
                        {
                            allRequiredMemories.Add(memory);
                            
                            if (!requiredMemoryUsages.ContainsKey(memory))
                            {
                                requiredMemoryUsages[memory] = new List<MemoryUsage>();
                            }
                            
                            requiredMemoryUsages[memory].Add(new MemoryUsage
                            {
                                requiredMemory = memory,
                                csvFileName = csvFileName,
                                dialogMemoryId = dialog.memoryId,
                                minLoop = dialog.minLoop,
                                usageType = MemoryUsage.UsageType.Choice,
                                choiceIndex = choiceIndex
                            });
                            
                            Debug.Log($"Choice RequiredMemory gefunden: '{memory}' (Dialog: {dialog.memoryId}, Choice: {choiceIndex}, CSV: {csvFileName}, Loop: {dialog.minLoop})");
                        }
                    }
                }
            }
        }
    }
    
    private void CreateMemoryDependencies()
    {
        Debug.Log("=== ERSTELLE MEMORY-VERKNÜPFUNGEN ===");
        
        // Für jedes RequiredMemory: prüfe welche AddMemories es erfüllen können
        foreach (var requiredMemory in allRequiredMemories)
        {
            memoryDependencies[requiredMemory] = new HashSet<string>();
            
            // Direkte Übereinstimmung
            if (allAvailableMemories.Contains(requiredMemory))
            {
                memoryDependencies[requiredMemory].Add(requiredMemory);
                Debug.Log($"Memory-Verknüpfung: '{requiredMemory}' kann erfüllt werden durch '{requiredMemory}' (direkte Übereinstimmung)");
            }
            
            // Hier könnten später weitere Verknüpfungslogiken hinzugefügt werden
            // z.B. Pattern-Matching, Aliases, etc.
        }
        
        Debug.Log($"Memory-Verknüpfungen erstellt: {memoryDependencies.Count} RequiredMemories analysiert");
    }
    
    // Identifiziere RequiredMemories die nur im Notizbuch (letzte CSV) verwendet werden
    private void IdentifyNotebookOnlyMemories()
    {
        Debug.Log("=== IDENTIFIZIERE NOTIZBUCH-SPEZIFISCHE MEMORIES ===");
        
        notebookOnlyMemories.Clear();
        
        // Bestimme die Notizbuch-CSV (letzte in der Liste)
        string notebookCsvName = "";
        if (dialogCSVFiles != null && dialogCSVFiles.Length > 0)
        {
            var lastCsv = dialogCSVFiles[dialogCSVFiles.Length - 1];
            if (lastCsv != null)
            {
                notebookCsvName = lastCsv.name;
                Debug.Log($"Notizbuch-CSV identifiziert: '{notebookCsvName}' (Index: {dialogCSVFiles.Length - 1})");
            }
        }
        
        if (string.IsNullOrEmpty(notebookCsvName))
        {
            Debug.LogWarning("Notizbuch-CSV konnte nicht identifiziert werden!");
            return;
        }
        
        // Prüfe alle RequiredMemories
        foreach (var kvp in requiredMemoryUsages)
        {
            string requiredMemory = kvp.Key;
            var usages = kvp.Value;
            
            // Prüfe ob dieses RequiredMemory nur im Notizbuch verwendet wird
            bool onlyInNotebook = true;
            foreach (var usage in usages)
            {
                if (usage.csvFileName != notebookCsvName)
                {
                    onlyInNotebook = false;
                    break;
                }
            }
            
            if (onlyInNotebook)
            {
                notebookOnlyMemories.Add(requiredMemory);
                Debug.Log($"Notizbuch-spezifisches RequiredMemory: '{requiredMemory}' (nur in {notebookCsvName} verwendet)");
            }
        }
        
        Debug.Log($"Notizbuch-spezifische RequiredMemories identifiziert: {notebookOnlyMemories.Count}");
    }
    
    // Bestimme welche AddMemories für Loop-Progression relevant sind
    private void DetermineLoopRelevantAddMemories()
    {
        Debug.Log("=== BESTIMME LOOP-RELEVANTE ADDMEMORIES ===");
        
        loopRelevantAddMemories.Clear();
        
        // Erste Runde: Markiere direkt loop-relevante AddMemories
        var directlyRelevant = new HashSet<string>();
        var potentiallyIrrelevant = new HashSet<string>();
        
        foreach (var addMemory in allAvailableMemories)
        {
            // Ausnahme: AddMemories die mit "newdraw" beginnen sind niemals loop-relevant
            if (addMemory.StartsWith("newdraw", System.StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log($"AddMemory '{addMemory}' ausgeschlossen - beginnt mit 'newdraw' (nie loop-relevant)");
                continue;
            }
            
            bool isDirectlyLoopRelevant = false;
            
            // Prüfe ob dieses AddMemory für ein RequiredMemory benötigt wird, das NICHT notizbuch-spezifisch ist
            foreach (var kvp in memoryDependencies)
            {
                string requiredMemory = kvp.Key;
                var fulfillers = kvp.Value;
                
                // Wenn dieses AddMemory ein RequiredMemory erfüllen kann
                if (fulfillers.Contains(addMemory))
                {
                    // Und das RequiredMemory NICHT notizbuch-spezifisch ist
                    if (!notebookOnlyMemories.Contains(requiredMemory))
                    {
                        isDirectlyLoopRelevant = true;
                        Debug.Log($"AddMemory '{addMemory}' ist direkt loop-relevant (erfüllt nicht-notizbuch RequiredMemory: '{requiredMemory}')");
                        break;
                    }
                }
            }
            
            if (isDirectlyLoopRelevant)
            {
                directlyRelevant.Add(addMemory);
            }
            else
            {
                potentiallyIrrelevant.Add(addMemory);
                Debug.Log($"AddMemory '{addMemory}' ist potentiell nicht loop-relevant (führt ins Leere oder nur zu Notizbuch-Memories)");
            }
        }
        
        // Zweite Runde: Analysiere Choice-Ketten für potentiell irrelevante AddMemories
        var chainIrrelevant = AnalyzeChoiceChains(potentiallyIrrelevant);
        
        // Finale Bestimmung: Nur direkt relevante und nicht durch Ketten-Analyse ausgeschlossene
        foreach (var addMemory in allAvailableMemories)
        {
            if (addMemory.StartsWith("newdraw", System.StringComparison.OrdinalIgnoreCase))
            {
                continue; // Bereits ausgeschlossen
            }
            
            if (directlyRelevant.Contains(addMemory) && !chainIrrelevant.Contains(addMemory))
            {
                loopRelevantAddMemories.Add(addMemory);
            }
        }
        
        Debug.Log($"Loop-relevante AddMemories bestimmt: {loopRelevantAddMemories.Count} von {allAvailableMemories.Count}");
        Debug.Log($"  - Direkt relevant: {directlyRelevant.Count}");
        Debug.Log($"  - Durch Choice-Ketten ausgeschlossen: {chainIrrelevant.Count}");
    }
    
    // Analysiere Choice-Ketten: Finde AddMemories die zu irrelevanten Enden führen
    private HashSet<string> AnalyzeChoiceChains(HashSet<string> potentiallyIrrelevantMemories)
    {
        Debug.Log("=== ANALYSIERE CHOICE-KETTEN ===");
        
        var chainIrrelevant = new HashSet<string>();
        var visitedMemories = new HashSet<string>();
        
        foreach (var memory in potentiallyIrrelevantMemories)
        {
            if (visitedMemories.Contains(memory)) continue;
            
            var chainResult = TraceChoiceChainBackwards(memory, new HashSet<string>());
            
            if (chainResult.leadsToDeadEnd)
            {
                Debug.Log($"Choice-Kette für '{memory}' führt ins Leere - markiere ganze Kette als nicht loop-relevant:");
                foreach (var chainMemory in chainResult.chainMemories)
                {
                    chainIrrelevant.Add(chainMemory);
                    visitedMemories.Add(chainMemory);
                    Debug.Log($"  - '{chainMemory}' (Teil der ins Leere führenden Kette)");
                }
            }
            else
            {
                foreach (var chainMemory in chainResult.chainMemories)
                {
                    visitedMemories.Add(chainMemory);
                }
            }
        }
        
        Debug.Log($"Choice-Ketten-Analyse abgeschlossen: {chainIrrelevant.Count} AddMemories durch Ketten-Logik ausgeschlossen");
        return chainIrrelevant;
    }
    
    // Verfolge eine Choice-Kette rückwärts um zu prüfen ob sie zu einem irrelevanten Ende führt
    private (bool leadsToDeadEnd, HashSet<string> chainMemories) TraceChoiceChainBackwards(string targetMemory, HashSet<string> visitedInCurrentTrace)
    {
        if (visitedInCurrentTrace.Contains(targetMemory))
        {
            // Zyklus erkannt - behandle als nicht irrelevant
            return (false, new HashSet<string>());
        }
        
        visitedInCurrentTrace.Add(targetMemory);
        var chainMemories = new HashSet<string> { targetMemory };
        
        // Prüfe ob dieses Memory direkt irrelevant ist (nur Notizbuch oder gar nicht verwendet)
        bool isDirectlyIrrelevant = true;
        foreach (var kvp in memoryDependencies)
        {
            string requiredMemory = kvp.Key;
            var fulfillers = kvp.Value;
            
            if (fulfillers.Contains(targetMemory) && !notebookOnlyMemories.Contains(requiredMemory))
            {
                isDirectlyIrrelevant = false;
                break;
            }
        }
        
        if (isDirectlyIrrelevant)
        {
            Debug.Log($"  Memory '{targetMemory}' ist direkt irrelevant (Endpunkt der Kette)");
            return (true, chainMemories);
        }
        
        // Finde alle AddMemories die zu diesem Memory führen könnten (über Choices)
        var precedingMemories = FindMemoriesLeadingToTarget(targetMemory);
        
        if (precedingMemories.Count == 0)
        {
            // Kein Vorläufer gefunden - dies ist kein Kettenende
            return (false, chainMemories);
        }
        
        // Prüfe alle Vorläufer - wenn ALLE ins Leere führen, dann ist die ganze Kette irrelevant
        bool allPrecedingLeadToDeadEnd = true;
        foreach (var precedingMemory in precedingMemories)
        {
            var result = TraceChoiceChainBackwards(precedingMemory, new HashSet<string>(visitedInCurrentTrace));
            chainMemories.UnionWith(result.chainMemories);
            
            if (!result.leadsToDeadEnd)
            {
                allPrecedingLeadToDeadEnd = false;
                // Nicht brechen - wir wollen alle chainMemories sammeln
            }
        }
        
        return (allPrecedingLeadToDeadEnd, chainMemories);
    }
    
    // Finde AddMemories die zu einem bestimmten Memory führen (über Choice-Logik)
    private HashSet<string> FindMemoriesLeadingToTarget(string targetMemory)
    {
        var leadingMemories = new HashSet<string>();
        
        // Durchsuche alle Dialoge nach Choices die targetMemory als RequiredMemory haben
        foreach (var csvDialogs in allDialogs.Values)
        {
            foreach (var dialog in csvDialogs)
            {
                if (dialog.choices != null)
                {
                    foreach (var choice in dialog.choices)
                    {
                        // Wenn diese Choice targetMemory als Requirement hat
                        if (choice.requiredMemory != null && choice.requiredMemory.Contains(targetMemory))
                        {
                            // Dann füge alle AddMemories aus dieser Choice hinzu
                            if (choice.addMemory != null)
                            {
                                foreach (var addMem in choice.addMemory)
                                {
                                    if (!string.IsNullOrEmpty(addMem))
                                    {
                                        leadingMemories.Add(addMem);
                                        Debug.Log($"    '{addMem}' führt zu '{targetMemory}' (Choice in Dialog: {dialog.memoryId})");
                                    }
                                }
                            }
                            
                            // Und auch AddMemories aus dem Dialog selbst, da der Dialog für die Choice verfügbar sein muss
                            if (dialog.addMemory != null)
                            {
                                foreach (var addMem in dialog.addMemory)
                                {
                                    if (!string.IsNullOrEmpty(addMem))
                                    {
                                        leadingMemories.Add(addMem);
                                        Debug.Log($"    '{addMem}' führt zu '{targetMemory}' (Dialog für Choice: {dialog.memoryId})");
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        
        return leadingMemories;
    }
    
    // Debug-Methoden für Memory-Mapping
    public void PrintMemoryMappingStats()
    {
        Debug.Log("=== MEMORY-MAPPING STATISTIKEN ===");
        Debug.Log($"Alle verfügbaren AddMemories ({allAvailableMemories.Count}): [{string.Join(", ", allAvailableMemories)}]");
        Debug.Log($"Alle verwendeten RequiredMemories ({allRequiredMemories.Count}): [{string.Join(", ", allRequiredMemories)}]");
        Debug.Log($"Notizbuch-spezifische RequiredMemories ({notebookOnlyMemories.Count}): [{string.Join(", ", notebookOnlyMemories)}]");
        Debug.Log($"Loop-relevante AddMemories ({loopRelevantAddMemories.Count}): [{string.Join(", ", loopRelevantAddMemories)}]");
        
        // Unverknüpfte Memories finden
        var unlinkedRequired = new HashSet<string>();
        var unlinkedAvailable = new HashSet<string>();
        var notebookOnlyButLinked = new HashSet<string>();
        
        foreach (var required in allRequiredMemories)
        {
            if (!memoryDependencies.ContainsKey(required) || memoryDependencies[required].Count == 0)
            {
                unlinkedRequired.Add(required);
            }
            else if (notebookOnlyMemories.Contains(required))
            {
                notebookOnlyButLinked.Add(required);
            }
        }
        
        foreach (var available in allAvailableMemories)
        {
            bool isLinked = false;
            foreach (var deps in memoryDependencies.Values)
            {
                if (deps.Contains(available))
                {
                    isLinked = true;
                    break;
                }
            }
            if (!isLinked)
            {
                unlinkedAvailable.Add(available);
            }
        }
        
        if (unlinkedRequired.Count > 0)
        {
            Debug.LogWarning($"Unverknüpfte RequiredMemories ({unlinkedRequired.Count}): [{string.Join(", ", unlinkedRequired)}]");
        }
        
        if (unlinkedAvailable.Count > 0)
        {
            Debug.Log($"Unverknüpfte AddMemories ({unlinkedAvailable.Count}): [{string.Join(", ", unlinkedAvailable)}]");
        }
        
        if (notebookOnlyButLinked.Count > 0)
        {
            Debug.Log($"Notizbuch-spezifische aber verknüpfte RequiredMemories ({notebookOnlyButLinked.Count}): [{string.Join(", ", notebookOnlyButLinked)}]");
        }
        
        var loopRelevantLinked = memoryDependencies.Keys.Count(k => !notebookOnlyMemories.Contains(k));
        Debug.Log($"Loop-relevante Memory-Paare: {loopRelevantLinked}");
        Debug.Log($"Nur-Notizbuch Memory-Paare: {notebookOnlyButLinked.Count}");
    }
    
    [ContextMenu("Print Complete Memory Analysis")]
    public void PrintCompleteMemoryAnalysis()
    {
        Debug.Log("=== VOLLSTÄNDIGE MEMORY-ANALYSE ===");
        
        Debug.Log("\n--- ADDMEMORY QUELLEN ---");
        foreach (var kvp in addMemorySources)
        {
            Debug.Log($"AddMemory '{kvp.Key}' ({kvp.Value.Count} Quellen):");
            foreach (var source in kvp.Value)
            {
                Debug.Log($"  - {source}");
            }
        }
        
        Debug.Log("\n--- REQUIREDMEMORY VERWENDUNGEN ---");
        foreach (var kvp in requiredMemoryUsages)
        {
            Debug.Log($"RequiredMemory '{kvp.Key}' ({kvp.Value.Count} Verwendungen):");
            foreach (var usage in kvp.Value)
            {
                Debug.Log($"  - {usage}");
            }
        }
        
        Debug.Log("\n--- MEMORY-VERKNÜPFUNGEN ---");
        foreach (var kvp in memoryDependencies)
        {
            if (kvp.Value.Count > 0)
            {
                Debug.Log($"RequiredMemory '{kvp.Key}' kann erfüllt werden durch: [{string.Join(", ", kvp.Value)}]");
            }
            else
            {
                Debug.LogWarning($"RequiredMemory '{kvp.Key}' hat KEINE verfügbaren AddMemories!");
            }
        }
    }
    
    // Sammle alle verfügbaren Memories pro Loop aus allen Dialogen (nur loop-relevante)
    private void CollectRequiredMemoriesPerLoop()
    {
        requiredMemoriesPerLoop.Clear();
        
        foreach (var dialogFile in allDialogs)
        {
            foreach (var dialog in dialogFile.Value)
            {
                // 1. Dialog AddMemory berücksichtigen (nur loop-relevante)
                if (dialog.addMemory != null && dialog.addMemory.Count > 0)
                {
                    int loopForMemory = dialog.minLoop;
                    
                    foreach (var memory in dialog.addMemory)
                    {
                        if (!string.IsNullOrEmpty(memory) && loopRelevantAddMemories.Contains(memory))
                        {
                            if (!requiredMemoriesPerLoop.ContainsKey(loopForMemory))
                            {
                                requiredMemoriesPerLoop[loopForMemory] = new HashSet<string>();
                            }
                            
                            requiredMemoriesPerLoop[loopForMemory].Add(memory);
                            Debug.Log($"Loop-relevantes Memory '{memory}' ist verfügbar ab Loop {loopForMemory} (Dialog: {dialog.memoryId})");
                        }
                        else if (!string.IsNullOrEmpty(memory))
                        {
                            Debug.Log($"Memory '{memory}' übersprungen - nicht loop-relevant (Dialog: {dialog.memoryId})");
                        }
                    }
                }
                
                // 2. Choice AddMemory berücksichtigen (nur loop-relevante)
                if (dialog.choices != null)
                {
                    foreach (var choice in dialog.choices)
                    {
                        if (choice.addMemory != null && choice.addMemory.Count > 0)
                        {
                            int loopForMemory = dialog.minLoop; // Choice hat den gleichen minLoop wie der Dialog
                            
                            foreach (var memory in choice.addMemory)
                            {
                                if (!string.IsNullOrEmpty(memory) && loopRelevantAddMemories.Contains(memory))
                                {
                                    if (!requiredMemoriesPerLoop.ContainsKey(loopForMemory))
                                    {
                                        requiredMemoriesPerLoop[loopForMemory] = new HashSet<string>();
                                    }
                                    
                                    requiredMemoriesPerLoop[loopForMemory].Add(memory);
                                    Debug.Log($"Loop-relevantes Memory '{memory}' ist verfügbar ab Loop {loopForMemory} (Choice in Dialog: {dialog.memoryId})");
                                }
                                else if (!string.IsNullOrEmpty(memory))
                                {
                                    Debug.Log($"Memory '{memory}' übersprungen - nicht loop-relevant (Choice in Dialog: {dialog.memoryId})");
                                }
                            }
                        }
                    }
                }
            }
        }
        
        Debug.Log($"Loop-Progression-System: {requiredMemoriesPerLoop.Keys.Count} Loops mit insgesamt {requiredMemoriesPerLoop.Values.Sum(s => s.Count)} loop-relevanten Memories");
    }
    
    // === MEMORY-MAPPING UTILITY-METHODEN ===
    
    // Finde alle Quellen für ein bestimmtes AddMemory
    public List<MemorySource> GetMemorySourcesFor(string memoryId)
    {
        return addMemorySources.ContainsKey(memoryId) ? addMemorySources[memoryId] : new List<MemorySource>();
    }
    
    // Finde alle Verwendungen für ein bestimmtes RequiredMemory
    public List<MemoryUsage> GetMemoryUsagesFor(string memoryId)
    {
        return requiredMemoryUsages.ContainsKey(memoryId) ? requiredMemoryUsages[memoryId] : new List<MemoryUsage>();
    }
    
    // Prüfe ob ein Memory überhaupt verfügbar ist
    public bool IsMemoryAvailable(string memoryId)
    {
        return allAvailableMemories.Contains(memoryId);
    }
    
    // Prüfe ob ein Memory irgendwo benötigt wird
    public bool IsMemoryRequired(string memoryId)
    {
        return allRequiredMemories.Contains(memoryId);
    }
    
    // Finde alle AddMemories die ein RequiredMemory erfüllen können
    public HashSet<string> GetMemoryFulfillers(string requiredMemory)
    {
        return memoryDependencies.ContainsKey(requiredMemory) ? memoryDependencies[requiredMemory] : new HashSet<string>();
    }
    
    // Finde unverknüpfte Memories (Debug-Hilfsmethode)
    public void FindUnlinkedMemories()
    {
        Debug.Log("=== SUCHE UNVERKNÜPFTE MEMORIES ===");
        
        var orphanedAddMemories = new List<string>();
        var unfulfillableRequiredMemories = new List<string>();
        var notebookOnlyAddMemories = new List<string>();
        var newdrawAddMemories = new List<string>();
        var chainIrrelevantAddMemories = new List<string>();
        
        // Finde AddMemories ohne entsprechende RequiredMemories
        foreach (var addMemory in allAvailableMemories)
        {
            // "newdraw"-Memories separat kategorisieren
            if (addMemory.StartsWith("newdraw", System.StringComparison.OrdinalIgnoreCase))
            {
                newdrawAddMemories.Add(addMemory);
            }
            else if (!allRequiredMemories.Contains(addMemory))
            {
                orphanedAddMemories.Add(addMemory);
            }
            else if (!loopRelevantAddMemories.Contains(addMemory))
            {
                // Prüfe ob es durch Choice-Ketten-Analyse ausgeschlossen wurde
                var potentiallyIrrelevant = new HashSet<string>();
                foreach (var mem in allAvailableMemories)
                {
                    if (!mem.StartsWith("newdraw", System.StringComparison.OrdinalIgnoreCase))
                    {
                        bool isDirectlyRelevant = false;
                        foreach (var kvp in memoryDependencies)
                        {
                            if (kvp.Value.Contains(mem) && !notebookOnlyMemories.Contains(kvp.Key))
                            {
                                isDirectlyRelevant = true;
                                break;
                            }
                        }
                        if (!isDirectlyRelevant)
                        {
                            potentiallyIrrelevant.Add(mem);
                        }
                    }
                }
                
                var chainIrrelevant = AnalyzeChoiceChains(potentiallyIrrelevant);
                if (chainIrrelevant.Contains(addMemory))
                {
                    chainIrrelevantAddMemories.Add(addMemory);
                }
                else
                {
                    // AddMemory wird nur für Notizbuch benötigt
                    notebookOnlyAddMemories.Add(addMemory);
                }
            }
        }
        
        // Finde RequiredMemories ohne entsprechende AddMemories
        foreach (var requiredMemory in allRequiredMemories)
        {
            if (!memoryDependencies.ContainsKey(requiredMemory) || memoryDependencies[requiredMemory].Count == 0)
            {
                unfulfillableRequiredMemories.Add(requiredMemory);
            }
        }
        
        if (orphanedAddMemories.Count > 0)
        {
            Debug.Log($"Vollständig verwaiste AddMemories ({orphanedAddMemories.Count}): Diese werden hinzugefügt, aber nirgends als RequiredMemory verwendet:");
            foreach (var memory in orphanedAddMemories)
            {
                var sources = GetMemorySourcesFor(memory);
                Debug.Log($"  - '{memory}' aus {sources.Count} Quellen:");
                foreach (var source in sources)
                {
                    Debug.Log($"    {source}");
                }
            }
        }
        
        if (notebookOnlyAddMemories.Count > 0)
        {
            Debug.Log($"Notizbuch-spezifische AddMemories ({notebookOnlyAddMemories.Count}): Diese sind nur für Notizbuch-Dialoge relevant (nicht loop-relevant):");
            foreach (var memory in notebookOnlyAddMemories)
            {
                var sources = GetMemorySourcesFor(memory);
                Debug.Log($"  - '{memory}' aus {sources.Count} Quellen:");
                foreach (var source in sources)
                {
                    Debug.Log($"    {source}");
                }
            }
        }
        
        if (chainIrrelevantAddMemories.Count > 0)
        {
            Debug.Log($"Choice-Ketten-irrelevante AddMemories ({chainIrrelevantAddMemories.Count}): Diese sind Teil von Ketten die ins Leere führen:");
            foreach (var memory in chainIrrelevantAddMemories)
            {
                var sources = GetMemorySourcesFor(memory);
                Debug.Log($"  - '{memory}' aus {sources.Count} Quellen:");
                foreach (var source in sources)
                {
                    Debug.Log($"    {source}");
                }
            }
        }
        
        if (newdrawAddMemories.Count > 0)
        {
            Debug.Log($"NewDraw AddMemories ({newdrawAddMemories.Count}): Diese beginnen mit 'newdraw' und sind automatisch von Loop-Progression ausgeschlossen:");
            foreach (var memory in newdrawAddMemories)
            {
                var sources = GetMemorySourcesFor(memory);
                Debug.Log($"  - '{memory}' aus {sources.Count} Quellen:");
                foreach (var source in sources)
                {
                    Debug.Log($"    {source}");
                }
            }
        }
        
        if (unfulfillableRequiredMemories.Count > 0)
        {
            Debug.LogWarning($"Unerfüllbare RequiredMemories ({unfulfillableRequiredMemories.Count}): Diese werden benötigt, aber nirgends als AddMemory bereitgestellt:");
            foreach (var memory in unfulfillableRequiredMemories)
            {
                var usages = GetMemoryUsagesFor(memory);
                bool isNotebookOnly = notebookOnlyMemories.Contains(memory);
                string prefix = isNotebookOnly ? "[NOTIZBUCH-SPEZIFISCH]" : "[LOOP-RELEVANT]";
                
                Debug.LogWarning($"  - {prefix} '{memory}' benötigt in {usages.Count} Orten:");
                foreach (var usage in usages)
                {
                    Debug.LogWarning($"    {usage}");
                }
            }
        }
        
        var loopRelevantOrphaned = orphanedAddMemories.Count;
        var totalProblems = unfulfillableRequiredMemories.Count;
        
        if (loopRelevantOrphaned == 0 && totalProblems == 0)
        {
            Debug.Log("✅ Alle loop-relevanten Memories sind korrekt verknüpft!");
        }
        
        Debug.Log($"📊 ZUSAMMENFASSUNG:");
        Debug.Log($"  - Vollständig verwaiste AddMemories: {orphanedAddMemories.Count}");
        Debug.Log($"  - Nur-Notizbuch AddMemories: {notebookOnlyAddMemories.Count}");
        Debug.Log($"  - Choice-Ketten-irrelevante AddMemories: {chainIrrelevantAddMemories.Count}");
        Debug.Log($"  - NewDraw AddMemories (ausgeschlossen): {newdrawAddMemories.Count}");
        Debug.Log($"  - Loop-relevante AddMemories: {loopRelevantAddMemories.Count}");
        Debug.Log($"  - Unerfüllbare RequiredMemories: {unfulfillableRequiredMemories.Count}");
    }
    
    private void LoadAllDialogCSVs()
    {
        Debug.Log($"LoadAllDialogCSVs gestartet...");
        
        if (dialogCSVFiles == null || dialogCSVFiles.Length == 0)
        {
            Debug.LogError("Keine Dialog-CSV-Dateien im GameManager zugewiesen! Bitte CSV-Dateien im Inspector hinzufügen.");
            return;
        }
        
        Debug.Log($"Versuche {dialogCSVFiles.Length} CSV-Dateien zu laden...");
        
        foreach (var csvFile in dialogCSVFiles)
        {
            if (csvFile == null) 
            {
                Debug.LogWarning("Eine CSV-Datei im Array ist NULL!");
                continue;
            }
            
            Debug.Log($"Lade CSV-Datei: {csvFile.name}");
            var dialogLines = DialogLoader.LoadDialogCSV(csvFile);
            string fileName = csvFile.name;
            
            allDialogs[fileName] = dialogLines;
            Debug.Log($"Dialog-CSV geladen: {fileName} mit {dialogLines.Count} Zeilen");
        }
        
        Debug.Log($"Alle Dialog-CSVs geladen: {allDialogs.Count} Dateien");
        Debug.Log($"Verfügbare CSV-Dateien: {string.Join(", ", allDialogs.Keys)}");
    }
    
    // Hilfsmethode: CSV-Dateiname per Index bekommen
    private string GetCSVFileNameByIndex(int index)
    {
        if (dialogCSVFiles == null || index < 0 || index >= dialogCSVFiles.Length)
        {
            Debug.LogWarning($"CSV-Index {index} ist ungültig! Verfügbare Indices: 0-{(dialogCSVFiles?.Length ?? 0) - 1}");
            return "";
        }
        
        var csvFile = dialogCSVFiles[index];
        return csvFile != null ? csvFile.name : "";
    }
    
    // Dialog-Zugriff für NPCs
    public List<DialogLine> GetDialogsForNPC(string npcId)
    {
        if (npcRegistry.ContainsKey(npcId))
        {
            var npcData = npcRegistry[npcId];
            string csvFileName = GetCSVFileNameByIndex(npcData.csvFileIndex);
            
            if (!string.IsNullOrEmpty(csvFileName))
            {
                return GetAllDialogsFromCSV(csvFileName);
            }
            else
            {
                Debug.LogWarning($"Ungültiger CSV-Index {npcData.csvFileIndex} für NPC '{npcId}'!");
            }
        }
        
        Debug.LogWarning($"NPC '{npcId}' nicht in Registry gefunden!");
        return new List<DialogLine>();
    }
    
    // Dialog-Zugriff für Items (itemId = memoryId aus CSV)
    public List<DialogLine> GetDialogsForItem(string itemId)
    {
        if (itemRegistry.ContainsKey(itemId))
        {
            var itemData = itemRegistry[itemId];
            string csvFileName = GetCSVFileNameByIndex(itemData.csvFileIndex);
            
            if (!string.IsNullOrEmpty(csvFileName))
            {
                // Lade alle Dialoge aus der CSV
                if (allDialogs.ContainsKey(csvFileName))
                {
                    var allCsvDialogs = allDialogs[csvFileName];
                    var filteredDialogs = FilterAvailableDialogs(allCsvDialogs);
                    
                    // Finde den spezifischen Dialog mit der passenden memoryId (= itemId)
                    var specificDialogs = new List<DialogLine>();
                    foreach (var dialog in filteredDialogs)
                    {
                        if (dialog.memoryId == itemId)
                        {
                            specificDialogs.Add(dialog);
                        }
                    }
                    
                    if (specificDialogs.Count > 0)
                    {
                        Debug.Log($"Item '{itemId}': {specificDialogs.Count} Dialoge gefunden");
                        return specificDialogs;
                    }
                    else
                    {
                        Debug.LogWarning($"Item '{itemId}': Keine Dialoge verfügbar (Loop: {currentLoopCount})");
                    }
                }
            }
            else
            {
                Debug.LogWarning($"Ungültiger CSV-Index {itemData.csvFileIndex} für Item '{itemId}'!");
            }
        }
        
        Debug.LogWarning($"Item '{itemId}' nicht in Registry gefunden!");
        return new List<DialogLine>();
    }
    
    public List<DialogLine> GetAllDialogsFromCSV(string csvFileName)
    {
        Debug.Log($"GetAllDialogsFromCSV aufgerufen mit: '{csvFileName}'");
        Debug.Log($"Verfügbare CSV-Dateien: {string.Join(", ", allDialogs.Keys)}");
        
        if (allDialogs.ContainsKey(csvFileName))
        {
            var dialogs = FilterAvailableDialogs(allDialogs[csvFileName]);
            Debug.Log($"Gefilterte Dialoge für '{csvFileName}': {dialogs.Count}");
            return dialogs;
        }
        
        Debug.LogWarning($"CSV-Datei '{csvFileName}' nicht gefunden!");
        return new List<DialogLine>();
    }
    
    // NPC-Info abrufen
    public NPCData GetNPCData(string npcId)
    {
        return npcRegistry.ContainsKey(npcId) ? npcRegistry[npcId] : null;
    }

    public string GetNPCName(string npcId)
    {
        var npcData = GetNPCData(npcId);
        return npcData != null ? npcData.npcName : npcId;
    }
    
    // Item-Info abrufen
    public ItemData GetItemData(string itemId)
    {
        return itemRegistry.ContainsKey(itemId) ? itemRegistry[itemId] : null;
    }

    // Player-Name Verwaltung
    public string GetPlayerName()
    {
        return playerName;
    }
    
    public void SetPlayerName(string newName)
    {
        string oldName = playerName;
        playerName = newName;
        Debug.Log($"Spieler-Name geändert: '{oldName}' → '{newName}'");
    }
    
    // Speziell für Items: Finde das korrekte Item basierend auf memoryId
    public DialogLine GetCurrentItemDialog(string memoryId)
    {
        // itemId = memoryId, also direkte Suche
        var dialogs = GetDialogsForItem(memoryId);
        if (dialogs.Count > 0)
        {
            return dialogs[0]; // Erstes verfügbares Dialog
        }
        
        Debug.LogWarning($"Kein Item mit itemId/memoryId '{memoryId}' gefunden oder verfügbar!");
        return null;
    }
    
    // Hilfsmethode: Alle verfügbaren Items für aktuellen Loop/Memory-Status
    public List<DialogLine> GetAllAvailableItems()
    {
        List<DialogLine> availableItems = new List<DialogLine>();
        
        foreach (var csvDialogs in allDialogs.Values)
        {
            var filteredDialogs = FilterAvailableDialogs(csvDialogs);
            availableItems.AddRange(filteredDialogs);
        }
        
        return availableItems;
    }
    
    // Finde das beste verfügbare Item für eine Basis-itemId (z.B. "mirrow" -> "mirrow_broken" oder "mirrow_fixed")
    public ItemData GetBestAvailableItem(string baseItemId)
    {
        ItemData bestItem = null;
        DialogLine bestDialog = null;
        
        // Suche alle Items die mit der baseItemId beginnen
        foreach (var kvp in itemRegistry)
        {
            var itemData = kvp.Value;
            if (itemData.itemId.StartsWith(baseItemId))
            {
                var dialogs = GetDialogsForItem(kvp.Key);
                if (dialogs.Count > 0)
                {
                    var dialog = dialogs[0];
                    // Nehme das Item mit dem höchsten minLoop (fortgeschrittenster Status)
                    if (bestDialog == null || dialog.minLoop > bestDialog.minLoop)
                    {
                        bestItem = itemData;
                        bestDialog = dialog;
                    }
                }
            }
        }
        
        return bestItem;
    }
    
    // Debug-Hilfsmethode: Zeigt alle verfügbaren memoryIds aus der Items-CSV
    [ContextMenu("Show Available Item MemoryIds")]
    public void ShowAvailableItemMemoryIds()
    {
        Debug.Log("=== VERFÜGBARE ITEM MEMORY IDs ===");
        
        // Durchlaufe alle registrierten Items und zeige ihre Dialoge
        foreach (var kvp in itemRegistry)
        {
            var itemId = kvp.Key;
            var itemData = kvp.Value;
            string csvName = GetCSVFileNameByIndex(itemData.csvFileIndex);
            
            Debug.Log($"Item '{itemId}' (CSV Index {itemData.csvFileIndex}: {csvName}):");
            
            if (allDialogs.ContainsKey(csvName))
            {
                var dialogs = allDialogs[csvName];
                foreach (var dialog in dialogs)
                {
                    if (dialog.memoryId == itemId)
                    {
                        Debug.Log($"  memoryId: '{dialog.memoryId}' | minLoop: {dialog.minLoop} | Text: {dialog.text.Substring(0, Mathf.Min(50, dialog.text.Length))}...");
                    }
                }
            }
            else
            {
                Debug.LogWarning($"  CSV '{csvName}' nicht gefunden!");
            }
        }
        
        Debug.Log($"Insgesamt {itemRegistry.Count} Items registriert");
    }
    
    // Memory-System Methoden
    public void AddMemory(string memoryId)
    {
        if (string.IsNullOrEmpty(memoryId)) return;
        
        bool wasNewMemory = memoryFlags.Add(memoryId);
        
        if (wasNewMemory)
        {
            // Neue Memory gefunden - tracke für Loop-Progression
            memoriesFoundInCurrentLoop.Add(memoryId);
            
            Debug.Log($"Memory hinzugefügt: {memoryId} (neu in diesem Loop)");
            Debug.Log($"Memories in aktuellem Loop gefunden: {memoriesFoundInCurrentLoop.Count}");
        }
        else
        {
            Debug.Log($"Memory bereits vorhanden: {memoryId} (keine Progression)");
        }
    }
    
    public bool HasMemory(string memoryId)
    {
        return memoryFlags.Contains(memoryId);
    }
    
    public void RemoveMemory(string memoryId)
    {
        if (memoryFlags.Contains(memoryId))
        {
            memoryFlags.Remove(memoryId);
            Debug.Log($"Memory entfernt: {memoryId}");
        }
    }
    
    public void ClearAllMemory()
    {
        memoryFlags.Clear();
        Debug.Log("Alle Memory-Flags gelöscht");
    }
    
    // === ERWEITERTE MEMORY-VERARBEITUNG FÜR CSV-FELDER ===
    
    // Verarbeite addMemory aus einem Dialog (wird nach dem Dialog-Text ausgeführt)
    public void ProcessDialogMemory(DialogLine dialog)
    {
        if (dialog.addMemory != null && dialog.addMemory.Count > 0)
        {
            foreach (var memory in dialog.addMemory)
            {
                if (!string.IsNullOrEmpty(memory))
                {
                    AddMemory(memory);
                }
            }
        }
    }
    
    // Verarbeite Choice-Memory (wird nach der Choice-Auswahl ausgeführt)
    public void ProcessChoiceMemory(DialogLine.ChoiceData choice)
    {
        if (choice.addMemory != null && choice.addMemory.Count > 0)
        {
            foreach (var memory in choice.addMemory)
            {
                if (!string.IsNullOrEmpty(memory))
                {
                    AddMemory(memory);
                }
            }
        }
    }
    
    // Prüfe ob alle Choice-Requirements erfüllt sind
    public bool AreChoiceRequirementsMet(DialogLine.ChoiceData choice)
    {
        if (choice.requiredMemory == null || choice.requiredMemory.Count == 0)
        {
            return true; // Keine Requirements = immer verfügbar
        }
        
        foreach (var requirement in choice.requiredMemory)
        {
            if (!HasMemory(requirement))
            {
                return false;
            }
        }
        
        return true;
    }
    
    // Filtere verfügbare Choices basierend auf Memory-Requirements
    public List<DialogLine.ChoiceData> GetAvailableChoices(DialogLine dialog)
    {
        var availableChoices = new List<DialogLine.ChoiceData>();
        
        if (dialog.choices != null)
        {
            foreach (var choice in dialog.choices)
            {
                if (AreChoiceRequirementsMet(choice))
                {
                    availableChoices.Add(choice);
                }
            }
        }
        
        return availableChoices;
    }
    
    // Erweiterte Dialog-Filterung mit minLoop UND Memory-Requirements
    private List<DialogLine> FilterAvailableDialogs(List<DialogLine> dialogs)
    {
        var availableDialogs = new List<DialogLine>();
        
        foreach (var dialog in dialogs)
        {
            // 1. Prüfe Loop-Bedingung (minLoop)
            if (currentLoopCount < dialog.minLoop) 
            {
                continue;
            }
            
            // 2. Prüfe Dialog-Memory-Requirements (requiredMemory)
            if (!AreDialogRequirementsMet(dialog))
            {
                continue;
            }
            
            availableDialogs.Add(dialog);
        }
        
        return availableDialogs;
    }
    
    // === STRANGER DIALOG EVOLUTION SYSTEM ===
    
    // Finde den besten verfügbaren Dialog für einen NPC (höchster minLoop + erfüllte Requirements)
    public DialogLine GetBestAvailableDialogForNPC(string npcId)
    {
        var allDialogs = GetDialogsForNPC(npcId);
        return GetBestAvailableDialog(allDialogs, npcId);
    }
    
    // Finde den besten verfügbaren Dialog für ein Item (höchster minLoop + erfüllte Requirements)
    public DialogLine GetBestAvailableDialogForItem(string itemId)
    {
        var allDialogs = GetDialogsForItem(itemId);
        return GetBestAvailableDialog(allDialogs, itemId);
    }
    
    // Kern-Methode: Finde besten Dialog aus einer Liste
    private DialogLine GetBestAvailableDialog(List<DialogLine> dialogs, string contextId)
    {
        if (dialogs == null || dialogs.Count == 0)
        {
            Debug.LogWarning($"GetBestAvailableDialog: Keine Dialoge für '{contextId}' verfügbar");
            return null;
        }
        
        DialogLine bestDialog = null;
        int highestValidMinLoop = -1;
        
        if (showDebugLogs)
        {

            Debug.Log($"=== FINDE BESTEN DIALOG FÜR '{contextId}' ===");
            Debug.Log($"Aktueller Loop: {currentLoopCount}");
            Debug.Log($"Verfügbare Dialoge: {dialogs.Count}");
        }
        
        foreach (var dialog in dialogs)
        {
            // 1. Prüfe Loop-Bedingung (minLoop)
            if (currentLoopCount < dialog.minLoop)
            {
                if (showDebugLogs)
                {
                    Debug.Log($"  Dialog '{dialog.memoryId}' (minLoop: {dialog.minLoop}): ❌ LOOP ZU NIEDRIG");
                }
                continue;
            }
            
            // 2. Prüfe Dialog-Memory-Requirements
            if (!AreDialogRequirementsMet(dialog))
            {
                if (showDebugLogs)
                {
                    var missingMemories = dialog.requiredMemory?.Where(m => !HasMemory(m)) ?? new List<string>();
                    Debug.Log($"  Dialog '{dialog.memoryId}' (minLoop: {dialog.minLoop}): ❌ MEMORY FEHLT [{string.Join(", ", missingMemories)}]");
                }
                continue;
            }
            
            // 3. Dieser Dialog ist verfügbar - prüfe ob er besser ist als der aktuelle beste
            if (dialog.minLoop > highestValidMinLoop)
            {
                bestDialog = dialog;
                highestValidMinLoop = dialog.minLoop;
                
                if (showDebugLogs)
                {
                    Debug.Log($"  Dialog '{dialog.memoryId}' (minLoop: {dialog.minLoop}): ✅ NEUE BESTE WAHL");
                }
            }
            else if (showDebugLogs)
            {
                Debug.Log($"  Dialog '{dialog.memoryId}' (minLoop: {dialog.minLoop}): ⚪ Gültig aber nicht besser (aktuell beste: {highestValidMinLoop})");
            }
        }
        
        if (bestDialog != null && showDebugLogs)
        {
            Debug.Log($"=== BESTER DIALOG: '{bestDialog.memoryId}' (minLoop: {bestDialog.minLoop}) ===");
            Debug.Log($"Text Vorschau: {bestDialog.text.Substring(0, Math.Min(100, bestDialog.text.Length))}...");
        }
        else if (showDebugLogs)
        {
            Debug.Log($"=== KEIN GÜLTIGER DIALOG GEFUNDEN FÜR '{contextId}' ===");
        }
        
        return bestDialog;
    }
    
    // Prüfe ob alle Dialog-Requirements erfüllt sind
    private bool AreDialogRequirementsMet(DialogLine dialog)
    {
        if (dialog.requiredMemory == null || dialog.requiredMemory.Count == 0)
        {
            return true; // Keine Requirements = immer verfügbar
        }
        
        foreach (var requirement in dialog.requiredMemory)
        {
            if (!HasMemory(requirement))

            {
                return false;
            }
        }
        
        return true;
    }
    
    // Debug-Methode: Zeige alle Memory-Abhängigkeiten eines Dialogs
    public void AnalyzeDialogMemoryDependencies(DialogLine dialog)
    {
        Debug.Log($"=== MEMORY-ANALYSE für Dialog '{dialog.memoryId}' ===");
        Debug.Log($"MinLoop: {dialog.minLoop} (aktuell: {currentLoopCount}) - {(currentLoopCount >= dialog.minLoop ? "✓ ERFÜLLT" : "✗ NICHT ERFÜLLT")}");
        
        // Dialog Requirements
        if (dialog.requiredMemory != null && dialog.requiredMemory.Count > 0)
        {
            Debug.Log("Dialog Requirements:");
            foreach (var req in dialog.requiredMemory)
            {
                Debug.Log($"  - {req}: {(HasMemory(req) ? "✓ ERFÜLLT" : "✗ FEHLT")}");
            }
        }
        else
        {
            Debug.Log("Dialog Requirements: KEINE");
        }
        
        // Dialog AddMemory
        if (dialog.addMemory != null && dialog.addMemory.Count > 0)
        {
            Debug.Log($"Dialog AddMemory: {string.Join(", ", dialog.addMemory)}");
        }
        
        // Choice Requirements
        if (dialog.choices != null)
        {
            for (int i = 0; i < dialog.choices.Count; i++)
            {
                var choice = dialog.choices[i];
                Debug.Log($"Choice {i + 1}: '{choice.choiceText}'");
                
                if (choice.requiredMemory != null && choice.requiredMemory.Count > 0)
                {
                    Debug.Log($"  Requirements:");
                    foreach (var req in choice.requiredMemory)
                    {
                        Debug.Log($"    - {req}: {(HasMemory(req) ? "✓ ERFÜLLT" : "✗ FEHLT")}");
                    }
                }
                
                if (choice.addMemory != null && choice.addMemory.Count > 0)
                {
                    Debug.Log($"  AddMemory: {string.Join(", ", choice.addMemory)}");
                }
                
                Debug.Log($"  Verfügbar: {(AreChoiceRequirementsMet(choice) ? "✓ JA" : "✗ NEIN")}");
            }
        }
    }
    
    // Loop-System
    public void NextLoop()
    {
        currentLoopCount++;
        
        // Reset: Memories für neuen Loop zurücksetzen
        memoriesFoundInCurrentLoop.Clear();
        
        Debug.Log($"Neuer Loop: {currentLoopCount}");
        Debug.Log($"Memory-Tracker für neuen Loop zurückgesetzt");
        
        // StrangerChange-Logik
        if (targetImageObject != null)
        {
            if (currentLoopCount == 6 && imageForLoop6 != null)
            {
                targetImageObject.sprite = imageForLoop6;
            }
            else if (currentLoopCount == 9 && imageForLoop9 != null)
            {
                targetImageObject.sprite = imageForLoop9;
            }
        }

        // Notebook Image Change: Bildwechsel in Loop 2
        if (notebookImageTarget != null && notebookNewSprite != null && currentLoopCount == 2)
        {
            var imageComp = notebookImageTarget.GetComponent<UnityEngine.UI.Image>();
            if (imageComp != null)
            {
                imageComp.sprite = notebookNewSprite;
            }
            else
            {
                var spriteRenderer = notebookImageTarget.GetComponent<SpriteRenderer>();
                if (spriteRenderer != null)
                {
                    spriteRenderer.sprite = notebookNewSprite;
                }
            }
        }

        OnLoopChanged(currentLoopCount);
    }
    
    // Wird vom WagonManager aufgerufen wenn ein Loop abgeschlossen wird
    public void OnLoopCompleted()
    {
        Debug.Log("=== LOOP ABGESCHLOSSEN: GameManager validiert Loop ===");
        
        // Loop-Validierung: Prüfe ob der Spieler neue Memories erhalten hat
        bool isValidLoop = ValidateLoop();
        
        if (isValidLoop)
        {
            int oldLoop = currentLoopCount;
            NextLoop();
            Debug.Log($"Loop-Count erhöht: {oldLoop} → {currentLoopCount} (Loop war gültig)");
        }
        else
        {
            Debug.Log($"Loop-Count NICHT erhöht - Loop war nicht gültig (aktuell bleibt: {currentLoopCount})");
        }
    }
    
    // Loop-Validierung: Prüfe ob alle verfügbaren Memories des aktuellen Loops gefunden wurden
    private bool ValidateLoop()
    {
        Debug.Log("=== LOOP-VALIDIERUNG STARTET ===");
        Debug.Log($"Aktueller Loop: {currentLoopCount}");
        Debug.Log($"Memories in diesem Loop gefunden: {memoriesFoundInCurrentLoop.Count} [{string.Join(", ", memoriesFoundInCurrentLoop)}]");

        // SPEZIELLE REGEL: Loop 1, 2, 9 und 10 sind immer gültig (keine Memory-Anforderungen)
        if (currentLoopCount == 1 || currentLoopCount == 2 || currentLoopCount == 9 || currentLoopCount == 10)
        {
            Debug.Log($"✅ LOOP {currentLoopCount} AUTOMATISCH GÜLTIG: Loop 1, 2, 9 und 10 haben keine Memory-Anforderungen");
            return true;
        }
        
        // Prüfe ob es für diesen Loop überhaupt verfügbare Memories gibt
        if (!requiredMemoriesPerLoop.ContainsKey(currentLoopCount))
        {
            Debug.Log($"Loop {currentLoopCount}: Keine verfügbaren Memories definiert - Loop ist GÜLTIG");
            return true;
        }
        
        var requiredMemories = requiredMemoriesPerLoop[currentLoopCount];
        Debug.Log($"Verfügbare Memories in Loop {currentLoopCount}: {requiredMemories.Count} [{string.Join(", ", requiredMemories)}]");
        
        // Prüfe ob ALLE verfügbaren Memories gefunden wurden
        bool allMemoriesFound = true;
        var missingMemories = new HashSet<string>();
        
        foreach (var requiredMemory in requiredMemories)
        {
            if (!memoriesFoundInCurrentLoop.Contains(requiredMemory))
            {
                allMemoriesFound = false;
                missingMemories.Add(requiredMemory);
            }
        }
        
        if (allMemoriesFound)
        {
            Debug.Log($"✅ LOOP GÜLTIG: Alle {requiredMemories.Count} verfügbaren Memories gefunden!");
            return true;
        }
        else
        {
            Debug.Log($"❌ LOOP UNGÜLTIG: {missingMemories.Count} Memories fehlen: [{string.Join(", ", missingMemories)}]");
            Debug.Log($"💡 TIPP: Erkunde den aktuellen Loop vollständig um alle Memories zu finden.");
            return false;
        }
    }
    
    public void ResetLoop()
    {
        currentLoopCount = 1;
        memoriesFoundInCurrentLoop.Clear();
        
        // Window2 Image System zurücksetzen
        if (window2SystemInitialized)
        {
            isWindow2Switching = false;
            
            // Sound stoppen falls noch aktiv
            if (window2CountdownAudioSource != null && window2CountdownAudioSource.isPlaying)
            {
                window2CountdownAudioSource.Stop();
                
                if (showDebugLogs)
                {
                    Debug.Log("Window2 Countdown-Sound gestoppt (Reset)");
                }
            }
            
            // Image zurück zum Original setzen
            if (window2GameObject != null && originalWindow2Sprite != null)
            {
                var window2Image = window2GameObject.GetComponent<Image>();
                if (window2Image != null)
                {
                    window2Image.sprite = originalWindow2Sprite;
                    
                    if (showDebugLogs)
                    {
                        Debug.Log("Window2 Image zurück zum Original gesetzt");
                    }
                }
            }
            
            if (showDebugLogs)
            {
                Debug.Log("Window2 Image System zurückgesetzt");
            }
        }
        
        Debug.Log("Loop zurückgesetzt - Memory-Tracker geleert");
    }
    
    public int GetCurrentLoop()
    {
        return currentLoopCount;
    }
    
    // Debug-Methoden
    public void PrintAllMemory()
    {
        Debug.Log($"Aktuelle Memory-Flags ({memoryFlags.Count}): {string.Join(", ", memoryFlags)}");
    }
    
    public void PrintLoopProgressionStatus()
    {
        Debug.Log("=== LOOP-PROGRESSION STATUS ===");
        Debug.Log($"Aktueller Loop: {currentLoopCount}");
        Debug.Log($"Memories in diesem Loop gefunden: {memoriesFoundInCurrentLoop.Count} [{string.Join(", ", memoriesFoundInCurrentLoop)}]");
        
        if (requiredMemoriesPerLoop.ContainsKey(currentLoopCount))
        {
            var required = requiredMemoriesPerLoop[currentLoopCount];
            Debug.Log($"Verfügbare Memories in Loop {currentLoopCount}: {required.Count} [{string.Join(", ", required)}]");
            
            var missing = new HashSet<string>(required);
            missing.ExceptWith(memoriesFoundInCurrentLoop);
            
            if (missing.Count > 0)
            {
                Debug.Log($"Fehlende Memories: {missing.Count} [{string.Join(", ", missing)}]");
            }
            else
            {
                Debug.Log($"✅ Alle Memories für Loop {currentLoopCount} gefunden!");
            }
        }
        else
        {
            Debug.Log($"Keine verfügbaren Memories für Loop {currentLoopCount} definiert");
        }
    }
    
    [ContextMenu("Analyze Memory Dependencies")]
    public void AnalyzeMemoryDependenciesFromMenu()
    {
        PrintCompleteMemoryAnalysis();
        PrintMemoryMappingStats();
        FindUnlinkedMemories();
    }
    
    [ContextMenu("Analyze Loop Progression System")]
    public void AnalyzeLoopProgressionSystem()
    {
        Debug.Log("=== LOOP-PROGRESSION-SYSTEM ANALYSE ===");
        
        Debug.Log($"📋 NOTIZBUCH-ANALYSE:");
        Debug.Log($"  - Notizbuch CSV: {(dialogCSVFiles != null && dialogCSVFiles.Length > 0 ? dialogCSVFiles[dialogCSVFiles.Length - 1]?.name : "NICHT GEFUNDEN")}");
        Debug.Log($"  - Notizbuch-spezifische RequiredMemories: {notebookOnlyMemories.Count}");
        
        // Zähle "newdraw"-Memories
        var newdrawCount = allAvailableMemories.Count(m => m.StartsWith("newdraw", System.StringComparison.OrdinalIgnoreCase));
        
        Debug.Log($"🔄 LOOP-RELEVANZ-ANALYSE:");
        Debug.Log($"  - Gesamt AddMemories: {allAvailableMemories.Count}");
        Debug.Log($"  - NewDraw AddMemories (ausgeschlossen): {newdrawCount}");
        Debug.Log($"  - Loop-relevante AddMemories: {loopRelevantAddMemories.Count}");
        Debug.Log($"  - Nicht loop-relevante AddMemories: {allAvailableMemories.Count - loopRelevantAddMemories.Count - newdrawCount}");
        
        Debug.Log($"📊 LOOP-PROGRESSION-VERTEILUNG:");
        foreach (var kvp in requiredMemoriesPerLoop.OrderBy(x => x.Key))
        {
            Debug.Log($"  - Loop {kvp.Key}: {kvp.Value.Count} relevante Memories [{string.Join(", ", kvp.Value)}]");
        }
        
        if (requiredMemoriesPerLoop.Count == 0)
        {
            Debug.LogWarning("⚠️ WARNUNG: Keine loop-relevanten Memories gefunden! Loop-Progression wird immer erfolgreich sein.");
        }
        
        // Aktuelle Loop-Status
        if (requiredMemoriesPerLoop.ContainsKey(currentLoopCount))
        {
            var currentLoopMemories = requiredMemoriesPerLoop[currentLoopCount];
            var foundInCurrentLoop = memoriesFoundInCurrentLoop.Intersect(currentLoopMemories).ToList();
            var missingInCurrentLoop = currentLoopMemories.Except(memoriesFoundInCurrentLoop).ToList();
            
            Debug.Log($"🎯 AKTUELLER LOOP ({currentLoopCount}) STATUS:");
            Debug.Log($"  - Verfügbare Memories: {currentLoopMemories.Count}");
            Debug.Log($"  - Bereits gefunden: {foundInCurrentLoop.Count} [{string.Join(", ", foundInCurrentLoop)}]");
            Debug.Log($"  - Noch fehlend: {missingInCurrentLoop.Count} [{string.Join(", ", missingInCurrentLoop)}]");
        }
        else
        {
            Debug.Log($"🎯 AKTUELLER LOOP ({currentLoopCount}): Keine relevanten Memories definiert - Loop ist automatisch gültig");
        }
        
        Debug.Log($"🚫 AUSSCHLUSS-REGELN:");
        Debug.Log($"  - Notizbuch-spezifische RequiredMemories: ausgeschlossen");
        Debug.Log($"  - AddMemories beginnend mit 'newdraw': ausgeschlossen");
        Debug.Log($"  - Verwaiste AddMemories (ohne RequiredMemory): ausgeschlossen");
        Debug.Log($"  - Choice-Ketten die ins Leere führen: ausgeschlossen");
        Debug.Log($"    (Neue Funktion: Erkennt wenn Choice-Antworten zu AddMemories führen die nur Notizbuch/nie verwendet werden)");
    }
    
    public void PrintAllDialogs()
    {
        foreach (var kvp in allDialogs)
        {
            Debug.Log($"CSV: {kvp.Key} - {kvp.Value.Count} Dialoge");
        }
    }
    
    public List<string> GetAllMemory()
    {
        return new List<string>(memoryFlags);
    }
    
    public Dictionary<string, int> GetDialogStats()
    {
        var stats = new Dictionary<string, int>();
        foreach (var kvp in allDialogs)
        {
            stats[kvp.Key] = kvp.Value.Count;
        }
        return stats;
    }
    
    // Utility-Methoden für sicheren Zugriff von anderen Scripts
    public static bool SafeHasMemory(string memoryId)
    {
        return Instance != null && Instance.HasMemory(memoryId);
    }
    
    public static void SafeAddMemory(string memoryId)
    {
        if (Instance != null)
        {
            Instance.AddMemory(memoryId);
        }
        else
        {
            Debug.LogWarning($"GameManager Instance ist null! Kann Memory '{memoryId}' nicht setzen.");
        }
    }
    
    public static int SafeGetCurrentLoop()
    {
        return Instance != null ? Instance.currentLoopCount : 1;
    }
    
    public static List<DialogLine> SafeGetDialogsForNPC(string npcId)
    {
        return Instance != null ? Instance.GetDialogsForNPC(npcId) : new List<DialogLine>();
    }
    
    public static List<DialogLine> SafeGetDialogsForItem(string itemId)
    {
        return Instance != null ? Instance.GetDialogsForItem(itemId) : new List<DialogLine>();
    }
    
    public static ItemData SafeGetItemData(string itemId)
    {
        return Instance != null ? Instance.GetItemData(itemId) : null;
    }
    
    public static DialogLine SafeGetCurrentItemDialog(string memoryId)
    {
        return Instance != null ? Instance.GetCurrentItemDialog(memoryId) : null;
    }
    
    public static string SafeGetPlayerName()
    {
        return Instance != null ? Instance.GetPlayerName() : " . . . ";
    }
    
    // === ERWEITERTE STATIC UTILITY-METHODEN FÜR MEMORY-SYSTEM ===
    
    public static void SafeProcessDialogMemory(DialogLine dialog)
    {
        if (Instance != null)
        {
            Instance.ProcessDialogMemory(dialog);
        }
    }
    
    public static void SafeProcessChoiceMemory(DialogLine.ChoiceData choice)
    {
        if (Instance != null)
        {
            Instance.ProcessChoiceMemory(choice);
        }
    }
    
    public static bool SafeAreChoiceRequirementsMet(DialogLine.ChoiceData choice)
    {
        return Instance != null ? Instance.AreChoiceRequirementsMet(choice) : false;
    }
    
    public static List<DialogLine.ChoiceData> SafeGetAvailableChoices(DialogLine dialog)
    {
        return Instance != null ? Instance.GetAvailableChoices(dialog) : new List<DialogLine.ChoiceData>();
    }
    
    public static void SafeAnalyzeDialogMemoryDependencies(DialogLine dialog)
    {
        if (Instance != null)
        {
            Instance.AnalyzeDialogMemoryDependencies(dialog);
        }
    }
    
    // Sichere Prüfung der Dialog-Verfügbarkeit (minLoop + Memory-Requirements)
    public static bool SafeIsDialogLineAvailable(DialogLine dialog)
    {
        if (Instance == null) return false;
        
        // Prüfe minLoop
        if (Instance.currentLoopCount < dialog.minLoop)
            return false;
            
        // Prüfe Memory-Requirements
        return Instance.AreDialogRequirementsMet(dialog);
    }

    private void OnLoopChanged(int newLoop)
    {
        // Deaktiviere das zugewiesene Objekt in Loop 6
        if (newLoop == 6 && objectToDeactivateInLoop6 != null)
        {
            objectToDeactivateInLoop6.SetActive(false);
        }
        
        // Deaktiviere das zugewiesene Objekt in Loop 11
        if (newLoop == 11 && objectToDeactivateInLoop11 != null)
        {
            objectToDeactivateInLoop11.SetActive(false);
        }
    }
}