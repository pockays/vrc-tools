using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class 统一光照锚点 : EditorWindow
{
    private GameObject targetObject;
    private GameObject anchorObject;
    private Vector2 scrollPosition;
    private string statusMessage = "";
    private bool isErrorMessage = false;
    private List<Renderer> foundRenderers = new List<Renderer>();

    [MenuItem("奇师傅工具箱/工具/对象管理/统一光照锚点")]
    private static void ShowWindow()
    {
        var window = GetWindow<统一光照锚点>();
        window.titleContent = new GUIContent("统一光照锚点");
        window.minSize = new Vector2(400, 200);
        window.Show();
    }

    private void OnGUI()
    {
        // 绘制标题
        EditorGUILayout.Space(10);
        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel);
        titleStyle.fontSize = 16;
        titleStyle.alignment = TextAnchor.MiddleCenter;
        EditorGUILayout.LabelField("光照锚点统一工具", titleStyle);
        EditorGUILayout.Space(10);
        
        // 绘制分割线
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        EditorGUILayout.Space(5);

        // 选择目标对象
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("目标对象", GUILayout.Width(60));
        targetObject = (GameObject)EditorGUILayout.ObjectField(targetObject, typeof(GameObject), true);
        EditorGUILayout.EndHorizontal();

        // 选择锚点对象
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("锚点对象", GUILayout.Width(60));
        anchorObject = (GameObject)EditorGUILayout.ObjectField(anchorObject, typeof(GameObject), true);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);

        // 按钮区域
        EditorGUILayout.BeginHorizontal();
        GUI.enabled = targetObject != null && anchorObject != null;
        if (GUILayout.Button("替换锚点", GUILayout.Height(30)))
        {
            ReplaceAnchors();
        }
        
        GUI.enabled = targetObject != null;
        if (GUILayout.Button("检查统一", GUILayout.Height(30)))
        {
            CheckAnchors();
        }
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);

        // 状态消息
        if (!string.IsNullOrEmpty(statusMessage))
        {
            EditorGUILayout.Space(5);
            GUIStyle messageStyle = new GUIStyle(EditorStyles.helpBox);
            messageStyle.fontSize = 12;
            messageStyle.normal.textColor = isErrorMessage ? Color.red : Color.green;
            messageStyle.alignment = TextAnchor.MiddleLeft;
            messageStyle.wordWrap = true;
            EditorGUILayout.LabelField(statusMessage, messageStyle);
        }

        // 渲染器列表
        if (foundRenderers.Count > 0)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("找到的渲染器：", EditorStyles.boldLabel);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            foreach (var renderer in foundRenderers)
            {
                if (renderer != null)
                {
                    EditorGUILayout.ObjectField(renderer, typeof(Renderer), true);
                }
            }
            
            EditorGUILayout.EndScrollView();
        }
    }

    private void FindRenderers()
    {
        foundRenderers.Clear();
        if (targetObject != null)
        {
            var meshRenderers = targetObject.GetComponentsInChildren<MeshRenderer>(true);
            var skinnedMeshRenderers = targetObject.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            
            foundRenderers.AddRange(meshRenderers);
            foundRenderers.AddRange(skinnedMeshRenderers);
        }
    }

    private void ReplaceAnchors()
    {
        if (targetObject == null || anchorObject == null)
        {
            UpdateStatus("请先选择目标对象和锚点对象", true);
            return;
        }

        FindRenderers();
        if (foundRenderers.Count == 0)
        {
            UpdateStatus("未找到任何渲染器组件", true);
            return;
        }

        int count = 0;
        foreach (var renderer in foundRenderers)
        {
            if (renderer != null)
            {
                Undo.RecordObject(renderer, "Set Light Probes Anchor");
                renderer.probeAnchor = anchorObject.transform;
                EditorUtility.SetDirty(renderer);
                count++;
            }
        }

        UpdateStatus($"成功更新了 {count} 个渲染器的光照锚点", false);
    }

    private void CheckAnchors()
    {
        if (targetObject == null)
        {
            UpdateStatus("请先选择目标对象", true);
            return;
        }

        FindRenderers();
        if (foundRenderers.Count == 0)
        {
            UpdateStatus("未找到任何渲染器组件", true);
            return;
        }

        Transform firstAnchor = null;
        bool isUnified = true;
        int count = 0;

        foreach (var renderer in foundRenderers)
        {
            if (renderer != null)
            {
                count++;
                if (firstAnchor == null)
                {
                    firstAnchor = renderer.probeAnchor;
                }
                else if (renderer.probeAnchor != firstAnchor)
                {
                    isUnified = false;
                    break;
                }
            }
        }

        if (isUnified)
        {
            string anchorName = firstAnchor != null ? firstAnchor.name : "未设置";
            UpdateStatus($"所有渲染器({count}个)的光照锚点已统一: {anchorName}", false);
        }
        else
        {
            UpdateStatus($"渲染器({count}个)的光照锚点不统一", true);
        }
    }

    private void UpdateStatus(string message, bool isError)
    {
        statusMessage = message;
        isErrorMessage = isError;
        Repaint();
    }
}
