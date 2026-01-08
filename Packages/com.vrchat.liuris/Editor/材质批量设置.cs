using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Reflection;

public class LilToonBatchMaterialEditor : EditorWindow
{
    private Vector2 scrollPosition;
    private List<Material> selectedMaterials = new List<Material>();
    private Dictionary<Material, bool> materialSelection = new Dictionary<Material, bool>();
    private Dictionary<string, bool> propertySelection = new Dictionary<string, bool>();
    private Dictionary<string, object> propertyValues = new Dictionary<string, object>();
    
    // lilToon 属性分类
    private Dictionary<string, List<string>> propertyCategories = new Dictionary<string, List<string>>();
    private Dictionary<string, bool> categoryFoldoutStates = new Dictionary<string, bool>();
    
    // 撤回系统
    private Stack<Dictionary<Material, Dictionary<string, object>>> undoStack = new Stack<Dictionary<Material, Dictionary<string, object>>>();
    private Stack<Dictionary<Material, Dictionary<string, object>>> redoStack = new Stack<Dictionary<Material, Dictionary<string, object>>>();
    
    // 渲染模式
    private string[] renderModes = new string[] { "Opaque", "Cutout", "Transparent", "Fur", "FurCutout", "Gem" };
    private int selectedRenderMode = 0;
    
    // 溶解模式选项
    private string[] dissolveTypeOptions = new string[] { "None", "Alpha", "UV", "Position" };
    private string[] dissolveShapeOptions = new string[] { "Point", "Line" };
    
    // 溶解效果相关的临时数据
    private Vector2 dissolveMaskTiling = new Vector2(1, 1);
    private Vector2 dissolveMaskOffset = new Vector2(0, 0);
    private Vector2 dissolveNoiseMaskTiling = new Vector2(1, 1);
    private Vector2 dissolveNoiseMaskOffset = new Vector2(0, 0);
    private Vector2 dissolveNoiseMaskScroll = new Vector2(0, 0);
    private bool dissolveHDR = true;
    private string dissolveColorHex = "EFFEFF";
    
    // 窗口锁定状态
    private bool isWindowLocked = false;
    private Texture2D lockIcon;
    private Texture2D unlockIcon;
    
    // lilToon 属性配置
    private class LilProperty
    {
        public string name;
        public string displayName;
        public ShaderUtil.ShaderPropertyType type;
        public float min;
        public float max;
        public bool isToggle;
        public string category;
        public bool isRenderSetting;
    }
    
    private List<LilProperty> lilProperties = new List<LilProperty>();

    [MenuItem("Tools/lilToon 批量材质编辑器")]
    public static void ShowWindow()
    {
        GetWindow<LilToonBatchMaterialEditor>("lilToon 批量编辑");
    }

    private void OnEnable()
    {
        InitializePropertyCategories();
        LoadIcons();
        RefreshSelection();
    }

    private void LoadIcons()
    {
        // 加载锁定和解锁图标
        lockIcon = EditorGUIUtility.IconContent("LockIcon-On").image as Texture2D;
        unlockIcon = EditorGUIUtility.IconContent("LockIcon").image as Texture2D;
        
        // 如果内置图标不可用，使用备用方案
        if (lockIcon == null)
        {
            // 可以创建简单的纹理作为备用
            lockIcon = new Texture2D(16, 16);
            unlockIcon = new Texture2D(16, 16);
        }
    }

    private void OnSelectionChange()
    {
        // 如果窗口被锁定，不响应选择变化
        if (isWindowLocked) return;
        
        RefreshSelection();
        Repaint();
    }

    private void InitializePropertyCategories()
    {
        // 基于 lilToon 官方编辑器的分类结构
        propertyCategories = new Dictionary<string, List<string>>
        {
            {"基本设置", new List<string>()},
            {"渲染设置", new List<string>()},
            {"主颜色", new List<string>()},
            {"阴影", new List<string>()},
            {"发射光", new List<string>()},
            {"法线贴图", new List<string>()},
            {"反射", new List<string>()},
            {"MatCap", new List<string>()},
            {"边缘光", new List<string>()},
            {"轮廓", new List<string>()},
            {"高级效果", new List<string>()},
            {"溶解效果", new List<string>()}
        };

        foreach (var category in propertyCategories.Keys)
        {
            categoryFoldoutStates[category] = true;
        }
    }

    private void RefreshSelection()
    {
        // 如果窗口被锁定，不刷新选择
        if (isWindowLocked) return;
        
        selectedMaterials.Clear();
        materialSelection.Clear();
        propertySelection.Clear();
        propertyValues.Clear();

        // 获取选中的所有材质
        var selectedObjects = Selection.GetFiltered<GameObject>(SelectionMode.Editable | SelectionMode.ExcludePrefab);
        foreach (var obj in selectedObjects)
        {
            CollectMaterialsFromObject(obj);
        }

        // 只保留 lilToon 材质
        selectedMaterials = selectedMaterials
            .Where(m => m != null && IsLilToonShader(m.shader))
            .Distinct()
            .OrderBy(m => m.name)
            .ToList();

        // 初始化选择状态
        foreach (var material in selectedMaterials)
        {
            materialSelection[material] = false;
        }

        // 如果有材质，加载第一个的属性
        if (selectedMaterials.Count > 0)
        {
            LoadMaterialProperties(selectedMaterials[0]);
            AnalyzeLilToonProperties(selectedMaterials[0]);
            UpdateRenderModeFromMaterial(selectedMaterials[0]);
        }
    }

    private bool IsLilToonShader(Shader shader)
    {
        return shader != null && shader.name.ToLower().Contains("liltoon");
    }

    private void CollectMaterialsFromObject(GameObject obj)
    {
        if (obj == null) return;

        // 检查所有渲染器组件
        var renderers = obj.GetComponentsInChildren<Renderer>(true);
        foreach (var renderer in renderers)
        {
            if (renderer == null) continue;
            
            foreach (var material in renderer.sharedMaterials)
            {
                if (material != null && !selectedMaterials.Contains(material))
                {
                    selectedMaterials.Add(material);
                }
            }
        }
    }

