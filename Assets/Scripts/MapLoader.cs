using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MapLoader : MonoBehaviour
{
    public GameObject nodePrefab;
    public GameObject roadPrefab;
    public GameObject playerPrefab;
    
    [Header("Map Settings")]
    [SerializeField] private float nodeHorizontalSpacing = 4f; // Space between nodes
    [SerializeField] private float nodeVerticalSpacing = 2f; // Space between nodes
    [SerializeField] private float roadWidth = 0.1f;
    [SerializeField] private float nodeDiameter = 2f; // Constant for node size
    
    private GameObject playerObject;
    private NodeController currentPlayerNode;
    private bool isMoving = false;
    private MapConfig mapConfig;
    private Dictionary<int, NodeController> nodeControllers = new Dictionary<int, NodeController>();
    private Map currentMap;

    void Start()
    {
        LoadMapConfig();
        CreateMap();
        SpawnPlayerSquad();
        CenterOnPlayerNode();
        CheckAvailableNodes();
    }

    void LoadMapConfig()
    {
        TextAsset json = Resources.Load<TextAsset>("Configs/Map");
        MapConfig allMaps = JsonUtility.FromJson<MapConfig>(json.text);
        
        // Find the current map configuration
        string mapName = GameManager.Instance.CurrentMap;
        currentMap = System.Array.Find(allMaps.maps, map => map.name == mapName);
        
        if (currentMap == null)
        {
            Debug.LogError($"Could not find map configuration for {mapName}");
            return;
        }
    }

    void CreateMap()
    {
        // Create all nodes first
        foreach (Node node in currentMap.nodes)
        {
            CreateNode(node);
        }

        // Then create all edges
        foreach (Edge edge in currentMap.edges)
        {
            CreateEdge(edge);
        }
    }

    void CreateNode(Node node)
    {
        Vector3 position = new Vector3(node.x * nodeHorizontalSpacing, node.y * nodeVerticalSpacing, 0);
        
        GameObject nodeObject = Instantiate(nodePrefab, position, Quaternion.identity, transform);
        nodeObject.transform.localScale = new Vector3(0.015f, 0.015f, 0.015f);
        nodeObject.name = $"Node{node.id}";
        
        // Set node sprite based on type
        nodeObject.GetComponent<Image>().sprite = GetUnitSprite(node.type);

        // Setup node controller
        NodeController controller = nodeObject.AddComponent<NodeController>();
        controller.nodeId = node.id;
        nodeControllers[node.id] = controller;
    }

    void CreateEdge(Edge edge)
    {
        NodeController fromNode = nodeControllers[edge.from];
        NodeController toNode = nodeControllers[edge.to];

        // Add connected nodes
        fromNode.connectedNodes.Add(toNode);
        toNode.connectedNodes.Add(fromNode);

        // Create road
        GameObject edgeObject = new GameObject($"Edge{edge.from}-{edge.to}");
        edgeObject.transform.SetParent(transform);

        GameObject roadInstance = Instantiate(roadPrefab, edgeObject.transform);
        
        // Set proper sorting order for roads
        Canvas roadCanvas = roadInstance.GetComponent<Canvas>();
        if (roadCanvas == null)
        {
            roadCanvas = roadInstance.AddComponent<Canvas>();
        }

        // Calculate road position and rotation
        Vector3 fromPos = fromNode.transform.position;
        Vector3 toPos = toNode.transform.position;
        Vector3 direction = toPos - fromPos;
        float distance = direction.magnitude;
        float roadLength = distance - nodeDiameter; // Subtract node diameter from road length

        roadInstance.transform.position = fromPos + direction / 2;
        roadInstance.transform.rotation = Quaternion.LookRotation(Vector3.forward, direction);
        roadInstance.transform.localScale = new Vector3(roadWidth, roadLength, 1);

        // Add enemy squad indicator if needed
        if (edge.squad.Length > 0 && !IsEdgeVisited(edge.from, edge.to))
        {
            CreateEnemyIndicator(edge, fromPos, toPos);
        }
    }

    void CreateEnemyIndicator(Edge edge, Vector3 fromPos, Vector3 toPos)
    {
        GameObject unitImage = new GameObject("UnitImage");
        Image image = unitImage.AddComponent<Image>();
        image.sprite = GetUnitSprite(edge.squad[0].unit);
        
        unitImage.transform.SetParent(transform);
        unitImage.transform.position = (fromPos + toPos) / 2;
        
        Button button = unitImage.AddComponent<Button>();
        button.onClick.AddListener(() => OnSquadClicked(edge.squad));

        RectTransform rectTransform = unitImage.GetComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(1, 1);
    }

    bool IsEdgeVisited(int node1, int node2)
    {
        // Normalize edge representation (smaller ID always comes first)
        int smallerId = Mathf.Min(node1, node2);
        int largerId = Mathf.Max(node1, node2);
        string edgeKey = $"{smallerId}-{largerId}";
        
        return GameManager.Instance.VisitedEdges.Contains(edgeKey);
    }

    void OnNodeClicked(NodeController node)
    {
        if (currentPlayerNode.connectedNodes.Contains(node) && !isMoving)
        {
            int fromId = currentPlayerNode.nodeId;
            int toId = node.nodeId;
            
            if (IsEdgeVisited(fromId, toId))
            {
                // Move to the node immediately without battle
                MoveToNode(node);
            }
            else
            {
                Edge edge = GetEdge(fromId, toId);
                if (edge.squad.Length > 0)
                {
                    SaveSquadsInfo(edge, toId);
                    SceneManager.LoadScene("BattleScene");
                }
                else
                {
                    MoveToNode(node);
                }
            }
        }
    }

    void SaveSquadsInfo(Edge edge, int targetNodeId)
    {
        // Store the enemy squad
        PlayerPrefs.SetString("EnemySquad", JsonUtility.ToJson(new UnitArrayWrapper { units = edge.squad }));
        PlayerPrefs.SetInt("TargetNode", targetNodeId);

        // Add to visited nodes and edges
        int fromId = Mathf.Min(edge.from, edge.to);
        int toId = Mathf.Max(edge.from, edge.to);
        string edgeKey = $"{fromId}-{toId}";
        
        GameManager.Instance.AddVisitedEdge(edgeKey);

        // Save node type for rewards
        string nodeType = GetNodeTypeById(targetNodeId);
        PlayerPrefs.SetString("CurrentNodeType", nodeType);
        PlayerPrefs.Save();
    }

    void SpawnPlayerSquad()
    {
        int startNodeId = GameManager.Instance.CurrentNodeId;
        NodeController startNode = GetNodeControllerById(startNodeId);
        currentPlayerNode = startNode;

        // Instantiate player marker
        playerObject = Instantiate(playerPrefab, startNode.transform.position, Quaternion.identity, transform);
        playerObject.transform.localScale = new Vector3(0.015f, 0.015f, 0.015f);
        playerObject.GetComponent<Image>().sprite = GetUnitSprite(GameManager.Instance.SelectedCharacter);
        playerObject.name = GameManager.Instance.SelectedCharacter;
        
        Button squadButton = playerObject.GetComponent<Button>();
        squadButton.onClick.AddListener(() => OnSquadClicked(GameManager.Instance.PlayerSquad.units, 1));
    }

    void CheckAvailableNodes()
    {
        foreach (NodeController connectedNode in currentPlayerNode.connectedNodes)
        {
            connectedNode.GetComponent<Button>().onClick.AddListener(() => OnNodeClicked(connectedNode));
        }
    }

    void MoveToNode(NodeController node)
    {
        playerObject.transform.position = node.transform.position;
        currentPlayerNode = node;
        
        // Save position after each move
        GameManager.Instance.SetCurrentNode(node.nodeId);
        GameManager.Instance.SaveGameState();
        
        StartCoroutine(CenterOnPlayerNodeAnimated());
        CheckAvailableNodes();
    }

    void CenterOnPlayerNode()
    { 
        // Convert currentPlayerNode's position to screen space
        Vector3 screenPos = currentPlayerNode.transform.position; 
        // Calculate the offset
        Vector3 offset = new Vector3(0.5f, 0.5f, screenPos.z) - screenPos; 
        // Apply the offset to MapManager to center
        transform.position += offset; 
    }

    IEnumerator CenterOnPlayerNodeAnimated()
    {
        isMoving = true;
        // Convert currentPlayerNode's position to screen space
        Vector3 screenPos = currentPlayerNode.transform.position;

        // Calculate the offset
        Vector3 offset = new Vector3(0.5f, 0.5f, screenPos.z) - screenPos;

        // Define duration and speed
        float duration = 1.0f; // Duration in seconds
        float elapsedTime = 0f;
        Vector3 startingPos = transform.position;

        while (elapsedTime < duration)
        {
            // Smoothly move the MapManager towards the target position
            transform.position = Vector3.Lerp(startingPos, startingPos + offset, elapsedTime / duration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // Ensure the final position is set
        transform.position = startingPos + offset;
        isMoving = false;
    }

    Sprite GetUnitSprite(string unitName)
    {
        return Resources.Load<Sprite>($"Images/{unitName}");
    }

    public NodeController GetNodeControllerById(int id)
    {
        if (nodeControllers.TryGetValue(id, out NodeController controller)) 
        { 
            return controller; 
        }
        return null;
    }

    public string GetNodeTypeById(int id)
    {
        foreach (Node node in currentMap.nodes)
        {
            if (node.id == id)
            {
                return node.type;
            }
        }
        return "unknown";
    }

    public Edge GetEdge(int from, int to)
    {
        foreach (Edge edge in currentMap.edges)
        {
            if ((edge.from == from && edge.to == to) || (edge.from == to && edge.to == from))
            {
                return edge;
            }
        }
        return null;
    }

    void OnSquadClicked(Unit[] squad, int squadType = 0)
    {
        // Store current position before leaving to squad screen
        GameManager.Instance.SetCurrentNode(currentPlayerNode.nodeId);
        GameManager.Instance.SaveGameState();

        // Store squad details for the squad screen
        PlayerPrefs.SetString("DetailsSquad", JsonUtility.ToJson(new UnitArrayWrapper { units = squad }));
        PlayerPrefs.SetInt("isPlayerSquad", squadType);
        PlayerPrefs.Save();

        SceneController.Instance.SquadDetailsScene();
    }
}
