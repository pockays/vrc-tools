using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;

public class MaterialManager : EditorWindow
{
    private Vector2 scrollPosition;
    private Dictionary<Material, List<MaterialInfo>> materialMap;
    private List<Material> uniqueMaterials;
    
    // 添加锁定状态变量
    private bool isLocked = false;
    private string lockButtonText = "锁定";
    
    [MenuItem("Tools/材质管理器")]
    public static void ShowWindow()
    {
        GetWindow<MaterialManager>("材质管理器");
    }

    private void OnEnable()
    {
        RefreshMaterialList();
    }

    private void OnSelectionChange()
    {
        // 如果窗口被锁定，不刷新材质列表
        if (!isLocked)
        {
            RefreshMaterialList();
            Repaint();
        }
    }

    private void RefreshMaterialList()
    {
        materialMap = new Dictionary<Material, List<MaterialInfo>>();
        uniqueMaterials = new List<Material>();

        GameObject[] selectedObjects = Selection.gameObjects;
        if (selectedObjects == null || selectedObjects.Length == 0) return;

        foreach (GameObject selectedObject in selectedObjects)
        {
            List<GameObject> allObjects = new List<GameObject>();
            GetAllChildren(selectedObject.transform, allObjects);
            allObjects.Add(selectedObject);

            foreach (GameObject obj in allObjects)
            {
                CollectMaterialsFromObject(obj);
            }
        }

        uniqueMaterials = uniqueMaterials.OrderBy(m => m.name).ToList();
    }

