using UnityEngine;
using UnityEditor;

public class 对象批量命名 : EditorWindow
{
    string textToAddOrRemove = ""; // 文本框中的内容
    GameObject selectedObject; // 当前选中的游戏对象

    [MenuItem("奇师傅工具箱/工具/其他/对象批量命名", false, 0)]
    public static void ShowWindow()
    {
        EditorWindow.GetWindow(typeof(对象批量命名), false, "对象批量命名");
    }

    void OnGUI()
    {
        GUILayout.Label("对象批量命名", EditorStyles.boldLabel);
        selectedObject = EditorGUILayout.ObjectField("选择游戏对象", selectedObject, typeof(GameObject), true) as GameObject;

        textToAddOrRemove = EditorGUILayout.TextField("文本内容", textToAddOrRemove);

        if (GUILayout.Button("添加文本到子对象名称"))
        {
            AddTextToChildrenNames(selectedObject.transform);
        }

        if (GUILayout.Button("从子对象名称删除文本"))
        {
            RemoveTextFromChildrenNames(selectedObject.transform);
        }
    }

    void AddTextToChildrenNames(Transform root)
    {
        if (root != null)
        {
            foreach (Transform child in root)
            {
                child.name += textToAddOrRemove;
                EditorUtility.SetDirty(child); // 标记对象为“脏”，以便保存更改
                AddTextToChildrenNames(child); // 递归调用以处理子对象的子对象
            }
        }
        else
        {
            Debug.LogError("没有选中任何游戏对象！");
        }
    }

    void RemoveTextFromChildrenNames(Transform root)
    {
        if (root != null)
        {
            foreach (Transform child in root)
            {
                child.name = child.name.Replace(textToAddOrRemove, "");
                EditorUtility.SetDirty(child); // 标记对象为“脏”，以便保存更改
                RemoveTextFromChildrenNames(child); // 递归调用以处理子对象的子对象
            }
        }
        else
        {
            Debug.LogError("没有选中任何游戏对象！");
        }
    }
}
