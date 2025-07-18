using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;

public class 形态键动画路径修复 : EditorWindow
{
    private AnimationClip targetClip;
    private string modifyText = "";
    private Vector2 scrollPosition;
    private List<BlendShapePath> blendShapePaths = new List<BlendShapePath>();

    // 用于存储形态键路径和编辑状态
    private class BlendShapePath
    {
        public string originalPath;
        public string editedPath;
        public string bindingPath;
        public EditorCurveBinding binding;
        public bool isModified => originalPath != editedPath;
    }

    [MenuItem("奇师傅工具箱/工具/其他/形态键动画路径修复", false, 0)]
    public static void ShowWindow()
    {
        GetWindow<形态键动画路径修复>("形态键动画路径修复");
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10);
        
        // 动画文件选择
        EditorGUI.BeginChangeCheck();
        targetClip = EditorGUILayout.ObjectField("目标动画", targetClip, typeof(AnimationClip), false) as AnimationClip;
        if (EditorGUI.EndChangeCheck())
        {
            RefreshBlendShapePaths();
        }

        EditorGUILayout.Space(10);

        if (targetClip == null)
        {
            EditorGUILayout.HelpBox("请选择一个动画文件", MessageType.Info);
            return;
        }

        // 批量操作区域
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("批量操作", EditorStyles.boldLabel);
        
        modifyText = EditorGUILayout.TextField("操作文本", modifyText);

        EditorGUILayout.BeginHorizontal();
        GUI.enabled = targetClip != null && !string.IsNullOrEmpty(modifyText);
        
        if (GUILayout.Button("添加前缀", GUILayout.Height(25)))
        {
            if (EditorUtility.DisplayDialog("确认", "确定要为所有形态键添加前缀吗？", "确定", "取消"))
            {
                BatchModifyPaths(PathModificationType.AddPrefix);
            }
        }
        
        if (GUILayout.Button("添加后缀", GUILayout.Height(25)))
        {
            if (EditorUtility.DisplayDialog("确认", "确定要为所有形态键添加后缀吗？", "确定", "取消"))
            {
                BatchModifyPaths(PathModificationType.AddSuffix);
            }
        }
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("删除前缀", GUILayout.Height(25)))
        {
            if (EditorUtility.DisplayDialog("确认", "确定要删除所有形态键的指定前缀吗？", "确定", "取消"))
            {
                BatchModifyPaths(PathModificationType.RemovePrefix);
            }
        }
        
        if (GUILayout.Button("删除后缀", GUILayout.Height(25)))
        {
            if (EditorUtility.DisplayDialog("确认", "确定要删除所有形态键的指定后缀吗？", "确定", "取消"))
            {
                BatchModifyPaths(PathModificationType.RemoveSuffix);
            }
        }
        
        EditorGUILayout.EndHorizontal();
        GUI.enabled = true;
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(10);

        if (blendShapePaths.Count == 0)
        {
            EditorGUILayout.HelpBox("该动画文件中没有形态键动画路径", MessageType.Info);
            return;
        }

        // 显示修改计数
        int modifiedCount = blendShapePaths.Count(p => p.isModified);
        if (modifiedCount > 0)
        {
            EditorGUILayout.HelpBox($"已修改 {modifiedCount} 个路径", MessageType.Info);
        }

        // 保存按钮
        GUI.enabled = modifiedCount > 0;
        if (GUILayout.Button("保存修改", GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog("确认", "确定要保存所有修改吗？", "确定", "取消"))
            {
                SaveModifications();
            }
        }
        GUI.enabled = true;

        EditorGUILayout.Space(10);

        // 形态键路径列表
        EditorGUILayout.LabelField("形态键路径列表：", EditorStyles.boldLabel);
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        for (int i = 0; i < blendShapePaths.Count; i++)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // 显示原始路径（只读）
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("原始路径", blendShapePaths[i].originalPath);
            EditorGUI.EndDisabledGroup();

            // 编辑框
            string newPath = EditorGUILayout.TextField("新路径", blendShapePaths[i].editedPath);
            if (newPath != blendShapePaths[i].editedPath)
            {
                blendShapePaths[i].editedPath = newPath;
                Repaint();
            }

            // 如果有修改，显示恢复按钮
            if (blendShapePaths[i].isModified)
            {
                if (GUILayout.Button("恢复原始路径"))
                {
                    blendShapePaths[i].editedPath = blendShapePaths[i].originalPath;
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }

        EditorGUILayout.EndScrollView();
    }

    private enum PathModificationType
    {
        AddPrefix,
        AddSuffix,
        RemovePrefix,
        RemoveSuffix
    }

    private void BatchModifyPaths(PathModificationType modificationType)
    {
        if (targetClip == null || string.IsNullOrEmpty(modifyText))
            return;

        foreach (var pathInfo in blendShapePaths)
        {
            string blendShapeName = pathInfo.editedPath; // 使用当前编辑的路径作为基础
            string newBlendShapeName = blendShapeName;

            switch (modificationType)
            {
                case PathModificationType.AddPrefix:
                    newBlendShapeName = modifyText + blendShapeName;
                    break;

                case PathModificationType.AddSuffix:
                    newBlendShapeName = blendShapeName + modifyText;
                    break;

                case PathModificationType.RemovePrefix:
                    if (blendShapeName.StartsWith(modifyText))
                    {
                        newBlendShapeName = blendShapeName.Substring(modifyText.Length);
                    }
                    break;

                case PathModificationType.RemoveSuffix:
                    if (blendShapeName.EndsWith(modifyText))
                    {
                        newBlendShapeName = blendShapeName.Substring(0, blendShapeName.Length - modifyText.Length);
                    }
                    break;
            }

            if (newBlendShapeName != blendShapeName)
            {
                pathInfo.editedPath = newBlendShapeName;
            }
        }

        Repaint();
    }

    private void RefreshBlendShapePaths()
    {
        blendShapePaths.Clear();

        if (targetClip == null)
            return;

        var curveBindings = AnimationUtility.GetCurveBindings(targetClip);
        var blendShapeBindings = curveBindings.Where(binding => 
            binding.type == typeof(SkinnedMeshRenderer) && 
            binding.propertyName.StartsWith("blendShape.")
        );

        foreach (var binding in blendShapeBindings)
        {
            string path = binding.propertyName.Replace("blendShape.", "");
            blendShapePaths.Add(new BlendShapePath
            {
                originalPath = path,
                editedPath = path,
                bindingPath = binding.path,
                binding = binding
            });
        }
    }

    private void SaveModifications()
    {
        bool anyModified = false;

        foreach (var pathInfo in blendShapePaths)
        {
            if (!pathInfo.isModified)
                continue;

            // 获取原始曲线
            AnimationCurve curve = AnimationUtility.GetEditorCurve(targetClip, pathInfo.binding);
                
            // 创建新的绑定
            EditorCurveBinding newBinding = new EditorCurveBinding
            {
                type = pathInfo.binding.type,
                path = pathInfo.bindingPath,
                propertyName = "blendShape." + pathInfo.editedPath
            };

            // 移除旧的曲线
            AnimationUtility.SetEditorCurve(targetClip, pathInfo.binding, null);
                
            // 设置新的曲线
            AnimationUtility.SetEditorCurve(targetClip, newBinding, curve);

            anyModified = true;
        }

        if (anyModified)
        {
            EditorUtility.SetDirty(targetClip);
            AssetDatabase.SaveAssets();
            RefreshBlendShapePaths(); // 刷新列表
            Debug.Log("已保存所有修改");
        }
    }
}
