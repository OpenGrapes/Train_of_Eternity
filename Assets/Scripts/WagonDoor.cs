/* WAGON DOOR SCRIPT - Später aktivieren wenn benötigt

using UnityEngine;

public class WagonDoor : MonoBehaviour
{
    [Header("Wagon Settings")]
    public string nextWagonSceneName = "Wagon2"; // Name der nächsten Szene
    
    [Header("References")]
    public DialogManager dialogManager; // Referenz zum DialogManager
    
    private void Start()
    {
        // Automatisch DialogManager finden, falls nicht zugewiesen
        if (dialogManager == null)
        {
            dialogManager = FindFirstObjectByType<DialogManager>();
        }
    }
    
    // Wird aufgerufen wenn auf die Wagon-Tür geklickt wird
    private void OnMouseDown()
    {
        GoToNextWagon();
    }
    
    // Alternative für UI-Button oder andere Trigger
    public void OnInteract()
    {
        GoToNextWagon();
    }
    
    private void GoToNextWagon()
    {
        // Dialog schließen falls aktiv
        if (dialogManager != null)
        {
            dialogManager.ForceEndDialog();
        }
        
        // Zum nächsten Wagon wechseln
        if (!string.IsNullOrEmpty(nextWagonSceneName))
        {
            Debug.Log($"Wechsle zu Wagon: {nextWagonSceneName}");
            // Hier können Sie Scene-Loading implementieren:
            // UnityEngine.SceneManagement.SceneManager.LoadScene(nextWagonSceneName);
        }
        else
        {
            Debug.Log("Nächster Wagon erreicht - Ende des Zuges");
        }
    }
    
    // Für Hover-Info oder UI-Text
    public string GetWagonInfo()
    {
        return $"Zum nächsten Wagon: {nextWagonSceneName}";
    }
}

*/
