using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class MenuDoorAndLangue : MonoBehaviour
{
    public Image imageEnglish;
    public Image imageGerman;
    public Image imageDoor;
    public GameObject dialogPanelEnglish;
    public GameObject dialogPanelGerman;

    private string currentLanguage = "German";

    private void Start()
    {
        SetupHoverAndClick(imageEnglish);
        SetupHoverAndClick(imageGerman);
        SetupHoverAndClick(imageDoor);
    }

    private void SetupHoverAndClick(Image img)
    {
        if (img == null) return;
        var btn = img.GetComponent<Button>();
        if (btn == null) btn = img.gameObject.AddComponent<Button>();
        SetupInvertedHoverColors(btn);
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => OnImageClicked(img));
    }

    // Original aus DialogManager übernommen
    private void SetupInvertedHoverColors(Button button)
    {
        if (button == null) return;
        var colors = button.colors;
        colors.normalColor = new Color(240f/255f, 240f/255f, 240f/255f, 0.15f); // Basis: leicht sichtbar
        colors.highlightedColor = new Color(40f/255f, 40f/255f, 40f/255f, 0.55f); // Hover: deutlich dunkler
        colors.pressedColor = new Color(10f/255f, 10f/255f, 10f/255f, 0.7f); // Klick: sehr dunkel
        colors.selectedColor = new Color(80f/255f, 80f/255f, 80f/255f, 0.35f); // Selected: dunkler
        colors.disabledColor = new Color(240f/255f, 240f/255f, 240f/255f, 0f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.2f;
        button.colors = colors;
        button.transition = Selectable.Transition.ColorTint;
        var image = button.GetComponent<Image>();
        if (image != null)
        {
            button.targetGraphic = image;
            image.raycastTarget = true;
            image.color = new Color(240f/255f, 240f/255f, 240f/255f, 1f); // Hellgrau als Basis
        }
    }

    private void OnImageClicked(Image img)
    {
        if (img == imageEnglish && dialogPanelEnglish != null)
        {
            dialogPanelEnglish.SetActive(true);
            currentLanguage = "English";
            Debug.Log("Sprache auf Englisch umgestellt. Szene: Scenes/GameStartEnglish");
        }
        else if (img == imageGerman && dialogPanelGerman != null)
        {
            dialogPanelGerman.SetActive(true);
            currentLanguage = "German";
            Debug.Log("Sprache auf Deutsch umgestellt. Szene: Scenes/GameStartGerman");
        }
        else if (img == imageDoor)
        {
            string sceneToLoad = currentLanguage == "English" ? "GameStartEnglish" : "GameStartGerman";
            Debug.Log($"Tür geklickt. Lade Szene: {sceneToLoad}");
            SceneManager.LoadScene(sceneToLoad);
        }
        Debug.Log($"Image '{img.name}' wurde geklickt.");
    }

    private void Update()
    {
        // DialogPanel schließen, wenn aktiv und Mausklick außerhalb
        if (dialogPanelEnglish != null && dialogPanelEnglish.activeSelf && Input.GetMouseButtonDown(0))
        {
            dialogPanelEnglish.SetActive(false);
        }
        if (dialogPanelGerman != null && dialogPanelGerman.activeSelf && Input.GetMouseButtonDown(0))
        {
            dialogPanelGerman.SetActive(false);
        }
    }
}
