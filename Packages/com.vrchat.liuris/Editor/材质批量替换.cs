using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;

public class MaterialManager : EditorWindow
{
    private Vector2 scrollPosition;
    private Dictionary<Material, List<GameObject>> materialMap;
    private List<Material> uniqueMaterials;
    private Dictionary<Material, Material> replacementMap = new Dictionary<Material, Material>();

    // 撤回操作记录
    private class MaterialReplacement
    {
        public GameObject gameObject;
        public Material originalMaterial;
        public Material newMaterial;
        public int materialIndex;
    }

    private List<List<MaterialReplacement>> undoStack = new List<List<MaterialReplacement>>();
    private int maxUndoSteps = 10;

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
        RefreshMaterialList();
        Repaint();
    }

    private void RefreshMaterialList()
    {
        materialMap = new Dictionary<Material, List<GameObject>>();
        uniqueMaterials = new List<Material>();
        replacementMap.Clear();

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
        
        foreach (var material in uniqueMaterials)
        {
            replacementMap[material] = null;
        }
    }

    private void CollectMaterialsFromObject(GameObject obj)
    {
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer != null)
        {
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material == null) continue;

                if (!materialMap.ContainsKey(material))
                {
                    materialMap[material] = new List<GameObject>();
                    uniqueMaterials.Add(material);
                }
                
                if (!materialMap[material].Contains(obj))
                {
                    materialMap[material].Add(obj);
                }
            }
        }

        var skinnedRenderer = obj.GetComponent<SkinnedMeshRenderer>();
        if (skinnedRenderer != null)
        {
            foreach (Material material in skinnedRenderer.sharedMaterials)
            {
                if (material == null) continue;

                if (!materialMap.ContainsKey(material))
                {
                    materialMap[material] = new List<GameObject>();
                    uniqueMaterials.Add(material);
                }
                
                if (!materialMap[material].Contains(obj))
                {
                    materialMap[material].Add(obj);
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
        // 使用垂直布局确保所有内容都在滚动视图内
        EditorGUILayout.BeginVertical();
        
        GUILayout.Label($"选中的对象: {Selection.gameObjects.Length} 个", EditorStyles.boldLabel);
        
        DrawUndoControls();
        
        // 主滚动视图 - 包含所有内容
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandWidth(true));
        
        DrawMaterialManagementContent();
        
        EditorGUILayout.EndScrollView();
        
        EditorGUILayout.EndVertical();
    }

    private void DrawUndoControls()
    {
        EditorGUILayout.BeginHorizontal();
        
        GUILayout.Label("撤回操作:", EditorStyles.boldLabel);
        
        bool canUndo = undoStack.Count > 0;
        EditorGUI.BeginDisabledGroup(!canUndo);
        if (GUILayout.Button("撤回", GUILayout.Width(60)))
        {
            UndoLastOperation();
        }
        EditorGUI.EndDisabledGroup();

        if (GUILayout.Button("清空撤回记录", GUILayout.Width(100)))
        {
            ClearUndoStack();
        }

        GUILayout.Label($"可撤回步骤: {undoStack.Count}/{maxUndoSteps}", EditorStyles.miniLabel);
        
        EditorGUILayout.EndHorizontal();
        
        GUILayout.Space(10);
    }

    private void DrawMaterialManagementContent()
    {
        GUILayout.Label("材质球列表 - 每个材质可单独指定替换目标", EditorStyles.boldLabel);
        
        if (uniqueMaterials == null || uniqueMaterials.Count == 0)
        {
            EditorGUILayout.HelpBox("请选中一个或多个包含材质的对象", MessageType.Info);
            return;
        }

        // 材质列表区域 - 固定高度，确保可以滚动
        EditorGUILayout.BeginVertical(GUILayout.Height(400));
        foreach (Material material in uniqueMaterials)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();

            // 显示原始材质信息
            EditorGUILayout.BeginVertical(GUILayout.Width(250));
            EditorGUILayout.LabelField("原始材质", EditorStyles.miniBoldLabel);
            EditorGUILayout.ObjectField(material, typeof(Material), false);
            GUILayout.Label($"使用对象: {materialMap[material].Count}", EditorStyles.miniLabel);
            GUILayout.Label($"着色器: {material.shader.name}", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            // 预览材质球
            GUILayout.BeginVertical();
            EditorGUILayout.LabelField("预览", EditorStyles.miniBoldLabel, GUILayout.Width(50));
            GUILayout.Box(AssetPreview.GetAssetPreview(material), GUILayout.Width(50), GUILayout.Height(50));
            GUILayout.EndVertical();

            // 替换材质选择
            EditorGUILayout.BeginVertical(GUILayout.Width(250));
            EditorGUILayout.LabelField("替换为", EditorStyles.miniBoldLabel);
            replacementMap[material] = (Material)EditorGUILayout.ObjectField(
                replacementMap[material], 
                typeof(Material), 
                false);
            EditorGUILayout.EndVertical();

            // 操作按钮
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("操作", EditorStyles.miniBoldLabel, GUILayout.Width(40));
            if (GUILayout.Button("替换", GUILayout.Width(60)))
            {
                ReplaceSingleMaterial(material);
            }
            if (GUILayout.Button("选择", GUILayout.Width(60)))
            {
                SelectObjectsWithMaterial(material);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            
            GUILayout.Space(5);
        }
        EditorGUILayout.EndVertical();

        GUILayout.Space(10);

        // 批量操作区域 - 固定在底部
        DrawBatchOperations();
    }

    private void DrawBatchOperations()
    {
        GUILayout.Label("批量操作", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("批量替换所有已设置材质", GUILayout.Width(180)))
        {
            BatchReplaceAllSetMaterials();
        }
        if (GUILayout.Button("清除所有替换设置", GUILayout.Width(140)))
        {
            ClearAllReplacements();
        }
        if (GUILayout.Button("刷新列表", GUILayout.Width(80)))
        {
            RefreshMaterialList();
        }
        EditorGUILayout.EndHorizontal();

        int materialsToReplace = replacementMap.Values.Count(m => m != null);
        if (materialsToReplace > 0)
        {
            int totalObjects = 0;
            foreach (var kvp in replacementMap)
            {
                if (kvp.Value != null && materialMap.ContainsKey(kvp.Key))
                    totalObjects += materialMap[kvp.Key].Count;
            }
            
            EditorGUILayout.HelpBox(
                $"{materialsToReplace} 个材质已设置替换目标，将影响 {totalObjects} 个对象", 
                MessageType.Info);
        }
    }

    private void ReplaceSingleMaterial(Material oldMaterial)
    {
        if (oldMaterial == null || !replacementMap.ContainsKey(oldMaterial) || replacementMap[oldMaterial] == null)
        {
            EditorUtility.DisplayDialog("错误", "请先选择要替换的目标材质", "确定");
            return;
        }

        Material newMaterial = replacementMap[oldMaterial];
        int objectCount = materialMap[oldMaterial].Count;

        if (!EditorUtility.DisplayDialog("确认替换", 
            $"确定要将材质 '{oldMaterial.name}' 替换为 '{newMaterial.name}' 吗？\n影响对象: {objectCount}", 
            "确定", "取消"))
        {
            return;
        }

        List<MaterialReplacement> replacements = new List<MaterialReplacement>();
        int replaceCount = 0;

        foreach (GameObject obj in materialMap[oldMaterial])
        {
            bool replaced = ReplaceMaterialInObject(obj, oldMaterial, newMaterial, replacements);
            if (replaced) replaceCount++;
        }

        if (replaceCount > 0)
        {
            // 记录撤回操作
            AddToUndoStack(replacements);
            
            // 注册Unity的撤回操作
            Undo.RegisterCompleteObjectUndo(GetAffectedObjects(replacements), $"替换材质 {oldMaterial.name} -> {newMaterial.name}");
        }

        EditorUtility.DisplayDialog("完成", $"成功替换 {replaceCount} 个材质实例", "确定");
        RefreshMaterialList();
    }

    private void BatchReplaceAllSetMaterials()
    {
        var materialsToReplace = replacementMap.Where(kvp => kvp.Value != null).ToList();
        if (materialsToReplace.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "没有设置任何材质替换目标", "确定");
            return;
        }

        // 计算总影响
        int totalReplacements = 0;
        foreach (var kvp in materialsToReplace)
        {
            if (materialMap.ContainsKey(kvp.Key))
                totalReplacements += materialMap[kvp.Key].Count;
        }

        if (!EditorUtility.DisplayDialog("确认批量替换", 
            $"确定要批量替换 {materialsToReplace.Count} 个材质吗？\n影响对象: {totalReplacements}", 
            "确定", "取消"))
        {
            return;
        }

        List<MaterialReplacement> allReplacements = new List<MaterialReplacement>();
        int actualReplaceCount = 0;

        foreach (var kvp in materialsToReplace)
        {
            foreach (GameObject obj in materialMap[kvp.Key])
            {
                bool replaced = ReplaceMaterialInObject(obj, kvp.Key, kvp.Value, allReplacements);
                if (replaced) actualReplaceCount++;
            }
        }

        if (actualReplaceCount > 0)
        {
            // 记录撤回操作
            AddToUndoStack(allReplacements);
            
            // 注册Unity的撤回操作
            Undo.RegisterCompleteObjectUndo(GetAffectedObjects(allReplacements), "批量替换材质");
        }

        EditorUtility.DisplayDialog("完成", $"成功替换 {actualReplaceCount} 个材质实例", "确定");
        RefreshMaterialList();
    }

    private bool ReplaceMaterialInObject(GameObject obj, Material oldMaterial, Material newMaterial, List<MaterialReplacement> replacements = null)
    {
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material[] materials = renderer.sharedMaterials;
            bool replaced = false;
            
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == oldMaterial)
                {
                    // 记录替换信息用于撤回
                    if (replacements != null)
                    {
                        replacements.Add(new MaterialReplacement
                        {
                            gameObject = obj,
                            originalMaterial = oldMaterial,
                            newMaterial = newMaterial,
                            materialIndex = i
                        });
                    }

                    materials[i] = newMaterial;
                    replaced = true;
                }
            }
            
            if (replaced)
            {
                renderer.sharedMaterials = materials;
                return true;
            }
        }

        // 检查SkinnedMeshRenderer
        var skinnedRenderer = obj.GetComponent<SkinnedMeshRenderer>();
        if (skinnedRenderer != null)
        {
            Material[] materials = skinnedRenderer.sharedMaterials;
            bool replaced = false;
            
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == oldMaterial)
                {
                    // 记录替换信息用于撤回
                    if (replacements != null)
                    {
                        replacements.Add(new MaterialReplacement
                        {
                            gameObject = obj,
                            originalMaterial = oldMaterial,
                            newMaterial = newMaterial,
                            materialIndex = i
                        });
                    }

                    materials[i] = newMaterial;
                    replaced = true;
                }
            }
            
            if (replaced)
            {
                skinnedRenderer.sharedMaterials = materials;
                return true;
            }
        }

        return false;
    }

    private void AddToUndoStack(List<MaterialReplacement> replacements)
    {
        undoStack.Add(new List<MaterialReplacement>(replacements));
        
        // 限制撤回栈大小
        if (undoStack.Count > maxUndoSteps)
        {
            undoStack.RemoveAt(0);
        }
    }

    private void UndoLastOperation()
    {
        if (undoStack.Count == 0) return;

        var lastOperation = undoStack[undoStack.Count - 1];
        int undoCount = 0;

        foreach (var replacement in lastOperation)
        {
            if (replacement.gameObject != null)
            {
                Renderer renderer = replacement.gameObject.GetComponent<Renderer>();
                var skinnedRenderer = replacement.gameObject.GetComponent<SkinnedMeshRenderer>();

                if (renderer != null)
                {
                    Material[] materials = renderer.sharedMaterials;
                    if (replacement.materialIndex < materials.Length)
                    {
                        materials[replacement.materialIndex] = replacement.originalMaterial;
                        renderer.sharedMaterials = materials;
                        undoCount++;
                    }
                }
                else if (skinnedRenderer != null)
                {
                    Material[] materials = skinnedRenderer.sharedMaterials;
                    if (replacement.materialIndex < materials.Length)
                    {
                        materials[replacement.materialIndex] = replacement.originalMaterial;
                        skinnedRenderer.sharedMaterials = materials;
                        undoCount++;
                    }
                }
            }
        }

        undoStack.RemoveAt(undoStack.Count - 1);
        
        // 注册Unity的撤回操作的撤回（重做）
        Undo.RegisterCompleteObjectUndo(GetAffectedObjects(lastOperation), "撤回材质替换");

        Debug.Log($"已撤回 {undoCount} 个材质替换操作");
        RefreshMaterialList();
        Repaint();
    }

    private UnityEngine.Object[] GetAffectedObjects(List<MaterialReplacement> replacements)
    {
        var objects = new HashSet<UnityEngine.Object>();
        foreach (var replacement in replacements)
        {
            if (replacement.gameObject != null)
            {
                objects.Add(replacement.gameObject);
                var renderer = replacement.gameObject.GetComponent<Renderer>();
                if (renderer != null) objects.Add(renderer);
                
                var skinnedRenderer = replacement.gameObject.GetComponent<SkinnedMeshRenderer>();
                if (skinnedRenderer != null) objects.Add(skinnedRenderer);
            }
        }
        return objects.ToArray();
    }

    private void ClearUndoStack()
    {
        undoStack.Clear();
        Debug.Log("已清空撤回记录");
    }

    private void ClearAllReplacements()
    {
        foreach (var material in replacementMap.Keys.ToList())
        {
            replacementMap[material] = null;
        }
        Repaint();
    }

    private void SelectObjectsWithMaterial(Material material)
    {
        if (material == null || !materialMap.ContainsKey(material)) return;

        List<GameObject> objects = new List<GameObject>();
        foreach (var obj in materialMap[material])
        {
            if (obj != null)
                objects.Add(obj);
        }

        Selection.objects = objects.ToArray();
        EditorUtility.DisplayDialog("选择完成", $"已选中 {objects.Count} 个使用材质 '{material.name}' 的对象", "确定");
    }
}