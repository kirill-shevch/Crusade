using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro; // Add this for TextMeshPro

public class ChooseNextMapController : MonoBehaviour
{
    public GameObject mapPanel;
    
    private const float BUTTON_WIDTH = 600f;
    private const float BUTTON_HEIGHT = 150f;
    private const float HORIZONTAL_SPACING = 40f; // Space between buttons
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        LoadMapConfig();
    }

    void LoadMapConfig()
    {
        // Load the map configuration
        TextAsset json = Resources.Load<TextAsset>("Configs/Map");
        MapConfig allMaps = JsonUtility.FromJson<MapConfig>(json.text);
        
        // Find current map
        string currentMapName = GameManager.Instance.CurrentMap;
        Map currentMap = System.Array.Find(allMaps.maps, map => map.name == currentMapName);
        
        if (currentMap == null)
        {
            Debug.LogError($"Could not find map configuration for {currentMapName}");
            return;
        }

        // Calculate total width needed
        float totalWidth = (BUTTON_WIDTH + HORIZONTAL_SPACING) * currentMap.nextMaps.Length - HORIZONTAL_SPACING;
        float startX = -totalWidth / 2f + BUTTON_WIDTH / 2f;

        // Create buttons
        for (int i = 0; i < currentMap.nextMaps.Length; i++)
        {
            float xPosition = startX + i * (BUTTON_WIDTH + HORIZONTAL_SPACING);
            CreateMapButton(currentMap.nextMaps[i], new Vector2(xPosition, 0));
        }
    }

    void CreateMapButton(string mapName, Vector2 position)
    {
        // Create button GameObject
        GameObject buttonObj = new GameObject(mapName + "Button");
        buttonObj.transform.SetParent(mapPanel.transform, false);

        // Set up RectTransform with position
        RectTransform rectTransform = buttonObj.AddComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(BUTTON_WIDTH, BUTTON_HEIGHT);
        rectTransform.anchoredPosition = position;
        
        // Add Image component (button background)
        Image buttonImage = buttonObj.AddComponent<Image>();
        buttonImage.sprite = Resources.Load<Sprite>("Images/RPG Button 7/PNG/Blue/Normal");
        buttonImage.type = Image.Type.Sliced;
        
        // Add Button component
        Button button = buttonObj.AddComponent<Button>();
        
        // Create text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(buttonObj.transform, false);

        TextMeshProUGUI buttonText = textObj.AddComponent<TextMeshProUGUI>();
        buttonText.text = mapName;
        buttonText.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        buttonText.alignment = TextAlignmentOptions.Center;
        buttonText.color = Color.white;
        buttonText.fontSize = 72;

        // Set text RectTransform
        RectTransform textRectTransform = textObj.GetComponent<RectTransform>();
        textRectTransform.anchorMin = Vector2.zero;
        textRectTransform.anchorMax = Vector2.one;
        textRectTransform.sizeDelta = Vector2.zero;
        textRectTransform.offsetMin = Vector2.zero;
        textRectTransform.offsetMax = Vector2.zero;
        
        // Add click handler
        button.onClick.AddListener(() => OnMapSelected(mapName));

        // Set transition colors
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.9f, 0.9f, 0.9f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
        button.colors = colors;
    }

    void OnMapSelected(string mapName)
    {
        var character = GameManager.Instance.SelectedCharacter;
        
        // Store permanent buffs
        string permanentBuffs = PlayerPrefs.GetString("PermanentBuffs", "");
        
        GameManager.Instance.ResetGame();
        
        // Restore permanent buffs
        PlayerPrefs.SetString("PermanentBuffs", permanentBuffs);
        
        // Save selected map
        GameManager.Instance.SetCurrentMap(mapName);
        GameManager.Instance.SetSelectedCharacter(character);
        // Reset node position for new map
        GameManager.Instance.SetCurrentNode(1);

        Unit[] playerSquad = new Unit[] { new Unit { unit = character, placement = 1 } };
        GameManager.Instance.SetPlayerSquad(new UnitArrayWrapper { units = playerSquad });
        
        // Load map scene
        SceneManager.LoadScene("MapScene");
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
