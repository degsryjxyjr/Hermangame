// MapManager.cs
using System.Collections.Generic;
using UnityEngine;

public class MapManager : MonoBehaviour
{
    public static MapManager Instance;
    
    public List<MapNode> allNodes;
    public MapNode currentNode;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void Start()
    {
        InitializeMap();
    }
    
    public void InitializeMap()
    {
        // Clear all nodes
        foreach (var node in allNodes)
        {
            node.isAvailable = false;
            node.isCompleted = false;
            node.UpdateVisuals();
        }
        
        // Set starting node
        if (allNodes.Count > 0)
        {
            currentNode = allNodes[0];
            currentNode.isCompleted = true;
            currentNode.UpdateVisuals();
            
            // Make connected nodes available
            SetAvailableNodes();
        }
    }
    
    public void SelectNode(MapNode selectedNode)
    {
        if (selectedNode.isAvailable && !selectedNode.isCompleted)
        {
            // Mark node as completed
            selectedNode.isCompleted = true;
            selectedNode.UpdateVisuals();
            
            // Set as current node
            currentNode = selectedNode;
            
            // Load the appropriate encounter based on node type
            LoadEncounter(selectedNode.nodeType);
            
            // After encounter is completed, make new nodes available
            SetAvailableNodes();
        }
    }
    
    private void SetAvailableNodes()
    {
        // Clear all availability first
        foreach (var node in allNodes)
        {
            node.isAvailable = false;
        }
        
        // Simple linear path for demo - you can implement more complex pathing
        int currentIndex = allNodes.IndexOf(currentNode);
        if (currentIndex < allNodes.Count - 1)
        {
            allNodes[currentIndex + 1].isAvailable = true;
            allNodes[currentIndex + 1].UpdateVisuals();
        }
        
        // For branching paths, you'd need a more sophisticated system
        // with pre-defined connections between nodes
    }
    
    private void LoadEncounter(MapNode.NodeType nodeType)
    {
        Debug.Log($"Loading {nodeType} encounter");
        
        // Here you would transition to the appropriate scene/encounter
        // For example:
        switch (nodeType)
        {
            case MapNode.NodeType.Combat:
                // Load combat scene
                break;
            case MapNode.NodeType.Elite:
                // Load elite combat scene
                break;
            case MapNode.NodeType.Rest:
                // Load rest scene
                break;
            case MapNode.NodeType.Shop:
                // Load shop scene
                break;
            case MapNode.NodeType.Treasure:
                // Load treasure scene
                break;
            case MapNode.NodeType.Boss:
                // Load boss scene
                break;
        }
    }
}