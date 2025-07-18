using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MaterialReplacer
{
    public class MaterialReplacerWindow : EditorWindow
    {
        private GameObject targetObject;
        private Object materialFolder;
        private string materialFolderPath;
        private Vector2 scrollPosition;
        private bool showPreview = false;
        private List<MaterialReplaceInfo> replacementInfos = new List<MaterialReplaceInfo>();
        private GUIStyle headerStyle;
        private GUIStyle subHeaderStyle;
        private GUIStyle infoBoxStyle;
        private Color defaultBackgroundColor;

        [MenuItem("Window/Material Replacer")]
        public static void ShowWindow()
        {
            MaterialReplacerWindow window = GetWindow<MaterialReplacerWindow>("材质替换工具");
            window.minSize = new Vector2(400, 300);
            window.Show();
        }

        private void OnEnable()
        {
            // 如果当前有选中的对象，自动填充目标对象
            if (Selection.activeGameObject != null)
            {
                targetObject = Selection.activeGameObject;
            }
        }

        private void InitializeStyles()
        {
            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(EditorStyles.boldLabel);
                headerStyle.fontSize = 14;
                headerStyle.margin = new RectOffset(5, 5, 10, 5);
            }

            if (subHeaderStyle == null)
            {
                subHeaderStyle = new GUIStyle(EditorStyles.boldLabel);
                subHeaderStyle.fontSize = 12;
                subHeaderStyle.margin = new RectOffset(5, 5, 5, 5);
            }

            if (infoBoxStyle == null)
            {
                infoBoxStyle = new GUIStyle(EditorStyles.helpBox);
                infoBoxStyle.padding = new RectOffset(10, 10, 10, 10);
                infoBoxStyle.margin = new RectOffset(5, 5, 5, 5);
            }

            defaultBackgroundColor = GUI.backgroundColor;
        }

        private void OnGUI()
        {
            InitializeStyles();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("材质替换工具", headerStyle);
            EditorGUILayout.HelpBox("此工具可以批量替换对象及其子对象上的材质。\n选择一个对象和材质文件夹，工具将自动替换同名材质。", MessageType.Info);
            EditorGUILayout.Space(10);

            EditorGUI.BeginChangeCheck();

            // 目标对象选择
            EditorGUILayout.LabelField("步骤 1: 选择目标对象", subHeaderStyle);
            targetObject = EditorGUILayout.ObjectField("目标对象", targetObject, typeof(GameObject), true) as GameObject;

            EditorGUILayout.Space(10);

            // 材质文件夹选择
            EditorGUILayout.LabelField("步骤 2: 选择材质文件夹", subHeaderStyle);
            materialFolder = EditorGUILayout.ObjectField("材质文件夹", materialFolder, typeof(Object), false);

            if (EditorGUI.EndChangeCheck())
            {
                showPreview = false;
                replacementInfos.Clear();
            }

            if (materialFolder != null)
            {
                materialFolderPath = AssetDatabase.GetAssetPath(materialFolder);
                if (!AssetDatabase.IsValidFolder(materialFolderPath))
                {
                    materialFolderPath = Path.GetDirectoryName(materialFolderPath);
                }
            }
            else
            {
                materialFolderPath = null;
            }

            EditorGUILayout.Space(10);

            // 预览按钮
            EditorGUILayout.LabelField("步骤 3: 预览替换结果", subHeaderStyle);
            GUI.enabled = targetObject != null && !string.IsNullOrEmpty(materialFolderPath);
            
            if (GUILayout.Button("预览替换结果"))
            {
                GeneratePreview();
                showPreview = true;
            }
            
            GUI.enabled = true;

            // 显示预览信息
            if (showPreview)
            {
                EditorGUILayout.Space(5);
                DisplayPreviewInfo();
            }

            EditorGUILayout.Space(10);

            // 替换按钮
            EditorGUILayout.LabelField("步骤 4: 执行替换", subHeaderStyle);
            GUI.enabled = showPreview && replacementInfos.Count > 0;
            
            GUI.backgroundColor = new Color(0.3f, 0.8f, 0.3f);
            if (GUILayout.Button("执行替换", GUILayout.Height(30)))
            {
                PerformReplacement();
            }
            GUI.backgroundColor = defaultBackgroundColor;
            
            GUI.enabled = true;
        }

        private void GeneratePreview()
        {
            if (targetObject == null || string.IsNullOrEmpty(materialFolderPath))
                return;

            replacementInfos.Clear();

            // 获取文件夹中的所有材质
            string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[] { materialFolderPath });
            Dictionary<string, Material> folderMaterials = new Dictionary<string, Material>();

            foreach (string guid in materialGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material != null)
                {
                    folderMaterials[material.name] = material;
                }
            }

            // 查找目标对象及其子对象上的所有渲染器
            Renderer[] renderers = targetObject.GetComponentsInChildren<Renderer>(true);

            foreach (Renderer renderer in renderers)
            {
                Material[] materials = renderer.sharedMaterials;

                for (int i = 0; i < materials.Length; i++)
                {
                    Material currentMaterial = materials[i];
                    if (currentMaterial == null)
                        continue;

                    if (folderMaterials.TryGetValue(currentMaterial.name, out Material replacementMaterial))
                    {
                        if (currentMaterial != replacementMaterial)
                        {
                            replacementInfos.Add(new MaterialReplaceInfo
                            {
                                Renderer = renderer,
                                MaterialIndex = i,
                                OriginalMaterial = currentMaterial,
                                ReplacementMaterial = replacementMaterial
                            });
                        }
                    }
                }
            }
        }

        private void DisplayPreviewInfo()
        {
            EditorGUILayout.BeginVertical(infoBoxStyle);

            if (replacementInfos.Count == 0)
            {
                EditorGUILayout.HelpBox("未找到可替换的材质。请确保文件夹中包含与对象上材质同名的材质。", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.LabelField($"找到 {replacementInfos.Count} 个可替换的材质:", EditorStyles.boldLabel);
                
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(150));
                
                foreach (var info in replacementInfos)
                {
                    EditorGUILayout.BeginHorizontal();
                    
                    EditorGUILayout.ObjectField(info.Renderer.gameObject, typeof(GameObject), true, GUILayout.Width(150));
                    EditorGUILayout.LabelField("材质槽 " + info.MaterialIndex, GUILayout.Width(70));
                    
                    EditorGUILayout.ObjectField(info.OriginalMaterial, typeof(Material), false, GUILayout.Width(100));
                    EditorGUILayout.LabelField("→", GUILayout.Width(20));
                    EditorGUILayout.ObjectField(info.ReplacementMaterial, typeof(Material), false, GUILayout.Width(100));
                    
                    EditorGUILayout.EndHorizontal();
                }
                
                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.EndVertical();
        }

        private void PerformReplacement()
        {
            if (replacementInfos.Count == 0)
                return;

            int replacementCount = replacementInfos.Count;
            
            // 按渲染器分组，以便一次性记录每个渲染器的撤销操作
            var rendererGroups = replacementInfos.GroupBy(info => info.Renderer);

            foreach (var group in rendererGroups)
            {
                Renderer renderer = group.Key;
                Material[] materials = renderer.sharedMaterials.ToArray(); // 创建副本
                bool changed = false;

                // 记录撤销操作
                Undo.RecordObject(renderer, "Replace Materials");

                foreach (var info in group)
                {
                    if (info.MaterialIndex < materials.Length)
                    {
                        materials[info.MaterialIndex] = info.ReplacementMaterial;
                        changed = true;
                    }
                }

                if (changed)
                {
                    renderer.sharedMaterials = materials;
                    EditorUtility.SetDirty(renderer);
                }
            }

            showPreview = false;
            replacementInfos.Clear();
            
            // 刷新场景视图
            SceneView.RepaintAll();
            
            EditorUtility.DisplayDialog("替换完成", $"已成功替换 {replacementCount} 个材质。", "确定");
        }
    }

    public class MaterialReplaceInfo
    {
        public Renderer Renderer;
        public int MaterialIndex;
        public Material OriginalMaterial;
        public Material ReplacementMaterial;
    }
}
