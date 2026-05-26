using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System;
#endif

[AddComponentMenu("咩卡布/PhysBone Optimizer")]
public class VRCPhysBoneOptimizer : MonoBehaviour
{
    [Header("动骨优化")]
    [Tooltip("需要迁移PhysBone的源对象（可为空），将把该对象子级的PhysBone迁移到对应骨骼上")]
    public GameObject sourcePBObject;

    [HideInInspector] public bool hasOptimized = false;

    void Start()
    {
#if UNITY_EDITOR
        if (hasOptimized) return;

        if (sourcePBObject != null)
        {
            string status;
            int migrated = PhysBoneHelper.MigratePhysBonesToRoot(sourcePBObject, out status);
            Debug.Log($"[PhysBone优化-迁移] {status}");
        }

        int groups, objects;
        PhysBoneHelper.MergePhysBones(gameObject, out groups, out objects);
        Debug.Log($"<color=green>[PhysBone优化] 合并完成: {groups} 组（{objects} 个对象）</color>");

        hasOptimized = true;
#endif
    }
}

#if UNITY_EDITOR
public static class PhysBoneHelper
{
    public static Component GetPhysBoneComponent(GameObject obj)
    {
        if (obj == null) return null;

        Component[] components = obj.GetComponents<Component>();

        foreach (Component component in components)
        {
            if (component == null) continue;

            string typeName = component.GetType().Name;

            if ((typeName.Contains("PhysBone") || typeName.Contains("Phys_Bone")))
            {
                if (!typeName.Contains("Collider"))
                {
                    return component;
                }
            }
        }

        return null;
    }

    public static Transform GetRootTransformField(Component physBone)
    {
        if (physBone == null) return null;

        System.Type type = physBone.GetType();
        FieldInfo[] fields = type.GetFields(
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.Instance
        );

        foreach (FieldInfo field in fields)
        {
            string fieldName = field.Name.ToLower();
            if ((fieldName.Contains("root") && field.FieldType == typeof(Transform)) ||
                (fieldName.Contains("roottransform") && field.FieldType == typeof(Transform)))
            {
                try
                {
                    return (Transform)field.GetValue(physBone);
                }
                catch
                {
                    return null;
                }
            }
        }

        return null;
    }

    public static List<GameObject> GetAllObjectsInHierarchy(GameObject parent)
    {
        var objects = new List<GameObject> { parent };
        foreach (Transform child in parent.transform)
            objects.AddRange(GetAllObjectsInHierarchy(child.gameObject));
        return objects;
    }

    public static List<GameObject> FindObjectsWithPhysBones(List<GameObject> objects)
    {
        List<GameObject> result = new List<GameObject>();

        foreach (GameObject obj in objects)
        {
            if (GetPhysBoneComponent(obj) != null)
            {
                result.Add(obj);
            }
        }

        return result;
    }

    public static Dictionary<string, List<GameObject>> GroupByPhysBoneConfig(List<GameObject> objects)
    {
        Dictionary<string, List<GameObject>> groups = new Dictionary<string, List<GameObject>>();

        foreach (GameObject obj in objects)
        {
            Component physBone = GetPhysBoneComponent(obj);
            if (physBone == null) continue;

            string configHash = GetPhysBoneConfigHash(physBone);

            if (!groups.ContainsKey(configHash))
            {
                groups[configHash] = new List<GameObject>();
            }

            groups[configHash].Add(obj);
        }

        return groups;
    }

    public static string GetPhysBoneConfigHash(Component physBone)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        System.Type type = physBone.GetType();

        FieldInfo[] allFields = type.GetFields(
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.Instance
        );

        var serializableFields = allFields.Where(f =>
            f.IsPublic ||
            f.GetCustomAttribute<SerializeField>() != null
        ).OrderBy(f => f.Name).ToArray();

        FieldInfo collidersField = serializableFields.FirstOrDefault(f =>
            f.Name.ToLower().Contains("colliders") || f.Name.ToLower().Contains("collider"));

        if (collidersField != null)
        {
            object collidersValue = collidersField.GetValue(physBone);
            string collidersHash = GetCollidersHash(collidersValue);
            sb.Append($"Colliders:{collidersHash}|");
        }

        foreach (FieldInfo field in serializableFields)
        {
            string fieldName = field.Name.ToLower();

            if (fieldName.Contains("root") && field.FieldType == typeof(Transform))
                continue;

            if (fieldName.Contains("transform") && field.FieldType == typeof(Transform))
                continue;

            if (fieldName.Contains("colliders") || fieldName.Contains("collider"))
                continue;

            try
            {
                object value = field.GetValue(physBone);

                if (field.FieldType == typeof(AnimationCurve) || field.Name.ToLower().Contains("curve"))
                {
                    if (value is AnimationCurve curve)
                    {
                        if (curve == null)
                        {
                            sb.Append($"{field.Name}:Curve:null|");
                        }
                        else
                        {
                            string curveHash = GetSimpleCurveHash(curve);
                            sb.Append($"{field.Name}:Curve:{curveHash}|");
                        }
                    }
                    else
                    {
                        sb.Append($"{field.Name}:{value}|");
                    }
                }
                else
                {
                    sb.Append($"{field.Name}:{GetValueString(value)}|");
                }
            }
            catch { }
        }

