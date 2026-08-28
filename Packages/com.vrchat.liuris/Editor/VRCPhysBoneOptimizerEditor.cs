using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(VRCPhysBoneOptimizer))]
public class VRCPhysBoneOptimizerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox(
            "这是一个作用域组件，挂载后 NDMF 构建时只处理【本对象自己的子树】：\n" +
            "1. 将子树下 rootTransform 非空的 VRCPhysBone 迁移到其 root 骨骼\n" +
            "2. 清理迁移后产生的空壳对象\n" +
            "3. 对子树内同父级同配置的 PhysBone 执行合并\n\n" +
            "不同挂载对象之间互不影响。",
            MessageType.Info);
    }
}
