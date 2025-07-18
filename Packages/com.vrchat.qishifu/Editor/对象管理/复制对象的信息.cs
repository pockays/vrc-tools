using UnityEngine;
using UnityEditor;

public class 复制对象的信息 : EditorWindow
{
    private GameObject source;
    private GameObject target;
    private int materialsCopiedCount; // 用于跟踪复制的材质球数量
    private int transformsCopiedCount; // 用于跟踪复制的变换数量
    private int componentsCopiedCount; // 用于跟踪复制的组件数量
    private int blendShapesCopiedCount; // 用于跟踪复制的形态键数量

    [MenuItem("奇师傅工具箱/工具/对象管理/复制对象的信息", false, 0)]
    public static void ShowWindow()
    {
        GetWindow<复制对象的信息>("复制对象的信息");
    }

    void OnGUI()
    {
        source = (GameObject)EditorGUILayout.ObjectField("复制源对象", source, typeof(GameObject), true);
        target = (GameObject)EditorGUILayout.ObjectField("粘贴目标对象", target, typeof(GameObject), true);

        GUILayout.Space(20);

        // 材质球复制按钮
        if (GUILayout.Button("复制材质球"))
        {
            materialsCopiedCount = 0; // 重置计数器
            Undo.RegisterCompleteObjectUndo(target, "复制材质球"); // 注册完整的撤销
            CopyMaterials();
            EditorUtility.DisplayDialog("材质球复制完成", $"成功复制了 {materialsCopiedCount} 个材质球。", "确定");
        }

        GUILayout.Space(10);

        // 变换复制按钮
        if (GUILayout.Button("复制变换"))
        {
            transformsCopiedCount = 0; // 重置计数器
            Undo.RegisterCompleteObjectUndo(target, "复制变换"); // 注册完整的撤销
            CopyTransformData();
            EditorUtility.DisplayDialog("变换复制完成", $"成功复制变换数据到 {transformsCopiedCount} 个对象。", "确定");
        }

        GUILayout.Space(10);

        // 组件复制按钮
        if (GUILayout.Button("复制组件"))
        {
            componentsCopiedCount = 0; // 重置计数器
            Undo.RegisterCompleteObjectUndo(target, "复制组件"); // 注册完整的撤销
            CopyComponents();
            EditorUtility.DisplayDialog("组件复制完成", $"成功复制了 {componentsCopiedCount} 个组件。", "确定");
        }

        GUILayout.Space(10);

        // X轴对称复制按钮
        if (GUILayout.Button("X轴对称复制变换"))
        {
            if (source == null || target == null)
            {
                Debug.LogError("未指定对象");
                return;
            }
            Undo.RegisterCompleteObjectUndo(target, "X轴对称复制"); // 注册完整的撤销
            MirrorTransformOnX();
            EditorUtility.DisplayDialog("X轴对称复制完成", "成功完成X轴对称复制。", "确定");
        }

        GUILayout.Space(10);

        // 形态键复制按钮
        if (GUILayout.Button("复制形态键"))
        {
            blendShapesCopiedCount = 0; // 重置计数器
            Undo.RegisterCompleteObjectUndo(target, "复制形态键");
            CopyBlendShapes();
            EditorUtility.DisplayDialog("形态键复制完成", $"成功复制了 {blendShapesCopiedCount} 个形态键。", "确定");
        }

        GUILayout.Space(20);

        GUILayout.Label("警告：使用工具前请对指定对象进行备份。工具是死板的，但人是灵活的。", EditorStyles.helpBox);
    }

    private void CopyMaterials()
    {
        if (source == null || target == null)
        {
            Debug.LogError("未指定对象");
            return;
        }

        // 复制根对象的材质
        CopyMaterialsFromRenderers(source, target);
        // 复制子对象的材质
        CopyAllMaterialsRecursive(source.transform, target.transform);

        Debug.Log($"材质球复制完成，共复制了 {materialsCopiedCount} 个材质球。");
    }

