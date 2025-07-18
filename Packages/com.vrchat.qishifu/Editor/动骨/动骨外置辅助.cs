using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class 动骨外置辅助 : EditorWindow
{
    private GameObject rootObject;
    private GameObject pbDynamicContainer;
    private HashSet<GameObject> processedObjects;

    [MenuItem("奇师傅工具箱/工具/动骨/动骨外置辅助", false, 0)]
    public static void ShowWindow()
    {
        GetWindow<动骨外置辅助>("动骨外置辅助");
    }

    void OnGUI()
    {
        rootObject = EditorGUILayout.ObjectField("指定对象", rootObject, typeof(GameObject), true) as GameObject;

        if (GUILayout.Button("处理动骨"))
        {
            if (rootObject != null)
            {
                ProcessOrganizeObjects();
            }
            else
            {
                Debug.LogError("未指定对象。");
            }
        }

        if (GUILayout.Button("去除后缀"))
        {
            if (rootObject != null)
            {
                RemoveSuffixFromObjects(rootObject);
                Debug.Log("后缀去除完成。");
            }
            else
            {
                Debug.LogError("未指定对象");
            }
        }

        GUILayout.Space(20);
        GUILayout.Label("警告：使用工具前请对指定对象进行备份\n工具是死板的，但人是灵活的\n\n这是一个PB外置辅助工具，它会进行以下操作：\n1. 将指定对象之下包含PB组件的对象复制到PB Dynamic之下\n2. 删除原对象的PB组件\n3. 为原对象和复制出来的对象添加便于搜索操作的后缀\n4. 对于PB碰撞体，不会被进行复制和删除组件操作，而是会被添加“碰撞体”后缀\n\n 如何使用？\n1. 在脚本处理完成之后，在层级搜索栏中搜索“PB外置”\n2. 将后缀为PB外置_1的对象，拖到PB外置_2的对象组件内的PB Root Transform之中\n3. 操作完成后点击“清理后缀”将后缀删除\n\n 可能的使用场景示例：\n1. 不使用MA绑定衣服时，它可以方便隐藏动骨（因为MA绑定衣服后，动画会包含动骨隐藏）\n2. 需要对一个对象内的动骨进行详细管理时，外置动骨更方便", EditorStyles.helpBox);
    }

    void ProcessOrganizeObjects()
    {
        if (pbDynamicContainer != null) DestroyImmediate(pbDynamicContainer);
        processedObjects = new HashSet<GameObject>();
        pbDynamicContainer = new GameObject("PB Dynamic");
        pbDynamicContainer.transform.SetParent(rootObject.transform, false);

        OrganizeObjectsWithVRCComponents(rootObject);
        EditorUtility.DisplayDialog("操作完成", "VRC物理骨骼对象已成功重构。", "确定");
    }

    void RemoveSuffixFromObjects(GameObject rootObj)
    {
        void ProcessGameObject(GameObject obj)
        {
            if (obj.name.EndsWith("_PB外置_1"))
            {
                obj.name = obj.name.Replace("_PB外置_1", "");
            }
            else if (obj.name.EndsWith("_PB外置_2"))
            {
                obj.name = obj.name.Replace("_PB外置_2", "");
            }
            else if (obj.name.EndsWith("_碰撞体"))
            {
                obj.name = obj.name.Replace("_碰撞体", "");
            }

            foreach (Transform child in obj.transform)
            {
                ProcessGameObject(child.gameObject);
            }
        }

        ProcessGameObject(rootObj);
    }

    void OrganizeObjectsWithVRCComponents(GameObject rootObj)
    {
        void ProcessGameObject(GameObject obj)
        {
            if (obj == pbDynamicContainer || obj.transform.IsChildOf(pbDynamicContainer.transform)) return;
            if (processedObjects.Contains(obj)) return;
            processedObjects.Add(obj);

            var physBone = obj.GetComponent("VRCPhysBone");
            var physBoneCollider = obj.GetComponent("VRCPhysBoneCollider");

            if (physBone != null)
            {
                var parentName = obj.transform.parent != null ? obj.transform.parent.name : "Root";
                var newParentObject = FindOrCreateParentObjectInPBDynamic(parentName);
                var clone = Instantiate(obj, newParentObject.transform);

                obj.name += "_PB外置_1";
                clone.name = obj.name.Replace("_PB外置_1", "_PB外置_2"); // Ensure clone name is set correctly

                // Remove the VRCPhysBone component from the original object
                DestroyImmediate(physBone, true);
            }
            else if (physBoneCollider != null)
            {
                // For objects with the VRC Phys Bone Collider component, only add the suffix "_碰撞体"
                obj.name += "_碰撞体";
                // No cloning or component removal is done for VRC Phys Bone Collider
            }

            foreach (Transform child in obj.transform)
            {
                ProcessGameObject(child.gameObject);
            }
        }

        ProcessGameObject(rootObj);
    }

    GameObject FindOrCreateParentObjectInPBDynamic(string parentName)
    {
        Transform foundParent = pbDynamicContainer.transform.Find(parentName);
        if (foundParent == null)
        {
            GameObject newParent = new GameObject(parentName);
            newParent.transform.SetParent(pbDynamicContainer.transform);
            return newParent;
        }
        return foundParent.gameObject;
    }
}