    private void AnalyzeLilToonProperties(Material material)
    {
        lilProperties.Clear();
        
        if (material == null || material.shader == null) return;

        Shader shader = material.shader;
        int propertyCount = ShaderUtil.GetPropertyCount(shader);

        for (int i = 0; i < propertyCount; i++)
        {
            var property = new LilProperty
            {
                name = ShaderUtil.GetPropertyName(shader, i),
                type = ShaderUtil.GetPropertyType(shader, i),
                displayName = GetPropertyDisplayName(ShaderUtil.GetPropertyName(shader, i))
            };

            // 设置范围
            if (property.type == ShaderUtil.ShaderPropertyType.Range)
            {
                property.min = ShaderUtil.GetRangeLimits(shader, i, 1);
                property.max = ShaderUtil.GetRangeLimits(shader, i, 2);
            }

            // 判断是否是开关属性
            property.isToggle = property.name.StartsWith("_Use") || 
                               property.name.StartsWith("_Enable") || 
                               property.name.Contains("Toggle");

            // 判断是否是渲染设置
            property.isRenderSetting = property.name.Contains("Blend") || 
                                      property.name.Contains("Src") || 
                                      property.name.Contains("Dst") ||
                                      property.name == "_ZWrite" || 
                                      property.name == "_ZTest" ||
                                      property.name == "_Cull";

            // 分类属性
            property.category = CategorizeProperty(property.name);

            lilProperties.Add(property);
        }
    }

    private string GetPropertyDisplayName(string propertyName)
    {
        // lilToon 属性显示名称映射
        var displayNames = new Dictionary<string, string>
        {
            // 基本设置
            {"_Cutoff", "Cutoff"},
            {"_Cull", "Cull Mode"},
            {"_SubpassCutoff", "Subpass Cutoff"},
            {"_FlipNormal", "翻转背面法线"},
            {"_BackfaceForceShadow", "背面阴影力量"},
            {"_BackfaceColor", "背面颜色"},
            {"_Invisible", "不可见"},
            {"_ZWrite", "深度写入"},
            {"_ZTest", "深度测试"},
            {"_AlphaToMask", "AlphaToMask"},
            
            // 渲染设置
            {"_SrcBlend", "源混合模式"},
            {"_DstBlend", "目标混合模式"},
            {"_BlendOp", "混合操作"},
            {"_SrcBlendAlpha", "源Alpha混合"},
            {"_DstBlendAlpha", "目标Alpha混合"},
            {"_BlendOpAlpha", "Alpha混合操作"},
            
            // 主颜色
            {"_Color", "主颜色"},
            {"_MainTex", "主纹理"},
            {"_MainTex_ScrollRotate", "UV动画"},
            {"_MainTexHSVG", "色调校正"},
            
            // 阴影
            {"_UseShadow", "使用阴影"},
            {"_ShadowColor", "阴影颜色"},
            {"_ShadowBorder", "阴影边界"},
            {"_ShadowBlur", "阴影模糊"},
            {"_ShadowStrength", "阴影强度"},
            {"_ShadowReceive", "接收阴影"},
            {"_ShadowEnvStrength", "阴影环境强度"},
            
            // 发射光
            {"_UseEmission", "使用发射光"},
            {"_EmissionColor", "发射光颜色"},
            {"_EmissionMap", "发射光纹理"},
            {"_EmissionBlink", "闪烁"},
            {"_EmissionUseGrad", "使用渐变"},
            
            // 法线贴图
            {"_UseBumpMap", "使用法线贴图"},
            {"_BumpMap", "法线贴图"},
            {"_BumpScale", "法线强度"},
            
            // 反射
            {"_UseReflection", "使用反射"},
            {"_Smoothness", "平滑度"},
            {"_Metallic", "金属度"},
            {"_ReflectionColor", "反射颜色"},
            
            // MatCap
            {"_UseMatCap", "使用MatCap"},
            {"_MatCapColor", "MatCap颜色"},
            {"_MatCapTex", "MatCap纹理"},
            {"_MatCapBlend", "混合"},
            
            // 边缘光
            {"_UseRim", "使用边缘光"},
            {"_RimColor", "边缘光颜色"},
            {"_RimBorder", "边缘光边界"},
            {"_RimBlur", "边缘光模糊"},
            {"_RimFresnelPower", "菲涅尔折射率"},
            
            // 轮廓
            {"_UseOutline", "使用轮廓"},
            {"_OutlineColor", "轮廓颜色"},
            {"_OutlineWidth", "轮廓宽度"},
            {"_OutlineTex", "轮廓纹理"},
            
            // 高级效果
            {"_UseParallax", "使用视差"},
            {"_UseAudioLink", "使用AudioLink"},
            
            // 溶解效果
            {"_UseDissolve", "使用溶解"},
            {"_DissolveMask", "溶解遮罩"},
            {"_DissolveNoiseMask", "溶解噪波遮罩"},
            {"_DissolveNoiseStrength", "溶解噪波强度"},
            {"_DissolveColor", "溶解颜色"},
            {"_DissolveParams", "溶解参数"},
            {"_DissolvePos", "溶解位置"},
            {"_DissolveWidth", "溶解宽度"},
            {"_DissolveEdge", "溶解边缘"},
            {"_DissolveBlur", "溶解模糊"}
        };

        return displayNames.ContainsKey(propertyName) ? displayNames[propertyName] : ObjectNames.NicifyVariableName(propertyName);
    }

