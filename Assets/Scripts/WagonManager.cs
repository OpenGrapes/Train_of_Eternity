using UnityEngine;
using System.Collections;

public class WagonManager : MonoBehaviour
{
    public static WagonManager Instance;
    
    [Header("Wagon System")]
    public int currentWagonNumber = 1;
    public int maxWagons = 5;
    
    [Header("Canvas References")]
    public Canvas[] wagonCanvases; // [0]=CanvasWagon1, [1]=CanvasWagon2, etc.
    public Canvas transitionCanvas;
    
    [Header("Settings")]
    public float transitionDuration = 1f;
    
    [Header("Loop Transition Sound")]
    [Tooltip("AudioSource für Loop-Übergang Sound (Wagon 5 → Wagon 1)")]
    public AudioSource loopTransitionAudioSource;
    
    [Header("Music Mute System")]
    [Tooltip("AudioSources der Hintergrundmusik die während Loop-Übergang gemutet werden sollen")]
    public AudioSource[] backgroundMusicSources;
    [Tooltip("Mute alle Hintergrundmusik während des Loop-Übergangs")]
    public bool muteBackgroundMusicDuringLoop = true;
    
    [Header("References")]
    public DialogManager dialogManager;
    
    [Header("Debug")]
    public bool showDebugInfo = true;
    
    private bool isTransitioning = false;
    
    // Mute-System für Loop-Übergang
    private float[] originalMusicVolumes;
    private bool musicWasMuted = false;
    
