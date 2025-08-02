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
    
    [Header("References")]
    public DialogManager dialogManager;
    
    [Header("Debug")]
    public bool showDebugInfo = true;
    
    private bool isTransitioning = false;
    
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
    
    // Von WagonDoor aufgerufen - zum nächsten Wagon (mit Loop)
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
        
        // 1. ÜBERGANGS-CANVAS AKTIVIEREN
        if (transitionCanvas != null)
        {
            transitionCanvas.gameObject.SetActive(true);
            if (showDebugInfo)
            {
                Debug.Log("Übergangs-Canvas aktiviert");
            }
        }
        
        // 2. AKTUELLEN WAGON DEAKTIVIEREN
        int currentIndex = currentWagonNumber - 1; // Array ist 0-basiert
        if (currentIndex >= 0 && currentIndex < wagonCanvases.Length && wagonCanvases[currentIndex] != null)
        {
            wagonCanvases[currentIndex].gameObject.SetActive(false);
            if (showDebugInfo)
            {
                Debug.Log($"Wagon {currentWagonNumber} Canvas deaktiviert");
            }
        }
        
        // 3. WARTEN (Übergangszeit)
        if (showDebugInfo)
        {
            Debug.Log($"Warte {transitionDuration} Sekunden...");
        }
        yield return new WaitForSeconds(transitionDuration);
        
        // 4. NEUEN WAGON AKTIVIEREN
        int targetIndex = targetWagon - 1; // Array ist 0-basiert
        if (targetIndex >= 0 && targetIndex < wagonCanvases.Length && wagonCanvases[targetIndex] != null)
        {
            // LOOP-ZÄHLUNG: Von Wagon 5 zurück zu Wagon 1 = ein Loop abgeschlossen
            if (currentWagonNumber == maxWagons && targetWagon == 1)
            {
                OnLoopCompleted();
            }
            
            wagonCanvases[targetIndex].gameObject.SetActive(true);
            currentWagonNumber = targetWagon;
            if (showDebugInfo)
            {
                Debug.Log($"Wagon {targetWagon} Canvas aktiviert");
            }
        }
        else
        {
            Debug.LogError($"WagonManager: Kann Wagon {targetWagon} nicht aktivieren - Canvas nicht gefunden!");
        }
        
        // 5. ÜBERGANGS-CANVAS DEAKTIVIEREN
        if (transitionCanvas != null)
        {
            transitionCanvas.gameObject.SetActive(false);
            if (showDebugInfo)
            {
                Debug.Log("Übergangs-Canvas deaktiviert");
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
}
