using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif

#if UNITY_EDITOR
public class VRCPhysBoneOrganizer : EditorWindow
{
    private Vector2 scrollPosition;
    private string statusText = "就绪";
    private int totalProcessed = 0;
    private int totalMerged = 0;
    
    [MenuItem("Tools/VRC Phys Bone Organizer")]
    public static void ShowWindow()
    {
        GetWindow<VRCPhysBoneOrganizer>("Phys Bone Organizer");
    }
    
    void OnGUI()
    {
        GUILayout.Label("VRC Phys Bone 合并工具", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        
        if (GUILayout.Button("合并选中的Phys Bones", GUILayout.Height(40)))
        {
            ProcessSelectedObjects();
        }
        
        EditorGUILayout.Space();
        
        // 显示处理状态
        EditorGUILayout.LabelField("处理状态:", statusText);
        EditorGUILayout.LabelField("总处理组数:", totalProcessed.ToString());
        EditorGUILayout.LabelField("总合并对象:", totalMerged.ToString());
        
        EditorGUILayout.Space();
        
        // 显示警告提示
        EditorGUILayout.HelpBox(
            "！！！！！注意！！！！！\n" +
            "注意备份文件\n" +
            "合并前请先完全解压对象\n" +
            "当动骨在PB对象下或者骨骼中含有constrain组件时请不要使用",
            MessageType.Warning
        );

        // ---------- 新增：右下角高亮署名 ----------
        // 推动内容到窗口右下角（使用水平布局+弹性空间）
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace(); // 填充左侧空白，将署名推到右侧
        
        // 定义高亮样式
        GUIStyle highlightStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = Color.yellow }, // 高亮黄色文字
            fontStyle = FontStyle.Bold, // 粗体增强高亮效果
            alignment = TextAnchor.LowerRight // 右下对齐
        };
        
