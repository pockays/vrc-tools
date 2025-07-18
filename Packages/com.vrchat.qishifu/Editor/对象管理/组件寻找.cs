using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class 查找指定组件 : EditorWindow
{
    private GameObject selectedObject;
    private string searchText = string.Empty;
    private List<Component> matchingComponents = new List<Component>();
    private Vector2 scrollPosition;

    // 定义要排除的基础组件类型
    private readonly string[] excludedComponents = new string[] 
    {
        "SkinnedMeshRenderer",
        "MeshRenderer",
        "MeshFilter",
        "Transform"
    };

    [MenuItem("奇师傅工具箱/工具/对象管理/查找指定组件", false, 0)]
    public static void ShowWindow()
    {
        GetWindow<查找指定组件>("查找指定组件");
    }

    private void OnGUI()
    {
        selectedObject = (GameObject)EditorGUILayout.ObjectField("指定对象", selectedObject, typeof(GameObject), true);
        searchText = EditorGUILayout.TextField("搜索文本", searchText);

        if (GUILayout.Button("搜索"))
        {
            FindComponents(selectedObject, searchText);
        }

        GUILayout.Space(10);
        EditorGUILayout.LabelField("它可以在指定对象内，进行宽泛搜索，对寻找某些特定组件很好用\n如果统计结果太长导致显示不全，请将窗口拉长\n当搜索文本为空时，将显示所有组件（不包含Transform和基础渲染组件）", EditorStyles.helpBox);

        GUILayout.Label("搜索结果：", EditorStyles.boldLabel);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));
        foreach (var component in matchingComponents)
        {
            if (component != null)
            {
                EditorGUILayout.ObjectField(component, typeof(Component), true);
            }
        }
        EditorGUILayout.EndScrollView();
    }

    private void FindComponents(GameObject obj, string search)
    {
        matchingComponents.Clear();

        if (obj != null)
        {
            SearchComponentsInGameObject(obj, search.ToLower());
        }

        Repaint();
    }

    private void SearchComponentsInGameObject(GameObject obj, string search)
    {
        foreach (Component component in obj.GetComponents<Component>())
        {
            if (component == null) continue;

            string componentName = component.GetType().Name;
            
            // 如果搜索文本为空，显示所有非排除的组件
            if (string.IsNullOrEmpty(search))
            {
                if (!IsExcludedComponent(componentName))
                {
                    matchingComponents.Add(component);
                }
            }
            // 如果有搜索文本，则按搜索文本匹配
            else if (componentName.ToLower().Contains(search))
            {
                matchingComponents.Add(component);
            }
        }

        foreach (Transform child in obj.transform)
        {
            SearchComponentsInGameObject(child.gameObject, search);
        }
    }

    private bool IsExcludedComponent(string componentName)
    {
        foreach (string excludedComponent in excludedComponents)
        {
            if (componentName == excludedComponent)
            {
                return true;
            }
        }
        return false;
    }
}
