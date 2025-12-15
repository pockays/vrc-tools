using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;

public class MaterialManager : EditorWindow
{
    private Vector2 scrollPosition;
    private Dictionary<Material, List<GameObject>> materialMap;
    private List<Material> uniqueMaterials;

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
        GUILayout.Label($"选中的对象: {Selection.gameObjects.Length} 个", EditorStyles.boldLabel);
        
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
            
            foreach (Material material in uniqueMaterials)
            {
                DrawMaterialRow(material);
            }
            
            EditorGUILayout.EndVertical();
        }
        
        EditorGUILayout.EndScrollView();
    }

    private void DrawMaterialRow(Material material)
    {
        EditorGUILayout.BeginHorizontal("box", GUILayout.Height(65), GUILayout.ExpandWidth(true));
        
        // 原始材质信息和预览
        DrawMaterialWithPreview("原始材质", material, false);
        
        // 替换材质信息和预览
        DrawMaterialWithPreview("替换为", GetCurrentAppliedMaterial(material), true);
        
        // 选择按钮
        EditorGUILayout.BeginVertical(GUILayout.Width(50));
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("选择", GUILayout.Width(50), GUILayout.Height(30)))
        {
            SelectObjectsWithMaterial(material);
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();
    }

    private void DrawMaterialWithPreview(string label, Material material, bool isReplaceField)
    {
        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        
        EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel, GUILayout.Height(15));
        
        EditorGUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
        
        // 预览
        EditorGUILayout.BeginVertical(GUILayout.Width(45));
        GUILayout.Box(AssetPreview.GetAssetPreview(material) ?? Texture2D.whiteTexture, 
            GUILayout.Width(40), GUILayout.Height(40));
        EditorGUILayout.EndVertical();
        
        // 材质选择框
        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        
        if (isReplaceField)
        {
            Material currentMaterial = GetCurrentAppliedMaterial(FindOriginalMaterial(material));
            EditorGUI.BeginChangeCheck();
            Material newMaterial = (Material)EditorGUILayout.ObjectField(
                material, 
                typeof(Material), 
                false,
                GUILayout.ExpandWidth(true));
            
            if (EditorGUI.EndChangeCheck())
            {
                ReplaceMaterial(FindOriginalMaterial(material), newMaterial);
            }
        }
        else
        {
            EditorGUILayout.ObjectField(material, typeof(Material), false, GUILayout.ExpandWidth(true));
        }
        
        if (!isReplaceField && material != null)
        {
            GUILayout.Label($"使用对象: {materialMap[material].Count}", EditorStyles.miniLabel, GUILayout.Height(15));
        }
        
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private Material FindOriginalMaterial(Material currentMaterial)
    {
        if (currentMaterial == null) return null;
        
        if (uniqueMaterials.Contains(currentMaterial))
        {
            return currentMaterial;
        }
        
        foreach (var originalMaterial in uniqueMaterials)
        {
            if (GetCurrentAppliedMaterial(originalMaterial) == currentMaterial)
            {
                return originalMaterial;
            }
        }
        
        return currentMaterial;
    }

    private Material GetCurrentAppliedMaterial(Material originalMaterial)
    {
        if (originalMaterial == null || !materialMap.ContainsKey(originalMaterial) || materialMap[originalMaterial].Count == 0)
            return originalMaterial;

        GameObject firstObj = materialMap[originalMaterial][0];
        if (firstObj == null) return originalMaterial;

        Renderer renderer = firstObj.GetComponent<Renderer>();
        if (renderer != null && renderer.sharedMaterials.Length > 0)
        {
            return renderer.sharedMaterials[0];
        }

        return originalMaterial;
    }

    private void ReplaceMaterial(Material oldMaterial, Material newMaterial)
    {
        if (oldMaterial == null) return;
        
        var affectedObjects = new HashSet<UnityEngine.Object>();
        
        foreach (GameObject obj in materialMap[oldMaterial])
        {
            if (obj == null) continue;
            
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                affectedObjects.Add(obj);
                affectedObjects.Add(renderer);
                
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == oldMaterial)
                    {
                        materials[i] = newMaterial;
                    }
                }
                renderer.sharedMaterials = materials;
            }
        }

        Undo.RegisterCompleteObjectUndo(affectedObjects.ToArray(), 
            $"材质替换: {oldMaterial.name} -> {(newMaterial == null ? "空" : newMaterial.name)}");
    }

    private void SelectObjectsWithMaterial(Material material)
    {
        if (material == null || !materialMap.ContainsKey(material)) return;

        Selection.objects = materialMap[material].Where(obj => obj != null).ToArray();
    }
}