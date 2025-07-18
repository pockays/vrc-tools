using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public class 将动画应用 : EditorWindow
{
    private AnimationClip animClip;
    private Animator targetAnimator;
    private int frameNumber = 0;
    private bool showHelp = true;

    [MenuItem("奇师傅工具箱/工具/动画/将动画应用", false, 0)]
    public static void ShowWindow()
    {
        GetWindow<将动画应用>("将动画应用");
    }

    private void OnGUI()
    {
        // 显示帮助信息
        if (showHelp)
        {
            EditorGUILayout.HelpBox("1. 选择一个动画片段\n2. 选择一个Animator\n3. 输入帧数\n4. 点击应用", MessageType.Info);
        }

        // 动画片段选择
        animClip = (AnimationClip)EditorGUILayout.ObjectField("动画片段", animClip, typeof(AnimationClip), false);

        // Animator选择
        targetAnimator = (Animator)EditorGUILayout.ObjectField("目标Animator", targetAnimator, typeof(Animator), true);

        // 帧数输入
        frameNumber = EditorGUILayout.IntField("帧数", frameNumber);

        EditorGUILayout.BeginHorizontal();
        {
            // 应用按钮
            if (GUILayout.Button("应用", GUILayout.Height(30)))
            {
                if (animClip != null && targetAnimator != null)
                {
                    ApplyAnimationAtFrame();
                }
                else
                {
                    Debug.LogWarning("请先选择动画片段和目标Animator");
                }
            }

            // 仅应用形态键按钮
            if (GUILayout.Button("仅应用形态键", GUILayout.Height(30)))
            {
                if (animClip != null && targetAnimator != null)
                {
                    ApplyBlendShapesOnly();
                }
                else
                {
                    Debug.LogWarning("请先选择动画片段和目标Animator");
                }
            }
        }
        EditorGUILayout.EndHorizontal();
    }

    private void HandleTransformProperty(EditorCurveBinding binding, Transform transform, float value)
    {
        if (transform == null) return;

        switch (binding.propertyName)
        {
            // 位置
            case "m_LocalPosition.x":
                transform.localPosition = new Vector3(value, transform.localPosition.y, transform.localPosition.z);
                break;
            case "m_LocalPosition.y":
                transform.localPosition = new Vector3(transform.localPosition.x, value, transform.localPosition.z);
                break;
            case "m_LocalPosition.z":
                transform.localPosition = new Vector3(transform.localPosition.x, transform.localPosition.y, value);
                break;

            // 旋转
            case "localEulerAnglesRaw.x":
                Vector3 rotationX = transform.localEulerAngles;
                rotationX.x = value;
                transform.localEulerAngles = rotationX;
                break;
            case "localEulerAnglesRaw.y":
                Vector3 rotationY = transform.localEulerAngles;
                rotationY.y = value;
                transform.localEulerAngles = rotationY;
                break;
            case "localEulerAnglesRaw.z":
                Vector3 rotationZ = transform.localEulerAngles;
                rotationZ.z = value;
                transform.localEulerAngles = rotationZ;
                break;

            // 缩放
            case "m_LocalScale.x":
                transform.localScale = new Vector3(value, transform.localScale.y, transform.localScale.z);
                break;
            case "m_LocalScale.y":
                transform.localScale = new Vector3(transform.localScale.x, value, transform.localScale.z);
                break;
            case "m_LocalScale.z":
                transform.localScale = new Vector3(transform.localScale.x, transform.localScale.y, value);
                break;
        }
    }

    private void HandleSkinnedMeshProperty(EditorCurveBinding binding, SkinnedMeshRenderer skinnedMesh, float value)
    {
        if (skinnedMesh == null) return;

        // 处理BlendShape权重
        if (binding.propertyName.StartsWith("blendShape."))
        {
            string blendShapeName = binding.propertyName.Substring("blendShape.".Length);
            int index = skinnedMesh.sharedMesh.GetBlendShapeIndex(blendShapeName);
            if (index != -1)
            {
                skinnedMesh.SetBlendShapeWeight(index, value);
            }
        }
    }

    private void HandleMaterialProperty(EditorCurveBinding binding, Material material, float value)
    {
        if (material == null) return;

        string propertyName = binding.propertyName;
        if (propertyName.StartsWith("material."))
        {
            propertyName = propertyName.Substring("material.".Length);

            // 处理颜色属性
            if (binding.propertyName.EndsWith(".r") || 
                binding.propertyName.EndsWith(".g") || 
                binding.propertyName.EndsWith(".b") || 
                binding.propertyName.EndsWith(".a"))
            {
                string colorPropertyName = propertyName.Substring(0, propertyName.Length - 2); // 移除.r/.g/.b/.a
                if (material.HasProperty(colorPropertyName))
                {
                    Color currentColor = material.GetColor(colorPropertyName);
                    if (binding.propertyName.EndsWith(".r")) currentColor.r = value;
                    else if (binding.propertyName.EndsWith(".g")) currentColor.g = value;
                    else if (binding.propertyName.EndsWith(".b")) currentColor.b = value;
                    else if (binding.propertyName.EndsWith(".a")) currentColor.a = value;
                    material.SetColor(colorPropertyName, currentColor);
                }
            }
            // 处理浮点数属性
            else if (material.HasProperty(propertyName))
            {
                material.SetFloat(propertyName, value);
            }
        }
    }

    private void ApplyBlendShapesOnly()
    {
        // 开始记录撤销组
        Undo.IncrementCurrentGroup();

        // 计算时间
        float time = frameNumber / animClip.frameRate;

        // 只处理SkinnedMeshRenderer的BlendShape曲线
        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(animClip))
        {
            // 只处理SkinnedMeshRenderer类型且是BlendShape属性的曲线
            if (binding.type == typeof(SkinnedMeshRenderer) && binding.propertyName.StartsWith("blendShape."))
            {
                AnimationCurve curve = AnimationUtility.GetEditorCurve(animClip, binding);
                float value = curve.Evaluate(time);

                Object target = AnimationUtility.GetAnimatedObject(targetAnimator.gameObject, binding);
                if (target == null) continue;

                SkinnedMeshRenderer skinnedMesh = target as SkinnedMeshRenderer;
                if (skinnedMesh != null)
                {
                    // 记录要修改的对象状态
                    Undo.RecordObject(skinnedMesh, "Apply BlendShape Value");
                    HandleSkinnedMeshProperty(binding, skinnedMesh, value);
                }
            }
        }

        // 标记场景已修改
        EditorSceneManager.MarkAllScenesDirty();

        // 设置撤销组名称
        Undo.SetCurrentGroupName("Apply BlendShapes");

        Debug.Log($"成功应用第 {frameNumber} 帧的形态键状态");
    }

    // 通用属性设置
    private void ApplyPropertyValue(Object target, string propertyPath, float value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyPath);
        if (property != null)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Float:
                    property.floatValue = value;
                    break;
                case SerializedPropertyType.Integer:
                    property.intValue = Mathf.RoundToInt(value);
                    break;
                case SerializedPropertyType.Boolean:
                    property.boolValue = value > 0.5f;
                    break;
            }
            serializedObject.ApplyModifiedProperties();
        }
    }

    // 获取材质实例，避免修改共享材质
    private Material GetMaterialInstance(Material material)
    {
        if (material == null) return null;

        // 检查是否是共享材质
        Renderer[] renderers = targetAnimator.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == material)
                {
                    // 创建材质实例并记录修改
                    materials[i] = new Material(material);
                    Undo.RecordObject(renderer, "Create Material Instance");
                    renderer.sharedMaterials = materials;
                    return materials[i];
                }
            }
        }
        return material;
    }

    private void ApplyAnimationAtFrame()
    {
        // 开始记录撤销组
        Undo.IncrementCurrentGroup();

        // 计算时间
        float time = frameNumber / animClip.frameRate;

        // 预先记录所有可能被修改的对象状态
        var allBindings = AnimationUtility.GetCurveBindings(animClip);
        foreach (var binding in allBindings)
        {
            Object target = AnimationUtility.GetAnimatedObject(targetAnimator.gameObject, binding);
            if (target != null)
            {
                Undo.RecordObject(target, "Apply Animation Frame");
            }
        }

        // 处理浮点数曲线
        foreach (EditorCurveBinding binding in allBindings)
        {
            AnimationCurve curve = AnimationUtility.GetEditorCurve(animClip, binding);
            float value = curve.Evaluate(time);

            Object target = AnimationUtility.GetAnimatedObject(targetAnimator.gameObject, binding);
            if (target == null) continue;

            if (binding.type == typeof(Transform))
            {
                HandleTransformProperty(binding, target as Transform, value);
            }
            else if (binding.type == typeof(SkinnedMeshRenderer))
            {
                HandleSkinnedMeshProperty(binding, target as SkinnedMeshRenderer, value);
            }
            else if (target is Material material)
            {
                // 处理材质属性
                Material materialInstance = GetMaterialInstance(material);
                if (materialInstance != null)
                {
                    Undo.RecordObject(materialInstance, "Apply Animation Material");
                    HandleMaterialProperty(binding, materialInstance, value);
                }
            }
            else
            {
                ApplyPropertyValue(target, binding.propertyName, value);
            }
        }

        // 处理对象引用
        foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(animClip))
        {
            Object target = AnimationUtility.GetAnimatedObject(targetAnimator.gameObject, binding);
            if (target == null) continue;

            // 获取对象引用曲线
            ObjectReferenceKeyframe[] keyframes = AnimationUtility.GetObjectReferenceCurve(animClip, binding);
            if (keyframes == null || keyframes.Length == 0) continue;

            // 找到当前时间对应的关键帧
            Object referenceValue = null;
            for (int i = keyframes.Length - 1; i >= 0; i--)
            {
                if (keyframes[i].time <= time)
                {
                    referenceValue = keyframes[i].value;
                    break;
                }
            }

            // 记录对象状态并应用值
            Undo.RecordObject(target, "Apply Animation Reference");
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(binding.propertyName);
            if (property != null)
            {
                property.objectReferenceValue = referenceValue;
                serializedObject.ApplyModifiedProperties();
            }
        }

        // 标记场景已修改
        EditorSceneManager.MarkAllScenesDirty();

        // 设置撤销组名称
        Undo.SetCurrentGroupName("Apply Animation Frame");

        Debug.Log($"成功应用第 {frameNumber} 帧的状态");
    }
}
