using UnityEngine;
using UnityEngine.UI;

public class DoorChangeWagon5 : MonoBehaviour
{
    [Header("Tür-Objekt, das angeklickt wird")]
    public GameObject doorObject;

    [Header("WagonDoor-Script zuweisen")]
    public WagonDoor wagonDoorScript;

    private Image image;
    private Button doorButton;
    private bool waitForSecondClick = false;
    private float wagonDoorActiveTime = 0f;
    private float wagonDoorDuration = 3f;

    void Start()
    {
        image = doorObject.GetComponent<Image>();
        if (image != null)
        {
            image.raycastTarget = true;
        }

        // Button-Komponente automatisch hinzufügen, falls nicht vorhanden
        doorButton = doorObject.GetComponent<Button>();
        if (doorButton == null)
        {
            doorButton = doorObject.AddComponent<Button>();
        }

        // Entferne alle Listener und füge eigenen Listener hinzu
        if (doorButton != null)
        {
            doorButton.onClick.RemoveAllListeners();
            // Button ruft nur StartSecondClickWait auf
            doorButton.onClick.AddListener(StartSecondClickWait);
        }
    }

    void Update()
    {
        if (waitForSecondClick)
        {
            wagonDoorActiveTime += Time.deltaTime;
            if (Input.GetMouseButtonDown(0))
            {
                if (wagonDoorScript != null)
                {
                    wagonDoorScript.enabled = true;
                    wagonDoorScript.OnWagonDoorClicked();
                }
                waitForSecondClick = false;
            }
            if (wagonDoorActiveTime >= wagonDoorDuration)
            {
                if (wagonDoorScript != null)
                    wagonDoorScript.enabled = false;
                waitForSecondClick = false;
            }
        }
    }

    // Diese Methode im Button-OnClick zuweisen!
    public void StartSecondClickWait()
    {
        waitForSecondClick = true;
        wagonDoorActiveTime = 0f;
    }
}