    private void Awake()
    {
        // Singleton Pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }
    
    private void Start()
    {
        // Automatisch DialogManager finden
        if (dialogManager == null)
        {
            dialogManager = FindFirstObjectByType<DialogManager>();
        }
        
        InitializeWagons();
        
        if (showDebugInfo)
        {
            Debug.Log($"WagonManager initialisiert - Wagon {currentWagonNumber} aktiv");
        }
    }
    
    // Wagon-System initialisieren
    private void InitializeWagons()
    {
        if (wagonCanvases == null || wagonCanvases.Length == 0)
        {
            Debug.LogError("WagonManager: Keine Wagon-Canvases zugewiesen!");
            return;
        }

        // Alle Wagon-Canvases deaktivieren
        for (int i = 0; i < wagonCanvases.Length; i++)
        {
            if (wagonCanvases[i] != null)
            {
                wagonCanvases[i].gameObject.SetActive(false);
            }
        }

        // Übergangs-Canvas deaktivieren
        if (transitionCanvas != null)
        {
            transitionCanvas.gameObject.SetActive(false);
        }

        // Ersten Wagon aktivieren
        if (wagonCanvases.Length > 0 && wagonCanvases[0] != null)
        {
            wagonCanvases[0].gameObject.SetActive(true);
            currentWagonNumber = 1;

            if (showDebugInfo)
            {
                Debug.Log($"WagonManager: Wagon {currentWagonNumber} aktiviert");
            }
        }
    }
    
    // Aktiviere alle Wagon-Canvases temporär für Cache-Erstellung
    public void ActivateAllCanvasesForCaching()
    {
        if (showDebugInfo)
        {
            Debug.Log("WagonManager: Aktiviere alle Canvases temporär für Item-Cache");
        }
        
        for (int i = 0; i < wagonCanvases.Length; i++)
        {
            if (wagonCanvases[i] != null)
            {
                wagonCanvases[i].gameObject.SetActive(true);
                if (showDebugInfo)
                {
                    Debug.Log($"WagonManager: Canvas {i + 1} aktiviert für Caching");
                }
            }
        }
        
        // Übergangs-Canvas auch aktivieren falls Items darauf sind
        if (transitionCanvas != null)
        {
            transitionCanvas.gameObject.SetActive(true);
        }
    }
    
    // Stelle normale Wagon-Canvas Konfiguration wieder her
    public void RestoreNormalCanvasState()
    {
        if (showDebugInfo)
        {
            Debug.Log("WagonManager: Stelle normale Canvas-Konfiguration wieder her");
        }
        
        // Alle Canvases deaktivieren
        for (int i = 0; i < wagonCanvases.Length; i++)
        {
            if (wagonCanvases[i] != null)
            {
                wagonCanvases[i].gameObject.SetActive(false);
            }
        }
        
        // Übergangs-Canvas deaktivieren
        if (transitionCanvas != null)
        {
            transitionCanvas.gameObject.SetActive(false);
        }
        
        // Aktuellen Wagon wieder aktivieren
        if (currentWagonNumber > 0 && currentWagonNumber <= wagonCanvases.Length)
        {
            int wagonIndex = currentWagonNumber - 1;
            if (wagonCanvases[wagonIndex] != null)
            {
                wagonCanvases[wagonIndex].gameObject.SetActive(true);
                if (showDebugInfo)
                {
                    Debug.Log($"WagonManager: Wagon {currentWagonNumber} wieder aktiviert nach Caching");
                }
            }
        }
    }    // Von WagonDoor aufgerufen - zum nächsten Wagon (mit Loop)
    public void GoToNextWagon()
    {
        if (isTransitioning)
        {
            if (showDebugInfo)
            {
                Debug.Log("WagonManager: Übergang bereits aktiv - ignoriere Anfrage");
            }
            return;
        }
        
        // Loop-Logik: Nach dem letzten Wagon zurück zum ersten
        int nextWagon;
        if (currentWagonNumber >= maxWagons)
        {
            nextWagon = 1; // Zurück zum ersten Wagon
            if (showDebugInfo)
            {
                Debug.Log($"WagonManager: Loop - von Wagon {maxWagons} zurück zu Wagon 1");
            }
        }
        else
        {
            nextWagon = currentWagonNumber + 1; // Nächster Wagon
        }
        
        // Dialog schließen falls aktiv
        if (dialogManager != null)
        {
            dialogManager.ForceEndDialog();
        }
        
        StartCoroutine(TransitionToWagon(nextWagon));
    }
    
    // Zu spezifischem Wagon wechseln
    public void GoToWagon(int wagonNumber)
    {
        if (isTransitioning)
        {
            Debug.Log("WagonManager: Übergang bereits aktiv - ignoriere direkten Wechsel");
            return;
        }
        
        if (wagonNumber < 1 || wagonNumber > maxWagons)
        {
            Debug.LogWarning($"WagonManager: Ungültige Wagon-Nummer: {wagonNumber} (gültig: 1-{maxWagons})");
            return;
        }
        
        if (wagonNumber == currentWagonNumber)
        {
            if (showDebugInfo)
            {
                Debug.Log($"WagonManager: Bereits in Wagon {wagonNumber}");
            }
            return;
        }
        
        // Dialog schließen falls aktiv
        if (dialogManager != null)
        {
            dialogManager.ForceEndDialog();
        }
        
        StartCoroutine(TransitionToWagon(wagonNumber));
    }
    
    // Zum vorherigen Wagon wechseln (mit Loop)
    public void GoToPreviousWagon()
    {
        if (isTransitioning)
        {
            Debug.Log("WagonManager: Übergang bereits aktiv - ignoriere Rückwärts-Anfrage");
            return;
        }
        
        // Loop-Logik: Vom ersten Wagon zum letzten
        int previousWagon;
        if (currentWagonNumber <= 1)
        {
            previousWagon = maxWagons; // Zum letzten Wagon
            if (showDebugInfo)
            {
                Debug.Log($"WagonManager: Loop - von Wagon 1 zurück zu Wagon {maxWagons}");
            }
        }
        else
        {
            previousWagon = currentWagonNumber - 1; // Vorheriger Wagon
        }
        
        // Dialog schließen falls aktiv
        if (dialogManager != null)
        {
            dialogManager.ForceEndDialog();
        }
        
        StartCoroutine(TransitionToWagon(previousWagon));
    }
    
    // Coroutine für den Wagon-Übergang
    private IEnumerator TransitionToWagon(int targetWagon)
    {
        isTransitioning = true;
        
        if (showDebugInfo)
        {
            Debug.Log($"=== WAGON ÜBERGANG: {currentWagonNumber} → {targetWagon} ===");
        }
        
        // Prüfe ob es ein Loop-Übergang ist (Wagon 5 → Wagon 1)
        bool isLoopTransition = (currentWagonNumber == maxWagons && targetWagon == 1);
        
        // 1. ÜBERGANGS-CANVAS AKTIVIEREN
        if (transitionCanvas != null)
        {
            transitionCanvas.gameObject.SetActive(true);
            if (showDebugInfo)
            {
                Debug.Log("Übergangs-Canvas aktiviert");
            }
        }
        
        // 2. NEUEN WAGON AKTIVIEREN (sofort nach Canvas-Aktivierung)
        int targetIndex = targetWagon - 1; // Array ist 0-basiert
        if (targetIndex >= 0 && targetIndex < wagonCanvases.Length && wagonCanvases[targetIndex] != null)
        {
            wagonCanvases[targetIndex].gameObject.SetActive(true);
            if (showDebugInfo)
            {
                Debug.Log($"Wagon {targetWagon} Canvas aktiviert");
            }
        }
        else
        {
            Debug.LogError($"WagonManager: Kann Wagon {targetWagon} nicht aktivieren - Canvas nicht gefunden!");
        }
        
        // 3. ALTEN WAGON DEAKTIVIEREN (nach Aktivierung des neuen)
        int currentIndex = currentWagonNumber - 1; // Array ist 0-basiert
        if (currentIndex >= 0 && currentIndex < wagonCanvases.Length && wagonCanvases[currentIndex] != null)
        {
            wagonCanvases[currentIndex].gameObject.SetActive(false);
            if (showDebugInfo)
            {
                Debug.Log($"Wagon {currentWagonNumber} Canvas deaktiviert");
            }
        }
        
        // LOOP-ZÄHLUNG: Von Wagon 5 zurück zu Wagon 1 = ein Loop abgeschlossen
        if (isLoopTransition)
        {
            OnLoopCompleted();
            
            // MUSIK MUTEN vor Sound-Wiedergabe
            MuteBackgroundMusic();
            
            // SPEZIAL: Loop-Übergang Sound abspielen und warten
            if (loopTransitionAudioSource != null && loopTransitionAudioSource.clip != null)
            {
                if (showDebugInfo)
                {
                    Debug.Log($"🔊 LOOP-ÜBERGANG SOUND: Spiele '{loopTransitionAudioSource.clip.name}' ab und warte...");
                }
                
                // Sound abspielen
                loopTransitionAudioSource.Play();
                
                // Warte bis Sound fertig ist
                yield return new WaitWhile(() => loopTransitionAudioSource.isPlaying);
                
                if (showDebugInfo)
                {
                    Debug.Log($"🔊 LOOP-ÜBERGANG SOUND: Abgeschlossen - setze Übergang fort");
                }
                
                // NACH SOUND-ENDE: Loop-Dialog triggern
                TriggerLoopDialogAfterSound();
            }
            else if (showDebugInfo)
            {
                Debug.Log("🔊 LOOP-ÜBERGANG SOUND: Kein AudioSource oder AudioClip zugewiesen - verwende normalen Timer");
            }
            
            // MUSIK ENTMUTEN nach Sound-Wiedergabe
            UnmuteBackgroundMusic();
        }
        
        // Wagon-Nummer aktualisieren
        currentWagonNumber = targetWagon;
        
        // 4. WECHSEL COMPLETE EVENT (Items werden getauscht)
        OnTransitionComplete(targetWagon);
        if (showDebugInfo)
        {
            Debug.Log("Complete Event ausgelöst - Items werden getauscht");
        }
        
        // 5. TIMER STARTEN (nur bei normalen Übergängen, bei Loop-Übergang direkt weiter)
        if (!isLoopTransition)
        {
            if (showDebugInfo)
            {
                Debug.Log($"Normaler Übergang: Warte {transitionDuration} Sekunden NACH Item-Tausch...");
            }
            yield return new WaitForSeconds(transitionDuration);
        }
        else if (showDebugInfo)
        {
            Debug.Log("Loop-Übergang: Kein zusätzlicher Timer - Sound war bereits die Wartezeit");
        }
        
        // 6. ÜBERGANGS-CANVAS DEAKTIVIEREN (nach Timer-Ablauf oder direkt nach Sound)
        if (transitionCanvas != null)
        {
            transitionCanvas.gameObject.SetActive(false);
            if (showDebugInfo)
            {
                Debug.Log("Übergangs-Canvas deaktiviert - Übergang abgeschlossen");
            }
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"=== WAGON ÜBERGANG ABGESCHLOSSEN: Jetzt in Wagon {currentWagonNumber} ===");
        }
        
        isTransitioning = false;
    }
    
