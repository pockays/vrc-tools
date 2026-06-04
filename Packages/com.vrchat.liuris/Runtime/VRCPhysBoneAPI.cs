#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System;

/// <summary>
/// VRCPhysBone 共享静态 API —— 供 VRCPhysBoneOptimizer (Runtime) 和 VRCPhysBoneOrganizer (Editor) 共同调用。
/// 所有方法均为 Editor-only，封装在 #if UNITY_EDITOR 中。
/// </summary>
public static class VRCPhysBoneAPI
{
    // ============================================================
    // 主要静态 API
    // ============================================================

    /// <summary>
    /// 对指定对象执行"迁移PhysBone到Root"操作。
    /// 将对象及其子对象上的PhysBone和PhysBoneCollider组件移动到rootTransform对应的骨骼上。
    /// </summary>
    /// <returns>是否有任何组件被迁移</returns>
    public static bool MovePhysBonesToRoot(GameObject target, out string status)
    {
        status = "";
        if (target == null)
        {
            status = "错误: target 为 null";
            return false;
        }

        var allObjects = GetAllObjectsInHierarchy(target);
        int movedPhysBonesCount = 0;
        int movedCollidersCount = 0;
        int totalPhysBonesCount = 0;
        int totalCollidersCount = 0;
        List<Component> allPhysBones = new List<Component>();
        List<Component> allColliders = new List<Component>();

        foreach (var obj in allObjects)
        {
            var physBones = GetAllPhysBoneComponents(obj);
            var colliders = GetAllPhysBoneColliderComponents(obj);
            allPhysBones.AddRange(physBones);
            allColliders.AddRange(colliders);
            totalPhysBonesCount += physBones.Length;
            totalCollidersCount += colliders.Length;
        }

        foreach (var physBone in allPhysBones)
        {
            var rootTransform = GetRootTransformField(physBone);
            if (rootTransform == null)
            {
                Debug.LogWarning($"[PhysBone优化-迁移] {physBone.gameObject.name} 上的 PhysBone rootTransform 为 null，跳过迁移");
                continue;
            }
            if (rootTransform == physBone.gameObject.transform)
            {
                // PhysBone 已经在 root 骨骼上，无需迁移
                continue;
            }
            // 安全检查: rootTransform 在 target 内部则跳过，避免后续删除时丢失
            if (rootTransform.IsChildOf(target.transform) || rootTransform == target.transform)
            {
                Debug.LogWarning($"[PhysBone优化-迁移] {physBone.gameObject.name} 的 rootTransform ({rootTransform.name}) 在源对象内部，跳过迁移（删除源对象时会丢失）");
                continue;
            }

            // 使用 AddComponent + CopySerialized 方式替代 CopyComponent/PasteComponentAsNew，
            // 确保第三方 DLL 组件（VRCPhysBone、VRCPhysBoneCollider）能正确迁移
            var newComponent = rootTransform.gameObject.AddComponent(physBone.GetType());
            EditorUtility.CopySerialized(physBone, newComponent);
            Undo.DestroyObjectImmediate(physBone);
            movedPhysBonesCount++;
        }

        foreach (var collider in allColliders)
        {
            var rootTransform = GetRootTransformField(collider);
            if (rootTransform == null)
            {
                Debug.LogWarning($"[PhysBone优化-迁移] {collider.gameObject.name} 上的 PhysBoneCollider rootTransform 为 null，跳过迁移");
                continue;
            }
            if (rootTransform == collider.gameObject.transform)
            {
                continue;
            }
            if (rootTransform.IsChildOf(target.transform) || rootTransform == target.transform)
            {
                Debug.LogWarning($"[PhysBone优化-迁移] {collider.gameObject.name} 的 rootTransform ({rootTransform.name}) 在源对象内部，跳过迁移");
                continue;
            }

            // 使用 AddComponent + CopySerialized 方式替代 CopyComponent/PasteComponentAsNew，
            // 确保第三方 DLL 组件（VRCPhysBoneCollider）能正确迁移
            var newCollider = rootTransform.gameObject.AddComponent(collider.GetType());
            EditorUtility.CopySerialized(collider, newCollider);
            Undo.DestroyObjectImmediate(collider);
            movedCollidersCount++;
        }

        int totalMoved = movedPhysBonesCount + movedCollidersCount;
        if (totalMoved > 0)
        {
            status = $"移动完成: PhysBone {movedPhysBonesCount}/{totalPhysBonesCount}, Collider {movedCollidersCount}/{totalCollidersCount}";
            return true;
        }
        else
        {
            status = "未找到可移动的PhysBone或PhysBoneCollider组件";
            return false;
        }
    }

