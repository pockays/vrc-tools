using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(VRCPhysBoneOptimizer))]
public class VRCPhysBoneOptimizerEditor : Editor
{
    private ReorderableList sourcePBObjectsList;

    private void OnEnable()
    {
        var sourcePBObjectsProp = serializedObject.FindProperty("sourcePBObjects");
        sourcePBObjectsList = new ReorderableList(serializedObject, sourcePBObjectsProp, true, true, true, true);

        sourcePBObjectsList.drawHeaderCallback = (Rect rect) =>
        {
            EditorGUI.LabelField(rect, "源PB对象列表（迁移PhysBone到Root）");
        };

        sourcePBObjectsList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
        {
            var element = sourcePBObjectsList.serializedProperty.GetArrayElementAtIndex(index);
            rect.y += 2;
            rect.height = EditorGUIUtility.singleLineHeight;
            EditorGUI.PropertyField(rect, element, GUIContent.none);
        };

        sourcePBObjectsList.onAddCallback = (ReorderableList list) =>
        {
            list.serializedProperty.arraySize++;
            list.serializedProperty.GetArrayElementAtIndex(list.serializedProperty.arraySize - 1).objectReferenceValue = null;
        };

        sourcePBObjectsList.onRemoveCallback = (ReorderableList list) =>
        {
            if (list.index >= 0 && list.index < list.serializedProperty.arraySize)
            {
                list.serializedProperty.DeleteArrayElementAtIndex(list.index);
            }
        };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.HelpBox(
            "NDMF构建时自动执行：\n" +
            "1. 将上方列表中所有对象的PhysBone迁移到Root（完成后删除源对象）\n" +
            "2. 对当前挂载对象执行PhysBone合并",
            MessageType.Info);

        EditorGUILayout.Space();

        sourcePBObjectsList.DoLayoutList();

        EditorGUILayout.Space();

        serializedObject.ApplyModifiedProperties();
    }
}
