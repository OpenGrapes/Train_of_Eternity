using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class DialogLoader
{
    public static List<DialogLine> LoadDialogCSV(TextAsset csvFile)
    {
        var dialogLines = new List<DialogLine>();
        
        if (csvFile == null || string.IsNullOrEmpty(csvFile.text))
        {
            Debug.LogError("CSV-Datei ist leer oder null!");
            return dialogLines;
        }
        
        Debug.Log($"Lade CSV-Datei: {csvFile.name} mit {csvFile.text.Length} Zeichen");
        
        using (var reader = new StringReader(csvFile.text))
        {
            // Erste Zeile: Header (ignorieren)
            string header = reader.ReadLine();
            Debug.Log($"CSV Header: {header}");

            string row;
            int lineNumber = 1;
            while ((row = reader.ReadLine()) != null)
            {
                lineNumber++;
                if (string.IsNullOrWhiteSpace(row)) continue; // Leere Zeilen überspringen
                
                Debug.Log($"Verarbeite CSV-Zeile {lineNumber}: {row.Substring(0, Mathf.Min(100, row.Length))}...");
                
                // Robuste CSV-Zeile splitten - berücksichtigt Anführungszeichen
                var cells = ParseCSVLine(row);
                
                // Debug: Anzahl Spalten prüfen
                Debug.Log($"CSV-Zeile hat {cells.Length} Spalten");
                
                // Mindestens 5 Spalten erforderlich (memoryId, minLoop, requiredMemory, text, addMemory)
                if (cells.Length < 5)
                {
                    Debug.LogWarning($"CSV-Zeile hat zu wenige Spalten ({cells.Length}): {row}");
                    continue;
                }

                var line = new DialogLine();
                
                try
                {
                    line.memoryId = GetSafeString(cells, 0);
                    line.minLoop = GetSafeInt(cells, 1, 1);
                    line.requiredMemory = ParseStringList(GetSafeString(cells, 2));
                    line.text = GetSafeString(cells, 3);
                    line.addMemory = ParseStringList(GetSafeString(cells, 4));

                    // Choices sicher parsen (ab Spalte 5)
                    ParseChoices(cells, line);
                    
                    dialogLines.Add(line);
                    Debug.Log($"Dialog geladen: {line.memoryId} - '{line.text.Substring(0, Mathf.Min(30, line.text.Length))}...'");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Fehler beim Parsen der CSV-Zeile: {row}\nFehler: {e.Message}");
                }
            }
        }
        
        Debug.Log($"DialogLoader: {dialogLines.Count} Dialoge aus {csvFile.name} geladen");
        return dialogLines;
    }
    
    // Robuste CSV-Zeile Parser (berücksichtigt Anführungszeichen)
    private static string[] ParseCSVLine(string line)
    {
        var result = new List<string>();
        var current = "";
        bool inQuotes = false;
        
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            
            if (c == '"')
            {
                // Prüfe auf escaped quotes ("")
                if (i + 1 < line.Length && line[i + 1] == '"')
                {
                    current += '"';
                    i++; // Skip das nächste Anführungszeichen
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.Trim());
                current = "";
            }
            else
            {
                current += c;
            }
        }
        
        // Letztes Feld hinzufügen
        result.Add(current.Trim());
        
        return result.ToArray();
    }
    
    // Sichere String-Extraktion aus Array
    private static string GetSafeString(string[] array, int index)
    {
        return index < array.Length ? array[index].Trim() : "";
    }
    
    // Sichere Int-Extraktion aus Array
    private static int GetSafeInt(string[] array, int index, int defaultValue = 0)
    {
        if (index >= array.Length) return defaultValue;
        
        string value = array[index].Trim();
        if (string.IsNullOrEmpty(value)) return defaultValue;
        
        if (int.TryParse(value, out int result))
            return result;
        
        Debug.LogWarning($"Kann '{value}' nicht zu int konvertieren, verwende {defaultValue}");
        return defaultValue;
    }
    
    // String-Liste parsen (kommagetrennt)
    private static List<string> ParseStringList(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return new List<string>();
        
        var list = new List<string>();
        var parts = input.Split(',');
        foreach (var part in parts)
        {
            string trimmed = part.Trim();
            if (!string.IsNullOrEmpty(trimmed))
                list.Add(trimmed);
        }
        return list;
    }
    
    // Choices sicher parsen
    private static void ParseChoices(string[] cells, DialogLine line)
    {
        // Choice-Pattern: choice1_requiredMemory(5), choice1(6), choice1_answer(7), choice1_addMemory(8)
        //                choice2_requiredMemory(9), choice2(10), choice2_answer(11), choice2_addMemory(12)
        //                choice3_requiredMemory(13), choice3(14), choice3_answer(15), choice3_addMemory(16)
        
        Debug.Log($"ParseChoices für Dialog '{line.memoryId}': CSV hat {cells.Length} Spalten");
        
        for (int choiceNum = 1; choiceNum <= 3; choiceNum++)
        {
            int baseIndex = 1 + (choiceNum * 4); // 5, 9, 13
            
            // Prüfen ob genug Spalten vorhanden sind
            if (baseIndex + 3 >= cells.Length) 
            {
                Debug.Log($"Choice {choiceNum}: Nicht genug Spalten (brauche {baseIndex + 3}, habe {cells.Length})");
                break;
            }
            
            string choiceText = GetSafeString(cells, baseIndex + 1); // choice1, choice2, choice3
            
            Debug.Log($"Choice {choiceNum} - Text: '{choiceText}' (Spalte {baseIndex + 1})");
            
            // Nur wenn choiceText vorhanden ist, Choice erstellen
            if (!string.IsNullOrWhiteSpace(choiceText))
            {
                var choice = new DialogLine.ChoiceData();
                choice.requiredMemory = ParseStringList(GetSafeString(cells, baseIndex));     // choice1_requiredMemory
                choice.choiceText = choiceText;                                               // choice1
                choice.answerText = GetSafeString(cells, baseIndex + 2);                     // choice1_answer
                choice.addMemory = ParseStringList(GetSafeString(cells, baseIndex + 3));     // choice1_addMemory
                
                Debug.Log($"Choice {choiceNum} erstellt:");
                Debug.Log($"  RequiredMemory: '{GetSafeString(cells, baseIndex)}' (Spalte {baseIndex})");
                Debug.Log($"  ChoiceText: '{choice.choiceText}' (Spalte {baseIndex + 1})");
                Debug.Log($"  AnswerText: '{choice.answerText}' (Spalte {baseIndex + 2})");
                Debug.Log($"  AddMemory: '{GetSafeString(cells, baseIndex + 3)}' (Spalte {baseIndex + 3})");
                
                line.choices.Add(choice);
                Debug.Log($"Choice {choiceNum} zu Dialog '{line.memoryId}' hinzugefügt");
            }
            else
            {
                Debug.Log($"Choice {choiceNum}: Kein Text vorhanden, überspringe");
            }
        }
        
        Debug.Log($"ParseChoices beendet: {line.choices.Count} Choices für Dialog '{line.memoryId}' erstellt");
    }
}
