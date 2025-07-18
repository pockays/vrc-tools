using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;

public class ToolCollectionWindow : EditorWindow
{
    private bool isInSelectionMode = true;
    private EditorWindow currentToolWindow;
    private Type currentToolType;
    private Vector2 scrollPosition;
    private string selectedCategory = "全部";

    // 定义工具类别
    private readonly string[] categories = new[]
    {
        "全部",
        "动骨",
        "动画",
        "资源管理",
        "对象管理",
        "不常用"
    };

    // 定义工具类别映射
    private readonly Dictionary<string, string[]> categoryMapping = new Dictionary<string, string[]>
    {
        {"对象管理", new[] {"复制对象的信息", "查找模型对应的骨骼", "查找指定组件", "形态键编辑器", "清理无用骨骼", "统一光照锚点"}},
        {"动画", new[] {"按路径查找动画", "将动画应用"}},
        {"动骨", new[] {"动骨外置辅助", "动骨复制", "统计动骨数量"}},
        {"资源管理", new[] {"批量导入包", "批量压缩材质"}},
        {"不常用", new[] {"形态键动画路径修复", "对象批量命名"}}
    };

    // 定义工具列表，包含工具类型和显示名称
    private readonly (Type type, string displayName)[] tools = new[]
    {
        (typeof(将动画应用), "将动画应用"),
        (typeof(动骨外置辅助), "动骨外置辅助"),
        (typeof(按路径查找动画), "按路径查找动画"),
        (typeof(复制对象的信息), "复制对象的信息"),
        (typeof(批量导入包), "批量导入包"),
        (typeof(对象批量命名), "对象批量命名"),
        (typeof(查找模型对应的骨骼), "查找模型对应的骨骼"),
        (typeof(批量压缩材质), "批量压缩材质"),
        (typeof(动骨复制), "动骨复制"),
        (typeof(清理无用骨骼), "清理无用骨骼"),
        (typeof(形态键编辑器), "形态键编辑器"),
        (typeof(查找指定组件), "查找指定组件"),
        (typeof(统计动骨数量), "统计动骨数量"),
        (typeof(形态键动画路径修复), "形态键动画路径修复"),
        (typeof(统一光照锚点), "统一光照锚点"),
    };

    [MenuItem("奇师傅工具箱/打开工具箱")]
    public static void ShowWindow()
    {
        GetWindow<ToolCollectionWindow>("奇师傅工具箱");
    }

    private void OnGUI()
    {
        if (isInSelectionMode)
        {
            DrawToolSelectionGUI();
        }
        else
        {
            DrawCurrentToolGUI();
        }
    }

    private void DrawToolSelectionGUI()
    {
        // 标题和更新按钮
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(10);
        EditorGUILayout.LabelField("奇师傅工具箱", EditorStyles.boldLabel);
        if (GUILayout.Button("更新工具箱", GUILayout.Width(80)))
        {
            UpdateTool.ShowUpdateConfirmation();
        }
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(5);

        // 绘制分类选择按钮
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace(); // 居中对齐
        foreach (var category in categories)
        {
            var style = new GUIStyle(EditorStyles.miniButton);
            if (selectedCategory == category)
            {
                style.normal.textColor = Color.white;
                style.normal.background = EditorGUIUtility.whiteTexture;
            }
            if (GUILayout.Button(category, style, GUILayout.MinWidth(80)))
            {
                selectedCategory = category;
            }
        }
        GUILayout.FlexibleSpace(); // 居中对齐
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(10);
        
        // 使用Box包装工具列表
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        if (selectedCategory != "全部")
        {
            EditorGUILayout.LabelField($"{selectedCategory}类工具", EditorStyles.boldLabel);
        }
        
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        // 创建一个网格布局
        float windowWidth = position.width - 20; // 减去边距
        float buttonWidth = 150; // 按钮宽度
        float spacing = 10; // 按钮间距
        int columnsCount = Mathf.Max(1, Mathf.FloorToInt((windowWidth + spacing) / (buttonWidth + spacing)));
        
        List<(Type type, string displayName)> filteredTools = new List<(Type type, string displayName)>();
        
        // 筛选当前类别的工具
        foreach (var tool in tools)
        {
            if (selectedCategory == "全部" || 
                (categoryMapping.ContainsKey(selectedCategory) && 
                Array.IndexOf(categoryMapping[selectedCategory], tool.displayName) != -1))
            {
                filteredTools.Add(tool);
            }
        }

        // 按网格布局绘制工具按钮
        for (int i = 0; i < filteredTools.Count; i++)
        {
            if (i % columnsCount == 0)
                EditorGUILayout.BeginHorizontal();

            var buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.wordWrap = true;
            buttonStyle.fixedWidth = buttonWidth;
            buttonStyle.fixedHeight = 40;
            
            if (GUILayout.Button(filteredTools[i].displayName, buttonStyle))
            {
                SwitchToTool(filteredTools[i].type);
            }

            if ((i + 1) % columnsCount == 0 || i == filteredTools.Count - 1)
            {
                GUILayout.FlexibleSpace(); // 填充剩余空间
                EditorGUILayout.EndHorizontal();
            }
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawCurrentToolGUI()
    {
        using (new EditorGUILayout.VerticalScope())
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                // 在顶部添加返回按钮
                if (GUILayout.Button("返回工具列表", GUILayout.Height(25)))
                {
                    ReturnToSelection();
                    GUIUtility.ExitGUI(); // 立即退出GUI循环
                    return;
                }
                
                // 添加更新按钮
                if (GUILayout.Button("更新工具箱", GUILayout.Height(25), GUILayout.Width(80)))
                {
                    UpdateTool.ShowUpdateConfirmation();
                }
            }

            EditorGUILayout.Space();

            if (currentToolWindow != null)
            {
                try
                {
                    // 使用OnGUI反射调用
                    var onGUIMethod = currentToolType.GetMethod("OnGUI", 
                        System.Reflection.BindingFlags.NonPublic | 
                        System.Reflection.BindingFlags.Instance);
                    
                    if (onGUIMethod != null)
                    {
                        onGUIMethod.Invoke(currentToolWindow, null);
                    }
                }
                catch (Exception e)
                {
                    EditorGUILayout.HelpBox($"工具加载失败: {e.Message}", MessageType.Error);
                }
            }
        }
    }

    private void SwitchToTool(Type toolType)
    {
        try
        {
            // 创建工具窗口实例
            currentToolWindow = CreateInstance(toolType) as EditorWindow;
            currentToolType = toolType;
            isInSelectionMode = false;

            // 强制重绘窗口
            Repaint();
        }
        catch (Exception e)
        {
            Debug.LogError($"切换工具失败: {e.Message}");
        }
    }

    private void ReturnToSelection()
    {
        // 确保所有GUI组都被正确关闭
        while (EditorGUIUtility.GetControlID(FocusType.Passive) > 0)
        {
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        if (currentToolWindow != null)
        {
            // 清理当前工具窗口
            DestroyImmediate(currentToolWindow);
            currentToolWindow = null;
            currentToolType = null;
        }

        isInSelectionMode = true;
        Repaint();
    }
}
