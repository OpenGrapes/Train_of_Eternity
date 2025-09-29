using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class DoorChangeNewSzene : MonoBehaviour
{
    [Header("Inspector-Zuweisungen")]
    public GameObject targetObject;
    public Button targetButton;
    
    [Header("Canvas Übergang & Audio")]
    public GameObject canvasUebergang;
    public float muteDuration = 3f;

    private bool waitForSecondClick = false;

    void Start()
    {
        // Setup wird über Inspector-Zuweisungen gehandhabt
        // Kein automatisches Setup mehr nötig
    }

    void Update()
    {
        if (waitForSecondClick)
        {
            if (Input.GetMouseButtonDown(0))
            {
                waitForSecondClick = false;
                StartCoroutine(HandleSceneTransition());
            }
        }
    }
    
    private System.Collections.IEnumerator HandleSceneTransition()
    {
        // 1. Canvas Übergang aktivieren
        if (canvasUebergang != null)
        {
            canvasUebergang.SetActive(true);
        }
        
        // 2. Alle Musik muten
        MuteAllMusic();
        
        // 3. Warten für die angegebene Dauer
        yield return new WaitForSeconds(muteDuration);
        
        // 4. Szenenwechsel zu 'Haupmenue'
        UnityEngine.SceneManagement.SceneManager.LoadScene("Haupmenue");
    }
    
    private void MuteAllMusic()
    {
        // Finde alle AudioSources in der Szene und mute sie
        AudioSource[] allAudioSources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
        
        foreach (AudioSource audioSource in allAudioSources)
        {
            if (audioSource != null && audioSource.isPlaying)
            {
                // Mute alle Audio Sources (besonders die mit Loop = true für Musik)
                audioSource.mute = true;
            }
        }
    }

    // Diese Methode im Button-OnClick zuweisen!
    public void StartSecondClickWait()
    {
        waitForSecondClick = true;
    }
}
