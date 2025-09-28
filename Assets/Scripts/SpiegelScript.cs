using UnityEngine;
using UnityEngine.UI;

public class SpiegelScript : MonoBehaviour
{
    // Ziehe den Button im Inspector hier rein
    public Button myButton;
    // Ziehe das zu aktivierende Objekt im Inspector hier rein
    public GameObject targetObject;

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

    void OnSpiegelButtonClicked()
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
                // Suche explizit nach ItemInteractable und rufe OnInteract auf
                var interactable = targetObject.GetComponent<ItemInteractable>();
                if (interactable != null)
                {
                    interactable.OnInteract();
                }
            }
            waitForMouseClick = false;
        }
    }
}
