using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneController : MonoBehaviour
{
    public Button continueButton; // Reference to the Continue button

    void Start()
    {
        // Check if there is an info about selected character
        if (PlayerPrefs.HasKey("SelectedCharacter"))
        {
            // Enable Continue button and add listener
            continueButton.interactable = true;
            continueButton.onClick.AddListener(LoadMap);
        }
        else
        {
            // Clear existing listeners and disable Continue button
            continueButton.onClick.RemoveAllListeners();
            continueButton.interactable = false;
        }
    }

    // Method to load a scene by name
    public void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    // Method to load the Main Menu scene
    public void LoadMainMenu()
    {
        SceneManager.LoadScene("MainMenuScene");
    }

    // Method to load the Options scene
    public void LoadOptions()
    {
        SceneManager.LoadScene("OptionsScene");
    }

    // Method to load the Crusade Setup scene
    public void LoadCrusadeSetup()
    {
        PlayerPrefs.DeleteAll();
        SceneManager.LoadScene("CrusadeSetupScene");
    }

    // Method to load the Map scene
    public void LoadMap()
    {
        SceneManager.LoadScene("MapScene");
    }

    // Method to load the Squad Settings scene
    public void LoadSquadSettings()
    {
        SceneManager.LoadScene("SquadSettingsScene");
    }

    // Method to load the Battle scene
    public void LoadBattle()
    {
        SceneManager.LoadScene("BattleScene");
    }

    // Method to load the Rewards scene
    public void LoadRewards()
    {
        SceneManager.LoadScene("RewardsScene");
    }

    // Method to load the Lose scene
    public void LoadLose()
    {
        SceneManager.LoadScene("LoseScene");
    }

    // Method to load the Win scene
    public void LoadWin()
    {
        SceneManager.LoadScene("WinScene");
    }
}
