using UnityEngine;
using UnityEngine.UI;

public class SpiegelScript : MonoBehaviour
{
    // Ziehe den Button im Inspector hier rein
    public Button myButton;
    // Ziehe das zu aktivierende Objekt im Inspector hier rein
    public GameObject targetObject;
    [Tooltip("Optional: Button dessen onClick Event nach dem Aktivieren ausgelöst werden soll")]
    public Button targetButton;

    private bool waitForMouseClick = false;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (myButton != null)
        {
            myButton.onClick.AddListener(OnSpiegelButtonClicked);
        }
        if (targetObject != null)
        {
            targetObject.SetActive(false);
        }
    }

    public void OnSpiegelButtonClicked()
    {
        waitForMouseClick = true;
    }

    // Update is called once per frame
    void Update()
    {
        if (waitForMouseClick && Input.GetMouseButtonDown(0))
        {
            if (targetObject != null)
            {
                targetObject.SetActive(true);
                // Button-Event auslösen (Inspector-Referenz bevorzugt, sonst direkt vom Zielobjekt)
                var buttonToInvoke = targetButton != null ? targetButton : targetObject.GetComponent<Button>();
                if (buttonToInvoke != null)
                {
                    buttonToInvoke.onClick?.Invoke();
                }
            }
            waitForMouseClick = false;
        }
    }
}
