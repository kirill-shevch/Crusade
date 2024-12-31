using System.Collections.Generic;
using UnityEngine;

public class NodeController : MonoBehaviour
{
    public int nodeId;
    public List<NodeController> connectedNodes = new List<NodeController>();

    public void OnMouseDown()
    {
        // Handle node click
        Debug.Log("Node " + nodeId + " clicked");
    }
}