        return sb.ToString();
    }

    public static string GetCollidersHash(object collidersValue)
    {
        if (collidersValue == null) return "null";

        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        try
        {
            var collidersList = collidersValue as System.Collections.IEnumerable;
            if (collidersList != null)
            {
                int count = 0;
                foreach (var collider in collidersList)
                {
                    if (collider != null)
                    {
                        count++;
                        sb.Append(GetColliderInfo(collider));
                    }
                }
                return $"Count:{count}|Items:{sb}";
            }
        }
        catch { }

        return collidersValue.ToString();
    }

    public static string GetColliderInfo(object collider)
    {
        if (collider == null) return "null";

        if (collider is UnityEngine.Object unityObj)
        {
            return $"[{unityObj.GetInstanceID()}:{unityObj.name}]";
        }

        return collider.ToString();
    }

    public static string GetSimpleCurveHash(AnimationCurve curve)
    {
        if (curve == null || curve.keys == null || curve.keys.Length == 0)
            return "empty";

        return $"keys:{curve.keys.Length}";
    }

    public static string GetValueString(object value)
    {
        if (value == null) return "null";

        if (value is UnityEngine.Object unityObj)
        {
            return unityObj ? $"{unityObj.GetType().Name}:{unityObj.name}" : "null";
        }

        return value.ToString();
    }

    public static bool CopyPhysBoneComponentToParent(Component sourceComponent, GameObject parentObject)
    {
        System.Type componentType = sourceComponent.GetType();

        try
        {
            Component newComponent = parentObject.AddComponent(componentType);
            EditorUtility.CopySerialized(sourceComponent, newComponent);

            ClearCollidersInParent(newComponent);
            SetRootTransformToNull(newComponent);

            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"复制失败: {e.Message}");

            try
            {
                return CopyComponentUsingSerializedObject(sourceComponent, parentObject);
            }
            catch (System.Exception e2)
            {
                Debug.LogError($"备份方法也失败: {e2.Message}");
                return false;
            }
        }
    }

    public static void ClearCollidersInParent(Component physBone)
    {
        System.Type type = physBone.GetType();

        FieldInfo[] fields = type.GetFields(
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.Instance
        );

        foreach (FieldInfo field in fields)
        {
            string fieldName = field.Name.ToLower();
            if (fieldName.Contains("colliders") || fieldName.Contains("collider"))
            {
                try
                {
                    if (field.FieldType.IsGenericType &&
                        field.FieldType.GetGenericTypeDefinition() == typeof(List<>))
                    {
                        var emptyList = System.Activator.CreateInstance(field.FieldType);
                        field.SetValue(physBone, emptyList);
                        EditorUtility.SetDirty(physBone.gameObject);
                    }
                    else if (field.FieldType.IsArray)
                    {
                        var elementType = field.FieldType.GetElementType();
                        var emptyArray = Array.CreateInstance(elementType, 0);
                        field.SetValue(physBone, emptyArray);
                        EditorUtility.SetDirty(physBone.gameObject);
                    }
                    else
                    {
                        field.SetValue(physBone, null);
                        EditorUtility.SetDirty(physBone.gameObject);
                    }
                    break;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"无法清空colliders字段: {e.Message}");
                }
            }
        }
    }

    public static void CollectAndSetCollidersToParent(List<GameObject> objects, GameObject parentObject, Component templatePhysBone)
    {
        System.Type physBoneType = templatePhysBone.GetType();

        FieldInfo collidersField = null;
        PropertyInfo collidersProperty = null;

        FieldInfo[] fields = physBoneType.GetFields(
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.Instance
        );

        foreach (FieldInfo field in fields)
        {
            string fieldName = field.Name.ToLower();
            if (fieldName.Contains("colliders") || fieldName.Contains("collider"))
            {
                collidersField = field;
                break;
            }
        }

        if (collidersField == null)
        {
            PropertyInfo[] properties = physBoneType.GetProperties(
                BindingFlags.Public |
                BindingFlags.Instance
            );

            foreach (PropertyInfo property in properties)
            {
                string propertyName = property.Name.ToLower();
                if (propertyName.Contains("colliders") || propertyName.Contains("collider"))
                {
                    collidersProperty = property;
                    break;
                }
            }
        }

        if (collidersField == null && collidersProperty == null)
        {
            Debug.LogWarning("未找到colliders字段或属性");
            return;
        }

        HashSet<UnityEngine.Object> allColliders = new HashSet<UnityEngine.Object>();

        foreach (GameObject obj in objects)
        {
            Component physBone = GetPhysBoneComponent(obj);
            if (physBone != null)
            {
                object collidersValue = null;
                if (collidersField != null)
                {
                    collidersValue = collidersField.GetValue(physBone);
                }
                else if (collidersProperty != null && collidersProperty.CanRead)
                {
                    collidersValue = collidersProperty.GetValue(physBone);
                }

                if (collidersValue != null)
                {
                    var collidersEnumerable = collidersValue as System.Collections.IEnumerable;
                    if (collidersEnumerable != null)
                    {
                        foreach (var collider in collidersEnumerable)
                        {
                            if (collider is UnityEngine.Object unityCollider && unityCollider != null)
                            {
                                allColliders.Add(unityCollider);
                            }
                        }
                    }
                }
            }
        }

        Component parentPhysBone = GetPhysBoneComponent(parentObject);
        if (parentPhysBone != null && allColliders.Count > 0)
        {
            try
            {
                if (collidersField != null)
                {
                    object colliderCollection;
                    Type fieldType = collidersField.FieldType;

                    if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>))
                    {
                        Type elementType = fieldType.GetGenericArguments()[0];
                        colliderCollection = System.Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType));
                        var addMethod = colliderCollection.GetType().GetMethod("Add");

                        foreach (var collider in allColliders)
                        {
                            addMethod.Invoke(colliderCollection, new object[] { collider });
                        }
                    }
                    else if (fieldType.IsArray)
                    {
                        Type elementType = fieldType.GetElementType();
                        var array = Array.CreateInstance(elementType, allColliders.Count);
                        int index = 0;
                        foreach (var collider in allColliders)
                        {
                            array.SetValue(collider, index++);
                        }
                        colliderCollection = array;
                    }
                    else
                    {
                        Debug.LogWarning($"不支持的colliders字段类型: {fieldType}");
                        return;
                    }

                    collidersField.SetValue(parentPhysBone, colliderCollection);
                    EditorUtility.SetDirty(parentObject);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"设置colliders失败: {e.Message}");
            }
        }
    }

    public static bool CopyComponentUsingSerializedObject(Component source, GameObject target)
    {
        System.Type type = source.GetType();
        Component newComponent = target.AddComponent(type);

        SerializedObject sourceSO = new SerializedObject(source);
        SerializedObject targetSO = new SerializedObject(newComponent);

        SerializedProperty sourceProp = sourceSO.GetIterator();
        bool enterChildren = true;

        while (sourceProp.NextVisible(enterChildren))
        {
            enterChildren = false;

            if (sourceProp.name == "m_Script") continue;

            SerializedProperty targetProp = targetSO.FindProperty(sourceProp.propertyPath);
            if (targetProp != null)
            {
                targetSO.CopyFromSerializedProperty(sourceProp);
            }
        }

        targetSO.ApplyModifiedProperties();

        ClearCollidersInParent(newComponent);
        SetRootTransformToNull(newComponent);

        return true;
    }

    public static void SetRootTransformToNull(Component physBone)
    {
        System.Type type = physBone.GetType();
        bool foundAndCleared = false;

        FieldInfo[] fields = type.GetFields(
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.Instance
        );

        foreach (FieldInfo field in fields)
        {
            string fieldName = field.Name.ToLower();

            if ((fieldName.Contains("root") && field.FieldType == typeof(Transform)) ||
                (fieldName.Contains("roottransform") && field.FieldType == typeof(Transform)) ||
                (fieldName == "_roottransform" && field.FieldType == typeof(Transform)))
            {
                try
                {
                    field.SetValue(physBone, null);
                    foundAndCleared = true;
                    break;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"无法设置root transform字段: {e.Message}");
                }
            }
        }

        if (!foundAndCleared)
        {
            PropertyInfo[] properties = type.GetProperties(
                BindingFlags.Public |
                BindingFlags.Instance
            );

            foreach (PropertyInfo property in properties)
            {
                string propertyName = property.Name.ToLower();
                if ((propertyName.Contains("root") || propertyName.Contains("transform")) &&
                    property.PropertyType == typeof(Transform) &&
                    property.CanWrite)
                {
                    try
                    {
                        property.SetValue(physBone, null);
                        foundAndCleared = true;
                        break;
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"无法设置root transform属性: {e.Message}");
                    }
                }
            }
        }

        if (foundAndCleared)
        {
            EditorUtility.SetDirty(physBone.gameObject);
        }
        else
        {
            Debug.LogWarning("未找到root transform字段或属性");
        }
    }

    public static bool ProcessPhysBoneGroup(Transform parentTransform, List<GameObject> objects, out string status)
    {
        status = "";
        if (objects.Count < 2)
        {
            status = "跳过：对象数量不足2个";
            return false;
        }

        GameObject newParent = new GameObject($"[PhysBones] {objects[0].name}_Group");
        newParent.transform.SetParent(parentTransform);
        newParent.transform.localPosition = Vector3.zero;
        newParent.transform.localRotation = Quaternion.identity;
        newParent.transform.localScale = Vector3.one;

        Undo.RegisterCreatedObjectUndo(newParent, "Create PhysBone Group");

        Component templatePhysBone = GetPhysBoneComponent(objects[0]);
        if (templatePhysBone == null)
        {
            Undo.DestroyObjectImmediate(newParent);
            status = $"警告: 对象 {objects[0].name} 没有找到Phys Bone组件";
            return false;
        }

        bool componentCopied = CopyPhysBoneComponentToParent(templatePhysBone, newParent);

        if (!componentCopied)
        {
            Undo.DestroyObjectImmediate(newParent);
            status = $"错误: 无法复制Phys Bone组件到父对象 {newParent.name}";
            return false;
        }

        CollectAndSetCollidersToParent(objects, newParent, templatePhysBone);

        foreach (GameObject obj in objects)
        {
            Component physBone = GetPhysBoneComponent(obj);
            if (physBone != null)
            {
                Undo.DestroyObjectImmediate(physBone);
            }
        }

        foreach (GameObject obj in objects)
        {
            Undo.SetTransformParent(obj.transform, newParent.transform, "Move to PhysBone Group");
            obj.transform.SetParent(newParent.transform, true);
        }

        Selection.activeGameObject = newParent;

        status = $"成功合并 {objects.Count} 个Phys Bones";
        return true;
    }

    public static int MigratePhysBonesToRoot(GameObject target, out string status)
    {
        var allObjects = GetAllObjectsInHierarchy(target);
        int movedCount = 0;
        int totalCount = 0;

        foreach (var obj in allObjects)
        {
            var physBone = GetPhysBoneComponent(obj);
            if (physBone != null)
            {
                totalCount++;

                var rootTransform = GetRootTransformField(physBone);
                if (rootTransform != null && rootTransform != obj.transform)
                {
                    var existingOnBone = GetPhysBoneComponent(rootTransform.gameObject);
                    if (existingOnBone != null)
                    {
                        Debug.Log($"骨骼 {rootTransform.name} 上已有PhysBone组件，跳过: {obj.name}");
                        continue;
                    }

                    ComponentUtility.CopyComponent(physBone);
                    ComponentUtility.PasteComponentAsNew(rootTransform.gameObject);

                    Undo.DestroyObjectImmediate(physBone);
                    movedCount++;
                }
            }
        }

        status = $"迁移 {movedCount}/{totalCount} 个PhysBone到对应骨骼";
        return movedCount;
    }

    public static void MergePhysBones(GameObject target, out int processedGroups, out int processedObjects)
    {
        processedGroups = 0;
        processedObjects = 0;

        var allObjects = GetAllObjectsInHierarchy(target);
        var objectsWithPhysBones = FindObjectsWithPhysBones(allObjects);

        if (objectsWithPhysBones.Count <= 1) return;

        var byParent = new Dictionary<Transform, List<GameObject>>();
        foreach (var obj in objectsWithPhysBones)
        {
            Transform parent = obj.transform.parent;
            if (parent == null) continue;
            if (!byParent.ContainsKey(parent))
                byParent[parent] = new List<GameObject>();
            byParent[parent].Add(obj);
        }

        foreach (var kvp in byParent)
        {
            if (kvp.Value.Count <= 1) continue;

            var byConfig = GroupByPhysBoneConfig(kvp.Value);
            foreach (var configGroup in byConfig)
            {
                if (configGroup.Value.Count > 1)
                {
                    string status;
                    if (ProcessPhysBoneGroup(kvp.Key, configGroup.Value, out status))
                    {
                        processedGroups++;
                        processedObjects += configGroup.Value.Count;
                    }
                }
            }
        }
    }
}

[CustomEditor(typeof(VRCPhysBoneOptimizer))]
public class VRCPhysBoneOptimizerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        VRCPhysBoneOptimizer optimizer = (VRCPhysBoneOptimizer)target;

        optimizer.sourcePBObject = (GameObject)EditorGUILayout.ObjectField(
            "源PB对象", optimizer.sourcePBObject, typeof(GameObject), true);

        if (GUI.changed)
            EditorUtility.SetDirty(optimizer);
    }
}
#endif