        // 绘制署名文字
        GUILayout.Label("闲鱼@咩卡布w  禁止转载", highlightStyle, GUILayout.ExpandWidth(false));
        GUILayout.EndHorizontal();
        // ------------------------------------------
    }
    
    private void ProcessSelectedObjects()
    {
        List<GameObject> selectedObjects = new List<GameObject>();
        
        // 获取所有选中的对象，自动包含子对象
        foreach (GameObject obj in Selection.gameObjects)
        {
            if (obj != null)
            {
                selectedObjects.Add(obj);
                AddAllChildren(obj, selectedObjects);
            }
        }
        
        if (selectedObjects.Count == 0)
        {
            statusText = "错误: 没有选择任何对象！";
            Debug.LogWarning(statusText);
            return;
        }
        
        statusText = $"开始处理 {selectedObjects.Count} 个对象...";
        Debug.Log(statusText);
        
        // 重置计数器
        totalProcessed = 0;
        totalMerged = 0;
        
        // 按父对象分组
        var objectsByParent = GroupObjectsByParent(selectedObjects);
        
        int processedGroups = 0;
        int processedObjects = 0;
        
        // 处理每个父级组
        foreach (var parentGroup in objectsByParent)
        {
            if (parentGroup.Value.Count <= 1) continue;
            
            statusText = $"处理父级: {parentGroup.Key.name} ({parentGroup.Value.Count}个子对象)";
            Debug.Log(statusText);
            
            // 查找所有带有VRC Phys Bone的对象
            var objectsWithPhysBones = FindObjectsWithPhysBones(parentGroup.Value);
            
            if (objectsWithPhysBones.Count <= 1) continue;
            
            // 按Phys Bone配置分组（考虑Colliders的差异）
            var groupsByPhysBoneConfig = GroupByPhysBoneConfig(objectsWithPhysBones);
            
            // 处理每个配置组
            foreach (var configGroup in groupsByPhysBoneConfig)
            {
                if (configGroup.Value.Count > 1)
                {
                    if (ProcessPhysBoneGroup(parentGroup.Key, configGroup.Value))
                    {
                        processedGroups++;
                        processedObjects += configGroup.Value.Count;
                    }
                }
            }
        }
        
        totalProcessed = processedGroups;
        totalMerged = processedObjects;
        
        if (processedGroups > 0)
        {
            statusText = $"✅ 处理完成！合并了 {processedGroups} 组，共 {processedObjects} 个对象";
        }
        else
        {
            statusText = "⚠️ 未找到可合并的Phys Bone组";
        }
        
        Debug.Log(statusText);
        
        // 刷新UI显示
        Repaint();
    }
    
    private void AddAllChildren(GameObject parent, List<GameObject> list)
    {
        foreach (Transform child in parent.transform)
        {
            list.Add(child.gameObject);
            AddAllChildren(child.gameObject, list);
        }
    }
    
    private Dictionary<Transform, List<GameObject>> GroupObjectsByParent(List<GameObject> objects)
    {
        Dictionary<Transform, List<GameObject>> groups = new Dictionary<Transform, List<GameObject>>();
        
        foreach (GameObject obj in objects)
        {
            Transform parent = obj.transform.parent;
            if (parent == null) continue;
            
            if (!groups.ContainsKey(parent))
            {
                groups[parent] = new List<GameObject>();
            }
            
            groups[parent].Add(obj);
        }
        
        return groups;
    }
    
    private List<GameObject> FindObjectsWithPhysBones(List<GameObject> objects)
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
    
    private Dictionary<string, List<GameObject>> GroupByPhysBoneConfig(List<GameObject> objects)
    {
        Dictionary<string, List<GameObject>> groups = new Dictionary<string, List<GameObject>>();
        
        foreach (GameObject obj in objects)
        {
            Component physBone = GetPhysBoneComponent(obj);
            if (physBone == null) continue;
            
            // 获取配置哈希（包含Colliders信息）
            string configHash = GetPhysBoneConfigHash(physBone);
            
            if (!groups.ContainsKey(configHash))
            {
                groups[configHash] = new List<GameObject>();
            }
            
            groups[configHash].Add(obj);
        }
        
        return groups;
    }
    
    private bool ProcessPhysBoneGroup(Transform parentTransform, List<GameObject> objects)
    {
        if (objects.Count < 2) return false;
        
        statusText = $"正在合并 {objects.Count} 个Phys Bones...";
        Debug.Log(statusText);
        
        // 创建新的父对象
        GameObject newParent = new GameObject($"[PhysBones] {objects[0].name}_Group");
        newParent.transform.SetParent(parentTransform);
        newParent.transform.localPosition = Vector3.zero;
        newParent.transform.localRotation = Quaternion.identity;
        newParent.transform.localScale = Vector3.one;
        
        Undo.RegisterCreatedObjectUndo(newParent, "Create PhysBone Group");
        
        // 获取第一个对象的Phys Bone组件作为模板
        Component templatePhysBone = GetPhysBoneComponent(objects[0]);
        if (templatePhysBone == null)
        {
            statusText = $"警告: 对象 {objects[0].name} 没有找到Phys Bone组件";
            Debug.LogWarning(statusText);
            Undo.DestroyObjectImmediate(newParent);
            return false;
        }
        
        // 复制Phys Bone组件到新父对象
        bool componentCopied = CopyPhysBoneComponentToParent(templatePhysBone, newParent);
        
        if (!componentCopied)
        {
            statusText = $"错误: 无法复制Phys Bone组件到父对象 {newParent.name}";
            Debug.LogError(statusText);
            Undo.DestroyObjectImmediate(newParent);
            return false;
        }
        
        // 收集所有Colliders并添加到父对象
        CollectAndSetCollidersToParent(objects, newParent, templatePhysBone);
        
        // 从所有子对象中移除Phys Bone组件
        int removedCount = 0;
        foreach (GameObject obj in objects)
        {
            Component physBone = GetPhysBoneComponent(obj);
            if (physBone != null)
            {
                Undo.DestroyObjectImmediate(physBone);
                removedCount++;
            }
        }
        
        // 将对象移动到新父级下
        foreach (GameObject obj in objects)
        {
            Undo.SetTransformParent(obj.transform, newParent.transform, "Move to PhysBone Group");
            obj.transform.SetParent(newParent.transform, true);
        }
        
        // 选中新创建的对象
        Selection.activeGameObject = newParent;
        
        statusText = $"✅ 成功合并 {objects.Count} 个Phys Bones";
        Debug.Log(statusText);
        
        return true;
    }
    
    private Component GetPhysBoneComponent(GameObject obj)
    {
        if (obj == null) return null;
        
        Component[] components = obj.GetComponents<Component>();
        
        foreach (Component component in components)
        {
            if (component == null) continue;
            
            string typeName = component.GetType().Name;
            
            // 更精确地匹配VRCPhysBone组件
            // 1. 必须包含"PhysBone"或"Phys_Bone"
            // 2. 绝对不能包含"Collider"
            if ((typeName.Contains("PhysBone") || typeName.Contains("Phys_Bone")))
            {
                // 检查是否不是Collider组件
                if (!typeName.Contains("Collider"))
                {
                    return component;
                }
                else
                {
                    // 检查是否是特殊的组合情况，如"VRCPhysBoneCollider"
                    // 如果是这种类型，应该跳过
                    continue;
                }
            }
        }
        
        return null;
    }
    
    private string GetPhysBoneConfigHash(Component physBone)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        System.Type type = physBone.GetType();
        
        // 获取所有字段（包括公共和私有）
        FieldInfo[] allFields = type.GetFields(
            BindingFlags.Public | 
            BindingFlags.NonPublic | 
            BindingFlags.Instance
        );
        
        // 筛选可序列化的字段
        var serializableFields = allFields.Where(f => 
            f.IsPublic || 
            f.GetCustomAttribute<SerializeField>() != null
        ).OrderBy(f => f.Name).ToArray();
        
        // 首先检查是否有Colliders字段
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
            
            // 跳过root transform相关字段
            if (fieldName.Contains("root") && field.FieldType == typeof(Transform))
                continue;
                
            // 跳过其他transform相关字段
            if (fieldName.Contains("transform") && field.FieldType == typeof(Transform))
                continue;
            
            // 跳过已经处理过的colliders字段
            if (fieldName.Contains("colliders") || fieldName.Contains("collider"))
                continue;
            
            try
            {
                object value = field.GetValue(physBone);
                
                // 检查是否是曲线类型
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
                            // 生成曲线的简化哈希
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
    
    private string GetCollidersHash(object collidersValue)
    {
        if (collidersValue == null) return "null";
        
        // 尝试获取colliders的数量信息
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        
        // 尝试反射获取colliders列表
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
                        // 添加collider的基本信息
                        sb.Append(GetColliderInfo(collider));
                    }
                }
                return $"Count:{count}|Items:{sb}";
            }
        }
        catch { }
        
        return collidersValue.ToString();
    }
    
    private string GetColliderInfo(object collider)
    {
        if (collider == null) return "null";
        
        // 获取collider的引用信息
        if (collider is UnityEngine.Object unityObj)
        {
            return $"[{unityObj.GetInstanceID()}:{unityObj.name}]";
        }
        
        return collider.ToString();
    }
    
    private string GetSimpleCurveHash(AnimationCurve curve)
    {
        if (curve == null || curve.keys == null || curve.keys.Length == 0)
            return "empty";
        
        // 简化曲线哈希，只检查关键点数量
        return $"keys:{curve.keys.Length}";
    }
    
    private string GetValueString(object value)
    {
        if (value == null) return "null";
        
        if (value is UnityEngine.Object unityObj)
        {
            return unityObj ? $"{unityObj.GetType().Name}:{unityObj.name}" : "null";
        }
        
        return value.ToString();
    }
    
    private bool CopyPhysBoneComponentToParent(Component sourceComponent, GameObject parentObject)
    {
        System.Type componentType = sourceComponent.GetType();
        
        try
        {
            Component newComponent = parentObject.AddComponent(componentType);
            EditorUtility.CopySerialized(sourceComponent, newComponent);
            
            // 清空父对象的Colliders
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
    
    private void ClearCollidersInParent(Component physBone)
    {
        System.Type type = physBone.GetType();
        
        // 查找colliders字段
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
                    // 尝试获取当前值
                    object currentValue = field.GetValue(physBone);
                    
                    // 尝试创建一个空列表或数组
                    if (field.FieldType.IsGenericType && 
                        field.FieldType.GetGenericTypeDefinition() == typeof(List<>))
                    {
                        var emptyList = System.Activator.CreateInstance(field.FieldType);
                        field.SetValue(physBone, emptyList);
                        Debug.Log($"已清空colliders列表");
                        EditorUtility.SetDirty(physBone.gameObject);
                    }
                    else if (field.FieldType.IsArray)
                    {
                        var elementType = field.FieldType.GetElementType();
                        var emptyArray = System.Array.CreateInstance(elementType, 0);
                        field.SetValue(physBone, emptyArray);
                        Debug.Log($"已清空colliders数组");
                        EditorUtility.SetDirty(physBone.gameObject);
                    }
                    else
                    {
                        field.SetValue(physBone, null);
                        Debug.Log($"已将colliders字段设置为null");
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
    
    private void CollectAndSetCollidersToParent(List<GameObject> objects, GameObject parentObject, Component templatePhysBone)
    {
        System.Type physBoneType = templatePhysBone.GetType();
        
        // 查找colliders字段
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
        
        // 收集所有colliders
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
                    // 尝试作为集合处理
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
        
        // 设置到父对象的PhysBone
        Component parentPhysBone = GetPhysBoneComponent(parentObject);
        if (parentPhysBone != null && allColliders.Count > 0)
        {
            try
            {
                // 根据字段类型创建相应的集合
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
                    Debug.Log($"已设置 {allColliders.Count} 个colliders到父对象");
                    EditorUtility.SetDirty(parentObject);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"设置colliders失败: {e.Message}");
            }
        }
    }
    
    private bool CopyComponentUsingSerializedObject(Component source, GameObject target)
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
    
    private void SetRootTransformToNull(Component physBone)
    {
        System.Type type = physBone.GetType();
        bool foundAndCleared = false;
        
        // 首先尝试查找名为"Root Transform"或类似名称的字段
        FieldInfo[] fields = type.GetFields(
            BindingFlags.Public | 
            BindingFlags.NonPublic | 
            BindingFlags.Instance
        );
        
        foreach (FieldInfo field in fields)
        {
            string fieldName = field.Name.ToLower();
            
            // 匹配常见的root transform字段名称
            if ((fieldName.Contains("root") && field.FieldType == typeof(Transform)) ||
                (fieldName.Contains("roottransform") && field.FieldType == typeof(Transform)) ||
                (fieldName == "_roottransform" && field.FieldType == typeof(Transform)))
            {
                try
                {
                    field.SetValue(physBone, null);
                    foundAndCleared = true;
                    Debug.Log($"已将root transform字段设置为null");
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
                        Debug.Log($"已将root transform属性设置为null");
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
            Debug.LogWarning($"未找到root transform字段或属性");
        }
    }
}
#endif