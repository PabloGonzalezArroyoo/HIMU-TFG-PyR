using System.Collections.Generic;
using Unity.VisualScripting.Antlr3.Runtime.Tree;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;


public class HimuEditorWindow : EditorWindow
{
    private Label textTest;
    private Button buttonTest;
    private UnityEditor.UIElements.ObjectField sceneField;
    private DropdownField platformDropdown;
    private string dropdown;

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
        window.maxSize = new Vector2(600, 400);
        window.minSize = window.maxSize;

        UnityEngine.Debug.Log("HIMU: Tab opened.");
    }

    //private void OnEnable()
    //{
    //    AssemblyReloadEvents.afterAssemblyReload += Rebuild;
    //}

    //private void OnDisable()
    //{
    //    AssemblyReloadEvents.afterAssemblyReload -= Rebuild;
    //}

    //private void Rebuild()
    //{
    //    rootVisualElement.Clear();
    //    CreateGUI();
    //}

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
        tree.style.flexGrow = 1;
        
        root.Add(tree);

        // Store elements of the VisualTree for future use
        textTest = root.Q<Label>("TestText2");
        sceneField = root.Q<UnityEditor.UIElements.ObjectField>("SceneField");
        sceneField.objectType = typeof(SceneAsset);
        platformDropdown = root.Q<DropdownField>("PlatformDropdown");
        platformDropdown.choices = new List<string> { "Android", "Web"};
        platformDropdown.value = "Android";
        buttonTest = root.Q<Button>("TestButton");

        dropdown = "Platform: ";
        sceneField.RegisterValueChangedCallback(TestField);
        platformDropdown.RegisterValueChangedCallback(evt => { TestDropdown(evt.newValue); });
        buttonTest.clicked += TestButton;

        UnityEngine.Debug.Log("HIMU: GUI created.");
    }

    private void TestField(ChangeEvent<UnityEngine.Object> evt)
    {
        if (evt.newValue != null)
            textTest.text = "Scene: " + evt.newValue.ToString();
    }

    private void TestDropdown(string changed)
    {
        dropdown += " " + changed;
        textTest.text = dropdown;
    }

    private void TestButton()
    {
        platformDropdown.value = 
            (platformDropdown.value == platformDropdown.choices[0])
            ? platformDropdown.choices[1]
            : platformDropdown.choices[0];

        sceneField.value = null;
    }
}