    // Debug-Informationen
    public void DebugWagonState()
    {
        Debug.Log("=== WAGON MANAGER DEBUG ===");
        Debug.Log($"Aktueller Wagon: {currentWagonNumber}/{maxWagons}");
        Debug.Log($"Übergang aktiv: {isTransitioning}");
        Debug.Log($"Loop-System: AKTIV (Wagon {maxWagons} → Wagon 1 = Loop++)");
        Debug.Log($"Nächster Wagon: {(currentWagonNumber >= maxWagons ? 1 : currentWagonNumber + 1)}");
        Debug.Log($"Vorheriger Wagon: {(currentWagonNumber <= 1 ? maxWagons : currentWagonNumber - 1)}");
        
        // Loop-Übergang Sound Info
        Debug.Log($"Loop-Übergang AudioSource: {(loopTransitionAudioSource != null ? loopTransitionAudioSource.name : "nicht zugewiesen")}");
        if (loopTransitionAudioSource != null)
        {
            Debug.Log($"  - AudioClip: {(loopTransitionAudioSource.clip != null ? loopTransitionAudioSource.clip.name : "nicht zugewiesen")}");
            Debug.Log($"  - Spielt gerade: {loopTransitionAudioSource.isPlaying}");
            if (loopTransitionAudioSource.clip != null)
            {
                Debug.Log($"  - Clip Länge: {loopTransitionAudioSource.clip.length:F2} Sekunden");
            }
        }
        
        // Musik Mute-System Info
        Debug.Log($"Musik Mute-System: {(muteBackgroundMusicDuringLoop ? "AKTIV" : "DEAKTIVIERT")}");
        Debug.Log($"Background Musik AudioSources: {(backgroundMusicSources != null ? backgroundMusicSources.Length.ToString() : "NULL")} zugewiesen");
        if (backgroundMusicSources != null && backgroundMusicSources.Length > 0)
        {
            for (int i = 0; i < backgroundMusicSources.Length; i++)
            {
                if (backgroundMusicSources[i] != null)
                {
                    float originalVol = (originalMusicVolumes != null && i < originalMusicVolumes.Length) ? originalMusicVolumes[i] : 1f;
                    Debug.Log($"  [{i}] {backgroundMusicSources[i].name}:");
                    Debug.Log($"    - Aktuelle Lautstärke: {backgroundMusicSources[i].volume:F2}");
                    Debug.Log($"    - Originale Lautstärke: {originalVol:F2}");
                    Debug.Log($"    - Spielt gerade: {backgroundMusicSources[i].isPlaying}");
                }
                else
                {
                    Debug.Log($"  [{i}] NULL");
                }
            }
            Debug.Log($"  - Derzeit gemutet: {musicWasMuted}");
        }
        
        // GameManager Loop-Info
        if (GameManager.Instance != null)
        {
            Debug.Log($"Aktueller Game-Loop: {GameManager.Instance.GetCurrentLoop()}");
        }
        else
        {
            Debug.Log("GameManager: NICHT VERFÜGBAR");
        }
        
        Debug.Log($"Übergangs-Canvas: {(transitionCanvas != null ? transitionCanvas.name : "NULL")}");
        
        if (wagonCanvases != null)
        {
            Debug.Log($"Wagon-Canvases: {wagonCanvases.Length} zugewiesen");
            for (int i = 0; i < wagonCanvases.Length; i++)
            {
                if (wagonCanvases[i] != null)
                {
                    bool isActive = wagonCanvases[i].gameObject.activeInHierarchy;
                    Debug.Log($"  Wagon {i + 1}: {wagonCanvases[i].name} - Aktiv: {isActive}");
                }
                else
                {
                    Debug.Log($"  Wagon {i + 1}: NULL");
                }
            }
        }
    }
    
