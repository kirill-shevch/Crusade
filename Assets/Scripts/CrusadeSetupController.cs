using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CrusadeSetupController : MonoBehaviour
{
    public Button mageButton;
    public Button knightButton;
    public Button archerButton;
    public Button startButton;

    private Button selectedButton;
    private string selectedOption;

    void Start()
    {
        // Initially disable the Start button
        startButton.interactable = false;

        // Add listeners to the buttons
        mageButton.onClick.AddListener(() => OnCharacterButtonClicked(mageButton, "Mage"));
        knightButton.onClick.AddListener(() => OnCharacterButtonClicked(knightButton, "Knight"));
        archerButton.onClick.AddListener(() => OnCharacterButtonClicked(archerButton, "Archer"));
        startButton.onClick.AddListener(OnStartButtonClicked);
    }

    void OnCharacterButtonClicked(Button button, string characterType)
    {
        // Deselect previously selected button
        if (selectedButton != null)
        {
            // Change color back to normal (or implement any deselection logic)
            selectedButton.GetComponent<Image>().color = Color.white;
        }

        // Select the new button
        selectedButton = button;
        selectedOption = characterType;

        // Change color to indicate selection (or implement any selection logic)
        selectedButton.GetComponent<Image>().color = Color.grey;

        // Enable the Start button
        startButton.interactable = true;
    }

    void OnStartButtonClicked()
    {
        if (!string.IsNullOrEmpty(selectedOption))
        {
            // Store the selected option in PlayerPrefs
            PlayerPrefs.SetString("SelectedCharacter", selectedOption);
            PlayerPrefs.SetInt("CharacterNode", 1);
            PlayerPrefs.Save();

            // Load the MapScene
            SceneManager.LoadScene("MapScene");
        }
        else
        {
            Debug.LogWarning("No character selected!");
        }
    }
}
