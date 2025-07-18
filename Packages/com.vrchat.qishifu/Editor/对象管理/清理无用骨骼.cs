using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class 清理无用骨骼 : EditorWindow
{
    private GameObject 模型总体;
    private Transform 根骨骼;
    private HashSet<Transform> 有权重的骨骼 = new HashSet<Transform>();
    private List<Transform> 待清理的骨骼 = new List<Transform>();
    private Vector2 滚动位置;
    private bool 已预览 = false;
    private bool 包含未启用组件 = true; // 新增选项
    private bool 忽略组件检查 = false; // 是否忽略组件检查条件
    private bool 忽略物理骨骼 = false; // 是否忽略VRC Phys Bone组件
    private bool 忽略物理碰撞 = false; // 是否忽略VRC Phys Bone Collider组件

    [MenuItem("奇师傅工具箱/工具/对象管理/清理无用骨骼", false, 0)]
    public static void 打开工具()
    {
        GetWindow<清理无用骨骼>("清理无用骨骼");
    }

    private void OnGUI()
    {
        // 绘制标题
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("无用骨骼清理工具", new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 16,
            normal = { textColor = new Color(0.35f, 0.65f, 1f) }
        });
        EditorGUILayout.Space(10);

        // 主要设置区域
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("选择目标对象", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // 对象选择区域
            模型总体 = EditorGUILayout.ObjectField(
                new GUIContent("模型总体", "包含所有SkinnedMeshRenderer的父级对象"),
                模型总体, typeof(GameObject), true) as GameObject;

            根骨骼 = EditorGUILayout.ObjectField(
                new GUIContent("根骨骼", "需要清理的骨骼层级的根节点"),
                根骨骼, typeof(Transform), true) as Transform;

            EditorGUILayout.Space(5);

            // 检查选项
            包含未启用组件 = EditorGUILayout.Toggle(
                new GUIContent("检查未打开的Mesh模型", "是否检查被禁用的SkinnedMeshRenderer组件"),
                包含未启用组件);
                
            忽略组件检查 = EditorGUILayout.Toggle(
                new GUIContent("不删带组件的骨骼", "是否忽略骨骼上的其他组件"),
                忽略组件检查);

            EditorGUILayout.Space(5);

            using (new EditorGUI.DisabledGroupScope(忽略组件检查))
            {
                忽略物理骨骼 = EditorGUILayout.Toggle(
                    new GUIContent("不删动骨", "是否忽略VRC Phys Bone组件"),
                    忽略物理骨骼);

                忽略物理碰撞 = EditorGUILayout.Toggle(
                    new GUIContent("不删动骨碰撞体", "是否忽略VRC Phys Bone Collider组件"),
                    忽略物理碰撞);
            }

            EditorGUILayout.Space(15);

            // 检查按钮
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("预览清理", GUILayout.Width(120), GUILayout.Height(30)))
            {
                预览清理();
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);
        }
        EditorGUILayout.EndVertical();

        // 结果显示区域
        if (已预览 && 待清理的骨骼.Count > 0)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField($"找到 {待清理的骨骼.Count} 个可清理的骨骼", EditorStyles.boldLabel);
                EditorGUILayout.Space(5);

                滚动位置 = EditorGUILayout.BeginScrollView(滚动位置);
                {
                    foreach (var bone in 待清理的骨骼)
                    {
                        if (bone != null)  // 防止已删除的对象
                        {
                            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                            {
                                EditorGUI.BeginDisabledGroup(true);
                                EditorGUILayout.ObjectField(bone, typeof(Transform), true);
                                EditorGUI.EndDisabledGroup();
                            }
                            EditorGUILayout.EndHorizontal();
                        }
                    }
                }
                EditorGUILayout.EndScrollView();

                EditorGUILayout.Space(10);

                // 执行清理按钮
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("执行清理", GUILayout.Width(120), GUILayout.Height(30)))
                {
                    执行清理();
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(5);
            }
            EditorGUILayout.EndVertical();
        }
        else if (已预览)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox("没有找到需要清理的骨骼。", MessageType.Info);
        }
    }

    private void 预览清理()
    {
        if (模型总体 == null || 根骨骼 == null)
        {
            EditorUtility.DisplayDialog("错误", "请先选择模型总体和根骨骼!", "确定");
            return;
        }

        // 重置数据
        有权重的骨骼.Clear();
        待清理的骨骼.Clear();

        // 收集所有有权重的骨骼
        收集有权重骨骼();

        // 查找需要清理的骨骼
        查找待清理骨骼(根骨骼);

        已预览 = true;
        Repaint();
    }

    private void 收集有权重骨骼()
    {
        // 获取所有SkinnedMeshRenderer,包括未启用的
        var renderers = 模型总体.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        
        // 显示找到的组件数量
        int 总组件数 = renderers.Length;
        int 未启用组件数 = renderers.Count(r => !r.gameObject.activeInHierarchy || !r.enabled);
        
        if (总组件数 > 0)
        {
            Debug.Log($"找到 {总组件数} 个SkinnedMeshRenderer组件,其中 {未启用组件数} 个未启用");
        }

        foreach (var renderer in renderers)
        {
            // 如果组件未启用且不包含未启用组件,则跳过
            if (!包含未启用组件 && (!renderer.gameObject.activeInHierarchy || !renderer.enabled))
            {
                continue;
            }

            if (renderer.sharedMesh == null) continue;

            // 获取所有bones和权重
            var bones = renderer.bones;
            var weights = renderer.sharedMesh.boneWeights;

            // 创建一个数组来存储每个骨骼的最大权重
            float[] maxBoneWeights = new float[bones.Length];

            // 遍历所有顶点的骨骼权重
            foreach (var bw in weights)
            {
                if (bw.boneIndex0 >= 0 && bw.boneIndex0 < bones.Length)
                    maxBoneWeights[bw.boneIndex0] = Mathf.Max(maxBoneWeights[bw.boneIndex0], bw.weight0);
                if (bw.boneIndex1 >= 0 && bw.boneIndex1 < bones.Length)
                    maxBoneWeights[bw.boneIndex1] = Mathf.Max(maxBoneWeights[bw.boneIndex1], bw.weight1);
                if (bw.boneIndex2 >= 0 && bw.boneIndex2 < bones.Length)
                    maxBoneWeights[bw.boneIndex2] = Mathf.Max(maxBoneWeights[bw.boneIndex2], bw.weight2);
                if (bw.boneIndex3 >= 0 && bw.boneIndex3 < bones.Length)
                    maxBoneWeights[bw.boneIndex3] = Mathf.Max(maxBoneWeights[bw.boneIndex3], bw.weight3);
            }

            // 添加有权重的骨骼
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] != null && maxBoneWeights[i] > 0)
                {
                    有权重的骨骼.Add(bones[i]);
                }
            }
        }

        if (有权重的骨骼.Count > 0)
        {
            Debug.Log($"找到 {有权重的骨骼.Count} 个有权重的骨骼");
        }
    }

    private void 查找待清理骨骼(Transform current)
    {
        // 检查所有子对象
        for (int i = 0; i < current.childCount; i++)
        {
            var child = current.GetChild(i);
            查找待清理骨骼(child);
        }

        // 检查当前对象是否满足清理条件
        if (是可清理骨骼(current))
        {
            待清理的骨骼.Add(current);
        }
    }

    private bool 是可清理骨骼(Transform bone)
    {
        // 条件1:不在有权重的骨骼列表中
        bool 无权重 = !有权重的骨骼.Contains(bone);

        // 条件2:是叶子节点(没有子对象)
        bool 是叶子节点 = bone.childCount == 0;

        // 条件3:检查组件
        bool 无其他组件 = true;
        
        if (!忽略组件检查)
        {
            var components = bone.GetComponents<Component>();
            foreach (var component in components)
            {
                if (component == null) continue;
                
                string componentTypeName = component.GetType().Name;
                
                // 跳过Transform组件
                if (component is Transform) continue;
                
                // 检查是否是VRC Phys Bone
                if (componentTypeName == "VRCPhysBone" && 忽略物理骨骼)
                    continue;
                
                // 检查是否是VRC Phys Bone Collider
                if (componentTypeName == "VRCPhysBoneCollider" && 忽略物理碰撞)
                    continue;
                
                // 如果有其他组件,设置为false
                无其他组件 = false;
                break;
            }
        }

        return 无权重 && 是叶子节点 && 无其他组件;
    }

    private void 执行清理()
    {
        if (待清理的骨骼.Count == 0) return;

        // 创建撤销记录
        Undo.RecordObjects(待清理的骨骼.ToArray(), "清理无用骨骼");

        // 删除骨骼
        foreach (var bone in 待清理的骨骼.ToArray())
        {
            if (bone != null)
            {
                Undo.DestroyObjectImmediate(bone.gameObject);
            }
        }

        // 清空列表
        待清理的骨骼.Clear();
        已预览 = false;

        EditorUtility.DisplayDialog("完成", "无用骨骼清理完成!", "确定");
        Repaint();
    }
}
