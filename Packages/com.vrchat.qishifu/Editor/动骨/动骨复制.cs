using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class 动骨复制 : EditorWindow
{
    private GameObject sourceCharacter;
    private GameObject targetCharacter;
    private bool includeColliders = true;
    private bool includePhysBones = true;
    private Dictionary<Component, Component> colliderMapping = new Dictionary<Component, Component>();

    [MenuItem("奇师傅工具箱/工具/动骨/动骨复制", false, 0)]
    public static void ShowWindow()
    {
        GetWindow<动骨复制>("动骨复制");
    }

    private void OnGUI()
    {
        GUILayout.Label("动骨复制", EditorStyles.boldLabel);

        sourceCharacter = EditorGUILayout.ObjectField("源角色(1号)", sourceCharacter, typeof(GameObject), true) as GameObject;
        targetCharacter = EditorGUILayout.ObjectField("目标角色(2号)", targetCharacter, typeof(GameObject), true) as GameObject;

        includePhysBones = EditorGUILayout.Toggle("迁移动骨组件", includePhysBones);
        includeColliders = EditorGUILayout.Toggle("迁移碰撞体", includeColliders);

        if (GUILayout.Button("开始迁移"))
        {
            if (sourceCharacter == null || targetCharacter == null)
            {
                EditorUtility.DisplayDialog("错误", "请先选择源角色和目标角色!", "确定");
                return;
            }

            colliderMapping.Clear();
            MigrateComponents();
        }
    }

    private void MigrateComponents()
    {
        Dictionary<Transform, Transform> boneMapping = CreateBoneMapping();
        
        if (includeColliders)
        {
            MigrateColliders(boneMapping);
            Debug.Log($"已建立 {colliderMapping.Count} 个碰撞体映射关系");
        }

        if (includePhysBones)
        {
            MigratePhysBones(boneMapping);
        }

        RedirectReferences();

        // 强制刷新编辑器
        EditorUtility.SetDirty(targetCharacter);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("完成", "组件迁移完成!", "确定");
    }

    private Dictionary<Transform, Transform> CreateBoneMapping()
    {
        Dictionary<Transform, Transform> mapping = new Dictionary<Transform, Transform>();
        Transform[] sourceBones = sourceCharacter.GetComponentsInChildren<Transform>();
        Transform[] targetBones = targetCharacter.GetComponentsInChildren<Transform>();

        foreach (Transform sourceBone in sourceBones)
        {
            Transform targetBone = targetBones.FirstOrDefault(t => t.name == sourceBone.name);
            if (targetBone != null)
            {
                mapping[sourceBone] = targetBone;
            }
        }

        return mapping;
    }

    private void MigratePhysBones(Dictionary<Transform, Transform> boneMapping)
    {
        var physBones = sourceCharacter.GetComponentsInChildren<Component>()
            .Where(c => c.GetType().Name.Contains("VRCPhysBone"));

        foreach (var sourceBone in physBones)
        {
            Transform sourceTransform = sourceBone.transform;
            if (boneMapping.TryGetValue(sourceTransform, out Transform targetTransform))
            {
                // 检查是否已存在相同类型的组件
                var existingComponent = targetTransform.GetComponent(sourceBone.GetType());
                if (existingComponent != null)
                {
                    DestroyImmediate(existingComponent);
                }

                Component targetComponent = targetTransform.gameObject.AddComponent(sourceBone.GetType());
                EditorUtility.CopySerialized(sourceBone, targetComponent);
            }
        }
    }

    private void MigrateColliders(Dictionary<Transform, Transform> boneMapping)
    {
        var colliders = sourceCharacter.GetComponentsInChildren<Component>()
            .Where(c => c.GetType().Name.Contains("PhysBoneCollider"));

        foreach (var sourceCollider in colliders)
        {
            Transform sourceTransform = sourceCollider.transform;
            Component targetCollider = null;

            // 处理直接附着在骨架上的碰撞体
            if (boneMapping.TryGetValue(sourceTransform, out Transform targetBone))
            {
                // 检查是否已存在相同类型的组件
                var existingComponent = targetBone.GetComponent(sourceCollider.GetType());
                if (existingComponent != null)
                {
                    DestroyImmediate(existingComponent);
                }

                targetCollider = targetBone.gameObject.AddComponent(sourceCollider.GetType());
                EditorUtility.CopySerialized(sourceCollider, targetCollider);
            }
            // 处理独立的碰撞体对象
            else if (sourceTransform.parent != null && boneMapping.TryGetValue(sourceTransform.parent, out Transform targetParent))
            {
                // 在目标角色中查找或创建碰撞体对象
                Transform existingCollider = targetParent.Find(sourceTransform.name);
                GameObject colliderObj;

                if (existingCollider != null)
                {
                    colliderObj = existingCollider.gameObject;
                    // 清除现有的碰撞体组件
                    var existingComponent = colliderObj.GetComponent(sourceCollider.GetType());
                    if (existingComponent != null)
                    {
                        DestroyImmediate(existingComponent);
                    }
                }
                else
                {
                    colliderObj = new GameObject(sourceTransform.name);
                    colliderObj.transform.SetParent(targetParent);
                    colliderObj.transform.localPosition = sourceTransform.localPosition;
                    colliderObj.transform.localRotation = sourceTransform.localRotation;
                    colliderObj.transform.localScale = sourceTransform.localScale;
                }

                targetCollider = colliderObj.AddComponent(sourceCollider.GetType());
                EditorUtility.CopySerialized(sourceCollider, targetCollider);
            }

            if (targetCollider != null)
            {
                colliderMapping[sourceCollider] = targetCollider;
            }
        }
    }

    private void RedirectReferences()
    {
        // 只处理动骨组件中的碰撞体引用
        var physBones = targetCharacter.GetComponentsInChildren<Component>()
            .Where(c => c.GetType().Name.Contains("VRCPhysBone"));

        foreach (var physBone in physBones)
        {
            var serializedObject = new SerializedObject(physBone);
            var collidersProp = serializedObject.FindProperty("colliders");

            if (collidersProp != null && collidersProp.isArray)
            {
                bool modified = false;

                for (int i = 0; i < collidersProp.arraySize; i++)
                {
                    var colliderRef = collidersProp.GetArrayElementAtIndex(i);
                    var oldCollider = colliderRef.objectReferenceValue as Component;

                    if (oldCollider != null && colliderMapping.TryGetValue(oldCollider, out Component newCollider))
                    {
                        colliderRef.objectReferenceValue = newCollider;
                        modified = true;
                    }
                }

                if (modified)
                {
                    serializedObject.ApplyModifiedProperties();
                }
            }
        }
    }

    private void OnDestroy()
    {
        // 清理资源
        colliderMapping.Clear();
        EditorUtility.UnloadUnusedAssetsImmediate();
        System.GC.Collect();
    }
}
