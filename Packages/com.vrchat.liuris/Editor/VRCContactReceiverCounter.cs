#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using VRC.SDK3.Dynamics.Contact.Components;

public class VRCContactReceiverCounter : EditorWindow
{
    private Vector2 scrollPosition;
    private bool locked;
    private GameObject lockedTarget;

    [MenuItem("Tools/VRC Contact Receiver Counter")]
    public static void ShowWindow()
    {
        GetWindow<VRCContactReceiverCounter>("VRC Contact Receiver Counter");
    }

    private void OnGUI()
    {
        GUILayout.Label("VRC Contact 组件统计", EditorStyles.boldLabel);
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
            int count = child.GetComponentsInChildren<VRCContactReceiver>(includeInactive: true).Length
                      + child.GetComponentsInChildren<VRCContactSender>(includeInactive: true).Length;
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

    private void OnSelectionChange()
    {
        if (!locked)
        {
            Repaint();
        }
    }
}
#endif
