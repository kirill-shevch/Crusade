using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MapLoader : MonoBehaviour
{
    public GameObject nodePrefab;
    public GameObject edgePrefab;
    public GameObject roadPrefab;
    public Sprite barracksSprite;
    public Sprite treasureSprite;
    public GameObject playerPrefab;
    private GameObject playerObject;
    private NodeController currentPlayerNode;
    private List<int> visitedNodes = new List<int>();
    private List<string> visitedEdges = new List<string>();

    private MapConfig mapConfig;

    private Dictionary<int, NodeController> nodeControllers = new Dictionary<int, NodeController>();

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
        mapConfig = JsonUtility.FromJson<MapConfig>(json.text);

        // Load visited nodes and edges from PlayerPrefs
        string visitedNodesString = PlayerPrefs.GetString("VisitedNodes", "");
        if (!string.IsNullOrEmpty(visitedNodesString))
        {
            visitedNodes = new List<int>(visitedNodesString.Split(',').Select(int.Parse));
        }

        string visitedEdgesString = PlayerPrefs.GetString("VisitedEdges", "");
        visitedEdges = new List<string>(visitedEdgesString.Split(','));
    }

    void SpawnPlayerSquad()
    {
        string selectedCharacter = PlayerPrefs.GetString("SelectedCharacter", "Knight");
        int startNodeId = PlayerPrefs.GetInt("CharacterNode", 1); // Get the node ID from PlayerPrefs
        NodeController startNode = GetNodeControllerById(startNodeId); // Find the actual NodeController

        currentPlayerNode = startNode;

        UnitArrayWrapper playerSquadWrapper;
        if (PlayerPrefs.HasKey("PlayerSquad"))
        {
            // Use the existing player squad
            string playerSquadJson = PlayerPrefs.GetString("PlayerSquad");
            playerSquadWrapper = JsonUtility.FromJson<UnitArrayWrapper>(playerSquadJson);
        }
        else
        {
            // Create a new squad with the hero as the only member
            Unit[] playerSquad = new Unit[]
            {
            new Unit { unit = selectedCharacter, placement = 1 }
            };

            playerSquadWrapper = new UnitArrayWrapper { units = playerSquad };
            PlayerPrefs.SetString("PlayerSquad", JsonUtility.ToJson(playerSquadWrapper));
            PlayerPrefs.Save();
        }

        // Instantiate the player prefab on the map
        playerObject = Instantiate(playerPrefab, startNode.transform.position, Quaternion.identity, transform);
        playerObject.transform.localScale = new Vector3(0.015f, 0.015f, 0.015f);
        playerObject.GetComponent<Image>().sprite = GetUnitSprite(selectedCharacter);
        playerObject.name = selectedCharacter;
        playerObject.GetComponent<Button>().onClick.AddListener(() => OnSquadClicked(playerSquadWrapper.units, 1));

        visitedNodes.Add(startNodeId);
    }



    void CheckAvailableNodes()
    {
        foreach (NodeController connectedNode in currentPlayerNode.connectedNodes)
        {
            connectedNode.GetComponent<Button>().onClick.AddListener(() => OnNodeClicked(connectedNode));
        }
    }

    void OnNodeClicked(NodeController node)
    {
        if (currentPlayerNode.connectedNodes.Contains(node))
        {
            string edgeKey1 = currentPlayerNode.nodeId + "-" + node.nodeId; 
            string edgeKey2 = node.nodeId + "-" + currentPlayerNode.nodeId; // Check if the edge has already been visited
            if (visitedEdges.Contains(edgeKey1) || visitedEdges.Contains(edgeKey2))
            {
                // Move to the node immediately without going to the battle scene
                MoveToNode(node);
            }
            else
            {
                Edge edge = GetEdge(currentPlayerNode.nodeId, node.nodeId);
                if (edge.squad.Length > 0)
                {
                    SaveSquadsInfo(edge, node.nodeId);
                    SceneManager.LoadScene("BattleScene");
                }
                else
                {
                    MoveToNode(node);
                }
            }
        }
    }

    void OnSquadClicked(Unit[] squad, int isPlayerSquad = 0)
    {
        PlayerPrefs.SetString("DetailsSquad", JsonUtility.ToJson(new UnitArrayWrapper { units = squad }));
        PlayerPrefs.SetInt("isPlayerSquad", isPlayerSquad);
        PlayerPrefs.Save();
        SceneManager.LoadScene("SquadDetailsScene");
    }

    void SaveSquadsInfo(Edge edge, int targetNodeId)
    {
        // Convert playerObject.name to Unit[] format and store in PlayerPrefs
        //UnitArrayWrapper playerSquadWrapper = JsonUtility.FromJson<UnitArrayWrapper>(PlayerPrefs.GetString("PlayerSquad", "")) };
        //PlayerPrefs.SetString("PlayerSquad", JsonUtility.ToJson(playerSquadWrapper));

        // Store the enemy squad
        PlayerPrefs.SetString("EnemySquad", JsonUtility.ToJson(new UnitArrayWrapper { units = edge.squad }));
        PlayerPrefs.SetInt("TargetNode", targetNodeId);

        visitedNodes.Add(edge.to);
        visitedEdges.Add(currentPlayerNode.nodeId + "-" + edge.to);

        string nodeType = GetNodeTypeById(targetNodeId);
        PlayerPrefs.SetString("CurrentNodeType", nodeType);
        PlayerPrefs.SetString("VisitedNodes", string.Join(",", visitedNodes));
        PlayerPrefs.SetString("VisitedEdges", string.Join(",", visitedEdges));
        PlayerPrefs.Save();
    }



    void MoveToNode(NodeController node)
    {
        playerObject.transform.position = node.transform.position;
        currentPlayerNode = node;
        StartCoroutine(CenterOnPlayerNodeAnimated());
        CheckAvailableNodes();
    }

    void CreateMap()
    {
        Dictionary<int, List<Node>> levelNodes = new Dictionary<int, List<Node>>();
        Dictionary<int, int> nodeLevels = new Dictionary<int, int>();

        // BFS to determine levels of each node
        Queue<(Node, int)> queue = new Queue<(Node, int)>();
        Node rootNode = mapConfig.nodes[0];
        queue.Enqueue((rootNode, 0));
        nodeLevels[rootNode.id] = 0;

        while (queue.Count > 0)
        {
            var (currentNode, level) = queue.Dequeue();
            if (!levelNodes.ContainsKey(level))
            {
                levelNodes[level] = new List<Node>();
            }
            levelNodes[level].Add(currentNode);

            foreach (Edge edge in mapConfig.edges)
            {
                if (edge.from == currentNode.id && !nodeLevels.ContainsKey(edge.to))
                {
                    nodeLevels[edge.to] = level + 1;
                    queue.Enqueue((mapConfig.nodes[edge.to - 1], level + 1));
                }
                else if (edge.to == currentNode.id && !nodeLevels.ContainsKey(edge.from))
                {
                    nodeLevels[edge.from] = level + 1;
                    queue.Enqueue((mapConfig.nodes[edge.from - 1], level + 1));
                }
            }
        }

        Dictionary<int, GameObject> nodeObjects = new Dictionary<int, GameObject>();
        float xSpacing = 6.7f; // Adjust horizontal spacing
        float ySpacing = 4.0f; // Adjust vertical spacing
        float xOffset = 0f; // Initial horizontal offset

        foreach (var level in levelNodes)
        {
            float yOffset = -ySpacing * (level.Value.Count - 1) / 2; // Center nodes
            foreach (Node node in level.Value)
            {
                GameObject nodeObject = Instantiate(nodePrefab, new Vector3(xOffset, yOffset, 0), Quaternion.identity, transform);
                nodeObject.transform.localScale = new Vector3(0.015f, 0.015f, 0.015f);
                nodeObject.name = "Node" + node.id;
                nodeObject.GetComponent<Image>().sprite = GetUnitSprite(node.type);

                NodeController controller = nodeObject.AddComponent<NodeController>();
                controller.nodeId = node.id;
                nodeControllers[node.id] = controller;

                nodeObjects[node.id] = nodeObject;
                yOffset += ySpacing;
            }
            xOffset += xSpacing;
        }

        foreach (Edge edge in mapConfig.edges)
        {
            GameObject fromNode = nodeObjects[edge.from];
            GameObject toNode = nodeObjects[edge.to];

            fromNode.GetComponent<NodeController>().connectedNodes.Add(toNode.GetComponent<NodeController>());
            toNode.GetComponent<NodeController>().connectedNodes.Add(fromNode.GetComponent<NodeController>());

            GameObject edgeObject = new GameObject("Edge" + edge.from + "-" + edge.to);
            edgeObject.transform.SetParent(transform);
            edgeObject.transform.localScale = new Vector3(0.015f, 0.015f, 0.015f);

            // Remove LineRenderer code and use Road prefab
            GameObject roadInstance = Instantiate(roadPrefab, edgeObject.transform);

            // Calculate rotation and position to align the road between nodes
            Vector3 direction = toNode.transform.position - fromNode.transform.position;
            float distance = direction.magnitude;
            roadInstance.transform.position = fromNode.transform.position + direction / 2;
            roadInstance.transform.rotation = Quaternion.LookRotation(Vector3.forward, direction);
            roadInstance.transform.localScale = new Vector3(0.1f, roadInstance.transform.localScale.y, distance);

            // Adjust width of the road
            Vector3 localScale = roadInstance.transform.localScale;
            localScale.x = 0.1f; // Adjusting width
            roadInstance.transform.localScale = localScale;

            // Make roadInstance a child of edgeObject
            roadInstance.transform.SetParent(edgeObject.transform);


            if (edge.squad.Length > 0 && !visitedEdges.Contains(edge.from + "-" + edge.to))
            {
                GameObject unitImage = new GameObject("UnitImage");
                Image image = unitImage.AddComponent<Image>();
                image.sprite = GetUnitSprite(edge.squad[0].unit);
                unitImage.transform.SetParent(edgeObject.transform);
                unitImage.transform.position = (fromNode.transform.position + toNode.transform.position) / 2;
                unitImage.AddComponent<Button>().onClick.AddListener(() => OnSquadClicked(edge.squad));

                RectTransform rectTransform = unitImage.GetComponent<RectTransform>();
                rectTransform.sizeDelta = new Vector2(2, 2); // Set width and height to 1
            }
        }
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
        foreach (Node node in mapConfig.nodes)
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
        foreach (Edge edge in mapConfig.edges)
        {
            if ((edge.from == from && edge.to == to) || (edge.from == to && edge.to == from))
            {
                return edge;
            }
        }
        return null;
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
    }
}
