#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public class 摄像头同步 : EditorWindow
{
    private static bool isSyncing = false;
    private static Camera targetCamera;
    private static float moveDistance = 0.01f; // 每次移动的距离
    private static Vector3 cameraOffset = Vector3.zero; // 相对场景视图的偏移量
    private static float fov = 60f; // 摄像机视角

    [MenuItem("奇师傅工具箱/工具/其他/摄像头同步", false, 0)]
    public static void ShowWindow()
    {
        var window = GetWindow<摄像头同步>("摄像头同步");
        window.minSize = new Vector2(300, 220);
        window.maxSize = new Vector2(300, 220);
    }

    private void OnEnable()
    {
        // 当窗口打开或重新获得焦点时，尝试自动寻找并引用场景中的第一个Camera
        if (!targetCamera)
        {
            FindAndSetFirstCamera();
        }
    }

    private void OnGUI()
    {
        targetCamera = (Camera)EditorGUILayout.ObjectField("指定Game摄像头", targetCamera, typeof(Camera), true);

        if (targetCamera == null)
        {
            EditorGUILayout.HelpBox("请选择摄像头", MessageType.Warning);
            FindAndSetFirstCamera(); // 如果没有选择摄像头，尝试找到并设置一个
        }

        // 控制栏
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(5);
        
        // 同步按钮
        if (GUILayout.Button(isSyncing ? "停止同步" : "开始同步", GUILayout.Height(25)))
        {
            isSyncing = !isSyncing;
            if (isSyncing)
            {
                EditorApplication.update += EditorUpdate;
            }
            else
            {
                EditorApplication.update -= EditorUpdate;
            }
        }

        GUILayout.Space(5);
        
        // 焦距控制
        EditorGUI.BeginChangeCheck();
        
        // 创建自定义样式
        var labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = 18;
        labelStyle.alignment = TextAnchor.MiddleLeft;

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("焦距", labelStyle, GUILayout.Width(50));
        GUILayout.Space(5);
        
        Rect sliderRect = GUILayoutUtility.GetRect(200, 25);
        sliderRect.y += 5;  // 直接调整滑条的垂直位置
        float sliderFov = GUI.HorizontalSlider(sliderRect, fov, 20f, 60f);
        EditorGUILayout.EndHorizontal();
        
        if (EditorGUI.EndChangeCheck())
        {
            fov = Mathf.Clamp(sliderFov, 20f, 60f);
            if (targetCamera != null)
            {
                targetCamera.fieldOfView = fov;
            }
        }

        // 前进后退按钮
        GUILayout.Space(5);
        EditorGUILayout.BeginHorizontal(GUILayout.Width(65));
        if (GUILayout.Button("退", GUILayout.Width(30), GUILayout.Height(25)))
        {
            if (targetCamera != null)
            {
                cameraOffset += Vector3.back * moveDistance;
                UpdateCameraPosition();
            }
        }
        GUILayout.Space(5);
        if (GUILayout.Button("进", GUILayout.Width(30), GUILayout.Height(25)))
        {
            if (targetCamera != null)
            {
                cameraOffset += Vector3.forward * moveDistance;
                UpdateCameraPosition();
            }
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("它可以在编辑器模式下，使指定的摄像头与场景视角的位置进行实时同步，方便控制Game摄像头位置，同时它也为摄像头的检查器窗口添加了一个同步按钮。", MessageType.Info);
    }

    private void UpdateCameraPosition()
    {
        if (targetCamera != null && SceneView.lastActiveSceneView != null)
        {
            Transform sceneCamera = SceneView.lastActiveSceneView.camera.transform;
            if (isSyncing)
            {
                // 同步状态下，相对于场景摄像头的位置
                targetCamera.transform.position = sceneCamera.position + sceneCamera.rotation * cameraOffset;
                targetCamera.transform.rotation = sceneCamera.rotation;
            }
            else
            {
                // 非同步状态下，相对于当前位置前进/后退
                targetCamera.transform.position += targetCamera.transform.forward * cameraOffset.z;
                cameraOffset = Vector3.zero; // 重置偏移量，避免累积
            }
            SceneView.RepaintAll();
        }
    }

    private void EditorUpdate()
    {
        if (isSyncing && targetCamera != null && SceneView.lastActiveSceneView != null)
        {
            UpdateCameraPosition();
        }
    }

    private void OnDisable()
    {
        // 清理事件订阅
        EditorApplication.update -= EditorUpdate;
    }

    private void FindAndSetFirstCamera()
    {
        Camera foundCamera = FindObjectOfType<Camera>();
        if (foundCamera != null)
        {
            targetCamera = foundCamera;
        }
    }
}
#endif
