using UnityEngine;
using UnityEngine.UI;

public class DoorChangeNewSzene : MonoBehaviour
{
    [Header("Objekt für Szenenwechsel")]
    public GameObject sceneChangeObject;

    private Image sceneImage;
    private Button sceneButton;
    private bool waitForSecondClick = false;
    private float secondClickTimer = 0f;
    private float secondClickTimeout = 3f;

    void Start()
    {
        if (sceneChangeObject == null)
            sceneChangeObject = this.gameObject;

        sceneImage = sceneChangeObject.GetComponent<Image>();
        if (sceneImage != null)
        {
            sceneImage.raycastTarget = true;
        }

        // Button-Komponente automatisch hinzufügen, falls nicht vorhanden
        sceneButton = sceneChangeObject.GetComponent<Button>();
        if (sceneButton == null)
        {
            sceneButton = sceneChangeObject.AddComponent<Button>();
        }

        // Entferne alle Listener und füge eigenen Listener hinzu
        if (sceneButton != null)
        {
            sceneButton.onClick.AddListener(StartSecondClickWait);
        }
    }

    void Update()
    {
        if (waitForSecondClick)
        {
            secondClickTimer += Time.deltaTime;
            if (Input.GetMouseButtonDown(0))
            {
                // Szenenwechsel zu 'Haupmenue'
                UnityEngine.SceneManagement.SceneManager.LoadScene("Haupmenue");
                waitForSecondClick = false;
            }
            if (secondClickTimer >= secondClickTimeout)
            {
                waitForSecondClick = false;
            }
        }
    }

    // Diese Methode im Button-OnClick zuweisen!
    public void StartSecondClickWait()
    {
        waitForSecondClick = true;
        secondClickTimer = 0f;
    }
}
