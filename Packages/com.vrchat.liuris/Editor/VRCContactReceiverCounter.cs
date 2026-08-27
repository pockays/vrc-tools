#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public class VRCContactReceiverCounter : EditorWindow
{
    private Vector2 scrollPosition;
    private bool locked;
    private GameObject lockedTarget;
    private string searchFilter = "";

    [MenuItem("Tools/组件统计")]
    public static void ShowWindow()
    {
        GetWindow<VRCContactReceiverCounter>("组件统计");
    }

    private void OnGUI()
    {
        GUILayout.Label("组件统计", EditorStyles.boldLabel);
        GUILayout.Space(4);

        GameObject selected = locked ? lockedTarget : Selection.activeGameObject;

        EditorGUILayout.BeginHorizontal();
        locked = GUILayout.Toggle(locked, "锁定界面", "Button", GUILayout.Width(80));

        if (locked && lockedTarget == null)
        {
            lockedTarget = Selection.activeGameObject;
        }

        if (!locked)
        {
            lockedTarget = null;
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(4);

        if (selected == null)
        {
            EditorGUILayout.HelpBox("请在 Hierarchy 中选中一个 GameObject。", MessageType.Info);
            return;
        }

        GUILayout.Label($"当前对象: {selected.name}", EditorStyles.label);
        GUILayout.Space(8);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("搜索组件名:", GUILayout.Width(80));
        searchFilter = EditorGUILayout.TextField(searchFilter);
        if (GUILayout.Button("清除", GUILayout.Width(40)))
        {
            searchFilter = "";
            GUI.FocusControl(null);
        }
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(4);

        int childCount = selected.transform.childCount;
        if (childCount == 0)
        {
            EditorGUILayout.HelpBox("该对象没有子对象。", MessageType.Info);
            return;
        }

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        int total = 0;

        for (int i = 0; i < childCount; i++)
        {
            Transform child = selected.transform.GetChild(i);
            int count = CountComponents(child, searchFilter);
            total += count;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(child.name, GUILayout.Width(250));
            EditorGUILayout.LabelField(count.ToString(), GUILayout.Width(50));
            if (count > 0 && GUILayout.Button("定位", GUILayout.Width(40)))
            {
                Selection.activeGameObject = child.gameObject;
                EditorGUIUtility.PingObject(child.gameObject);
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();

        GUILayout.Space(8);
        GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(2));
        GUILayout.Space(4);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("总计:", EditorStyles.boldLabel, GUILayout.Width(250));
        GUILayout.Label(total.ToString(), EditorStyles.boldLabel, GUILayout.Width(50));
        EditorGUILayout.EndHorizontal();
    }

    private int CountComponents(Transform child, string filter)
    {
        // 未输入搜索时，默认不统计任何组件
        if (string.IsNullOrEmpty(filter))
        {
            return 0;
        }

        // 输入搜索时：只要组件名包含搜索字符就全部统计
        Component[] all = child.GetComponentsInChildren<Component>(includeInactive: true);
        int count = 0;
        foreach (Component component in all)
        {
            if (component != null && component.GetType().Name.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                count++;
            }
        }
        return count;
    }

    private void OnSelectionChange()
    {
        if (!locked)
        {
            Repaint();
        }
    }
}
#endif
