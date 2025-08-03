using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;

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
    
    // Debug flag für Dialog Evolution
    [Header("Dialog Evolution")]
    public bool showDebugLogs = true;
    
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
        return Instance != null ? Instance.GetPlayerName() : "???";
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
}