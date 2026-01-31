using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;


public class HimuEditorWindow : EditorWindow
{

    // Add new tab to the inspector context menus.
    // This has to be done in the "Tools" tab in order to comply with Unity's Asset Store guidelines.
    [MenuItem("Tools/HIMU")]

    /// <summary>
    /// Called when the tab is opened. Creates the new GUI with an specific size.
    /// </summary>
    public static void OpenEditorWindow()
    {
        HimuEditorWindow window = GetWindow<HimuEditorWindow>();
        window.titleContent = new GUIContent("HIMU Tool");
        window.maxSize = new Vector2(900, 600);
        window.minSize = window.maxSize;

        UnityEngine.Debug.Log("HIMU: Tab opened.");
    }

    /// <summary>
    /// Creates the GUI from a UXML.
    /// </summary>
    public void CreateGUI()
    {
        // Add to the main root the VisualTree of the new screen
        VisualElement root = rootVisualElement;
        var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
            "Assets/Scripts/Editor/VisualTrees/MainContent.uxml");
        VisualElement tree = visualTree.Instantiate();
        root.Add(tree);

        // Store elements of the VisualTree for future use
        //audioFileInput = root.Q<UnityEditor.UIElements.ObjectField>("audioField");

        UnityEngine.Debug.Log("HIMU: GUI created.");
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
