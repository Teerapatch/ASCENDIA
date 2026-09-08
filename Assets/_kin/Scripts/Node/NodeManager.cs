using System.Collections.Generic;
using UnityEngine;

public class NodeManager : MonoBehaviour
{
    public List<NodeData> nodes =
        new List<NodeData>();

    public int currentNodeID = 0;

    public bool CanMoveTo(int targetID)
    {
        NodeData currentNode =
            GetNode(currentNodeID);

        if (currentNode == null)
            return false;

        return currentNode.connectedNodes
            .Contains(targetID);
    }

    public void SelectNode(int targetID)
    {
        if (!CanMoveTo(targetID))
        {
            Debug.Log(
                "Cannot select this Node. " +
                "It is not connected."
            );

            return;
        }

        currentNodeID = targetID;

        GameManager.Instance.currentNodeID =
            targetID;

        Debug.Log(
            "Selected Node: " + targetID
        );
    }

    public NodeData GetNode(int id)
    {
        foreach (NodeData node in nodes)
        {
            if (node.id == id)
                return node;
        }

        return null;
    }
}