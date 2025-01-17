using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class CrusadeSetupController : MonoBehaviour
{
    [SerializeField] private Button mageButton;
    [SerializeField] private Button knightButton;
    [SerializeField] private Button archerButton;
    [SerializeField] private Button startButton;
    
    private Button selectedButton;
    private string selectedOption;
    
    void Awake()
    {
        // Ensure GameManager exists
        if (GameManager.Instance == null)
        {
            GameObject gameManagerObject = new GameObject("GameManager");
            gameManagerObject.AddComponent<GameManager>();
        }
    }
    
    void Start()
    {
        startButton.interactable = false;
        SetupButtonListeners();
    }
    
    void SetupButtonListeners()
    {
        mageButton.onClick.AddListener(() => OnCharacterButtonClicked(mageButton, "Mage"));
        knightButton.onClick.AddListener(() => OnCharacterButtonClicked(knightButton, "Knight"));
        archerButton.onClick.AddListener(() => OnCharacterButtonClicked(archerButton, "Archer"));
        startButton.onClick.AddListener(OnStartButtonClicked);
    }
    
    void OnCharacterButtonClicked(Button button, string characterType)
    {
        if (selectedButton != null)
        {
            selectedButton.GetComponent<Image>().color = Color.white;
        }
        
        selectedButton = button;
        selectedOption = characterType;
        selectedButton.GetComponent<Image>().color = Color.grey;
        startButton.interactable = true;
    }
    
    void OnStartButtonClicked()
    {
        if (!string.IsNullOrEmpty(selectedOption))
        {
            // Reset game state before starting new game
            GameManager.Instance.ResetGame();
            
            // Set up new game
            GameManager.Instance.SetSelectedCharacter(selectedOption);
            GameManager.Instance.SetCurrentNode(1);
            
            // Create initial squad with selected character
            Unit[] playerSquad = new Unit[] { new Unit { unit = selectedOption, placement = 1 } };
            GameManager.Instance.SetPlayerSquad(new UnitArrayWrapper { units = playerSquad });
            
            SceneController.Instance.LoadMap();
        }
    }
}
