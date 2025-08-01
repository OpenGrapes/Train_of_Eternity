using UnityEngine;
using System.Collections.Generic;
using System.IO;

[System.Serializable]
public class NPCData
{
    public string npcId;           // Eindeutige NPC-ID
    public string npcName;         // Anzeigename
    public string csvFileName;     // Name der CSV-Datei
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    
    [Header("Game State")]
    public int currentLoopCount = 1; // Aktueller Spielloop
    
    [Header("Dialog System")]
    public TextAsset[] dialogCSVFiles; // CSV-Dateien aus Assets/DialogeCSV im Inspector zuweisen
    
    [Header("NPC System")]
    public NPCData[] npcs; // NPCs und ihre CSV-Zuordnungen im Inspector definieren
    
    // Memory-System
    private HashSet<string> memoryFlags = new HashSet<string>();
    
    // Dialog-System
    private Dictionary<string, List<DialogLine>> allDialogs = new Dictionary<string, List<DialogLine>>();
    
    // NPC-System
    private Dictionary<string, NPCData> npcRegistry = new Dictionary<string, NPCData>();
    
    private void Awake()
    {
        // Singleton Pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadAllDialogCSVs();
            RegisterAllNPCs();
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
                Debug.Log($"NPC registriert: {npc.npcName} ({npc.npcId}) -> {npc.csvFileName}");
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
    
    // Dialog-Zugriff für NPCs
    public List<DialogLine> GetDialogsForNPC(string npcId)
    {
        if (npcRegistry.ContainsKey(npcId))
        {
            string csvFileName = npcRegistry[npcId].csvFileName;
            return GetAllDialogsFromCSV(csvFileName);
        }
        
        Debug.LogWarning($"NPC '{npcId}' nicht in Registry gefunden!");
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
    
    // Filtert Dialoge basierend auf Memory-System und Loop-Count
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
}