    private string CategorizeProperty(string propertyName)
    {
        if (propertyName.StartsWith("_Use") || propertyName == "_Cutoff" || propertyName == "_Cull" || 
            propertyName == "_Invisible" || propertyName == "_AlphaToMask" || propertyName == "_FlipNormal")
            return "基本设置";
        
        if (propertyName.Contains("Blend") || propertyName.Contains("Src") || propertyName.Contains("Dst") ||
            propertyName == "_ZWrite" || propertyName == "_ZTest")
            return "渲染设置";
            
        if (propertyName == "_Color" || propertyName.StartsWith("_MainTex") || propertyName.Contains("Main2nd") || propertyName.Contains("Main3rd"))
            return "主颜色";
            
        if (propertyName.StartsWith("_Shadow") || propertyName == "_UseShadow")
            return "阴影";
            
        if (propertyName.StartsWith("_Emission") || propertyName == "_UseEmission")
            return "发射光";
            
        if (propertyName.StartsWith("_Bump") || propertyName == "_UseBumpMap")
            return "法线贴图";
            
        if (propertyName.StartsWith("_Reflection") || propertyName == "_UseReflection" || 
            propertyName == "_Smoothness" || propertyName == "_Metallic")
            return "反射";
            
        if (propertyName.StartsWith("_MatCap") || propertyName == "_UseMatCap")
            return "MatCap";
            
        if (propertyName.StartsWith("_Rim") || propertyName == "_UseRim")
            return "边缘光";
            
        if (propertyName.StartsWith("_Outline") || propertyName == "_UseOutline")
            return "轮廓";
            
        if (propertyName.StartsWith("_UseParallax") || propertyName.StartsWith("_UseAudioLink"))
            return "高级效果";
            
        if (propertyName.StartsWith("_Dissolve") || propertyName == "_UseDissolve")
            return "溶解效果";
            
        return "基本设置";
    }

private void LoadMaterialProperties(Material material)
{
    propertyValues.Clear();
    propertySelection.Clear();

    if (material == null || material.shader == null) return;

    Shader shader = material.shader;
    int propertyCount = ShaderUtil.GetPropertyCount(shader);

    for (int i = 0; i < propertyCount; i++)
    {
        string propName = ShaderUtil.GetPropertyName(shader, i);
        ShaderUtil.ShaderPropertyType propType = ShaderUtil.GetPropertyType(shader, i);

        object value = null;
        switch (propType)
        {
            case ShaderUtil.ShaderPropertyType.Color:
                value = material.GetColor(propName);
                break;
            case ShaderUtil.ShaderPropertyType.Vector:
                value = material.GetVector(propName);
                break;
            case ShaderUtil.ShaderPropertyType.Float:
            case ShaderUtil.ShaderPropertyType.Range:
                value = material.GetFloat(propName);
                break;
            case ShaderUtil.ShaderPropertyType.TexEnv:
                value = material.GetTexture(propName);
                break;
        }

        // 修改这里：即使值为null也要添加到属性列表，特别是纹理属性
        propertyValues[propName] = value;
        propertySelection[propName] = false;
    }

    // 确保所有溶解相关属性都存在
    EnsureAllDissolvePropertiesExist(material);
}

// 确保所有溶解属性存在的辅助方法
private void EnsureAllDissolvePropertiesExist(Material material)
{
    string[] dissolveProperties = new string[]
    {
        "_UseDissolve", "_DissolveMask", "_DissolveNoiseMask", "_DissolveNoiseStrength",
        "_DissolveColor", "_DissolveParams", "_DissolvePos", "_DissolveWidth",
        "_DissolveEdge", "_DissolveBlur"
    };

    foreach (string propName in dissolveProperties)
    {
        if (!propertyValues.ContainsKey(propName) && material.HasProperty(propName))
        {
            // 根据属性类型设置默认值
            if (propName.EndsWith("Mask")) // 纹理属性
            {
                propertyValues[propName] = null;
            }
            else if (propName.Contains("Color")) // 颜色属性
            {
                propertyValues[propName] = Color.white;
            }
            else if (propName.Contains("Params") || propName.Contains("Pos")) // 向量属性
            {
                propertyValues[propName] = Vector4.zero;
            }
            else if (propName == "_DissolveNoiseStrength") // 噪波强度
            {
                propertyValues[propName] = 0.1f;
            }
            else // 其他浮点数属性
            {
                propertyValues[propName] = 0f;
            }
            propertySelection[propName] = false;
        }
    }
}

    private void UpdateRenderModeFromMaterial(Material material)
    {
        if (material == null) return;

        string shaderName = material.shader.name.ToLower();
        if (shaderName.Contains("opaque") || (!shaderName.Contains("cutout") && !shaderName.Contains("transparent") && !shaderName.Contains("fur") && !shaderName.Contains("gem")))
            selectedRenderMode = 0;
        else if (shaderName.Contains("cutout") && !shaderName.Contains("fur"))
            selectedRenderMode = 1;
        else if (shaderName.Contains("transparent"))
            selectedRenderMode = 2;
        else if (shaderName.Contains("fur") && !shaderName.Contains("cutout"))
            selectedRenderMode = 3;
        else if (shaderName.Contains("fur") && shaderName.Contains("cutout"))
            selectedRenderMode = 4;
        else if (shaderName.Contains("gem"))
            selectedRenderMode = 5;
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginVertical();
        
        // 标题栏
        DrawTitleBar();
        
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        
        // 渲染模式选择
        DrawRenderModeSelection();
        
        // 材质选择
        DrawMaterialSelection();
        
        // 属性选择
        DrawPropertySelection();
        
        // 属性设置
        DrawPropertySettings();
        
        EditorGUILayout.EndScrollView();
        
        // 操作按钮
        DrawActionButtons();
        
        EditorGUILayout.EndVertical();
    }

    private void DrawTitleBar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        
        // 标题
        GUILayout.Label("lilToon 批量材质编辑器", EditorStyles.boldLabel);
        
        GUILayout.FlexibleSpace();
        
        // 窗口锁定按钮
        DrawWindowLockButton();
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space();
        
