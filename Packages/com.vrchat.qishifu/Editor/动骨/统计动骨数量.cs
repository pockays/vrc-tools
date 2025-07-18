using UnityEngine;
using UnityEditor;
using System.Collections.Generic; // 引入字典所需的命名空间

public class 统计动骨数量 : EditorWindow
{
    private GameObject rootObject;
    private Dictionary<string, int> componentCounts = new Dictionary<string, int>();
    private int totalComponentCount = 0; // 用于存储所有组件的总数

    [MenuItem("奇师傅工具箱/工具/动骨/统计动骨数量", false, 0)]
    public static void ShowWindow()
    {
        GetWindow<统计动骨数量>("统计动骨数量");
    }

    void OnGUI()
    {
        rootObject = EditorGUILayout.ObjectField("指定对象", rootObject, typeof(GameObject), true) as GameObject;

        if (GUILayout.Button("统计"))
        {
            componentCounts.Clear(); // 清除旧的统计结果
            totalComponentCount = 0; // 重置总组件数
            if (rootObject != null)
            {
                CountComponentsInRootObject(rootObject);
            }
            else
            {
                Debug.LogError("未指定对象。");
            }
        }

        GUILayout.Space(10); // 添加一些间距
        EditorGUILayout.LabelField("它可以统计指定对象内，包含的VRC Phys Bone组件即动骨的数量，并展示每个一级子对象包含的动骨数量\n如果统计结果太长导致显示不全，请将窗口拉长", EditorStyles.helpBox);

        if (componentCounts.Count > 0)
        {
            GUILayout.Space(20);
            GUILayout.Label("统计结果:");
            DrawTable(); // 绘制表格显示结果
            GUILayout.Space(10);
            GUILayout.Label($"总共包含的VRC Phys Bone组件数量: {totalComponentCount}", EditorStyles.boldLabel); // 显示总数
        }
    }

    void CountComponentsInRootObject(GameObject rootObj)
    {
        foreach (Transform child in rootObj.transform)
        {
            int count = CountVRCPhysBoneComponentsInChildren(child.gameObject);
            if (!componentCounts.ContainsKey(child.name)) // 防止重复键异常
            {
                componentCounts.Add(child.name, count);
                totalComponentCount += count; // 累加到总数中
            }
        }
    }

    int CountVRCPhysBoneComponentsInChildren(GameObject obj)
    {
        int count = 0;
        if (obj.GetComponent("VRCPhysBone") != null)
        {
            count++;
        }

        foreach (Transform child in obj.transform)
        {
            count += CountVRCPhysBoneComponentsInChildren(child.gameObject);
        }

        return count;
    }

    void DrawTable()
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("一级子对象", EditorStyles.boldLabel, GUILayout.Width(200));
        GUILayout.Label("动骨数量", EditorStyles.boldLabel, GUILayout.Width(100));
        EditorGUILayout.EndHorizontal();

        foreach (var pair in componentCounts)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(pair.Key, GUILayout.Width(200));
            GUILayout.Label(pair.Value.ToString(), GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();
        }
    }
}
