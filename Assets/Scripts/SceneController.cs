using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneController : MonoBehaviour
{
    public static SceneController Instance { get; private set; }
    public Button continueButton;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "MainMenuScene")
        {
            SetupMainMenuButtons();
        }
    }

    void SetupMainMenuButtons()
    {
        // Find and setup continue button
        continueButton = GameObject.FindGameObjectWithTag("ContinueButton")?.GetComponent<Button>();
        if (continueButton != null)
        {
            bool hasSavedGame = PlayerPrefs.HasKey("SelectedCharacter") && !string.IsNullOrEmpty(PlayerPrefs.GetString("SelectedCharacter"));
            continueButton.interactable = hasSavedGame;
            continueButton.onClick.RemoveAllListeners();
            if (hasSavedGame)
            {
                continueButton.onClick.AddListener(LoadMap);
            }
        }

        // Find and setup new game button
        Button newGameButton = GameObject.FindGameObjectWithTag("NewGameButton")?.GetComponent<Button>();
        if (newGameButton != null)
        {
            newGameButton.onClick.RemoveAllListeners();
            newGameButton.onClick.AddListener(LoadCrusadeSetup);
        }

        // Find and setup options button
        Button optionsButton = GameObject.FindGameObjectWithTag("OptionsButton")?.GetComponent<Button>();
        if (optionsButton != null)
        {
            optionsButton.onClick.RemoveAllListeners();
            optionsButton.onClick.AddListener(LoadOptions);
        }
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
    public void SquadDetailsScene()
    {
        SceneManager.LoadScene("SquadDetailsScene");
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
        PlayerPrefs.DeleteAll(); // Clear all saved data when player loses
        SceneManager.LoadScene("LoseScene");
    }

    // Method to load the Win scene
    public void LoadWin()
    {
        SceneManager.LoadScene("WinScene");
    }
}
