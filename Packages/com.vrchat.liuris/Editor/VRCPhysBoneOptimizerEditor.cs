using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using System.Collections.Generic;

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
            "2. 对当前挂载对象执行PhysBone合并\n\n" +
            "可直接从Hierarchy拖拽对象到下方列表区域自动添加",
            MessageType.Info);

        EditorGUILayout.Space();

        // 记录列表区域用于拖拽检测
        Rect listRect = GUILayoutUtility.GetRect(0, sourcePBObjectsList.GetHeight(), GUILayout.ExpandWidth(true));
        sourcePBObjectsList.DoList(listRect);

        // 处理直接拖拽到列表区域
        HandleDragAndDrop(listRect);

        EditorGUILayout.Space();

        serializedObject.ApplyModifiedProperties();
    }

    private void HandleDragAndDrop(Rect dropArea)
    {
        Event evt = Event.current;
        if (!dropArea.Contains(evt.mousePosition))
            return;

        switch (evt.type)
        {
            case EventType.DragUpdated:
                if (HasValidDraggedObjects())
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    evt.Use();
                }
                break;

            case EventType.DragPerform:
                if (HasValidDraggedObjects())
                {
                    DragAndDrop.AcceptDrag();
                    AddDraggedObjectsToList();
                    evt.Use();
                }
                break;
        }
    }

    private bool HasValidDraggedObjects()
    {
        foreach (var obj in DragAndDrop.objectReferences)
        {
            if (obj is GameObject)
                return true;
        }
        return false;
    }

    private void AddDraggedObjectsToList()
    {
        var listProp = sourcePBObjectsList.serializedProperty;
        var existingSet = new HashSet<GameObject>();
        for (int i = 0; i < listProp.arraySize; i++)
        {
            var existing = listProp.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
            if (existing != null)
                existingSet.Add(existing);
        }

        foreach (var obj in DragAndDrop.objectReferences)
        {
            if (obj is GameObject go && !existingSet.Contains(go))
            {
                existingSet.Add(go);
                listProp.arraySize++;
                listProp.GetArrayElementAtIndex(listProp.arraySize - 1).objectReferenceValue = go;
            }
        }

        serializedObject.ApplyModifiedProperties();
    }
}