    /// <summary>
    /// 删除源对象。在 MovePhysBonesToRoot 迁移完成后调用。
    /// </summary>
    public static void DeleteSourceObject(GameObject target)
    {
        if (target != null)
        {
            Undo.DestroyObjectImmediate(target);
        }
    }

    /// <summary>
    /// 对指定GameObject执行"合并PhysBones"操作。
    /// 遍历 target 及其所有子对象，按父级分组，将同父级下相同配置的PhysBone合并到 [PhysBones] 父对象下。
    /// </summary>
    /// <returns>是否有任何组合并</returns>
    public static bool MergePhysBones(GameObject target, out int processedGroups, out int processedObjects, out string status)
    {
        processedGroups = 0;
        processedObjects = 0;
        status = "";

        if (target == null)
        {
            status = "错误: target 为 null";
            return false;
        }

        List<GameObject> allObjects = new List<GameObject> { target };
        AddAllChildren(target, allObjects);

        return MergePhysBonesFromList(allObjects, out processedGroups, out processedObjects, out status);
    }

    /// <summary>
    /// 对给定的GameObject列表执行"合并PhysBones"操作。
    /// </summary>
    public static bool MergePhysBonesFromList(List<GameObject> selectedObjects, out int processedGroups, out int processedObjects, out string status)
    {
        processedGroups = 0;
        processedObjects = 0;
        status = "";

        if (selectedObjects == null || selectedObjects.Count == 0)
        {
            status = "错误: 没有提供任何对象";
            return false;
        }

        var objectsByParent = GroupObjectsByParent(selectedObjects);

        foreach (var parentGroup in objectsByParent)
        {
            if (parentGroup.Value.Count <= 1) continue;

            var objectsWithPhysBones = FindObjectsWithPhysBones(parentGroup.Value);
            if (objectsWithPhysBones.Count <= 1) continue;

            var groupsByPhysBoneConfig = GroupByPhysBoneConfig(objectsWithPhysBones);

            foreach (var configGroup in groupsByPhysBoneConfig)
            {
                if (configGroup.Value.Count > 1)
                {
                    string groupStatus;
                    if (ProcessPhysBoneGroup(parentGroup.Key, configGroup.Value, out groupStatus))
                    {
                        processedGroups++;
                        processedObjects += configGroup.Value.Count;
                    }
                }
            }
        }

        if (processedGroups > 0)
            status = $"合并完成: {processedGroups} 组（{processedObjects} 个对象）";
        else
            status = "未找到可合并的Phys Bone组";

        return processedGroups > 0;
    }

    // ============================================================
    // 辅助方法
    // ============================================================

    public static Component[] GetAllPhysBoneColliderComponents(GameObject obj)
    {
        if (obj == null) return new Component[0];

        Component[] allComponents = obj.GetComponents<Component>();
        List<Component> colliders = new List<Component>();

        foreach (Component component in allComponents)
        {
            if (component == null) continue;

            string typeName = component.GetType().Name;

            // 只匹配 VRCPhysBoneCollider，排除 Unity 内置 BoxCollider/SphereCollider 等
            if (typeName.Contains("Phys") && typeName.Contains("Collider"))
            {
                colliders.Add(component);
            }
        }

        return colliders.ToArray();
    }

