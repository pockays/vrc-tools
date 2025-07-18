using UnityEngine;
using UnityEditor;
using VRC.SDK3.Dynamics.PhysBone.Components;

public class VRCPhysBoneCounter : EditorWindow
{
    private GameObject targetObject;
    private int physBoneCount = 0;

    [MenuItem("Tools/VRC Phys Bone Counter")]
    public static void ShowWindow()
    {
        GetWindow<VRCPhysBoneCounter>("VRC Phys Bone 数量统计");
    }

    private void OnGUI()
    {
        GUILayout.Label("VRC Phys Bone 数量统计", EditorStyles.boldLabel);
        GUILayout.Space(10);

        // 目标对象选择
        EditorGUI.BeginChangeCheck();
        targetObject = (GameObject)EditorGUILayout.ObjectField("目标对象", targetObject, typeof(GameObject), true);
        if (EditorGUI.EndChangeCheck())
        {
            CountPhysBones();
        }

        // 刷新按钮
        if (GUILayout.Button("刷新统计", GUILayout.Height(30)))
        {
            CountPhysBones();
        }

        GUILayout.Space(10);

        // 统计结果显示
        if (targetObject != null)
        {
            GUILayout.Label($"分析对象: {targetObject.name}", EditorStyles.boldLabel);
            
            // 显示PhysBones数量（使用大字体和绿色强调）
            GUILayout.Label($"VRC Phys Bones 数量: {physBoneCount}", 
                new GUIStyle(EditorStyles.label) { fontSize = 24, normal = { textColor = Color.green } });
        }
        else
        {
            GUILayout.Label("请从场景中拖入一个游戏对象", EditorStyles.helpBox);
        }
    }

    private void CountPhysBones()
    {
        physBoneCount = 0;
        
        if (targetObject == null) return;
        
        // 查找所有子对象中的VRCPhysBone组件
        VRCPhysBone[] physBones = targetObject.GetComponentsInChildren<VRCPhysBone>(true);
        physBoneCount = physBones.Length;
        
        Repaint();
    }
}    