    // Wird von WagonDoor aufgerufen wenn eine Tür angeklickt wird
    public void OnWagonDoorClicked()
    {
        if (showDebugInfo)
        {
            Debug.Log("WagonManager: Wagon-Tür wurde angeklickt!");
        }
        
        GoToNextWagon();
    }
    
    // Alternative Methode - falls andere Scripts RequestWagonTransition() erwarten
    public void RequestWagonTransition()
    {
        GoToNextWagon();
    }
    
    // Getter für andere Scripts
    public int GetCurrentWagon() => currentWagonNumber;
    public int GetMaxWagons() => maxWagons;
    public bool IsTransitioning() => isTransitioning;
    public string GetWagonInfo() => $"Wagon {currentWagonNumber}/{maxWagons}";
    public int GetCurrentGameLoop() => GameManager.Instance != null ? GameManager.Instance.GetCurrentLoop() : 1;
    
    // Prüft ob ein Wagon-Wechsel möglich ist (mit Loop-System immer möglich)
    public bool CanGoToNextWagon() => !isTransitioning;
    public bool CanGoToPreviousWagon() => !isTransitioning;
    
    // Loop-System: Wird aufgerufen wenn von Wagon 5 zu Wagon 1 gewechselt wird
    private void OnLoopCompleted()
    {
        if (showDebugInfo)
        {
            Debug.Log("=== LOOP ABGESCHLOSSEN: Wagon 5 → Wagon 1 ===");
        }
        
        // GameManager über Loop-Abschluss informieren (Validierung passiert dort)
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnLoopCompleted();
        }
        else
        {
            Debug.LogWarning("WagonManager: GameManager nicht gefunden - kann Loop nicht validieren!");
        }
    }
    
    // Wird aufgerufen wenn der Wagon-Wechsel complete ist
    private void OnTransitionComplete(int newWagon)
    {
        if (showDebugInfo)
        {
            Debug.Log($"=== TRANSITION COMPLETE: Wechsel zu Wagon {newWagon} abgeschlossen ===");
        }
        
        // Event für andere Scripts auslösen
        TriggerTransitionCompleteEvent(newWagon);
        
        // Hier können weitere Scripts benachrichtigt werden
        // TODO: Event-System für andere Scripts die auf Transition Complete reagieren sollen
        
        // Beispiel: ItemDialogTrigger könnte hier benachrichtigt werden
        // dass der Wechsel complete ist und Item-Evolution geprüft werden kann
    }
    
    // Öffentliche Methode für andere Scripts um auf Transition Complete zu reagieren
    public System.Action<int> OnTransitionCompleted;
    
    // Event auslösen für externe Scripts
    private void TriggerTransitionCompleteEvent(int newWagon)
    {
        if (OnTransitionCompleted != null)
        {
            OnTransitionCompleted.Invoke(newWagon);
        }
    }
    
    // MUSIK MUTE-SYSTEM für Loop-Übergang
    private void MuteBackgroundMusic()
    {
        if (!muteBackgroundMusicDuringLoop)
        {
            if (showDebugInfo)
            {
                Debug.Log("🔇 MUSIK MUTE: Deaktiviert im Inspector - Musik bleibt aktiv");
            }
            return;
        }
        
        if (backgroundMusicSources == null || backgroundMusicSources.Length == 0)
        {
            if (showDebugInfo)
            {
                Debug.Log("🔇 MUSIK MUTE: Keine Background-AudioSources zugewiesen");
            }
            return;
        }
        
        // Array für originale Lautstärken initialisieren
        originalMusicVolumes = new float[backgroundMusicSources.Length];
        
        // Alle AudioSources muten
        for (int i = 0; i < backgroundMusicSources.Length; i++)
        {
            if (backgroundMusicSources[i] != null)
            {
                // Originale Lautstärke speichern
                originalMusicVolumes[i] = backgroundMusicSources[i].volume;
                // Auf 0 setzen (muten)
                backgroundMusicSources[i].volume = 0f;
                
                if (showDebugInfo)
                {
                    Debug.Log($"🔇 MUSIK GEMUTET [{i}]: '{backgroundMusicSources[i].name}' - Originale Lautstärke {originalMusicVolumes[i]:F2} gespeichert, auf 0 gesetzt");
                }
            }
            else
            {
                originalMusicVolumes[i] = 1f; // Fallback-Wert
                if (showDebugInfo)
                {
                    Debug.Log($"🔇 MUSIK MUTE [{i}]: AudioSource ist NULL");
                }
            }
        }
        
        musicWasMuted = true;
        
        if (showDebugInfo)
        {
            Debug.Log($"🔇 MUSIK MUTE ABGESCHLOSSEN: {backgroundMusicSources.Length} AudioSources gemutet");
        }
    }
    
    private void UnmuteBackgroundMusic()
    {
        if (!musicWasMuted)
        {
            if (showDebugInfo)
            {
                Debug.Log("🔊 MUSIK UNMUTE: Musik war nicht gemutet - keine Aktion erforderlich");
            }
            return;
        }
        
        if (backgroundMusicSources == null || backgroundMusicSources.Length == 0)
        {
            if (showDebugInfo)
            {
                Debug.Log("🔊 MUSIK UNMUTE: Keine Background-AudioSources zugewiesen");
            }
            musicWasMuted = false;
            return;
        }
        
        if (originalMusicVolumes == null || originalMusicVolumes.Length != backgroundMusicSources.Length)
        {
            if (showDebugInfo)
            {
                Debug.Log("🔊 MUSIK UNMUTE: Originale Lautstärken nicht verfügbar - verwende Fallback-Werte");
            }
            musicWasMuted = false;
            return;
        }
        
        // Alle AudioSources entmuten
        for (int i = 0; i < backgroundMusicSources.Length; i++)
        {
            if (backgroundMusicSources[i] != null)
            {
                // Originale Lautstärke wiederherstellen
                backgroundMusicSources[i].volume = originalMusicVolumes[i];
                
                if (showDebugInfo)
                {
                    Debug.Log($"🔊 MUSIK ENTMUTET [{i}]: '{backgroundMusicSources[i].name}' - Lautstärke auf {originalMusicVolumes[i]:F2} wiederhergestellt");
                }
            }
            else if (showDebugInfo)
            {
                Debug.Log($"🔊 MUSIK UNMUTE [{i}]: AudioSource ist NULL");
            }
        }
        
        musicWasMuted = false;
        
        if (showDebugInfo)
        {
            Debug.Log($"🔊 MUSIK UNMUTE ABGESCHLOSSEN: {backgroundMusicSources.Length} AudioSources entmutet");
        }
    }
    
    // Debug-Methode für Musik-Mute System
    public void DebugMuteSystem()
    {
        Debug.Log("=== MUSIK MUTE-SYSTEM DEBUG ===");
        Debug.Log($"Mute-System aktiviert: {muteBackgroundMusicDuringLoop}");
        Debug.Log($"Background AudioSources: {(backgroundMusicSources != null ? backgroundMusicSources.Length.ToString() : "NULL")} zugewiesen");
        
        if (backgroundMusicSources != null && backgroundMusicSources.Length > 0)
        {
            for (int i = 0; i < backgroundMusicSources.Length; i++)
            {
                if (backgroundMusicSources[i] != null)
                {
                    float originalVol = (originalMusicVolumes != null && i < originalMusicVolumes.Length) ? originalMusicVolumes[i] : 1f;
                    Debug.Log($"AudioSource [{i}]: {backgroundMusicSources[i].name}");
                    Debug.Log($"  - Aktuelle Lautstärke: {backgroundMusicSources[i].volume:F2}");
                    Debug.Log($"  - Originale Lautstärke: {originalVol:F2}");
                    Debug.Log($"  - Spielt gerade: {backgroundMusicSources[i].isPlaying}");
                    Debug.Log($"  - AudioClip: {(backgroundMusicSources[i].clip != null ? backgroundMusicSources[i].clip.name : "nicht zugewiesen")}");
                }
                else
                {
                    Debug.Log($"AudioSource [{i}]: NULL");
                }
            }
        }
        else
        {
            Debug.Log("Keine Background AudioSources zugewiesen!");
        }
        
        Debug.Log($"Musik derzeit gemutet: {musicWasMuted}");
        Debug.Log($"Originale Lautstärken Array: {(originalMusicVolumes != null ? originalMusicVolumes.Length.ToString() : "NULL")} Einträge");
    }
    
    // LOOP-DIALOG SYSTEM: Triggere Loop-Dialog nach Sound-Ende
    private void TriggerLoopDialogAfterSound()
    {
        if (showDebugInfo)
        {
            Debug.Log("🎭 LOOP-DIALOG: Suche ItemDialogTrigger für Loop-Dialog nach Sound-Ende...");
        }
        
        // Suche ItemDialogTrigger in der Szene
        var itemDialogTrigger = FindFirstObjectByType<ItemDialogTrigger>();
        if (itemDialogTrigger != null)
        {
            int currentLoop = GameManager.Instance != null ? GameManager.Instance.currentLoopCount : 1;
            
            if (showDebugInfo)
            {
                Debug.Log($"🎭 LOOP-DIALOG: ItemDialogTrigger gefunden - triggere Loop-Dialog für Loop {currentLoop}");
            }
            
            // Triggere Loop-Dialog über die neue Methode
            itemDialogTrigger.TriggerLoopDialogAfterSound(currentLoop);
        }
        else if (showDebugInfo)
        {
            Debug.LogWarning("🎭 LOOP-DIALOG: Kein ItemDialogTrigger gefunden - Loop-Dialog kann nicht getriggert werden");
        }
    }
}
