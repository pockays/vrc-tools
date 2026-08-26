using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(VRCPhysBoneOptimizer))]
public class VRCPhysBoneOptimizerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox(
            "这是一个开关组件，挂载后 NDMF 构建时会自动执行：\n" +
            "1. 扫描整个 avatar 下所有 VRCPhysBone，将 rootTransform 非空的 PhysBone 迁移到其 root 骨骼\n" +
            "2. 清理迁移后产生的空壳对象\n" +
            "3. 对迁移后的 PhysBone 执行合并\n\n" +
            "无需再手动指定源对象。",
            MessageType.Info);
    }
}
