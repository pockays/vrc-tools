using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class 形态键编辑器 : EditorWindow
{
    private GameObject selectedObject;
    private SkinnedMeshRenderer skinnedMeshRenderer;
    private Dictionary<string, List<int>> sections = new Dictionary<string, List<int>>();
    private Dictionary<string, Vector2> scrollPositions = new Dictionary<string, Vector2>(); // 形态键组内容的滚动位置
    private Dictionary<string, Vector2> sectionInternalScrolls = new Dictionary<string, Vector2>(); // 形态键组内部的滚动位置
    private Dictionary<int, Vector2> columnScrollPositions = new Dictionary<int, Vector2>();
    private string detectedDelimiter = ""; // 自动检测的分隔符
    private bool isLoaded = false;
    private List<int> modifiedBlendShapes = new List<int>(); // 缓存已修改的形态键
    private string searchText = ""; // 搜索框的文本
    private List<int> searchResults = new List<int>(); // 搜索结果列表
    private bool searchFoldout = false; // 搜索组的折叠状态

    private Dictionary<string, bool> sectionFoldouts = new Dictionary<string, bool>();
    private int sectionsPerRow = 3;
    private float groupHeight = 200f;
    private float columnWidth = 400f; // 统一的列宽

    [MenuItem("奇师傅工具箱/工具/对象管理/形态键编辑器", false, 0)]
    public static void ShowWindow()
    {
        var window = GetWindow<形态键编辑器>("形态键编辑器", true);
        window.FindBodyObject();
    }

    private void FindBodyObject()
    {
        // 查找场景中所有对象
        var allObjects = GameObject.FindObjectsOfType<GameObject>();

        // 查找名称恰好为"Body"（完全匹配）的对象
        var bodyObject = allObjects.FirstOrDefault(go => 
            go.name == "Body" && // 精确匹配名称
            go.GetComponent<SkinnedMeshRenderer>() != null && // 必须有SkinnedMeshRenderer组件
            go.GetComponent<SkinnedMeshRenderer>().sharedMesh != null // Mesh不能为空
        );

        if (bodyObject != null)
        {
            selectedObject = bodyObject;
            skinnedMeshRenderer = selectedObject.GetComponent<SkinnedMeshRenderer>();
            detectedDelimiter = DetectDelimiter(skinnedMeshRenderer.sharedMesh);
            if (string.IsNullOrEmpty(detectedDelimiter))
            {
                EditorUtility.DisplayDialog("提示", "未检测到有效的分隔符或分组数量不足5组，将显示所有形态键。", "确定");
            }
            LoadBlendShapes();
            UpdateModifiedBlendShapes();
            isLoaded = true;
            Repaint(); // 刷新窗口
        }
        else
        {
            // 如果找不到符合条件的对象，显示提示消息
            Debug.LogWarning("未找到符合条件的Body对象（需要名称完全匹配'Body'且包含有效的SkinnedMeshRenderer组件）");
        }
    }

    // 检查分组是否有效（组数>=5）
    private bool IsValidGrouping(string delimiter, Mesh mesh)
    {
        if (string.IsNullOrEmpty(delimiter) || mesh == null) return false;

        HashSet<string> groups = new HashSet<string>();
        string currentGroup = "";

        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            string name = mesh.GetBlendShapeName(i);
            
            // 检查是否是分隔符行
            if (name.Contains(new string(delimiter[0], 4)))
            {
                currentGroup = name.Replace(delimiter, "").Trim();
                if (!string.IsNullOrEmpty(currentGroup))
                {
                    groups.Add(currentGroup);
                }
            }
        }

        return groups.Count >= 5;
    }

    // 智能检测分隔符
    private string DetectDelimiter(Mesh mesh)
    {
        if (mesh == null || mesh.blendShapeCount == 0) return "";

        // 候选分隔符列表（按优先级排序）
        char[] candidateDelimiters = new char[] { '_', '-', '=', '.', '#' };
        Dictionary<char, int> delimiterScores = new Dictionary<char, int>();

        foreach (char delimiter in candidateDelimiters)
        {
            delimiterScores[delimiter] = 0;
        }

        // 分析所有BlendShape名称
        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            string name = mesh.GetBlendShapeName(i);
            
            foreach (char delimiter in candidateDelimiters)
            {
                // 检查连续的分隔符
                if (name.Contains(new string(delimiter, 4)))
                {
                    delimiterScores[delimiter] += 10;
                    
                    // 检查分隔符前后是否有有意义的文本
                    string[] parts = name.Split(new string(delimiter, 4), System.StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[0]) && !string.IsNullOrWhiteSpace(parts[1]))
                    {
                        delimiterScores[delimiter] += 5;
                    }
                }
            }
        }

        // 按分数排序尝试每个分隔符
        foreach (var delimiterScore in delimiterScores.OrderByDescending(x => x.Value))
        {
            string candidateDelimiter = delimiterScore.Key.ToString();
            if (delimiterScore.Value > 0 && IsValidGrouping(candidateDelimiter, mesh))
            {
                return candidateDelimiter;
            }
        }

        return "";
    }

    void OnGUI()
    {
        if (!isLoaded)
        {
            GUILayout.Label("选择网格对象:", EditorStyles.boldLabel);
            selectedObject = EditorGUILayout.ObjectField("目标对象", selectedObject, typeof(GameObject), true) as GameObject;

            // 显示帮助信息
            EditorGUILayout.HelpBox("此工具会智能检测形态键名称中的分隔符（连续4个及以上相同的字符），并按照分隔符对形态键进行分组显示。要求分组数量不少于5组才会启用分组。", MessageType.Info);

            if (selectedObject != null)
            {
                skinnedMeshRenderer = selectedObject.GetComponent<SkinnedMeshRenderer>();
                if (skinnedMeshRenderer != null && skinnedMeshRenderer.sharedMesh != null)
                {
                    if (GUILayout.Button("加载形态键"))
                    {
                        detectedDelimiter = DetectDelimiter(skinnedMeshRenderer.sharedMesh);
                        if (string.IsNullOrEmpty(detectedDelimiter))
                        {
                            EditorUtility.DisplayDialog("提示", "未检测到有效的分隔符或分组数量不足5组，将显示所有形态键。", "确定");
                        }
                        LoadBlendShapes();
                        UpdateModifiedBlendShapes(); // 初始加载时更新已修改列表
                        isLoaded = true;
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("所选对象不包含SkinnedMeshRenderer组件或网格为空。", MessageType.Warning);
                }
            }
        }
        else
        {
            if (!string.IsNullOrEmpty(detectedDelimiter))
            {
                EditorGUILayout.HelpBox($"检测到的分隔符: {detectedDelimiter}", MessageType.Info);
            }

            if (GUILayout.Button("退出", GUILayout.Width(50)))
            {
                isLoaded = false;
                selectedObject = null;
                sections.Clear();
                sectionFoldouts.Clear();
                scrollPositions.Clear();
                columnScrollPositions.Clear();
                searchText = "";
                searchResults.Clear();
                Repaint();
                return;
            }

            GUILayout.Label("每排的形态键组数量:", EditorStyles.boldLabel);
            sectionsPerRow = EditorGUILayout.IntField("形态键组数量", sectionsPerRow);
            if (sectionsPerRow <= 0) sectionsPerRow = 1;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("形态键组：", EditorStyles.boldLabel);
            
            EditorGUILayout.LabelField("高度", GUILayout.Width(30));
            groupHeight = EditorGUILayout.FloatField(groupHeight, GUILayout.Width(50));
            if (groupHeight <= 0) groupHeight = 100f;
            
            GUILayout.Space(20);
            
            EditorGUILayout.LabelField("宽度", GUILayout.Width(30));
            columnWidth = EditorGUILayout.FloatField(columnWidth, GUILayout.Width(50));
            if (columnWidth < 200) columnWidth = 200f;
            else if (columnWidth > 800) columnWidth = 800f;
            
            EditorGUILayout.EndHorizontal();

            DisplayBlendShapesWithColumns();
        }
    }

    void LoadBlendShapes()
    {
        sections.Clear();
        sectionFoldouts.Clear();
        Mesh mesh = skinnedMeshRenderer.sharedMesh;
        string currentSection = "默认"; // 默认分组
        sections[currentSection] = new List<int>();

        // 初始化"已修改"组的折叠状态为false（收起）
        sectionFoldouts["已修改"] = false;

        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            string blendShapeName = mesh.GetBlendShapeName(i);
            
            if (!string.IsNullOrEmpty(detectedDelimiter))
            {
                // 检查是否包含连续4个及以上的分隔符
                if (blendShapeName.Contains(new string(detectedDelimiter[0], 4)))
                {
                    currentSection = blendShapeName.Replace(detectedDelimiter, "").Trim();
                    if (!sections.ContainsKey(currentSection))
                    {
                        sections[currentSection] = new List<int>();
                        // 初始化新组的折叠状态为false（收起）
                        sectionFoldouts[currentSection] = false;
                    }
                    continue; // 跳过分隔符行
                }
            }
            
            sections[currentSection].Add(i);
        }

        // 如果"默认"组为空，则删除它
        if (sections["默认"].Count == 0)
        {
            sections.Remove("默认");
            sectionFoldouts.Remove("默认");
        }
        else
        {
            // 确保默认组的折叠状态被初始化为false（收起）
            sectionFoldouts["默认"] = false;
        }
    }

    // 更新已修改的形态键列表
    private void UpdateModifiedBlendShapes()
    {
        modifiedBlendShapes.Clear();
        Mesh mesh = skinnedMeshRenderer.sharedMesh;

        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            float weight = skinnedMeshRenderer.GetBlendShapeWeight(i);
            if (weight != 0f)
            {
                modifiedBlendShapes.Add(i);
            }
        }
    }

    // 获取指定组中已修改的形态键数量
    private int GetModifiedCountInSection(List<int> sectionIndices)
    {
        return sectionIndices.Count(index => skinnedMeshRenderer.GetBlendShapeWeight(index) != 0f);
    }

    private void UpdateSearchResults()
    {
        searchResults.Clear();
        if (!string.IsNullOrEmpty(searchText))
        {
            var mesh = skinnedMeshRenderer.sharedMesh;
            for (int i = 0; i < mesh.blendShapeCount; i++)
            {
                if (mesh.GetBlendShapeName(i).ToLower().Contains(searchText.ToLower()))
                {
                    searchResults.Add(i);
                }
            }
        }
    }

    private void DisplayBlendShapesWithColumns()
    {
        List<KeyValuePair<string, List<int>>> allSections = new List<KeyValuePair<string, List<int>>>();
        
        // 首先添加搜索组
        allSections.Add(new KeyValuePair<string, List<int>>("搜索", searchResults));
        // 其次添加已修改组
        allSections.Add(new KeyValuePair<string, List<int>>("已修改", modifiedBlendShapes));
        // 最后添加其他组
        foreach (var section in sections)
        {
            allSections.Add(section);
        }

        // 计算每列应显示的组数
        int totalSections = allSections.Count;
        int sectionsPerColumn = Mathf.Max(1, (totalSections + sectionsPerRow - 1) / sectionsPerRow);

        EditorGUILayout.BeginHorizontal();
        
        // 为每列创建独立的滚动区域
        for (int col = 0; col < sectionsPerRow; col++)
        {
            if (!columnScrollPositions.ContainsKey(col))
            {
                columnScrollPositions[col] = Vector2.zero;
            }

            EditorGUILayout.BeginVertical("box", GUILayout.Width(columnWidth));
            
            // 列的滚动视图
            columnScrollPositions[col] = EditorGUILayout.BeginScrollView(columnScrollPositions[col]);

            // 显示当前列中的形态键组
            int startIndex = col * sectionsPerColumn;
            int endIndex = Mathf.Min(startIndex + sectionsPerColumn, totalSections);
            
            for (int sectionIndex = startIndex; sectionIndex < endIndex; sectionIndex++)
            {
                var section = allSections[sectionIndex];
                List<int> displayIndices = section.Value;


                GUILayout.BeginVertical("box");
                
                // 标题栏
                EditorGUILayout.BeginHorizontal();
                if (!sectionFoldouts.ContainsKey(section.Key))
                {
                    sectionFoldouts[section.Key] = false;
                }

                if (section.Key == "已修改")
                {
                    sectionFoldouts[section.Key] = EditorGUILayout.Foldout(
                        sectionFoldouts[section.Key], 
                        section.Key + $" ({displayIndices.Count})", 
                        true
                    );
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("刷新", GUILayout.Width(50)))
                    {
                        UpdateModifiedBlendShapes();
                        Repaint();
                    }
                }
                else
                {
                    int modifiedCount = GetModifiedCountInSection(displayIndices);
                    string displayName = modifiedCount > 0 ? 
                        $"{modifiedCount} {section.Key}" : section.Key;
                    
                    sectionFoldouts[section.Key] = EditorGUILayout.Foldout(
                        sectionFoldouts[section.Key], 
                        displayName, 
                        true
                    );
                }
                EditorGUILayout.EndHorizontal();

                // 内容区域
                if (sectionFoldouts[section.Key])
                {
                    EditorGUILayout.BeginVertical(GUILayout.Height(groupHeight));

                    if (section.Key == "搜索")
                    {
                        EditorGUILayout.BeginHorizontal();
                        var newSearchText = EditorGUILayout.TextField(searchText, GUILayout.ExpandWidth(true));
                        if (newSearchText != searchText)
                        {
                            searchText = newSearchText;
                            UpdateSearchResults();
                        }
                        if (GUILayout.Button("清除", GUILayout.Width(50)))
                        {
                            searchText = "";
                            searchResults.Clear();
                        }
                        EditorGUILayout.EndHorizontal();
                    }

                    if (displayIndices.Count == 0)
                    {
                        if (section.Key == "已修改")
                        {
                            EditorGUILayout.HelpBox("没有已修改的形态键", MessageType.Info);
                        }
                        else if (section.Key == "搜索")
                        {
                            if (!string.IsNullOrEmpty(searchText))
                            {
                                EditorGUILayout.HelpBox("没有找到匹配的形态键", MessageType.Info);
                            }
                        }
                    }
                    else
                    {
                        // 获取或创建此分组的内部滚动位置
                        if (!sectionInternalScrolls.ContainsKey(section.Key))
                        {
                            sectionInternalScrolls[section.Key] = Vector2.zero;
                        }
                        sectionInternalScrolls[section.Key] = EditorGUILayout.BeginScrollView(
                            sectionInternalScrolls[section.Key], 
                            GUILayout.Height(groupHeight)
                        );
                        foreach (int index in displayIndices)
                        {
                            string blendShapeName = skinnedMeshRenderer.sharedMesh.GetBlendShapeName(index);
                            float weight = skinnedMeshRenderer.GetBlendShapeWeight(index);
                            EditorGUI.BeginChangeCheck();
                            float newWeight = EditorGUILayout.Slider(blendShapeName, weight, 0.0f, 100.0f);
                            if (EditorGUI.EndChangeCheck())
                            {
                                Undo.RecordObject(skinnedMeshRenderer, "Change Blend Shape Weight");
                                skinnedMeshRenderer.SetBlendShapeWeight(index, newWeight);
                            }
                        }
                        EditorGUILayout.EndScrollView();
                    }

                    EditorGUILayout.EndVertical();
                }

                GUILayout.EndVertical();
                GUILayout.Space(5);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            
            if (col < sectionsPerRow - 1)
                GUILayout.Space(10);
        }
        
        EditorGUILayout.EndHorizontal();
    }
}
