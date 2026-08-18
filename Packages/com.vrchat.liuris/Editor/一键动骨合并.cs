using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
using VRC.SDK3.Dynamics.PhysBone.Components;
#endif

#if UNITY_EDITOR
public class VRCPhysBoneOrganizer : EditorWindow
{
    private Vector2 scrollPosition;
    private string statusText = "就绪";
    private int totalProcessed = 0;
    private int totalMerged = 0;

    private GameObject countTargetObject;
    private int physBoneCount = 0;

    [MenuItem("Tools/VRC Phys Bone Organizer")]
    public static void ShowWindow()
    {
        GetWindow<VRCPhysBoneOrganizer>("Phys Bone Organizer");
    }

    void OnGUI()
    {
        GUILayout.Label("VRC Phys Bone 合并工具", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        if (GUILayout.Button("合并选中的Phys Bones", GUILayout.Height(40)))
        {
            ProcessSelectedObjects();
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("移动PhysBones到Root", GUILayout.Height(40)))
        {
            MovePhysBonesToRoot();
        }

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("挂载PhysBone Optimizer", GUILayout.Height(36)))
        {
            AttachOptimizerToSelection();
        }
        if (GUILayout.Button("清除PhysBone Optimizer", GUILayout.Height(36)))
        {
            RemoveOptimizerFromSelection();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        EditorGUILayout.LabelField("处理状态:", statusText);
        EditorGUILayout.LabelField("总处理组数:", totalProcessed.ToString());
        EditorGUILayout.LabelField("总合并对象:", totalMerged.ToString());

        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Phys Bone 数量统计", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        countTargetObject = (GameObject)EditorGUILayout.ObjectField("统计对象", countTargetObject, typeof(GameObject), true);
        if (EditorGUI.EndChangeCheck())
        {
            CountPhysBones();
        }

        if (countTargetObject != null)
        {
            GUILayout.Label($"VRC Phys Bones 数量: {physBoneCount}",
                new GUIStyle(EditorStyles.label) { fontSize = 24, normal = { textColor = Color.green } });
        }

        EditorGUILayout.Space();

        EditorGUILayout.HelpBox(
            "！！！！！注意！！！！！\n" +
            "使用破坏性的合并时\n"+
            "注意备份文件\n" +
            "合并前请先完全解压对象\n" +
            "当骨骼中含有constrain类组件时请谨慎使用\n"+
            "当动骨都处于PB对象下时，请先对PB对象点击\"移动phybones到root\"，再点击衣服进行合并",
            MessageType.Warning
        );

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        GUIStyle highlightStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = Color.yellow },
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.LowerRight
        };

        GUILayout.Label("闲鱼@咩卡布w  禁止转载", highlightStyle, GUILayout.ExpandWidth(false));
        GUILayout.EndHorizontal();
    }

    // ============================================================
    // UI 方法 — 委托给 VRCPhysBoneAPI 静态方法
    // ============================================================

    private void MovePhysBonesToRoot()
    {
        if (Selection.activeGameObject == null)
        {
            EditorUtility.DisplayDialog("错误", "请先选中一个GameObject", "确定");
            return;
        }

        GameObject target = Selection.activeGameObject;
        string resultStatus;
        VRCPhysBoneAPI.MovePhysBonesToRoot(target, out resultStatus);
        VRCPhysBoneAPI.DeleteSourceObject(target);

        statusText = resultStatus + "，已删除源对象";
        Debug.Log(resultStatus);
        Repaint();
    }

    private void CountPhysBones()
    {
        physBoneCount = 0;

        if (countTargetObject == null) return;

        // 沿用 VRCPhysBoneCounter 的统计逻辑：统计目标对象及其所有子对象上的 VRCPhysBone 数量
        VRCPhysBone[] physBones = countTargetObject.GetComponentsInChildren<VRCPhysBone>(true);
        physBoneCount = physBones.Length;

        Repaint();
    }

    private void ProcessSelectedObjects()
    {
        var selected = Selection.gameObjects;
        if (selected == null || selected.Length == 0)
        {
            statusText = "错误: 没有选择任何对象！";
            Debug.LogWarning(statusText);
            return;
        }

        int totalProcGroups = 0;
        int totalProcObjects = 0;
        List<string> statusList = new List<string>();

        // 对每个选中的根对象独立处理，避免不同衣物被错误合并到同一个对象下
        foreach (GameObject rootObj in selected)
        {
            if (rootObj == null) continue;

            int procGroups, procObjects;
            string resultStatus;
            VRCPhysBoneAPI.MergePhysBones(rootObj, out procGroups, out procObjects, out resultStatus);

            totalProcGroups += procGroups;
            totalProcObjects += procObjects;
            if (procGroups > 0)
                statusList.Add($"{rootObj.name}: {procGroups}组({procObjects}对象)");
        }

        totalProcessed = totalProcGroups;
        totalMerged = totalProcObjects;

        if (totalProcGroups > 0)
        {
            statusText = $"合并完成: {totalProcGroups} 组（{totalProcObjects} 个对象）";
            Debug.Log($"<color=green>[PhysBone合并] {statusText}</color>");
            foreach (var s in statusList)
                Debug.Log($"  - {s}");
        }
        else
        {
            statusText = "未找到可合并的Phys Bone组";
            Debug.Log(statusText);
        }

        Repaint();
    }

    private void AttachOptimizerToSelection()
    {
        var selected = Selection.gameObjects;
        if (selected == null || selected.Length == 0)
        {
            EditorUtility.DisplayDialog("提示", "请先在Hierarchy中选中对象", "确定");
            return;
        }

        int count = 0;
        foreach (var go in selected)
        {
            if (go.GetComponent<VRCPhysBoneOptimizer>() == null)
            {
                Undo.AddComponent<VRCPhysBoneOptimizer>(go);
                count++;
            }
        }

        statusText = $"已挂载 PhysBone Optimizer 到 {count} 个对象";
        Debug.Log(statusText);
        Repaint();
    }

    private void RemoveOptimizerFromSelection()
    {
        var selected = Selection.gameObjects;
        if (selected == null || selected.Length == 0)
        {
            EditorUtility.DisplayDialog("提示", "请先在Hierarchy中选中对象", "确定");
            return;
        }

        int count = 0;
        foreach (var go in selected)
        {
            var comp = go.GetComponent<VRCPhysBoneOptimizer>();
            if (comp != null)
            {
                Undo.DestroyObjectImmediate(comp);
                count++;
            }
        }

        statusText = $"已清除 {count} 个对象上的 PhysBone Optimizer";
        Debug.Log(statusText);
        Repaint();
    }
}
#endif
