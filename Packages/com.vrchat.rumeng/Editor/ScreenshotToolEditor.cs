#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.IO;
using System;

namespace Moyuer.Tools
{
    internal class ScreenshotToolEditor : EditorWindow
    {
        [MenuItem("Moyuer/截图工具", false, 3)]
        public static void ShowWindow_ScreenshotToolEditor()
        {
            GetWindow(typeof(ScreenshotToolEditor), false);
        }
        /* ============================================== API ============================================== */
        private Scene scene;
        private Camera camera;
        private readonly static string CAMERA_NAME = "ScreenshotCamera";
        protected SerializedObject serializedObject;

        private void OnEnable()
        {
            minSize = new Vector2(256, 512);
            maxSize = new Vector2(512, 2048);
            serializedObject = new SerializedObject(this);

            scene = EditorSceneManager.NewPreviewScene();
            var cameraObj = new GameObject(CAMERA_NAME);
            SceneManager.MoveGameObjectToScene(cameraObj, scene);
            camera = cameraObj.AddComponent<Camera>();
            camera.scene = scene;
            camera.nearClipPlane = 0.01f;
            camera.clearFlags = CameraClearFlags.Color;
            camera.backgroundColor = backgroundColor;
            camera.cullingMask = 1 << 1;
            ResetCamera();

            var light = cameraObj.AddComponent<Light>();
            light.type = LightType.Directional;

            ReloadSceneItems(scene, itemObjList);
        }

        private void OnDisable()
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }

        public void OnGUI()
        {
            if (Application.isPlaying) Close();
            UpdateGUI();
        }

        public void OnInspectorUpdate()
        {
            Repaint();
            UpdateCameraSetup();
        }

        /* ============================================== UI ============================================== */
        private Color backgroundColor = Color.clear;
        private Vector2Int _imageSize = new(512, 512);
        private Vector2Int imageSize = new(512, 512);
        private bool orthographicMode = false;
        private float cameraZoom = 1.0f;
        private bool setActive = true;
        private string imageName = "Screenshot";

