using System.Collections.Generic;

// Datencontainer für eine Dialogzeile aus dem CSV-System
public class DialogLine
{
    public string memoryId;                  // Eindeutige ID dieser Dialogzeile
    public int minLoop;                      // Minimale Spielschleifen, bevor Dialog verfügbar wird
    public List<string> requiredMemory;     // Memory-Flags, die gesetzt sein müssen
    public string text;                      // Der Dialogtext
    public List<string> addMemory;          // Memory-Flags, die gesetzt werden
    public List<ChoiceData> choices = new List<ChoiceData>(); // Liste der Antwortoptionen

    // Datencontainer für eine Antwortmöglichkeit
    public class ChoiceData
    {
        public List<string> requiredMemory;  // Memory-Flags für diese Choice
        public string choiceText;            // Text der Antwortmöglichkeit
        public string answerText;            // Text nach der Auswahl
        public List<string> addMemory;       // Memory-Flags, die gesetzt werden
        public bool wasChosen = false;       // Ob diese Option bereits gewählt wurde
    }
}
