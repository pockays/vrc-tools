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
    /// <param name="root">迁移作用范围：只迁移 root 子树内的 PhysBone/Collider，并可清理其产生的空壳</param>
    /// <param name="referenceScope">
    /// 重建 colliders 引用的范围（默认 = root）。迁移 Collider 采用"销毁原组件 + 在 root 骨骼上新建"，
    /// 任何引用过被迁移 Collider 的 PhysBone 都必须改指向新组件，否则会留下 Missing 引用，
    /// 并被 AAO"Trace And Optimize → 自动删除未使用对象"误删。NDMF 构建时请传入整个 Avatar 根。
    /// </param>
    /// <returns>是否有任何组件被迁移</returns>
    public static bool MoveAllPhysBonesToRoot(GameObject root, out string status, out List<GameObject> migratedObjects, GameObject referenceScope = null)
    {
        status = "";
        migratedObjects = new List<GameObject>();
        if (root == null)
        {
            status = "错误: root 为 null";
            return false;
        }

        var allObjects = GetAllObjectsInHierarchy(root);
        int movedPhysBonesCount = 0;
        int movedCollidersCount = 0;
        int totalPhysBonesCount = 0;
        int totalCollidersCount = 0;
        List<Component> allPhysBones = new List<Component>();
        List<Component> allColliders = new List<Component>();
        HashSet<GameObject> migratedSet = new HashSet<GameObject>();

        // root 子树的 GameObject ID 集合：迁移动作（移动 PhysBone/Collider）只允许发生在 root 子树内
        var rootObjectIds = new HashSet<int>();
        foreach (var o in allObjects) rootObjectIds.Add(o.GetInstanceID());

        foreach (var obj in allObjects)
        {
            var physBones = GetAllPhysBoneComponents(obj);
            var colliders = GetAllPhysBoneColliderComponents(obj);
            allPhysBones.AddRange(physBones);
            allColliders.AddRange(colliders);
            totalPhysBonesCount += physBones.Length;
            totalCollidersCount += colliders.Length;
            if (physBones.Length > 0 || colliders.Length > 0)
                migratedSet.Add(obj);
        }

        migratedObjects = migratedSet.ToList();

        // ============================================================
        // 需要"重建 colliders 引用"的 PhysBone 全集（fixPhysBones）：
        //   · root 子树内的所有 PhysBone（含"本来就在 root 上、不会被迁移"的那些）
        //   · referenceScope 子树内、root 之外的 PhysBone（只修引用，不做迁移）
        // 原因：本工具迁移 Collider 采用"先销毁原组件、再在 root 骨骼上新建组件"，
        // 若只修复 root 子树内的引用，那么子树外仍引用这些 Collider 的 PhysBone 会留下
        // 指向"已销毁组件"的 Missing 引用；随后 AAO"Trace And Optimize → 自动删除未使用对象"
        // 在 BuildPhase.Optimizing 阶段扫描时，看到迁移后的碰撞球无人引用，就会把它当作
        // 未使用组件整个删除。因此插件构建时会传入整个 Avatar 根，把全身引用它的 PhysBone 都修好。
        // ============================================================
        if (referenceScope == null) referenceScope = root;
        var fixPhysBones = new List<Component>();
        if (referenceScope != root)
        {
            foreach (var obj in GetAllObjectsInHierarchy(referenceScope))
            foreach (var pb in GetAllPhysBoneComponents(obj))
                if (!fixPhysBones.Contains(pb))
                    fixPhysBones.Add(pb);
        }
        foreach (var pb in allPhysBones)
            if (!fixPhysBones.Contains(pb))
                fixPhysBones.Add(pb);

        // 快照必须发生在任何组件被销毁之前（此时引用仍有效）
        List<List<int>> physBoneColliderIds = new List<List<int>>();
        foreach (var physBone in fixPhysBones)
        {
            var ids = new List<int>();
            foreach (var c in GetColliderReferences(physBone))
                ids.Add(c.GetInstanceID());
            physBoneColliderIds.Add(ids);
        }

        // ============================================================
        // 第一步：先移动 collider，并记录 "旧 collider InstanceID -> 新 collider" 映射
        // ============================================================
        Dictionary<int, Component> colliderMap = new Dictionary<int, Component>();
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
                // 已经在 root 骨骼上，无需迁移
                continue;
            }
            if (rootTransform.IsChildOf(collider.transform))
            {
                // rootTransform 指向自身内部（子级），迁移会导致结构错乱，跳过
                Debug.LogWarning($"[PhysBone优化-迁移] {collider.gameObject.name} 的 rootTransform ({rootTransform.name}) 在其自身内部，跳过迁移");
                continue;
            }

            // 使用 AddComponent + CopySerialized 方式替代 CopyComponent/PasteComponentAsNew，
            // 确保第三方 DLL 组件（VRCPhysBoneCollider）能正确迁移
            int oldId = collider.GetInstanceID();
            var newCollider = rootTransform.gameObject.AddComponent(collider.GetType());
            EditorUtility.CopySerialized(collider, newCollider);
            // 新 collider 挂在 rootTransform 骨骼上，rootTransform 保留指向该骨骼 —— 组件挂载点==参考骨骼
            // 就是 VRChat 的"标准写法"，和"碰撞球本来就直接放在 root 骨骼上"的情形完全一致，位置不会变。
            // 千万不要把 rootTransform 置 null：置 null 会让碰撞球失去参考系（AAO 依赖 rootTransform
            // 记录它、运行时 gizmo 也依赖它），既可能"看不见"，也更容易被 AAO 的未使用对象清理误删。
            Undo.DestroyObjectImmediate(collider);
            colliderMap[oldId] = newCollider;
            movedCollidersCount++;
        }

        // ============================================================
        // 第二步：移动 physBone（仅 root 子树内、且确实需要迁移的），
        // 并对 fixPhysBones 里的每一个 PhysBone（无论是否迁移）重建 colliders 引用
        // ============================================================
        for (int i = 0; i < fixPhysBones.Count; i++)
        {
            var physBone = fixPhysBones[i];
            if (physBone == null) continue;
            Component survivingComponent = physBone;

            // 只有 root 子树内的 PhysBone 才允许迁移；
            // 子树外的（referenceScope 引入的引用者）只修引用，绝不动它的位置
            if (rootObjectIds.Contains(physBone.gameObject.GetInstanceID()))
            {
                var rootTransform = GetRootTransformField(physBone);
                if (rootTransform == null)
                {
                    Debug.LogWarning($"[PhysBone优化-迁移] {physBone.gameObject.name} 上的 PhysBone rootTransform 为 null，跳过迁移");
                }
                else if (rootTransform == physBone.gameObject.transform)
                {
                    // PhysBone 已经在 root 骨骼上，无需迁移（但仍需重建其 colliders 引用）
                }
                else if (rootTransform.IsChildOf(physBone.transform))
                {
                    // rootTransform 指向自身内部（子级），迁移会导致结构错乱，跳过
                    Debug.LogWarning($"[PhysBone优化-迁移] {physBone.gameObject.name} 的 rootTransform ({rootTransform.name}) 在其自身内部，跳过迁移");
                }
                else
                {
                    // 使用 AddComponent + CopySerialized 方式替代 CopyComponent/PasteComponentAsNew，
                    // 确保第三方 DLL 组件（VRCPhysBone、VRCPhysBoneCollider）能正确迁移
                    var newComponent = rootTransform.gameObject.AddComponent(physBone.GetType());
                    EditorUtility.CopySerialized(physBone, newComponent);
                    Undo.DestroyObjectImmediate(physBone);
                    survivingComponent = newComponent;
                    fixPhysBones[i] = newComponent;
                    movedPhysBonesCount++;
                }
            }

            // 根据"移动前的 InstanceID 快照 + 旧→新 Collider 映射"重建 colliders 引用。
            // 迁移过的 PhysBone 修复到新组件上；原位保留的 PhysBone 也必须一并修复——
            // 它们如果引用了被迁移的 Collider，此刻仍指向已销毁旧组件的 Missing 引用。
            // 这一步是防止 AAO 删除碰撞球的关键：必须让"迁移后的碰撞球"依然被 PhysBone 引用着。
            RebuildColliderReferences(survivingComponent, physBoneColliderIds[i], colliderMap);
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
    /// 删除迁移后产生的空壳对象。
    /// 只删除"曾含 PhysBone/Collider"的对象及其向上合并的纯空节点，
    /// 且仅当对象上除 Transform 外没有任何其它组件（保护网格、碰撞体、约束、动画等）。
    /// 不会删除传入的 root。
    /// </summary>
    public static int DeleteEmptyShells(GameObject root, List<GameObject> migratedObjects, out string status)
    {
        status = "";
        if (root == null)
        {
            status = "错误: root 为 null";
            return 0;
        }

        // 候选：曾含 PhysBone/Collider 的对象 + 它们到 root 的所有祖先
        var candidates = new HashSet<GameObject>();
        if (migratedObjects != null)
        {
            foreach (var o in migratedObjects)
            {
                if (o == null || o == root) continue;
                var cur = o.transform;
                while (cur != null && cur.gameObject != root)
                {
                    candidates.Add(cur.gameObject);
                    cur = cur.parent;
                }
            }
        }

        // 从深到浅处理（先删叶子，删光子对象后再判断父对象是否成空壳）
        var sorted = new List<GameObject>(candidates);
        sorted.Sort((a, b) => GetHierarchyDepth(b).CompareTo(GetHierarchyDepth(a)));

        // 预先收集"被引用"的对象 ID：被其它组件（骨骼、约束、碰撞体等）或动画引用的对象不能当空壳删除
        var referencedIds = CollectReferencedGameObjectIds(root);

        int deletedCount = 0;
        foreach (var obj in sorted)
        {
            if (obj == null || obj == root) continue;
            if (!IsEmptyShell(obj)) continue;                          // 有组件（网格、渲染器等）→ 不是空壳
            if (obj.transform.childCount > 0) continue;                // 还有子对象 → 有实质内容
            if (referencedIds.Contains(obj.GetInstanceID())) continue; // 被引用（骨骼/动画/约束等）→ 不能删

            Undo.DestroyObjectImmediate(obj);
            deletedCount++;
        }

        status = $"已删除 {deletedCount} 个空壳对象";
        return deletedCount;
    }

    /// <summary>
    /// 判断 GameObject 是否为"空壳"：除 Transform 外没有任何其它组件。
    /// </summary>
    private static bool IsEmptyShell(GameObject obj)
    {
        if (obj == null) return false;
        return obj.GetComponents<Component>().Length <= 1;
    }

    /// <summary>
    /// 收集 root 子树内所有"被引用"的 GameObject InstanceID。
    /// 引用来源：1) 任意组件的序列化对象引用（SkinnedMeshRenderer 的 bones/rootBone、
    /// 约束的 source、PhysBone 的 rootTransform/colliders、碰撞体等）；
    /// 2) Animator/Animation 动画 clip 的绑定路径。
    /// 这些对象即使除 Transform 外没有任何组件、也没有子对象，也不能当作空壳删除。
    /// </summary>
    private static HashSet<int> CollectReferencedGameObjectIds(GameObject root)
    {
        var referenced = new HashSet<int>();

        // 1) 组件对象引用
        foreach (var comp in root.GetComponentsInChildren<Component>(true))
        {
            if (comp == null) continue;
            if (comp is Transform) continue; // 父子关系由 childCount 判断，排除 m_Father/m_Children 干扰

            SerializedObject so;
            try { so = new SerializedObject(comp); }
            catch { continue; }

            var it = so.GetIterator();
            while (it.NextVisible(true))
            {
                if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                var refObj = it.objectReferenceValue;
                if (refObj == null) continue;

                var refGo = refObj as GameObject ?? (refObj as Component)?.gameObject;
                if (refGo != null)
                    referenced.Add(refGo.GetInstanceID());
            }
        }

        // 2) 动画绑定路径（失败时降级为仅组件引用，不影响删除流程）
        try
        {
            foreach (var animator in root.GetComponentsInChildren<Animator>(true))
            {
                if (animator == null || animator.runtimeAnimatorController == null) continue;
                AddAnimatedPathIds(animator.transform, animator.runtimeAnimatorController.animationClips, referenced);
            }
            foreach (var animation in root.GetComponentsInChildren<Animation>(true))
            {
                if (animation == null) continue;
                AddAnimatedPathIds(animation.transform, AnimationUtility.GetAnimationClips(animation.gameObject), referenced);
            }
        }
        catch { }

        return referenced;
    }

    /// <summary>
    /// 解析动画 clip 的绑定路径，把被动画的对象加入 referenced 集合。
    /// binding.path 相对其 Animator/Animation 的根，空路径表示动画作用于该根自身。
    /// </summary>
    private static void AddAnimatedPathIds(Transform animatedRoot, AnimationClip[] clips, HashSet<int> referenced)
    {
        if (clips == null) return;
        foreach (var clip in clips)
        {
            if (clip == null) continue;
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                var target = string.IsNullOrEmpty(binding.path)
                    ? animatedRoot
                    : animatedRoot.Find(binding.path);
                if (target != null)
                    referenced.Add(target.gameObject.GetInstanceID());
            }
        }
    }

    /// <summary>
    /// 计算 GameObject 在层级中的深度（到场景根的距离）。
    /// </summary>
    private static int GetHierarchyDepth(GameObject obj)
    {
        int depth = 0;
        Transform t = obj.transform;
        while (t != null)
        {
            depth++;
            t = t.parent;
        }
        return depth;
    }

    /// <summary>
    /// 读取 PhysBone 上 colliders 字段中所有非空的 UnityEngine.Object 引用。
    /// 必须在任何 collider 被销毁之前调用（引用仍有效时）。
    /// </summary>
    private static List<UnityEngine.Object> GetColliderReferences(Component physBone)
    {
        var result = new List<UnityEngine.Object>();
        if (physBone == null) return result;

        object collidersValue = GetCollidersValue(physBone);
        if (collidersValue == null) return result;

        var enumerable = collidersValue as System.Collections.IEnumerable;
        if (enumerable == null) return result;

        foreach (var collider in enumerable)
        {
            if (collider is UnityEngine.Object unityCollider && unityCollider != null)
                result.Add(unityCollider);
        }
        return result;
    }

    /// <summary>
    /// 根据移动前的 colliders InstanceID 快照与 "旧 collider -> 新 collider" 映射，
    /// 重建 PhysBone 的 colliders 字段，修复迁移后产生的 Missing 引用。
    /// </summary>
    private static void RebuildColliderReferences(Component physBone, List<int> snapshotIds, Dictionary<int, Component> colliderMap)
    {
        if (physBone == null || snapshotIds == null || snapshotIds.Count == 0) return;

        var rebuilt = new List<UnityEngine.Object>();
        foreach (int oldId in snapshotIds)
        {
            Component mapped;
            if (colliderMap.TryGetValue(oldId, out mapped) && mapped != null)
            {
                // 被移动的 collider：使用迁移后的新组件
                rebuilt.Add(mapped);
            }
            else
            {
                // 未被移动的 collider（不在源对象内）：原组件仍存活，按 InstanceID 找回引用
                UnityEngine.Object stillAlive = EditorUtility.InstanceIDToObject(oldId);
                if (stillAlive != null)
                    rebuilt.Add(stillAlive);
            }
        }

        SetCollidersValue(physBone, rebuilt);
    }

    /// <summary>
    /// 读取 PhysBone 上 colliders 字段/属性的当前值。
    /// </summary>
    private static object GetCollidersValue(Component physBone)
    {
        if (physBone == null) return null;

        System.Type type = physBone.GetType();

        FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (FieldInfo field in fields)
        {
            if (field.Name.ToLower().Contains("collider"))
                return field.GetValue(physBone);
        }

        PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (PropertyInfo property in properties)
        {
            if (property.DeclaringType == typeof(UnityEngine.Component))
                continue;
            if (property.Name.ToLower().Contains("collider") && property.CanRead)
                return property.GetValue(physBone, null);
        }

        return null;
    }

    /// <summary>
    /// 将 colliders 集合写回 PhysBone 的 colliders 字段/属性（支持 List&lt;&gt; 与数组类型）。
    /// </summary>
    private static void SetCollidersValue(Component physBone, List<UnityEngine.Object> colliders)
    {
        if (physBone == null) return;

        System.Type type = physBone.GetType();

        FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (FieldInfo field in fields)
        {
            if (!field.Name.ToLower().Contains("collider")) continue;

            Type fieldType = field.FieldType;
            object colliderCollection;

            if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>))
            {
                Type elementType = fieldType.GetGenericArguments()[0];
                colliderCollection = System.Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType));
                var addMethod = colliderCollection.GetType().GetMethod("Add");
                foreach (var collider in colliders)
                    addMethod.Invoke(colliderCollection, new object[] { collider });
            }
            else if (fieldType.IsArray)
            {
                Type elementType = fieldType.GetElementType();
                var array = Array.CreateInstance(elementType, colliders.Count);
                for (int i = 0; i < colliders.Count; i++)
                    array.SetValue(colliders[i], i);
                colliderCollection = array;
            }
            else
            {
                Debug.LogWarning($"不支持的colliders字段类型: {fieldType}");
                return;
            }

            field.SetValue(physBone, colliderCollection);
            EditorUtility.SetDirty(physBone.gameObject);
            return;
        }

        PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (PropertyInfo property in properties)
        {
            if (property.DeclaringType == typeof(UnityEngine.Component))
                continue;
            if (!property.Name.ToLower().Contains("collider") || !property.CanWrite) continue;

            Type propType = property.PropertyType;
            if (propType.IsGenericType && propType.GetGenericTypeDefinition() == typeof(List<>))
            {
                Type elementType = propType.GetGenericArguments()[0];
                object colliderCollection = System.Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType));
                var addMethod = colliderCollection.GetType().GetMethod("Add");
                foreach (var collider in colliders)
                    addMethod.Invoke(colliderCollection, new object[] { collider });

                property.SetValue(physBone, colliderCollection, null);
                EditorUtility.SetDirty(physBone.gameObject);
                return;
            }
            else
            {
                Debug.LogWarning($"不支持的colliders属性类型: {propType}");
            }
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
                // 跳过 UnityEngine.Component 基类上已弃用的 collider 属性
                if (property.DeclaringType == typeof(UnityEngine.Component))
                    continue;
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