        public List<GameObject> itemObjList = new();
        // private Vector2 mainScrollPos;
        private void UpdateGUI()
        {
            {
                float scale = Mathf.Min(position.width / imageSize.x, position.width / imageSize.y);
                var w = (int)Mathf.Round(imageSize.x * scale);
                var h = (int)Mathf.Round(imageSize.y * scale);
                var x = (position.width - w) / 2;
                EditorGUI.DrawTextureTransparent(new Rect(x, 0, w, h), camera.targetTexture);
                GUILayout.Space(Mathf.Min(position.width, h) + 5);
            }
            var labelStyle = new GUIStyle(GUI.skin.label)
            {
                stretchWidth = false,
                stretchHeight = false,
            };
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("背景颜色", labelStyle);
                camera.backgroundColor = EditorGUILayout.ColorField(camera.backgroundColor);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("像素尺寸", labelStyle);
                _imageSize = EditorGUILayout.Vector2IntField("", _imageSize, GUILayout.MinWidth(10));
                if (GUILayout.Button("应用"))
                {
                    imageSize = _imageSize;
                    ResetCamera();
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("正交视图", labelStyle);
                orthographicMode = EditorGUILayout.Toggle(orthographicMode);
                GUILayout.EndHorizontal();

                if (orthographicMode)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("缩放等级", labelStyle);
                    cameraZoom = EditorGUILayout.Slider(cameraZoom, 0.01f, 3.0f, GUILayout.MinWidth(10));
                    GUILayout.EndHorizontal();
                }

                EditorGUI.BeginChangeCheck();
                GUILayout.BeginHorizontal();
                GUILayout.Label("包含隐藏对象", labelStyle);
                setActive = EditorGUILayout.Toggle(setActive);
                GUILayout.EndHorizontal();
                if (EditorGUI.EndChangeCheck())
                {
                    ReloadSceneItems(scene, itemObjList, setActive);
                    camera.Render();
                }
            }

            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("itemObjList"), new GUIContent($"截图对象 ({itemObjList.Count})"));
            if (serializedObject.hasModifiedProperties)
            {
                serializedObject.ApplyModifiedProperties();
                ReloadSceneItems(scene, itemObjList, setActive);
                camera.Render();
            }

            GUILayout.FlexibleSpace();

            GUILayout.BeginHorizontal();
            GUILayout.Label("文件名称", labelStyle);
            imageName = EditorGUILayout.TextField(imageName, GUILayout.MinWidth(10));
            GUILayout.EndHorizontal();
            if (GUILayout.Button("保存图片", GUILayout.Height(30)))
                SaveScreenshot();
        }

        private void UpdateCameraSetup()
        {
            var sceneCam = SceneView.lastActiveSceneView.camera;
            camera.transform.SetPositionAndRotation(sceneCam.transform.position, sceneCam.transform.rotation);

            camera.orthographic = orthographicMode;
            camera.orthographicSize = cameraZoom;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 1000f;
        }

        private void ResetCamera()
        {
            camera.targetTexture = new RenderTexture(imageSize.x, imageSize.y, 16, RenderTextureFormat.ARGB32);
        }
        /* ============================================= PreviewScene ============================================= */

        private static void ReloadSceneItems(Scene scene, List<GameObject> itemObjList = null, bool setActive = false)
        {
            // Clear Scene Objects
            var objs = scene.GetRootGameObjects();
            for (var i = 0; i < objs.Length; i++)
            {
                if (objs[i].name.Equals(CAMERA_NAME)) continue;
                DestroyImmediate(objs[i]);
            }
            if (itemObjList == null) return;
            // Get Object Path
            List<string> itemPathList = new();
            foreach (var _obj in itemObjList)
                if (_obj != null) itemPathList.Add(VRC.Core.ExtensionMethods.GetHierarchyPath(_obj.transform));
            // Add Scene Objects
            var rootMap = new Dictionary<Transform, Transform>();
            var tranMap = new List<Transform>();
            foreach (var _obj in itemObjList)
            {
                if (_obj == null) continue;
                var _rootTran = _obj.transform.root;
                Transform rootTran;
                if (rootMap.ContainsKey(_rootTran))
                {
                    rootTran = rootMap[_rootTran];
                }
                else
                {
                    // Copy Root Object
                    var root = Instantiate(_rootTran.gameObject);
                    root.name = _rootTran.gameObject.name;
                    SceneManager.MoveGameObjectToScene(root, scene);
                    rootMap.Add(_rootTran, root.transform);
                    rootTran = root.transform;
                    // Set Layer
                    foreach (var obj in root.GetComponentsInChildren<Transform>())
                        obj.gameObject.layer = 0;
                }
                // Record Map
                if(_obj.transform == _rootTran)
                {
                    tranMap.Add(rootTran);
                }
                else
                {
                    var path = VRC.Core.ExtensionMethods.GetHierarchyPath(_obj.transform, _rootTran);
                    tranMap.Add(rootTran.Find(path));
                }
            }
            // Set Layer
            foreach (var tran in tranMap)
            {
                foreach (var _tran in tran.gameObject.GetComponentsInChildren<Transform>())
                {
                    _tran.gameObject.layer = 1;
                    if (setActive)
                    {
                        foreach (var com in _tran.gameObject.GetComponentsInChildren<Transform>())
                            com.gameObject.SetActive(true);
                        foreach (var com in _tran.gameObject.GetComponentsInChildren<Renderer>())
                            com.enabled = true;
                        foreach (var com in _tran.gameObject.GetComponentsInChildren<SkinnedMeshRenderer>())
                            com.enabled = true;
                        var parent = _tran;
                        while (parent != null)
                        {
                            parent.gameObject.SetActive(true);
                            parent = parent.parent;
                        }

                    }
                }
            }
        }

        /* ============================================== Screenshot ============================================== */

        private static readonly string IMAGE_SAVE_DIR = "Assets/Moyuer/Scripts/ScreenshotTool/Images/";

        public void SaveScreenshot()
        {
            if (!Directory.Exists(IMAGE_SAVE_DIR)) Directory.CreateDirectory(IMAGE_SAVE_DIR);
            var renderTexture = camera.targetTexture;
            var texture2D = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.ARGB32, true);
            RenderTexture.active = renderTexture;
            texture2D.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            texture2D.Apply();
            RenderTexture.active = null;

            var isPng = camera.backgroundColor.a < 1;

            var filePath = $"{IMAGE_SAVE_DIR}{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}_{imageName}.";
            filePath += isPng ? "png" : "jpg";
            var data = isPng ? texture2D.EncodeToPNG() : texture2D.EncodeToJPG();
            File.WriteAllBytes(filePath, data);
            AssetDatabase.Refresh();

            Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(filePath);
            EditorUtility.DisplayDialog("提示", $"截图已保存在：{filePath}", "确认");
        }
    }
}
#endif