using UnityEngine;
using UnityEngine.UI;

public class WagonDoor : MonoBehaviour
{
    [Header("Debug")]
    public bool showDebugInfo = true;
    
    private GameObject wagonManagerObject;
    private Button button;
    
    private void Start()
    {
        FindWagonManager();
        
        // Setup für UI-Button (Image auf Canvas) - WICHTIG!
        SetupUIButton();
        
        if (showDebugInfo)
        {
            Debug.Log($"WagonDoor '{gameObject.name}' bereit für UI-Interaktion");
        }
    }
    
    private void FindWagonManager()
    {
        // WagonManager-GameObject finden
        wagonManagerObject = GameObject.Find("WagonManager");
        if (wagonManagerObject == null)
        {
            // Alternative: Suche nach GameObject mit WagonManager-Komponente
            MonoBehaviour[] allScripts = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            foreach (var script in allScripts)
            {
                if (script.GetType().Name == "WagonManager")
                {
                    wagonManagerObject = script.gameObject;
                    break;
                }
            }
        }
        
        if (wagonManagerObject == null)
        {
            Debug.LogError("WagonDoor: Kein WagonManager GameObject gefunden! Bitte WagonManager in der Szene platzieren.");
            return;
        }
    }
    
    private void SetupUIButton()
    {
        // Button-Komponente finden oder hinzufügen
        button = GetComponent<Button>();
        if (button == null)
        {
            button = gameObject.AddComponent<Button>();
            if (showDebugInfo)
            {
                Debug.Log("Button-Component zu Canvas-WagonDoor hinzugefügt");
            }
        }
        
        // OnClick-Event registrieren
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnWagonDoorClicked);
        
        // Sicherstellen dass Raycast Target aktiviert ist
        Image image = GetComponent<Image>();
        if (image != null)
        {
            image.raycastTarget = true;
            if (showDebugInfo)
            {
                Debug.Log("Raycast Target für WagonDoor aktiviert");
            }
        }
    }
    
    public void OnWagonDoorClicked()
    {
        if (showDebugInfo)
        {
            Debug.Log($"WagonDoor '{gameObject.name}' wurde angeklickt!");
        }
        
        // Falls WagonManager noch nicht gefunden wurde, erneut suchen
        if (wagonManagerObject == null)
        {
            FindWagonManager();
        }
        
        // An WagonManager weiterleiten über SendMessage (löst Compiler-Abhängigkeitsprobleme)
        if (wagonManagerObject != null)
        {
            wagonManagerObject.SendMessage("OnWagonDoorClicked", SendMessageOptions.DontRequireReceiver);
        }
        else
        {
            Debug.LogError("WagonDoor: WagonManager GameObject nicht verfügbar!");
        }
    }
}