    private void CopyAllMaterialsRecursive(Transform sourceTransform, Transform targetTransform)
    {
        foreach (Transform sourceChild in sourceTransform)
        {
            Transform targetChild = targetTransform.Find(sourceChild.name);
            if (targetChild != null)
            {
                CopyMaterialsFromRenderers(sourceChild.gameObject, targetChild.gameObject);
                CopyAllMaterialsRecursive(sourceChild, targetChild);
            }
        }
    }

    private void CopyMaterialsFromRenderers(GameObject sourceObj, GameObject targetObj)
    {
        MeshRenderer sourceMeshRenderer = sourceObj.GetComponent<MeshRenderer>();
        SkinnedMeshRenderer sourceSkinnedMeshRenderer = sourceObj.GetComponent<SkinnedMeshRenderer>();

        MeshRenderer targetMeshRenderer = targetObj.GetComponent<MeshRenderer>();
        SkinnedMeshRenderer targetSkinnedMeshRenderer = targetObj.GetComponent<SkinnedMeshRenderer>();

        if (sourceMeshRenderer != null && targetMeshRenderer != null)
        {
            Undo.RecordObject(targetMeshRenderer, "复制材质球");
            targetMeshRenderer.sharedMaterials = sourceMeshRenderer.sharedMaterials;
            materialsCopiedCount += sourceMeshRenderer.sharedMaterials.Length;
        }

        if (sourceSkinnedMeshRenderer != null && targetSkinnedMeshRenderer != null)
        {
            Undo.RecordObject(targetSkinnedMeshRenderer, "复制材质球");
            targetSkinnedMeshRenderer.sharedMaterials = sourceSkinnedMeshRenderer.sharedMaterials;
            materialsCopiedCount += sourceSkinnedMeshRenderer.sharedMaterials.Length;
        }
    }

    private void CopyTransformData()
    {
        if (source == null || target == null)
        {
            Debug.LogError("未指定对象");
            return;
        }

        // 复制根对象的变换
        Undo.RecordObject(target.transform, "复制变换");
        target.transform.position = source.transform.position;
        target.transform.rotation = source.transform.rotation;
        target.transform.localScale = source.transform.localScale;
        transformsCopiedCount++;

        // 复制子对象的变换
        Transform[] sourceTransforms = source.GetComponentsInChildren<Transform>();
        Transform[] targetTransforms = target.GetComponentsInChildren<Transform>();

        foreach (Transform sourceTransform in sourceTransforms)
        {
            foreach (Transform targetTransform in targetTransforms)
            {
                if (sourceTransform.name == targetTransform.name && sourceTransform != source.transform)
                {
                    Undo.RecordObject(targetTransform, "复制变换");
                    targetTransform.position = sourceTransform.position;
                    targetTransform.rotation = sourceTransform.rotation;
                    targetTransform.localScale = sourceTransform.localScale;
                    transformsCopiedCount++;
                    break;
                }
            }
        }

        Debug.Log($"变换复制完成，共复制了 {transformsCopiedCount} 个对象的变换数据。");
    }

    private void CopyComponents()
    {
        if (source == null || target == null)
        {
            Debug.LogError("未指定对象");
            return;
        }

        // 复制根对象的组件
        CopyAllComponentsAsNew(source, target);
        // 复制子对象的组件
        CopyAllComponentsRecursive(source.transform, target.transform);

        Debug.Log($"组件复制成功，总共复制了 {componentsCopiedCount} 个组件。");
    }

    private void CopyAllComponentsRecursive(Transform sourceTransform, Transform targetTransform)
    {
        foreach (Transform sourceChild in sourceTransform)
        {
            Transform targetChild = targetTransform.Find(sourceChild.name);
            if (targetChild != null)
            {
                CopyAllComponentsAsNew(sourceChild.gameObject, targetChild.gameObject);
                CopyAllComponentsRecursive(sourceChild, targetChild);
            }
        }
    }