        // 撤回/重做按钮
        DrawUndoRedoControls();
    }

    private void DrawWindowLockButton()
    {
        // 锁定按钮
        Texture2D currentIcon = isWindowLocked ? lockIcon : unlockIcon;
        string tooltip = isWindowLocked ? "窗口已锁定 - 选择变化不会影响当前内容\n点击解锁" : "窗口未锁定 - 选择变化会自动刷新\n点击锁定";
        
        GUIContent lockContent = new GUIContent(
            currentIcon,
            tooltip
        );
        
        Color originalColor = GUI.color;
        if (isWindowLocked)
        {
            GUI.color = Color.yellow;
        }
        
        if (GUILayout.Button(lockContent, EditorStyles.toolbarButton, GUILayout.Width(30)))
        {
            isWindowLocked = !isWindowLocked;
            
            if (!isWindowLocked)
            {
                // 如果解锁了窗口，自动刷新选择
                RefreshSelection();
            }
            
            // 显示状态提示
            string status = isWindowLocked ? "窗口已锁定" : "窗口已解锁";
            ShowNotification(new GUIContent(status));
        }
        
        GUI.color = originalColor;
        
        // 显示锁定状态文字提示
        GUILayout.Label(isWindowLocked ? "已锁定" : "未锁定", EditorStyles.miniLabel, GUILayout.Width(40));
    }

    private void DrawUndoRedoControls()
    {
        EditorGUILayout.BeginHorizontal();
        
        EditorGUI.BeginDisabledGroup(undoStack.Count == 0);
        if (GUILayout.Button("撤回", GUILayout.Width(60)))
        {
            UndoLastOperation();
        }
        EditorGUI.EndDisabledGroup();
        
        EditorGUI.BeginDisabledGroup(redoStack.Count == 0);
        if (GUILayout.Button("重做", GUILayout.Width(60)))
        {
            RedoLastOperation();
        }
        EditorGUI.EndDisabledGroup();
        
        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField($"撤回: {undoStack.Count} 重做: {redoStack.Count}", EditorStyles.miniLabel);
        
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();
    }

    private void DrawRenderModeSelection()
    {
        EditorGUILayout.LabelField("渲染模式", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginVertical("Box");
        
        int newRenderMode = EditorGUILayout.Popup("渲染模式", selectedRenderMode, renderModes);
        if (newRenderMode != selectedRenderMode)
        {
            selectedRenderMode = newRenderMode;
        }
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("应用渲染模式"))
        {
            ApplyRenderModeToSelectedMaterials();
        }
        
        if (GUILayout.Button("从材质读取"))
        {
            if (selectedMaterials.Count > 0)
            {
                UpdateRenderModeFromMaterial(selectedMaterials[0]);
            }
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();
    }

    private void DrawMaterialSelection()
    {
        EditorGUILayout.LabelField("材质选择", EditorStyles.boldLabel);
        
        if (selectedMaterials.Count == 0)
        {
            EditorGUILayout.HelpBox("请选择包含 lilToon 材质的对象", MessageType.Info);
            return;
        }

        // 显示锁定状态提示
        if (isWindowLocked)
        {
            EditorGUILayout.HelpBox("窗口已锁定 - 当前显示的是之前选择的内容\n点击标题栏的锁定按钮解锁", MessageType.Warning);
        }

        // 全选/全不选
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("全选材质", GUILayout.Width(80)))
        {
            SelectAllMaterials(true);
        }
        if (GUILayout.Button("全不选", GUILayout.Width(60)))
        {
            SelectAllMaterials(false);
        }
        
        int selectedCount = materialSelection.Values.Count(v => v);
        EditorGUILayout.LabelField($"已选择: {selectedCount} / {selectedMaterials.Count}", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // 材质列表
        EditorGUILayout.BeginVertical("Box");
        foreach (var material in selectedMaterials)
        {
            EditorGUILayout.BeginHorizontal();
            
            // 修复复选框点击区域问题 - 使用更大的点击区域
            Rect toggleRect = GUILayoutUtility.GetRect(20, 20, GUILayout.Width(30));
            materialSelection[material] = EditorGUI.Toggle(toggleRect, materialSelection[material]);
            
            EditorGUILayout.ObjectField(material, typeof(Material), false, GUILayout.ExpandWidth(true));
            
            if (material.shader != null)
            {
                EditorGUILayout.LabelField(material.shader.name, EditorStyles.miniLabel, GUILayout.Width(150));
            }
            
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();
    }

    private void DrawPropertySelection()
    {
        EditorGUILayout.LabelField("属性选择", EditorStyles.boldLabel);
        
        if (propertyValues.Count == 0)
        {
            EditorGUILayout.HelpBox("请先加载材质属性", MessageType.Info);
            return;
        }

        EditorGUILayout.BeginVertical("Box");
        
        // 属性选择按钮
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("全选属性", GUILayout.Width(80)))
        {
            SelectAllProperties(true);
        }
        if (GUILayout.Button("全不选", GUILayout.Width(60)))
        {
            SelectAllProperties(false);
        }
        
        int selectedPropCount = propertySelection.Values.Count(v => v);
        EditorGUILayout.LabelField($"已选择: {selectedPropCount} / {propertyValues.Count}", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // 按分类显示属性
        foreach (var category in propertyCategories)
        {
            var propertiesInCategory = lilProperties
                .Where(p => p.category == category.Key && propertyValues.ContainsKey(p.name))
                .ToList();

            if (propertiesInCategory.Count == 0) continue;

            categoryFoldoutStates[category.Key] = EditorGUILayout.Foldout(
                categoryFoldoutStates[category.Key], 
                $"{category.Key} ({propertiesInCategory.Count})", 
                true
            );

            if (categoryFoldoutStates[category.Key])
            {
                EditorGUI.indentLevel++;
                foreach (var prop in propertiesInCategory)
                {
                    DrawPropertySelectionRow(prop);
                }
                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
            }
        }
        
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();
    }

    private void DrawPropertySelectionRow(LilProperty property)
    {
        EditorGUILayout.BeginHorizontal();
        
        // 修复复选框点击区域问题 - 使用更大的点击区域
        Rect toggleRect = GUILayoutUtility.GetRect(20, 20, GUILayout.Width(30));
        propertySelection[property.name] = EditorGUI.Toggle(toggleRect, propertySelection[property.name]);
        
        // 解决属性名显示不全问题 - 使用更宽的标签
        EditorGUILayout.LabelField(property.displayName, GUILayout.Width(180));
        
        string valueInfo = GetPropertyValueInfo(property.name, propertyValues[property.name]);
        EditorGUILayout.LabelField(valueInfo, EditorStyles.miniLabel, GUILayout.ExpandWidth(true));
        
        EditorGUILayout.EndHorizontal();
    }

    private string GetPropertyValueInfo(string propName, object value)
    {
        if (value is Color color)
            return $"颜色 ({color.r:F2}, {color.g:F2}, {color.b:F2}, {color.a:F2})";
        else if (value is Vector4 vector)
            return $"向量 ({vector.x:F2}, {vector.y:F2}, {vector.z:F2}, {vector.w:F2})";
        else if (value is float floatValue)
            return $"数值 {floatValue:F3}";
        else if (value is Texture texture)
            return texture != null ? $"纹理 {texture.name}" : "纹理 无";
        
        return value?.ToString() ?? "null";
    }

    private void DrawPropertySettings()
    {
        EditorGUILayout.LabelField("属性设置", EditorStyles.boldLabel);
        
        if (selectedMaterials.Count == 0 || propertyValues.Count == 0)
        {
            EditorGUILayout.HelpBox("没有可用的材质或属性", MessageType.Warning);
            return;
        }

        EditorGUILayout.BeginVertical("Box");
        
        // 参考材质选择
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("参考材质:", GUILayout.Width(80));
        
        Material referenceMaterial = selectedMaterials.Count > 0 ? selectedMaterials[0] : null;
        if (selectedMaterials.Count > 1)
        {
            int index = EditorGUILayout.Popup(0, selectedMaterials.Select(m => m.name).ToArray());
            referenceMaterial = selectedMaterials[index];
        }
        else if (referenceMaterial != null)
        {
            EditorGUILayout.LabelField(referenceMaterial.name);
        }
        
        if (GUILayout.Button("刷新属性", GUILayout.Width(80)) && referenceMaterial != null)
        {
            LoadMaterialProperties(referenceMaterial);
            AnalyzeLilToonProperties(referenceMaterial);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // 选中的属性设置
        var selectedProps = propertySelection.Where(kvp => kvp.Value).Select(kvp => kvp.Key).ToList();
        if (selectedProps.Count == 0)
        {
            EditorGUILayout.HelpBox("请选择要修改的属性", MessageType.Info);
        }
        else
        {
            foreach (var propName in selectedProps)
            {
                var property = lilProperties.FirstOrDefault(p => p.name == propName);
                if (property != null && referenceMaterial != null)
                {
                    DrawPropertyControl(property, referenceMaterial);
                }
            }
        }
        
        EditorGUILayout.EndVertical();
    }

    private void DrawPropertyControl(LilProperty property, Material referenceMaterial)
    {
        if (!propertyValues.ContainsKey(property.name)) return;

        EditorGUILayout.BeginVertical("Box");
        
        string displayName = property.displayName;
        object currentValue = propertyValues[property.name];

        // 特殊处理溶解效果属性
        if (property.category == "溶解效果")
        {
            DrawDissolvePropertyControl(property, currentValue);
        }
        else
        {
            DrawStandardPropertyControl(property, currentValue, displayName);
        }
        
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();
    }

    private void DrawStandardPropertyControl(LilProperty property, object currentValue, string displayName)
    {
        EditorGUILayout.BeginHorizontal();
        
        switch (property.type)
        {
            case ShaderUtil.ShaderPropertyType.Color:
                if (currentValue is Color colorValue)
                {
                    propertyValues[property.name] = EditorGUILayout.ColorField(displayName, colorValue);
                }
                break;

            case ShaderUtil.ShaderPropertyType.Vector:
                if (currentValue is Vector4 vectorValue)
                {
                    propertyValues[property.name] = EditorGUILayout.Vector4Field(displayName, vectorValue);
                }
                break;

            case ShaderUtil.ShaderPropertyType.Float:
                if (currentValue is float floatValue)
                {
                    propertyValues[property.name] = EditorGUILayout.FloatField(displayName, floatValue);
                }
                break;

            case ShaderUtil.ShaderPropertyType.Range:
                if (currentValue is float rangeValue)
                {
                    propertyValues[property.name] = EditorGUILayout.Slider(displayName, rangeValue, property.min, property.max);
                }
                break;

            case ShaderUtil.ShaderPropertyType.TexEnv:
                if (currentValue is Texture texValue)
                {
                    propertyValues[property.name] = EditorGUILayout.ObjectField(displayName, texValue, typeof(Texture), false);
                }
                break;
        }
        
        EditorGUILayout.EndHorizontal();
    }

private void DrawDissolvePropertyControl(LilProperty property, object currentValue)
{
    switch (property.name)
    {
        case "_DissolveParams":
            if (currentValue is Vector4)
            {
                Vector4 vectorValue = (Vector4)currentValue;
                EditorGUILayout.LabelField("溶解参数", EditorStyles.boldLabel);
                
                // 溶解类型选择 (x分量) - None, Alpha, UV, Position
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("类型", GUILayout.Width(60));
                int typeIndex = Mathf.Clamp(Mathf.RoundToInt(vectorValue.x), 0, dissolveTypeOptions.Length - 1);
                int newTypeIndex = EditorGUILayout.Popup(typeIndex, dissolveTypeOptions);
                if (newTypeIndex != typeIndex)
                {
                    vectorValue.x = newTypeIndex;
                }
                EditorGUILayout.EndHorizontal();
                
                // 形状选择 (y分量) - Point, Line
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("形状", GUILayout.Width(60));
                int shapeIndex = Mathf.Clamp(Mathf.RoundToInt(vectorValue.y), 0, dissolveShapeOptions.Length - 1);
                int newShapeIndex = EditorGUILayout.Popup(shapeIndex, dissolveShapeOptions);
                if (newShapeIndex != shapeIndex)
                {
                    vectorValue.y = newShapeIndex;
                }
                EditorGUILayout.EndHorizontal();
                
                // 边界 (z分量)
                vectorValue.z = EditorGUILayout.Slider("边界", vectorValue.z, 0f, 1f);
                
                // 模糊 (w分量)
                vectorValue.w = EditorGUILayout.Slider("模糊", vectorValue.w, 0f, 1f);
                
                propertyValues[property.name] = vectorValue;
            }
            break;

case "_DissolveMask":
    EditorGUILayout.LabelField("溶解遮罩", EditorStyles.boldLabel);
    
    // 获取当前纹理值
    Texture currentDissolveMask = currentValue as Texture;
    Texture newDissolveMask = (Texture)EditorGUILayout.ObjectField("遮罩纹理", currentDissolveMask, typeof(Texture), false);
    
    // 如果选择了新的纹理，更新属性值
    if (newDissolveMask != currentDissolveMask)
    {
        propertyValues[property.name] = newDissolveMask;
    }
    
    EditorGUILayout.Space();
    
    // Tiling - 使用更好的布局
    EditorGUILayout.BeginVertical("Box");
    EditorGUILayout.LabelField("Tiling", EditorStyles.miniBoldLabel);
    EditorGUILayout.BeginHorizontal();
    EditorGUILayout.LabelField("X", GUILayout.Width(20));
    dissolveMaskTiling.x = EditorGUILayout.FloatField(dissolveMaskTiling.x, GUILayout.Width(60));
    EditorGUILayout.LabelField("Y", GUILayout.Width(20));
    dissolveMaskTiling.y = EditorGUILayout.FloatField(dissolveMaskTiling.y, GUILayout.Width(60));
    EditorGUILayout.EndHorizontal();
    EditorGUILayout.EndVertical();
    
    EditorGUILayout.Space();
    
    // Offset - 使用更好的布局
    EditorGUILayout.BeginVertical("Box");
    EditorGUILayout.LabelField("Offset", EditorStyles.miniBoldLabel);
    EditorGUILayout.BeginHorizontal();
    EditorGUILayout.LabelField("X", GUILayout.Width(20));
    dissolveMaskOffset.x = EditorGUILayout.FloatField(dissolveMaskOffset.x, GUILayout.Width(60));
    EditorGUILayout.LabelField("Y", GUILayout.Width(20));
    dissolveMaskOffset.y = EditorGUILayout.FloatField(dissolveMaskOffset.y, GUILayout.Width(60));
    EditorGUILayout.EndHorizontal();
    EditorGUILayout.EndVertical();
    break;

case "_DissolveNoiseMask":
    EditorGUILayout.LabelField("溶解噪波遮罩", EditorStyles.boldLabel);
    
    // 获取当前纹理值
    Texture currentNoiseMask = currentValue as Texture;
    Texture newNoiseMask = (Texture)EditorGUILayout.ObjectField("噪波遮罩", currentNoiseMask, typeof(Texture), false);
    
    // 如果选择了新的纹理，更新属性值
    if (newNoiseMask != currentNoiseMask)
    {
        propertyValues[property.name] = newNoiseMask;
    }
    
    EditorGUILayout.Space();
    
    // 噪波强度
    float noiseStrength = 0.1f;
    if (propertyValues.ContainsKey("_DissolveNoiseStrength"))
    {
        noiseStrength = (float)propertyValues["_DissolveNoiseStrength"];
    }
    noiseStrength = EditorGUILayout.Slider("噪波强度", noiseStrength, 0f, 1f);
    propertyValues["_DissolveNoiseStrength"] = noiseStrength;
    
    EditorGUILayout.Space();
    
    // Tiling - 使用更好的布局
    EditorGUILayout.BeginVertical("Box");
    EditorGUILayout.LabelField("Tiling", EditorStyles.miniBoldLabel);
    EditorGUILayout.BeginHorizontal();
    EditorGUILayout.LabelField("X", GUILayout.Width(20));
    dissolveNoiseMaskTiling.x = EditorGUILayout.FloatField(dissolveNoiseMaskTiling.x, GUILayout.Width(60));
    EditorGUILayout.LabelField("Y", GUILayout.Width(20));
    dissolveNoiseMaskTiling.y = EditorGUILayout.FloatField(dissolveNoiseMaskTiling.y, GUILayout.Width(60));
    EditorGUILayout.EndHorizontal();
    EditorGUILayout.EndVertical();
    
    EditorGUILayout.Space();
    
    // Offset - 使用更好的布局
    EditorGUILayout.BeginVertical("Box");
    EditorGUILayout.LabelField("Offset", EditorStyles.miniBoldLabel);
    EditorGUILayout.BeginHorizontal();
    EditorGUILayout.LabelField("X", GUILayout.Width(20));
    dissolveNoiseMaskOffset.x = EditorGUILayout.FloatField(dissolveNoiseMaskOffset.x, GUILayout.Width(60));
    EditorGUILayout.LabelField("Y", GUILayout.Width(20));
    dissolveNoiseMaskOffset.y = EditorGUILayout.FloatField(dissolveNoiseMaskOffset.y, GUILayout.Width(60));
    EditorGUILayout.EndHorizontal();
    EditorGUILayout.EndVertical();
    
    EditorGUILayout.Space();
    
    // ScrollRotate - 使用更好的布局
    EditorGUILayout.BeginVertical("Box");
    EditorGUILayout.LabelField("滚动", EditorStyles.miniBoldLabel);
    EditorGUILayout.BeginHorizontal();
    EditorGUILayout.LabelField("X", GUILayout.Width(20));
    dissolveNoiseMaskScroll.x = EditorGUILayout.FloatField(dissolveNoiseMaskScroll.x, GUILayout.Width(60));
    EditorGUILayout.LabelField("Y", GUILayout.Width(20));
    dissolveNoiseMaskScroll.y = EditorGUILayout.FloatField(dissolveNoiseMaskScroll.y, GUILayout.Width(60));
    EditorGUILayout.EndHorizontal();
    EditorGUILayout.EndVertical();
    break;

        case "_DissolvePos":
            if (currentValue is Vector4)
            {
                Vector4 vectorValue = (Vector4)currentValue;
                EditorGUILayout.LabelField("溶解位置", EditorStyles.boldLabel);
                
                // 修复看不清的问题 - 使用更好的布局
                EditorGUILayout.BeginVertical("Box");
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("向量", GUILayout.Width(50));
                EditorGUILayout.LabelField("X", GUILayout.Width(20));
                vectorValue.x = EditorGUILayout.FloatField(vectorValue.x, GUILayout.Width(60));
                EditorGUILayout.LabelField("Y", GUILayout.Width(20));
                vectorValue.y = EditorGUILayout.FloatField(vectorValue.y, GUILayout.Width(60));
                EditorGUILayout.LabelField("Z", GUILayout.Width(20));
                vectorValue.z = EditorGUILayout.FloatField(vectorValue.z, GUILayout.Width(60));
                EditorGUILayout.EndHorizontal();
                
                propertyValues[property.name] = vectorValue;
                EditorGUILayout.EndVertical();
            }
            break;

        case "_DissolveWidth":
            if (currentValue is float)
            {
                float floatValue = (float)currentValue;
                propertyValues[property.name] = EditorGUILayout.Slider("溶解宽度", floatValue, 0f, 1f);
            }
            break;

        case "_DissolveEdge":
            if (currentValue is float)
            {
                float floatValue = (float)currentValue;
                propertyValues[property.name] = EditorGUILayout.Slider("溶解边缘", floatValue, 0f, 1f);
            }
            break;

        case "_DissolveBlur":
            if (currentValue is float)
            {
                float floatValue = (float)currentValue;
                propertyValues[property.name] = EditorGUILayout.Slider("溶解模糊", floatValue, 0f, 1f);
            }
            break;

        case "_DissolveNoiseStrength":
            // 单独处理噪波强度属性
            if (currentValue is float)
            {
                float floatValue = (float)currentValue;
                propertyValues[property.name] = EditorGUILayout.Slider("噪波强度", floatValue, 0f, 1f);
            }
            break;

        default:
            DrawStandardPropertyControl(property, currentValue, property.displayName);
            break;
    }
}

    private void DrawActionButtons()
    {
        EditorGUILayout.BeginVertical("Box");
        
        int selectedMatCount = materialSelection.Values.Count(v => v);
        int selectedPropCount = propertySelection.Values.Count(v => v);
        
        EditorGUI.BeginDisabledGroup(selectedMatCount == 0 || selectedPropCount == 0);
        if (GUILayout.Button($"应用选中属性到 {selectedMatCount} 个材质", GUILayout.Height(30)))
        {
            ApplyPropertiesToSelectedMaterials();
        }
        EditorGUI.EndDisabledGroup();
        
        if (GUILayout.Button("刷新选择", GUILayout.Height(25)))
        {
            RefreshSelection();
        }
        
        EditorGUILayout.EndVertical();
    }

    private void SelectAllMaterials(bool select)
    {
        foreach (var material in materialSelection.Keys.ToList())
        {
            materialSelection[material] = select;
        }
        Repaint();
    }

    private void SelectAllProperties(bool select)
    {
        foreach (var propName in propertySelection.Keys.ToList())
        {
            propertySelection[propName] = select;
        }
        Repaint();
    }

    private void ApplyRenderModeToSelectedMaterials()
    {
        var materialsToModify = materialSelection.Where(kvp => kvp.Value).Select(kvp => kvp.Key).ToList();
        if (materialsToModify.Count == 0)
        {
            materialsToModify = selectedMaterials;
        }

        if (materialsToModify.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "请选择要修改的材质", "确定");
            return;
        }

        // 保存当前状态用于撤回
        SaveUndoState(materialsToModify, new List<string> { "_RenderMode" });

        string renderMode = renderModes[selectedRenderMode];
        string targetShaderName = "";

        // 修正不透明模式的shader名称
        switch (selectedRenderMode)
        {
            case 0: // Opaque
                targetShaderName = "lilToon";
                break;
            case 1: // Cutout
                targetShaderName = "lilToonCutout";
                break;
            case 2: // Transparent
                targetShaderName = "lilToonTransparent";
                break;
            case 3: // Fur
                targetShaderName = "lilToonFur";
                break;
            case 4: // FurCutout
                targetShaderName = "lilToonFurCutout";
                break;
            case 5: // Gem
                targetShaderName = "lilToonGem";
                break;
        }

        int modifiedCount = 0;
        foreach (var material in materialsToModify)
        {
            // 查找对应的 lilToon shader
            var shader = FindLilToonShader(targetShaderName);
            if (shader != null)
            {
                material.shader = shader;
                EditorUtility.SetDirty(material);
                modifiedCount++;
            }
            else
            {
                Debug.LogWarning($"找不到对应的shader: {targetShaderName}");
            }
        }

        if (modifiedCount > 0)
        {
            Debug.Log($"成功修改 {modifiedCount} 个材质的渲染模式为 {renderMode}");
            EditorUtility.DisplayDialog("完成", $"成功修改 {modifiedCount} 个材质的渲染模式", "确定");
        }
    }

    private Shader FindLilToonShader(string shaderName)
    {
        // 首先尝试直接查找
        Shader shader = Shader.Find(shaderName);
        if (shader != null) return shader;

        // 如果在项目中查找 lilToon shader
        var shaderGuids = AssetDatabase.FindAssets("t:Shader");
        foreach (var guid in shaderGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var foundShader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (foundShader != null && foundShader.name.Contains(shaderName))
            {
                return foundShader;
            }
        }

        // 尝试查找包含 liltoon 的shader
        foreach (var guid in shaderGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var foundShader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (foundShader != null && foundShader.name.ToLower().Contains("liltoon"))
            {
                if (shaderName == "lilToon" && !foundShader.name.ToLower().Contains("cutout") && !foundShader.name.ToLower().Contains("transparent") && !foundShader.name.ToLower().Contains("fur") && !foundShader.name.ToLower().Contains("gem"))
                    return foundShader;
                else if (foundShader.name.Contains(shaderName))
                    return foundShader;
            }
        }

        return null;
    }

    private void ApplyPropertiesToSelectedMaterials()
    {
        var materialsToModify = materialSelection.Where(kvp => kvp.Value).Select(kvp => kvp.Key).ToList();
        var propertiesToApply = propertySelection.Where(kvp => kvp.Value).Select(kvp => kvp.Key).ToList();
        
        if (materialsToModify.Count == 0 || propertiesToApply.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "请选择要修改的材质和属性", "确定");
            return;
        }

        // 保存当前状态用于撤回
        SaveUndoState(materialsToModify, propertiesToApply);

        int modifiedCount = 0;
        
        foreach (var material in materialsToModify)
        {
            if (ApplyPropertiesToMaterial(material, propertiesToApply))
            {
                modifiedCount++;
            }
        }

        if (modifiedCount > 0)
        {
            Debug.Log($"成功修改 {modifiedCount} 个材质的 {propertiesToApply.Count} 个属性");
            EditorUtility.DisplayDialog("完成", $"成功修改 {modifiedCount} 个材质", "确定");
        }
    }

    private bool ApplyPropertiesToMaterial(Material material, List<string> propertiesToApply)
    {
        bool modified = false;
        
        foreach (var propName in propertiesToApply)
        {
            if (!propertyValues.ContainsKey(propName) || !material.HasProperty(propName)) continue;
            
            var property = lilProperties.FirstOrDefault(p => p.name == propName);
            if (property == null) continue;

            object newValue = propertyValues[propName];
            
            switch (property.type)
            {
                case ShaderUtil.ShaderPropertyType.Color:
                    material.SetColor(propName, (Color)newValue);
                    modified = true;
                    break;
                case ShaderUtil.ShaderPropertyType.Vector:
                    material.SetVector(propName, (Vector4)newValue);
                    modified = true;
                    break;
                case ShaderUtil.ShaderPropertyType.Float:
                case ShaderUtil.ShaderPropertyType.Range:
                    material.SetFloat(propName, (float)newValue);
                    modified = true;
                    break;
                case ShaderUtil.ShaderPropertyType.TexEnv:
                    material.SetTexture(propName, (Texture)newValue);
                    modified = true;
                    break;
            }
        }

        if (modified)
        {
            EditorUtility.SetDirty(material);
        }

        return modified;
    }

    private void SaveUndoState(List<Material> materials, List<string> properties)
    {
        var state = new Dictionary<Material, Dictionary<string, object>>();
        
        foreach (var material in materials)
        {
            var materialState = new Dictionary<string, object>();
            foreach (var propName in properties)
            {
                if (material.HasProperty(propName))
                {
                    var property = lilProperties.FirstOrDefault(p => p.name == propName);
                    if (property != null)
                    {
                        switch (property.type)
                        {
                            case ShaderUtil.ShaderPropertyType.Color:
                                materialState[propName] = material.GetColor(propName);
                                break;
                            case ShaderUtil.ShaderPropertyType.Vector:
                                materialState[propName] = material.GetVector(propName);
                                break;
                            case ShaderUtil.ShaderPropertyType.Float:
                            case ShaderUtil.ShaderPropertyType.Range:
                                materialState[propName] = material.GetFloat(propName);
                                break;
                            case ShaderUtil.ShaderPropertyType.TexEnv:
                                materialState[propName] = material.GetTexture(propName);
                                break;
                        }
                    }
                }
            }
            state[material] = materialState;
        }
        
        undoStack.Push(state);
        redoStack.Clear(); // 清空重做栈
    }

    private void UndoLastOperation()
    {
        if (undoStack.Count == 0) return;
        
        var state = undoStack.Pop();
        redoStack.Push(SaveCurrentState(state.Keys.ToList(), state.Values.First().Keys.ToList()));
        
        foreach (var materialState in state)
        {
            var material = materialState.Key;
            foreach (var propState in materialState.Value)
            {
                var property = lilProperties.FirstOrDefault(p => p.name == propState.Key);
                if (property != null)
                {
                    switch (property.type)
                    {
                        case ShaderUtil.ShaderPropertyType.Color:
                            material.SetColor(propState.Key, (Color)propState.Value);
                            break;
                        case ShaderUtil.ShaderPropertyType.Vector:
                            material.SetVector(propState.Key, (Vector4)propState.Value);
                            break;
                        case ShaderUtil.ShaderPropertyType.Float:
                        case ShaderUtil.ShaderPropertyType.Range:
                            material.SetFloat(propState.Key, (float)propState.Value);
                            break;
                        case ShaderUtil.ShaderPropertyType.TexEnv:
                            material.SetTexture(propState.Key, (Texture)propState.Value);
                            break;
                    }
                }
            }
            EditorUtility.SetDirty(material);
        }
        
        RefreshSelection();
    }

    private void RedoLastOperation()
    {
        if (redoStack.Count == 0) return;
        
        var state = redoStack.Pop();
        undoStack.Push(SaveCurrentState(state.Keys.ToList(), state.Values.First().Keys.ToList()));
        
        // 应用重做状态（与撤回逻辑相同）
        UndoLastOperation();
    }

    private Dictionary<Material, Dictionary<string, object>> SaveCurrentState(List<Material> materials, List<string> properties)
    {
        var state = new Dictionary<Material, Dictionary<string, object>>();
        
        foreach (var material in materials)
        {
            var materialState = new Dictionary<string, object>();
            foreach (var propName in properties)
            {
                if (material.HasProperty(propName))
                {
                    var property = lilProperties.FirstOrDefault(p => p.name == propName);
                    if (property != null)
                    {
                        switch (property.type)
                        {
                            case ShaderUtil.ShaderPropertyType.Color:
                                materialState[propName] = material.GetColor(propName);
                                break;
                            case ShaderUtil.ShaderPropertyType.Vector:
                                materialState[propName] = material.GetVector(propName);
                                break;
                            case ShaderUtil.ShaderPropertyType.Float:
                            case ShaderUtil.ShaderPropertyType.Range:
                                materialState[propName] = material.GetFloat(propName);
                                break;
                            case ShaderUtil.ShaderPropertyType.TexEnv:
                                materialState[propName] = material.GetTexture(propName);
                                break;
                        }
                    }
                }
            }
            state[material] = materialState;
        }
        
        return state;
    }
}