    private void CollectMaterialsFromObject(GameObject obj)
    {
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer != null && renderer.sharedMaterials != null)
        {
            for (int i = 0; i < renderer.sharedMaterials.Length; i++)
            {
                Material material = renderer.sharedMaterials[i];
                if (material == null) continue;

                // 对于每个材质，我们都将其视为一个原始材质
                if (!materialMap.ContainsKey(material))
                {
                    materialMap[material] = new List<MaterialInfo>();
                    uniqueMaterials.Add(material);
                }
                
                MaterialInfo info = new MaterialInfo
                {
                    gameObject = obj,
                    renderer = renderer,
                    materialIndex = i,
                    originalMaterial = material // 记录原始材质
                };
                
                if (!materialMap[material].Exists(m => m.gameObject == obj && m.materialIndex == i))
                {
                    materialMap[material].Add(info);
                }
            }
        }
    }

    private void GetAllChildren(Transform parent, List<GameObject> children)
    {
        foreach (Transform child in parent)
        {
            children.Add(child.gameObject);
            GetAllChildren(child, children);
        }
    }

    private void OnGUI()
    {
        // 添加锁定按钮
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        {
            GUILayout.FlexibleSpace();
            
            // 显示当前锁定状态
            string statusText = isLocked ? "已锁定 (选择对象不会更新列表)" : "未锁定";
            EditorGUILayout.LabelField(statusText, EditorStyles.miniLabel, GUILayout.Width(150));
            
            // 更新按钮文本
            lockButtonText = isLocked ? "解锁" : "锁定";
            
            // 锁定/解锁按钮
            if (GUILayout.Button(lockButtonText, EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                isLocked = !isLocked;
                
                // 如果解锁了，可以立即刷新一次列表
                if (!isLocked)
                {
                    RefreshMaterialList();
                }
            }
            
            // 添加手动刷新按钮
            if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                RefreshMaterialList();
            }
        }
        EditorGUILayout.EndHorizontal();
        
        // 显示锁定状态提示
        if (isLocked)
        {
            EditorGUILayout.HelpBox("列表已锁定。选择新的对象不会更新此列表。点击'解锁'以恢复自动更新。", 
                MessageType.Warning);
        }
        
        GUILayout.Label($"选中的对象: {Selection.gameObjects.Length} 个", EditorStyles.boldLabel);
        
        // 显示当前显示的材质数量
        if (uniqueMaterials != null)
        {
            GUILayout.Label($"显示的材质: {uniqueMaterials.Count} 个", EditorStyles.miniBoldLabel);
        }
        
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, 
            GUILayout.ExpandWidth(true), 
            GUILayout.ExpandHeight(true));
        
        if (uniqueMaterials == null || uniqueMaterials.Count == 0)
        {
            EditorGUILayout.HelpBox("请选中一个或多个包含材质的对象", MessageType.Info);
        }
        else
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            
            foreach (Material originalMaterial in uniqueMaterials)
            {
                DrawMaterialRow(originalMaterial);
            }
            
            EditorGUILayout.EndVertical();
        }
        
        EditorGUILayout.EndScrollView();
    }

    private void DrawMaterialRow(Material originalMaterial)
    {
        EditorGUILayout.BeginHorizontal("box", GUILayout.Height(65), GUILayout.ExpandWidth(true));
        
        // 原始材质信息和预览 - 固定显示
        DrawMaterialWithPreview("原始材质", originalMaterial, false, originalMaterial);
        
        // 替换材质信息和预览 - 实时显示当前实际材质
        Material currentAppliedMaterial = GetCurrentAppliedMaterial(originalMaterial);
        DrawMaterialWithPreview("替换为", currentAppliedMaterial, true, originalMaterial);
        
        // 选择按钮
        EditorGUILayout.BeginVertical(GUILayout.Width(50));
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("选择", GUILayout.Width(50), GUILayout.Height(30)))
        {
            SelectObjectsWithMaterial(originalMaterial);
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();
    }

    private Material GetCurrentAppliedMaterial(Material originalMaterial)
    {
        if (originalMaterial == null || !materialMap.ContainsKey(originalMaterial) || materialMap[originalMaterial].Count == 0)
            return originalMaterial;

        // 从第一个对象获取当前实际应用的材质
        MaterialInfo firstInfo = materialMap[originalMaterial][0];
        if (firstInfo.gameObject == null || firstInfo.renderer == null) 
            return originalMaterial;

        if (firstInfo.materialIndex < firstInfo.renderer.sharedMaterials.Length)
        {
            return firstInfo.renderer.sharedMaterials[firstInfo.materialIndex];
        }

        return originalMaterial;
    }

    private void DrawMaterialWithPreview(string label, Material material, bool isReplaceField, Material originalMaterial)
    {
        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        
        EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel, GUILayout.Height(15));
        
        EditorGUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
        
        // 预览
        EditorGUILayout.BeginVertical(GUILayout.Width(45));
        if (material != null)
        {
            GUILayout.Box(AssetPreview.GetAssetPreview(material) ?? Texture2D.whiteTexture, 
                GUILayout.Width(40), GUILayout.Height(40));
        }
        else
        {
            GUILayout.Box(Texture2D.whiteTexture, 
                GUILayout.Width(40), GUILayout.Height(40));
        }
        EditorGUILayout.EndVertical();
        
        // 材质选择框
        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        
        if (isReplaceField && originalMaterial != null)
        {
            // 实时获取当前材质
            Material currentMaterial = GetCurrentAppliedMaterial(originalMaterial);
            
            EditorGUI.BeginChangeCheck();
            Material newMaterial = (Material)EditorGUILayout.ObjectField(
                currentMaterial, 
                typeof(Material), 
                false,
                GUILayout.ExpandWidth(true));
            
            if (EditorGUI.EndChangeCheck() && newMaterial != currentMaterial)
            {
                // 执行替换
                ReplaceMaterial(originalMaterial, newMaterial);
                
                // 立即重绘以显示最新状态
                Repaint();
            }
        }
        else
        {
            // 原始材质字段 - 固定显示
            EditorGUILayout.ObjectField(originalMaterial, typeof(Material), false, GUILayout.ExpandWidth(true));
        }
        
        if (!isReplaceField && originalMaterial != null && materialMap.ContainsKey(originalMaterial))
        {
            GUILayout.Label($"使用对象: {materialMap[originalMaterial].Count}", EditorStyles.miniLabel, GUILayout.Height(15));
        }
        
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private void ReplaceMaterial(Material oldMaterial, Material newMaterial)
    {
        if (oldMaterial == null || !materialMap.ContainsKey(oldMaterial)) return;
        
        // 收集所有需要修改的Renderer
        List<Renderer> affectedRenderers = new List<Renderer>();
        List<MaterialInfo> infosToReplace = new List<MaterialInfo>();
        
        foreach (MaterialInfo info in materialMap[oldMaterial])
        {
            if (info.gameObject == null || info.renderer == null) continue;
            
            // 获取唯一的Renderer
            if (!affectedRenderers.Contains(info.renderer))
            {
                affectedRenderers.Add(info.renderer);
            }
            
            infosToReplace.Add(info);
        }
        
        if (affectedRenderers.Count == 0) return;
        
        // 注册撤销操作
        Undo.RecordObjects(affectedRenderers.ToArray(), 
            $"材质替换: {oldMaterial.name} -> {(newMaterial == null ? "空" : newMaterial.name)}");
        
        // 执行替换
        foreach (MaterialInfo info in infosToReplace)
        {
            if (info.renderer != null)
            {
                Material[] materials = info.renderer.sharedMaterials;
                if (info.materialIndex < materials.Length)
                {
                    materials[info.materialIndex] = newMaterial;
                }
                info.renderer.sharedMaterials = materials;
                
                // 标记为已修改
                EditorUtility.SetDirty(info.renderer);
                
                // 如果是预制体实例，记录修改
                if (PrefabUtility.IsPartOfAnyPrefab(info.renderer))
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(info.renderer);
                }
            }
        }
        
        // 不刷新列表，只重绘当前行
        // 这样可以保持列表顺序不变
    }

    private void SelectObjectsWithMaterial(Material material)
    {
        if (material == null || !materialMap.ContainsKey(material)) return;

        List<GameObject> validObjects = new List<GameObject>();
        foreach (MaterialInfo info in materialMap[material])
        {
            if (info.gameObject != null && !validObjects.Contains(info.gameObject))
            {
                validObjects.Add(info.gameObject);
            }
        }
        
        Selection.objects = validObjects.ToArray();
    }

    private class MaterialInfo
    {
        public GameObject gameObject;
        public Renderer renderer;
        public int materialIndex;
        public Material originalMaterial; // 原始材质
    }
}