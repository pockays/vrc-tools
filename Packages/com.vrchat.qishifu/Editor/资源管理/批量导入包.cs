using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

public class 批量导入包 : EditorWindow
{
    private List<string> availablePackages = new List<string>(); // 待选框
    private List<string> selectedPackages = new List<string>(); // 待安装框
    private Vector2 availableScrollPosition;
    private Vector2 selectedScrollPosition;

    [MenuItem("奇师傅工具箱/工具/资源管理/批量导入包", false, 0)]
    public static void ShowWindow()
    {
        GetWindow<批量导入包>("批量导入包");
    }

    private void OnGUI()
    {
        GUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));
        
        // 左侧：待选框
        GUILayout.BeginVertical(GUILayout.Width(position.width / 2 - 10));
        
        // 顶部按钮区域
        float buttonWidth = (position.width / 2 - 20) / 3; // 现在分成三份
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("选择文件夹", GUILayout.Width(buttonWidth)))
        {
            string path = EditorUtility.OpenFolderPanel("选择包所在文件夹", "", "");
            if (!string.IsNullOrEmpty(path))
            {
                string[] packageFiles = Directory.GetFiles(path, "*.unitypackage", SearchOption.AllDirectories);
                foreach (var packageFile in packageFiles)
                {
                    if (!availablePackages.Contains(packageFile) && !selectedPackages.Contains(packageFile))
                    {
                        availablePackages.Add(packageFile);
                    }
                }
            }
        }
        
        if (GUILayout.Button("全部添加", GUILayout.Width(buttonWidth)))
        {
            if (availablePackages.Count > 0)
            {
                if (EditorUtility.DisplayDialog("确认添加", 
                    $"确定要将所有 {availablePackages.Count} 个包添加到待安装列表吗？", 
                    "确定", "取消"))
                {
                    selectedPackages.AddRange(availablePackages);
                    availablePackages.Clear();
                }
            }
        }

        if (GUILayout.Button("清空待选框", GUILayout.Width(buttonWidth)))
        {
            if (availablePackages.Count > 0)
            {
                if (EditorUtility.DisplayDialog("确认清空", 
                    "确定要清空待选框中的所有包吗？", 
                    "确定", "取消"))
                {
                    availablePackages.Clear();
                }
            }
        }
        GUILayout.EndHorizontal();

        // 待选框标题
        GUILayout.Label("待选包列表：", EditorStyles.boldLabel);
        
        // 待选框内容区域
        availableScrollPosition = EditorGUILayout.BeginScrollView(availableScrollPosition, 
            GUILayout.ExpandHeight(true));
        
        List<string> packagesToMove = new List<string>();
        List<string> packagesToRemove = new List<string>();

        float contentWidth = position.width / 2 - 30; // 减去边距和滚动条宽度
        
        foreach (var package in availablePackages)
        {
            GUILayout.BeginHorizontal();
            
            string fileName = Path.GetFileName(package);
            float buttonWidth2 = 50;
            float labelWidth = contentWidth - (buttonWidth2 * 2) - 10; // 预留按钮空间和间距
            
            EditorGUILayout.LabelField(fileName, EditorStyles.wordWrappedLabel, 
                GUILayout.Width(labelWidth));

            if (GUILayout.Button("添加", GUILayout.Width(buttonWidth2)))
            {
                packagesToMove.Add(package);
            }
            
            if (GUILayout.Button("删除", GUILayout.Width(buttonWidth2)))
            {
                packagesToRemove.Add(package);
            }
            
            GUILayout.EndHorizontal();
        }

        foreach (var package in packagesToMove)
        {
            availablePackages.Remove(package);
            selectedPackages.Add(package);
        }
        
        foreach (var package in packagesToRemove)
        {
            availablePackages.Remove(package);
        }

        EditorGUILayout.EndScrollView();
        GUILayout.EndVertical();

        // 中间分隔线
        GUILayout.Box("", GUILayout.Width(2), GUILayout.ExpandHeight(true));

        // 右侧：待安装框
        GUILayout.BeginVertical(GUILayout.Width(position.width / 2 - 10));
        GUILayout.Label("待安装包列表：", EditorStyles.boldLabel);
        
        selectedScrollPosition = EditorGUILayout.BeginScrollView(selectedScrollPosition, 
            GUILayout.ExpandHeight(true));

        List<string> packagesToReturn = new List<string>();
        float rightContentWidth = position.width / 2 - 30;

        foreach (var package in selectedPackages)
        {
            GUILayout.BeginHorizontal();
            
            string fileName = Path.GetFileName(package);
            float buttonWidth2 = 50;
            float labelWidth = rightContentWidth - buttonWidth2 - 10;
            
            EditorGUILayout.LabelField(fileName, EditorStyles.wordWrappedLabel, 
                GUILayout.Width(labelWidth));

            if (GUILayout.Button("移出", GUILayout.Width(buttonWidth2)))
            {
                packagesToReturn.Add(package);
            }
            
            GUILayout.EndHorizontal();
        }

        foreach (var package in packagesToReturn)
        {
            selectedPackages.Remove(package);
            availablePackages.Add(package);
        }

        EditorGUILayout.EndScrollView();

        // 安装按钮
        GUI.enabled = selectedPackages.Count > 0;
        if (GUILayout.Button("安装选中的包"))
        {
            ImportSelectedPackages();
        }
        GUI.enabled = true;

        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
    }

    private void ImportSelectedPackages()
    {
        if (EditorUtility.DisplayDialog("确认安装", 
            $"确定要安装选中的 {selectedPackages.Count} 个包吗？", 
            "确定", "取消"))
        {
            foreach (var package in selectedPackages)
            {
                AssetDatabase.ImportPackage(package, false);
            }
            selectedPackages.Clear();
        }
    }
}
