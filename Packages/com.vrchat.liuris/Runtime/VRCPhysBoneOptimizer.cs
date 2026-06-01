using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
using System.Collections.Generic;
#endif

[AddComponentMenu("咩卡布/PhysBone Optimizer")]
[DefaultExecutionOrder(-32000)]
public class VRCPhysBoneOptimizer : MonoBehaviour
{
    [Header("动骨优化")]
    [Tooltip("需要迁移PhysBone的源对象列表（可为空），将对每个对象执行\"迁移PhysBone到Root\"，完成后删除源对象")]
    public List<GameObject> sourcePBObjects = new List<GameObject>();

    [HideInInspector] public bool hasOptimized = false;

    void Awake()
    {
#if UNITY_EDITOR
        if (hasOptimized) return;

        // ============================================================
        // 第一步：对所有源对象执行"迁移PhysBone到Root"（只迁移，不删除）
        // ============================================================
        if (sourcePBObjects != null && sourcePBObjects.Count > 0)
        {
            foreach (var obj in sourcePBObjects)
            {
                if (obj != null)
                {
                    string status;
                    VRCPhysBoneAPI.MovePhysBonesToRoot(obj, out status);
                    Debug.Log($"[PhysBone优化-迁移] {obj.name}: {status}");
                }
            }
        }

        // ============================================================
        // 第二步：删除源对象（迁移完成后安全删除）
        // ============================================================
        if (sourcePBObjects != null && sourcePBObjects.Count > 0)
        {
            foreach (var obj in sourcePBObjects)
            {
                if (obj != null)
                {
                    VRCPhysBoneAPI.DeleteSourceObject(obj);
                    Debug.Log($"[PhysBone优化-删除] 已删除源对象: {obj.name}");
                }
            }
        }

        // ============================================================
        // 第三步：对挂载对象执行"合并PhysBones"
        // ============================================================
        int groups, objects;
        string mergeStatus;
        VRCPhysBoneAPI.MergePhysBones(gameObject, out groups, out objects, out mergeStatus);
        Debug.Log($"<color=green>[PhysBone优化-合并] {mergeStatus}</color>");

        hasOptimized = true;
#endif
    }
}

#if UNITY_EDITOR
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
            "运行后自动执行：\n" +
            "1. 将上方列表中所有对象的PhysBone迁移到Root（完成后删除源对象）\n" +
            "2. 对当前挂载对象执行PhysBone合并",
            MessageType.Info);

        EditorGUILayout.Space();

        sourcePBObjectsList.DoLayoutList();

        EditorGUILayout.Space();

        if (GUI.changed)
            EditorUtility.SetDirty((VRCPhysBoneOptimizer)target);

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