    public static Component[] GetAllPhysBoneComponents(GameObject obj)
    {
        if (obj == null) return new Component[0];

        Component[] allComponents = obj.GetComponents<Component>();
        List<Component> physBones = new List<Component>();

        foreach (Component component in allComponents)
        {
            if (component == null) continue;

            string typeName = component.GetType().Name;

            if ((typeName.Contains("PhysBone") || typeName.Contains("Phys_Bone")) &&
                !typeName.Contains("Collider"))
            {
                physBones.Add(component);
            }
        }

        return physBones.ToArray();
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
                try { return (Transform)field.GetValue(physBone); }
                catch { return null; }
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

    public static void AddAllChildren(GameObject parent, List<GameObject> list)
    {
        foreach (Transform child in parent.transform)
        {
            list.Add(child.gameObject);
            AddAllChildren(child.gameObject, list);
        }
    }

    public static Dictionary<Transform, List<GameObject>> GroupObjectsByParent(List<GameObject> objects)
    {
        var groups = new Dictionary<Transform, List<GameObject>>();
        foreach (GameObject obj in objects)
        {
            Transform parent = obj.transform.parent;
            if (parent == null) continue;

            if (!groups.ContainsKey(parent))
                groups[parent] = new List<GameObject>();
            groups[parent].Add(obj);
        }
        return groups;
    }

    public static List<GameObject> FindObjectsWithPhysBones(List<GameObject> objects)
    {
        List<GameObject> result = new List<GameObject>();
        foreach (GameObject obj in objects)
        {
            if (GetPhysBoneComponent(obj) != null)
                result.Add(obj);
        }
        return result;
    }

    public static Dictionary<string, List<GameObject>> GroupByPhysBoneConfig(List<GameObject> objects)
    {
        var groups = new Dictionary<string, List<GameObject>>();
        foreach (GameObject obj in objects)
        {
            Component physBone = GetPhysBoneComponent(obj);
            if (physBone == null) continue;

            string configHash = GetPhysBoneConfigHash(physBone);
            if (!groups.ContainsKey(configHash))
                groups[configHash] = new List<GameObject>();
            groups[configHash].Add(obj);
        }
        return groups;
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
                Undo.DestroyObjectImmediate(physBone);
        }

        foreach (GameObject obj in objects)
        {
            Undo.SetTransformParent(obj.transform, newParent.transform, "Move to PhysBone Group");
            obj.transform.SetParent(newParent.transform, true);
        }

        status = $"成功合并 {objects.Count} 个Phys Bones";
        return true;
    }

    public static Component GetPhysBoneComponent(GameObject obj)
    {
        if (obj == null) return null;
        var allPhysBones = GetAllPhysBoneComponents(obj);
        return allPhysBones.Length > 0 ? allPhysBones[0] : null;
    }

    public static string GetPhysBoneConfigHash(Component physBone)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        System.Type type = physBone.GetType();

        FieldInfo[] allFields = type.GetFields(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

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
                        string curveHash = curve != null ? GetSimpleCurveHash(curve) : "null";
                        sb.Append($"{field.Name}:Curve:{curveHash}|");
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
            return $"[{unityObj.GetInstanceID()}:{unityObj.name}]";
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
            return unityObj ? $"{unityObj.GetType().Name}:{unityObj.name}" : "null";
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
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

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
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

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
                BindingFlags.Public | BindingFlags.Instance);
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
                    collidersValue = collidersField.GetValue(physBone);
                else if (collidersProperty != null && collidersProperty.CanRead)
                    collidersValue = collidersProperty.GetValue(physBone);

                if (collidersValue != null)
                {
                    var collidersEnumerable = collidersValue as System.Collections.IEnumerable;
                    if (collidersEnumerable != null)
                    {
                        foreach (var collider in collidersEnumerable)
                        {
                            if (collider is UnityEngine.Object unityCollider && unityCollider != null)
                                allColliders.Add(unityCollider);
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
                            addMethod.Invoke(colliderCollection, new object[] { collider });
                    }
                    else if (fieldType.IsArray)
                    {
                        Type elementType = fieldType.GetElementType();
                        var array = Array.CreateInstance(elementType, allColliders.Count);
                        int index = 0;
                        foreach (var collider in allColliders)
                            array.SetValue(collider, index++);
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
                targetSO.CopyFromSerializedProperty(sourceProp);
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
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

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
                BindingFlags.Public | BindingFlags.Instance);
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
                    catch (System.Exception e) { }
                }
            }
        }

        if (foundAndCleared)
            EditorUtility.SetDirty(physBone.gameObject);
    }
}
#endif
