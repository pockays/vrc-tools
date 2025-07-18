using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class 查找模型对应的骨骼 : EditorWindow
{
    private List<SkinnedMeshRenderer> 选择的SkinnedMeshRenderers = new List<SkinnedMeshRenderer>();
    private Dictionary<Transform, float> 绑定的骨骼及权重 = new Dictionary<Transform, float>();
    private Dictionary<SkinnedMeshRenderer, List<int>> 丢失的骨骼索引 = new Dictionary<SkinnedMeshRenderer, List<int>>();
    private Vector2 滚动位置;
    private Vector2 丢失骨骼滚动位置;
    private int 组件数量 = 1;
    
    // 骨骼替换相关
    private Transform 待替换骨骼;
    private Transform 目标替换骨骼;
    private bool 显示丢失骨骼面板 = false;
    private SkinnedMeshRenderer 当前选中渲染器;
    private int 当前选中丢失骨骼索引 = -1;

    [MenuItem("奇师傅工具箱/工具/对象管理/查找模型对应的骨骼", false, 0)]
    public static void 打开工具()
    {
        GetWindow<查找模型对应的骨骼>("查找模型对应的骨骼");
    }

    private void OnGUI()
    {
        // 绘制标题
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("查找模型对应的骨骼", new GUIStyle(EditorStyles.boldLabel)
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
            EditorGUILayout.LabelField("选择要检查的模型", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // SkinnedMeshRenderer数量滑块
            组件数量 = EditorGUILayout.IntSlider(new GUIContent("数量"), 组件数量, 1, 20);
            
            // 更新列表大小
            while (选择的SkinnedMeshRenderers.Count < 组件数量)
                选择的SkinnedMeshRenderers.Add(null);
            while (选择的SkinnedMeshRenderers.Count > 组件数量)
                选择的SkinnedMeshRenderers.RemoveAt(选择的SkinnedMeshRenderers.Count - 1);

            EditorGUILayout.Space(10);

            // 模型选择区域
            for (int i = 0; i < 选择的SkinnedMeshRenderers.Count; i++)
            {
                选择的SkinnedMeshRenderers[i] = EditorGUILayout.ObjectField(
                    new GUIContent($"模型 {i + 1}"),
                    选择的SkinnedMeshRenderers[i],
                    typeof(SkinnedMeshRenderer),
                    true) as SkinnedMeshRenderer;
            }

            EditorGUILayout.Space(15);

            // 查找按钮
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("查找骨骼", GUILayout.Width(120), GUILayout.Height(30)))
            {
                查找绑定的骨骼();
                显示丢失骨骼面板 = 丢失的骨骼索引.Any(x => x.Value.Count > 0);
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);
        }
        EditorGUILayout.EndVertical();

        // 丢失骨骼警告
        if (丢失的骨骼索引.Any(x => x.Value.Count > 0))
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                GUIStyle warningStyle = new GUIStyle(EditorStyles.boldLabel);
                warningStyle.normal.textColor = new Color(1f, 0.7f, 0f);
                EditorGUILayout.LabelField("⚠ 检测到丢失的骨骼引用", warningStyle);

                显示丢失骨骼面板 = EditorGUILayout.Foldout(显示丢失骨骼面板, "查看详情");
                
                if (显示丢失骨骼面板)
                {
                    丢失骨骼滚动位置 = EditorGUILayout.BeginScrollView(丢失骨骼滚动位置);
                    {
                        foreach (var pair in 丢失的骨骼索引)
                        {
                            if (pair.Value.Count > 0)
                            {
                                EditorGUILayout.Space(5);
                                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                                {
                                    EditorGUI.BeginDisabledGroup(true);
                                    EditorGUILayout.ObjectField("模型", pair.Key, typeof(SkinnedMeshRenderer), true);
                                    EditorGUI.EndDisabledGroup();

                                    foreach (int index in pair.Value)
                                    {
                                        EditorGUILayout.BeginHorizontal();
                                        {
                                            EditorGUILayout.LabelField($"丢失的骨骼索引: {index}");
                                            if (GUILayout.Button("修复此骨骼", GUILayout.Width(100)))
                                            {
                                                当前选中渲染器 = pair.Key;
                                                当前选中丢失骨骼索引 = index;
                                                待替换骨骼 = null;
                                                目标替换骨骼 = null;
                                            }
                                        }
                                        EditorGUILayout.EndHorizontal();
                                    }
                                }
                                EditorGUILayout.EndVertical();
                            }
                        }
                    }
                    EditorGUILayout.EndScrollView();
                }
            }
            EditorGUILayout.EndVertical();
        }

        // 结果显示区域
        if (绑定的骨骼及权重.Count > 0)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("查找结果", EditorStyles.boldLabel);
                EditorGUILayout.Space(5);

                滚动位置 = EditorGUILayout.BeginScrollView(滚动位置);
                {
                    foreach (var pair in 绑定的骨骼及权重.OrderByDescending(x => x.Value))
                    {
                        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                        {
                            EditorGUI.BeginDisabledGroup(true);
                            EditorGUILayout.ObjectField(pair.Key, typeof(Transform), true);
                            EditorGUI.EndDisabledGroup();
                            EditorGUILayout.LabelField($"权重: {pair.Value:F3}", GUILayout.Width(80));
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                }
                EditorGUILayout.EndScrollView();
                EditorGUILayout.Space(5);
            }
            EditorGUILayout.EndVertical();
        }

        // 骨骼替换区域
        if (当前选中渲染器 != null && 当前选中丢失骨骼索引 != -1)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("修复丢失的骨骼", EditorStyles.boldLabel);
                EditorGUILayout.Space(5);

                EditorGUILayout.HelpBox($"正在修复 {当前选中渲染器.name} 的第 {当前选中丢失骨骼索引} 号骨骼", MessageType.Info);
                EditorGUILayout.Space(5);

                目标替换骨骼 = EditorGUILayout.ObjectField(
                    new GUIContent("选择替换骨骼", "选择用于替换丢失骨骼的新骨骼"),
                    目标替换骨骼, typeof(Transform), true) as Transform;

                EditorGUILayout.Space(10);

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("执行修复", GUILayout.Width(120), GUILayout.Height(30)))
                {
                    if (目标替换骨骼 != null)
                    {
                        修复丢失骨骼();
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("错误", "请选择要替换的目标骨骼！", "确定");
                    }
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(5);
            }
            EditorGUILayout.EndVertical();
        }
        // 普通骨骼替换区域
        else if (绑定的骨骼及权重.Count > 0)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("骨骼替换", EditorStyles.boldLabel);
                EditorGUILayout.Space(5);

                EditorGUILayout.HelpBox("选择要替换的骨骼和目标骨骼。替换操作会保持mesh形状不变。", MessageType.Info);
                EditorGUILayout.Space(5);

                待替换骨骼 = EditorGUILayout.ObjectField(
                    new GUIContent("待替换骨骼", "选择要被替换的原始骨骼"),
                    待替换骨骼, typeof(Transform), true) as Transform;

                目标替换骨骼 = EditorGUILayout.ObjectField(
                    new GUIContent("目标替换骨骼", "选择要替换成的目标骨骼"),
                    目标替换骨骼, typeof(Transform), true) as Transform;

                EditorGUILayout.Space(10);

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("执行替换", GUILayout.Width(120), GUILayout.Height(30)))
                {
                    if (待替换骨骼 != null && 目标替换骨骼 != null)
                    {
                        if (EditorUtility.DisplayDialog("确认",
                            "确定要替换选中的骨骼吗？此操作可以撤销。", "确定", "取消"))
                        {
                            替换骨骼();
                        }
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("错误", "请先选择待替换骨骼和目标替换骨骼！", "确定");
                    }
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(5);
            }
            EditorGUILayout.EndVertical();
        }
    }

    private void 查找绑定的骨骼()
    {
        绑定的骨骼及权重.Clear();
        丢失的骨骼索引.Clear();

        foreach (var renderer in 选择的SkinnedMeshRenderers)
        {
            if (renderer == null) continue;

            Mesh mesh = renderer.sharedMesh;
            if (mesh == null) continue;

            Transform[] bones = renderer.bones;
            BoneWeight[] boneWeights = mesh.boneWeights;

            // 检查丢失的骨骼
            List<int> missingBones = new List<int>();
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] == null)
                {
                    missingBones.Add(i);
                }
            }
            if (missingBones.Count > 0)
            {
                丢失的骨骼索引[renderer] = missingBones;
                Debug.Log($"在 {renderer.name} 中发现 {missingBones.Count} 个丢失的骨骼");
            }

            // 创建一个数组来存储每个骨骼的最大权重
            float[] maxBoneWeights = new float[bones.Length];

            // 遍历所有顶点的骨骼权重
            foreach (var bw in boneWeights)
            {
                if (bw.boneIndex0 >= 0 && bw.boneIndex0 < bones.Length && bones[bw.boneIndex0] != null)
                    maxBoneWeights[bw.boneIndex0] = Mathf.Max(maxBoneWeights[bw.boneIndex0], bw.weight0);
                if (bw.boneIndex1 >= 0 && bw.boneIndex1 < bones.Length && bones[bw.boneIndex1] != null)
                    maxBoneWeights[bw.boneIndex1] = Mathf.Max(maxBoneWeights[bw.boneIndex1], bw.weight1);
                if (bw.boneIndex2 >= 0 && bw.boneIndex2 < bones.Length && bones[bw.boneIndex2] != null)
                    maxBoneWeights[bw.boneIndex2] = Mathf.Max(maxBoneWeights[bw.boneIndex2], bw.weight2);
                if (bw.boneIndex3 >= 0 && bw.boneIndex3 < bones.Length && bones[bw.boneIndex3] != null)
                    maxBoneWeights[bw.boneIndex3] = Mathf.Max(maxBoneWeights[bw.boneIndex3], bw.weight3);
            }
            // 添加所有骨骼
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] != null)
                {
                    if (绑定的骨骼及权重.ContainsKey(bones[i]))
                    {
                        绑定的骨骼及权重[bones[i]] = Mathf.Max(绑定的骨骼及权重[bones[i]], maxBoneWeights[i]);
                    }
                    else
                    {
                        绑定的骨骼及权重.Add(bones[i], maxBoneWeights[i]);
                    }
                }
            }
        }

        if (绑定的骨骼及权重.Count == 0 && 丢失的骨骼索引.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "没有找到任何有效的绑定骨骼。请确保选择了正确的SkinnedMeshRenderer。", "确定");
        }
        else
        {
            Debug.Log($"找到 {绑定的骨骼及权重.Count} 个有效骨骼");
        }
    }

    private void 替换骨骼()
    {
        bool 已执行替换 = false;

        foreach (var renderer in 选择的SkinnedMeshRenderers)
        {
            if (renderer == null || renderer.sharedMesh == null) continue;

            // 检查待替换骨骼是否在当前SkinnedMeshRenderer中
            int boneIndex = System.Array.IndexOf(renderer.bones, 待替换骨骼);
            if (boneIndex == -1) continue;

            // 创建撤销记录
            Undo.RecordObject(renderer, "替换骨骼");
            Undo.RecordObject(renderer.sharedMesh, "更新Mesh绑定姿势");

            // 获取当前的bones数组和bindposes
            Transform[] bones = renderer.bones.ToArray();
            Matrix4x4[] bindPoses = renderer.sharedMesh.bindposes;

            // 获取原始骨骼的bindpose
            Matrix4x4 originalBindPose = bindPoses[boneIndex];

            // 计算新的bindpose
            // 新bindpose = 目标骨骼的世界到本地矩阵 * 原始骨骼的本地到世界矩阵 * 原始bindpose
            Matrix4x4 newBindPose = 目标替换骨骼.worldToLocalMatrix * 
                                  待替换骨骼.localToWorldMatrix * 
                                  originalBindPose;

            // 更新bones数组和bindposes
            bones[boneIndex] = 目标替换骨骼;
            bindPoses[boneIndex] = newBindPose;

            // 应用更改
            renderer.bones = bones;
            renderer.sharedMesh.bindposes = bindPoses;

            已执行替换 = true;
            Debug.Log($"在 {renderer.name} 中替换了索引为 {boneIndex} 的骨骼");
        }

        if (已执行替换)
        {
            EditorUtility.DisplayDialog("完成", "骨骼替换完成！", "确定");
            // 刷新查找结果
            查找绑定的骨骼();
        }
        else
        {
            EditorUtility.DisplayDialog("提示", "在选择的模型中未找到指定的待替换骨骼。", "确定");
        }
    }

    private void 修复丢失骨骼()
    {
        if (当前选中渲染器 == null || 当前选中丢失骨骼索引 < 0 || 目标替换骨骼 == null)
        {
            EditorUtility.DisplayDialog("错误", "无效的修复参数！", "确定");
            return;
        }

        // 创建撤销记录
        Undo.RecordObject(当前选中渲染器, "修复丢失骨骼");
        Undo.RecordObject(当前选中渲染器.sharedMesh, "更新Mesh绑定姿势");

        // 获取当前的bones数组和bindposes
        Transform[] bones = 当前选中渲染器.bones;
        Matrix4x4[] bindPoses = 当前选中渲染器.sharedMesh.bindposes;

        // 计算新的bindpose
        Matrix4x4 newBindPose = 目标替换骨骼.worldToLocalMatrix;

        // 更新bones数组和bindposes
        bones[当前选中丢失骨骼索引] = 目标替换骨骼;
        bindPoses[当前选中丢失骨骼索引] = newBindPose;

        // 应用更改
        当前选中渲染器.bones = bones;
        当前选中渲染器.sharedMesh.bindposes = bindPoses;

        Debug.Log($"修复了 {当前选中渲染器.name} 中索引为 {当前选中丢失骨骼索引} 的丢失骨骼");
        EditorUtility.DisplayDialog("完成", "丢失骨骼修复完成！", "确定");

        // 重置选择状态
        当前选中渲染器 = null;
        当前选中丢失骨骼索引 = -1;
        目标替换骨骼 = null;

        // 刷新查找结果
        查找绑定的骨骼();
    }
}
