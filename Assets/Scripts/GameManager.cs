using UnityEngine;
using System.Collections.Generic;
using System.IO;

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
    
    [Header("Game State")]
    public int currentLoopCount = 1; // Aktueller Spielloop
    
    [Header("Player Settings")]
    // === PLAYER DATA ===
    public string playerName = "Reisender"; // Name des Spielers (wird später in der Story enthüllt)
    
    [Header("Dialog System")]
    public TextAsset[] dialogCSVFiles; // CSV-Dateien aus Assets/DialogeCSV im Inspector zuweisen
    
    [Header("NPC System")]
    public NPCData[] npcs; // NPCs und ihre CSV-Zuordnungen im Inspector definieren
    
    [Header("Item System")]
    public ItemData[] items; // Items und ihre CSV-Zuordnungen im Inspector definieren
    
    // Memory-System
    private HashSet<string> memoryFlags = new HashSet<string>();
    
    // Dialog-System
    private Dictionary<string, List<DialogLine>> allDialogs = new Dictionary<string, List<DialogLine>>();
    
    // NPC-System
    private Dictionary<string, NPCData> npcRegistry = new Dictionary<string, NPCData>();
    
    // Item-System
    private Dictionary<string, ItemData> itemRegistry = new Dictionary<string, ItemData>();
    
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
    
    // CSV-Loading System
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
    }    // Filtert Dialoge basierend auf Memory-System und Loop-Count
    private List<DialogLine> FilterAvailableDialogs(List<DialogLine> dialogs)
    {
        var availableDialogs = new List<DialogLine>();
        
        foreach (var dialog in dialogs)
        {
            // Prüfe Loop-Bedingung
            if (currentLoopCount < dialog.minLoop) continue;
            
            // Prüfe Memory-Bedingungen
            if (dialog.requiredMemory != null && dialog.requiredMemory.Count > 0)
            {
                bool allRequirementsMet = true;
                foreach (var requirement in dialog.requiredMemory)
                {
                    if (!HasMemory(requirement))
                    {
                        allRequirementsMet = false;
                        break;
                    }
                }
                if (!allRequirementsMet) continue;
            }
            
            availableDialogs.Add(dialog);
        }
        
        return availableDialogs;
    }
    
    // Memory-System Methoden
    public void AddMemory(string memoryId)
    {
        if (!string.IsNullOrEmpty(memoryId))
        {
            memoryFlags.Add(memoryId);
            Debug.Log($"Memory gesetzt: {memoryId}");
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
    
    // Loop-System
    public void NextLoop()
    {
        currentLoopCount++;
        Debug.Log($"Neuer Loop: {currentLoopCount}");
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
    
    // Loop-Validierung (später zu erweitern)
    private bool ValidateLoop()
    {
        // TODO: Hier später prüfen ob Spieler neue Memories erhalten hat
        // z.B. Track welche Memories im aktuellen Loop hinzugefügt wurden
        // Für jetzt wird jeder Loop als gültig betrachtet
        
        Debug.Log("Loop-Validierung: GÜLTIG (alle Loops akzeptiert - später Memory-Check)");
        return true;
    }
    
    public void ResetLoop()
    {
        currentLoopCount = 1;
        Debug.Log("Loop zurückgesetzt");
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
        return Instance != null ? Instance.GetPlayerName() : "???";
    }
}