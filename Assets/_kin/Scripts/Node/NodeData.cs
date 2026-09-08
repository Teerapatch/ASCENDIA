using System.Collections.Generic;

public enum NodeType
{
    Climb,
    Resource,
    Guardian,
    Event,
    Camp
}

[System.Serializable]
public class NodeData
{
    public int id;

    public NodeType type;

    public List<int> connectedNodes =
        new List<int>();
}