    private void CopyAllComponentsAsNew(GameObject sourceObj, GameObject targetObj)
    {
        Component[] components = sourceObj.GetComponents<Component>();
        foreach (Component sourceComp in components)
        {
            // 跳过Transform组件，因为它是每个GameObject必需的
            if (sourceComp is Transform)
                continue;

            if (UnityEditorInternal.ComponentUtility.CopyComponent(sourceComp))
            {
                // 在粘贴新组件之前注册撤销
                Undo.RegisterCompleteObjectUndo(targetObj, "复制组件");
                if (UnityEditorInternal.ComponentUtility.PasteComponentAsNew(targetObj))
                {
                    // 获取新添加的组件并记录它用于撤销
                    Component[] newComponents = targetObj.GetComponents(sourceComp.GetType());
                    if (newComponents.Length > 0)
                    {
                        Component lastAddedComponent = newComponents[newComponents.Length - 1];
                        Undo.RegisterCreatedObjectUndo(lastAddedComponent, "复制组件");
                    }
                    componentsCopiedCount++;
                }
            }
        }
    }

    private void CopyBlendShapes()
    {
        if (source == null || target == null)
        {
            Debug.LogError("未指定对象");
            return;
        }

        // 复制根对象的形态键
        CopyBlendShapesFromRenderer(source, target);
        // 复制子对象的形态键
        CopyAllBlendShapesRecursive(source.transform, target.transform);

        Debug.Log($"形态键复制完成，共复制了 {blendShapesCopiedCount} 个形态键。");
    }

    private void CopyAllBlendShapesRecursive(Transform sourceTransform, Transform targetTransform)
    {
        foreach (Transform sourceChild in sourceTransform)
        {
            Transform targetChild = targetTransform.Find(sourceChild.name);
            if (targetChild != null)
            {
                CopyBlendShapesFromRenderer(sourceChild.gameObject, targetChild.gameObject);
                CopyAllBlendShapesRecursive(sourceChild, targetChild);
            }
        }
    }

    private void CopyBlendShapesFromRenderer(GameObject sourceObj, GameObject targetObj)
    {
        SkinnedMeshRenderer sourceRenderer = sourceObj.GetComponent<SkinnedMeshRenderer>();
        SkinnedMeshRenderer targetRenderer = targetObj.GetComponent<SkinnedMeshRenderer>();

        if (sourceRenderer != null && targetRenderer != null)
        {
            Mesh sourceMesh = sourceRenderer.sharedMesh;
            Mesh targetMesh = targetRenderer.sharedMesh;

            if (sourceMesh != null && targetMesh != null)
            {
                Undo.RecordObject(targetRenderer, "复制形态键");

                // 复制所有形态键的权重
                for (int i = 0; i < sourceMesh.blendShapeCount; i++)
                {
                    string blendShapeName = sourceMesh.GetBlendShapeName(i);
                    int targetIndex = targetMesh.GetBlendShapeIndex(blendShapeName);

                    if (targetIndex != -1)
                    {
                        float weight = sourceRenderer.GetBlendShapeWeight(i);
                        targetRenderer.SetBlendShapeWeight(targetIndex, weight);
                        blendShapesCopiedCount++;
                    }
                }
            }
        }
    }

    private void MirrorTransformOnX()
    {
        // 获取源对象的世界空间变换
        Vector3 worldPosition = source.transform.position;
        Quaternion worldRotation = source.transform.rotation;
        Vector3 worldScale = source.transform.lossyScale;

        // 对X轴进行镜像处理
        worldPosition.x = -worldPosition.x;
        
        // 创建一个新的旋转，将Y和Z轴旋转反转
        Vector3 eulerAngles = worldRotation.eulerAngles;
        eulerAngles.y = -eulerAngles.y;
        eulerAngles.z = -eulerAngles.z;
        
        // 记录撤销
        Undo.RecordObject(target.transform, "X轴对称复制");

        // 应用镜像变换到目标对象
        target.transform.position = worldPosition;
        target.transform.rotation = Quaternion.Euler(eulerAngles);
        
        // 设置世界空间缩放
        if (target.transform.parent != null)
        {
            Vector3 parentWorldScale = target.transform.parent.lossyScale;
            target.transform.localScale = new Vector3(
                worldScale.x / parentWorldScale.x,
                worldScale.y / parentWorldScale.y,
                worldScale.z / parentWorldScale.z
            );
        }
        else
        {
            target.transform.localScale = worldScale;
        }
    }
}
