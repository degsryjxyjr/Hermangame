// Scripts/Core/Services/ClassManager.cs
using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class ClassManager : MonoBehaviour
{
    public static ClassManager Instance { get; private set; }

    [SerializeField] private List<PlayerClassDefinition> _availableClasses = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        RefreshClassList();
    }

    public void RefreshClassList()
    {
        _availableClasses.Clear();
        _availableClasses.AddRange(Resources.LoadAll<PlayerClassDefinition>("PlayerClasses"));
        Debug.Log($"Found {_availableClasses.Count} player classes");
    }

    public List<PlayerClassDefinition> GetAvailableClasses()
    {
        return new List<PlayerClassDefinition>(_availableClasses);
    }

#if UNITY_EDITOR
    [MenuItem("Tools/Refresh Player Classes")]
    private static void RefreshClassesEditor()
    {
        if (Instance != null)
        {
            Instance.RefreshClassList();
        }
    }
#endif
}