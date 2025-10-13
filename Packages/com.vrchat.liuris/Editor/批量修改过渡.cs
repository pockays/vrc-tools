using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

public class AnimatorControllerBatchEditor : EditorWindow
{
    private AnimatorController controller;
    private List<TransitionInfo> selectedTransitions = new List<TransitionInfo>();
    
    // 批量修改过渡参数
    private bool batchHasExitTime;
    private float batchExitTime = 0.75f;
    private bool batchFixedDuration = true;
    private float batchTransitionDuration = 0.25f;
    private float batchTransitionOffset;
    private bool batchCanTransitionToSelf;
    
    // 条件操作模式
    private enum ConditionOperationMode
    {
        Replace,    // 替换所有条件
        Add,        // 添加新条件
        Remove      // 移除匹配条件
    }
    private ConditionOperationMode conditionMode = ConditionOperationMode.Replace;
    
    // 批量条件管理
    private List<AnimatorCondition> batchConditions = new List<AnimatorCondition>();
    private string newConditionParameter = "";
    private AnimatorConditionMode newConditionMode = AnimatorConditionMode.Equals;
    private float newConditionThreshold;
    
    // AnyState 生成参数
    private int anyStateCount = 2;
    private string anyStateParameterName = "NewParameter";
    private Vector2 scrollPosition;
    private Vector2 previewScrollPosition; // 为预览窗口添加独立的滚动条变量

    // 过渡信息类
    [System.Serializable]
    private class TransitionInfo
    {
        public AnimatorStateTransition transition;
        public string layerName;
        public string sourceStateName;
        public string destinationStateName;
        public bool isAnyStateTransition;
        
        public TransitionInfo(AnimatorStateTransition transition, string layerName, string sourceStateName, string destinationStateName, bool isAnyStateTransition)
        {
            this.transition = transition;
            this.layerName = layerName;
            this.sourceStateName = sourceStateName;
            this.destinationStateName = destinationStateName;
            this.isAnyStateTransition = isAnyStateTransition;
        }
    }

    [MenuItem("Tools/动画控制器批量编辑器")]
    public static void ShowWindow()
    {
        GetWindow<AnimatorControllerBatchEditor>("动画控制器编辑器");
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("动画控制器批量编辑器", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // 选择动画控制器
        controller = (AnimatorController)EditorGUILayout.ObjectField("动画控制器", controller, typeof(AnimatorController), false);
        
        if (controller == null)
        {
            EditorGUILayout.HelpBox("请选择一个动画控制器", MessageType.Info);
            EditorGUILayout.EndScrollView();
            return;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("1. 获取Animator窗口选中的过渡", EditorStyles.boldLabel);
        
        // 直接获取Animator窗口选中的过渡
        DrawDirectSelectionSection();

        if (selectedTransitions.Count > 0)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("2. 批量修改设置", EditorStyles.boldLabel);
            
            // 批量修改部分
            DrawBatchModifySection();
        }
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("3. AnyState 批量生成", EditorStyles.boldLabel);
        
        // AnyState 生成部分
        DrawAnyStateGenerationSection();
        
        EditorGUILayout.EndScrollView();
    }

    private void DrawDirectSelectionSection()
    {
        EditorGUILayout.BeginVertical("box");
        
        EditorGUILayout.HelpBox("请确保：\n1. 已打开Animator窗口\n2. 在Animator窗口中选中了一个或多个过渡\n3. 点击下面的按钮读取选中的过渡", MessageType.Info);
        
        if (GUILayout.Button("读取Animator窗口选中的过渡", GUILayout.Height(30)))
        {
            ReadAnimatorWindowSelectedTransitions();
        }

        EditorGUILayout.LabelField($"当前选中的过渡数量: {selectedTransitions.Count}", EditorStyles.boldLabel);
        
        if (selectedTransitions.Count > 0)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("选中的过渡:", EditorStyles.miniBoldLabel);
            
            // 使用独立的滚动条变量
            previewScrollPosition = EditorGUILayout.BeginScrollView(previewScrollPosition, GUILayout.Height(150));
            for (int i = 0; i < selectedTransitions.Count; i++)
            {
                var transitionInfo = selectedTransitions[i];
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"{i + 1}. {GetTransitionDescription(transitionInfo)}", EditorStyles.miniLabel);
                if (GUILayout.Button("×", GUILayout.Width(20)))
                {
                    selectedTransitions.RemoveAt(i);
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
            
            if (GUILayout.Button("清除所有选中"))
            {
                selectedTransitions.Clear();
            }
        }
        
        EditorGUILayout.EndVertical();
    }

    private void ReadAnimatorWindowSelectedTransitions()
    {
        selectedTransitions.Clear();
        
        // 方法1: 尝试通过AnimatorControllerTool获取
        var transitions = GetSelectedTransitionsViaAnimatorControllerTool();
        if (transitions.Count > 0)
        {
            selectedTransitions = transitions;
            LoadFirstTransitionParameters();
            Debug.Log($"通过AnimatorControllerTool找到 {selectedTransitions.Count} 个选中的过渡");
            return;
        }
        
        // 方法2: 尝试通过反射获取Animator窗口内部选中的过渡
        transitions = GetSelectedTransitionsViaReflection();
        if (transitions.Count > 0)
        {
            selectedTransitions = transitions;
            LoadFirstTransitionParameters();
            Debug.Log($"通过反射找到 {selectedTransitions.Count} 个选中的过渡");
            return;
        }
        
        // 方法3: 尝试通过Selection.activeObject获取
        transitions = GetSelectedTransitionsViaSelection();
        if (transitions.Count > 0)
        {
            selectedTransitions = transitions;
            LoadFirstTransitionParameters();
            Debug.Log($"通过Selection找到 {selectedTransitions.Count} 个选中的过渡");
            return;
        }
        
        EditorUtility.DisplayDialog("提示", 
            "无法读取选中的过渡。请确保：\n" +
            "1. Animator窗口已打开\n" +
            "2. 在Animator窗口中选中了过渡（点击过渡箭头）\n" +
            "3. 选中的过渡属于当前动画控制器", "确定");
    }

    private List<TransitionInfo> GetSelectedTransitionsViaAnimatorControllerTool()
    {
        var transitions = new List<TransitionInfo>();
        var processedTransitions = new HashSet<AnimatorStateTransition>();
        
        try
        {
            // 获取AnimatorControllerTool类型
            var animatorControllerToolType = typeof(UnityEditor.Animations.AnimatorController).Assembly
                .GetType("UnityEditor.AnimatorControllerTool");
            
            if (animatorControllerToolType != null)
            {
                // 获取当前工具实例
                var toolProperty = animatorControllerToolType.GetProperty("tool", BindingFlags.Public | BindingFlags.Static);
                var toolInstance = toolProperty?.GetValue(null);
                
                if (toolInstance != null)
                {
                    // 获取选中的过渡
                    var selectedTransitionsProperty = animatorControllerToolType.GetProperty("selectedTransitions", 
                        BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                    var selectedTransitions = selectedTransitionsProperty?.GetValue(toolInstance) as List<UnityEngine.Object>;
                    
                    if (selectedTransitions != null)
                    {
                        foreach (var selectedObj in selectedTransitions)
                        {
                            if (selectedObj is AnimatorStateTransition stateTransition && 
                                !processedTransitions.Contains(stateTransition))
                            {
                                var transitionInfo = FindTransitionInfo(stateTransition);
                                if (transitionInfo != null)
                                {
                                    transitions.Add(transitionInfo);
                                    processedTransitions.Add(stateTransition);
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"通过AnimatorControllerTool获取选中过渡失败: {e.Message}");
        }
        
        return transitions;
    }

    private List<TransitionInfo> GetSelectedTransitionsViaReflection()
    {
        var transitions = new List<TransitionInfo>();
        var processedTransitions = new HashSet<AnimatorStateTransition>();
        
        try
        {
            // 获取所有打开的Animator窗口
            var animatorWindows = Resources.FindObjectsOfTypeAll<EditorWindow>()
                .Where(w => w.GetType().Name == "AnimatorControllerWindow");
            
            foreach (var animatorWindow in animatorWindows)
            {
                var animatorWindowType = animatorWindow.GetType();
                
                // 尝试多个可能的字段名
                string[] possibleFieldNames = {
                    "m_SelectedTransitions", "selectedTransitions", 
                    "m_SelectedTransitionArray", "selectedTransitionArray"
                };
                
                foreach (var fieldName in possibleFieldNames)
                {
                    var selectedTransitionsField = animatorWindowType.GetField(fieldName, 
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    
                    if (selectedTransitionsField != null)
                    {
                        var selectedTransitions = selectedTransitionsField.GetValue(animatorWindow) as List<UnityEngine.Object>;
                        if (selectedTransitions != null)
                        {
                            foreach (var selectedObj in selectedTransitions)
                            {
                                if (selectedObj is AnimatorStateTransition stateTransition && 
                                    !processedTransitions.Contains(stateTransition))
                                {
                                    var transitionInfo = FindTransitionInfo(stateTransition);
                                    if (transitionInfo != null)
                                    {
                                        transitions.Add(transitionInfo);
                                        processedTransitions.Add(stateTransition);
                                    }
                                }
                            }
                        }
                    }
                }
                
                // 如果没找到多个选中的，尝试获取单个选中的过渡
                if (transitions.Count == 0)
                {
                    string[] singleTransitionFieldNames = {
                        "m_SelectedTransition", "selectedTransition"
                    };
                    
                    foreach (var fieldName in singleTransitionFieldNames)
                    {
                        var selectedTransitionField = animatorWindowType.GetField(fieldName, 
                            BindingFlags.NonPublic | BindingFlags.Instance);
                        
                        if (selectedTransitionField != null)
                        {
                            var selectedTransition = selectedTransitionField.GetValue(animatorWindow) as AnimatorStateTransition;
                            if (selectedTransition != null && !processedTransitions.Contains(selectedTransition))
                            {
                                var transitionInfo = FindTransitionInfo(selectedTransition);
                                if (transitionInfo != null)
                                {
                                    transitions.Add(transitionInfo);
                                    processedTransitions.Add(selectedTransition);
                                }
                                break;
                            }
                        }
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"通过反射获取选中过渡失败: {e.Message}");
        }
        
        return transitions;
    }

    private List<TransitionInfo> GetSelectedTransitionsViaSelection()
    {
        var transitions = new List<TransitionInfo>();
        var processedTransitions = new HashSet<AnimatorStateTransition>();
        
        try
        {
            // 检查当前选中的对象
            var selectedObjects = Selection.objects;
            foreach (var selectedObj in selectedObjects)
            {
                if (selectedObj is AnimatorStateTransition stateTransition && 
                    !processedTransitions.Contains(stateTransition))
                {
                    var transitionInfo = FindTransitionInfo(stateTransition);
                    if (transitionInfo != null)
                    {
                        transitions.Add(transitionInfo);
                        processedTransitions.Add(stateTransition);
                    }
                }
            }
            
            // 如果Selection.activeObject是过渡
            if (Selection.activeObject is AnimatorStateTransition activeTransition && 
                !processedTransitions.Contains(activeTransition))
            {
                var transitionInfo = FindTransitionInfo(activeTransition);
                if (transitionInfo != null)
                {
                    transitions.Add(transitionInfo);
                    processedTransitions.Add(activeTransition);
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"通过Selection获取选中过渡失败: {e.Message}");
        }
        
        return transitions;
    }

    private TransitionInfo FindTransitionInfo(AnimatorStateTransition targetTransition)
    {
        if (controller == null) return null;
        
        // 在所有层中搜索这个过渡
        foreach (var layer in controller.layers)
        {
            var transitionInfo = SearchTransitionInStateMachine(layer.stateMachine, layer.name, targetTransition);
            if (transitionInfo != null)
            {
                return transitionInfo;
            }
        }
        
        return null;
    }

    private TransitionInfo SearchTransitionInStateMachine(AnimatorStateMachine stateMachine, string layerName, AnimatorStateTransition targetTransition, string parentPath = "")
    {
        string currentPath = string.IsNullOrEmpty(parentPath) ? stateMachine.name : parentPath + "." + stateMachine.name;

        // 搜索状态的过渡
        foreach (var childState in stateMachine.states)
        {
            var state = childState.state;
            foreach (var transition in state.transitions)
            {
                if (transition == targetTransition)
                {
                    return new TransitionInfo(
                        transition, 
                        layerName, 
                        currentPath + "." + state.name,
                        transition.destinationState?.name ?? "Null",
                        false
                    );
                }
            }
        }
        
        // 搜索 AnyState 的过渡
        foreach (var transition in stateMachine.anyStateTransitions)
        {
            if (transition == targetTransition)
            {
                return new TransitionInfo(
                    transition, 
                    layerName, 
                    "AnyState",
                    transition.destinationState?.name ?? "Null",
                    true
                );
            }
        }
        
        // 递归搜索子状态机
        foreach (var childStateMachine in stateMachine.stateMachines)
        {
            var result = SearchTransitionInStateMachine(childStateMachine.stateMachine, layerName, targetTransition, currentPath);
            if (result != null)
            {
                return result;
            }
        }
        
        return null;
    }

    private void DrawBatchModifySection()
    {
        EditorGUILayout.BeginVertical("box");
        
        EditorGUILayout.LabelField("过渡参数设置", EditorStyles.miniBoldLabel);
        
        // 过渡参数设置
        batchHasExitTime = EditorGUILayout.Toggle("Has Exit Time", batchHasExitTime);
        batchExitTime = EditorGUILayout.FloatField("Exit Time", batchExitTime);
        batchFixedDuration = EditorGUILayout.Toggle("Fixed Duration", batchFixedDuration);
        batchTransitionDuration = EditorGUILayout.FloatField("Transition Duration", batchTransitionDuration);
        batchTransitionOffset = EditorGUILayout.FloatField("Transition Offset", batchTransitionOffset);
        batchCanTransitionToSelf = EditorGUILayout.Toggle("Can Transition To Self", batchCanTransitionToSelf);
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("条件管理", EditorStyles.miniBoldLabel);
        
        // 条件操作模式
        conditionMode = (ConditionOperationMode)EditorGUILayout.EnumPopup("条件操作模式", conditionMode);
        EditorGUILayout.HelpBox(GetConditionModeDescription(), MessageType.Info);
        
        // 条件管理
        DrawConditionsManagement();
        
        EditorGUILayout.Space();
        
        if (GUILayout.Button("应用修改到选中过渡", GUILayout.Height(30)))
        {
            BatchModifySelectedTransitions();
        }
        
        EditorGUILayout.EndVertical();
    }

    private string GetConditionModeDescription()
    {
        switch (conditionMode)
        {
            case ConditionOperationMode.Replace: return "替换所有现有条件";
            case ConditionOperationMode.Add: return "在现有条件基础上添加新条件";
            case ConditionOperationMode.Remove: return "移除与指定条件匹配的条件";
            default: return "";
        }
    }

    private void DrawConditionsManagement()
    {
        // 添加新条件
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("添加新条件", EditorStyles.miniBoldLabel);
        
        newConditionParameter = EditorGUILayout.TextField("参数名称", newConditionParameter);
        newConditionMode = (AnimatorConditionMode)EditorGUILayout.EnumPopup("条件模式", newConditionMode);
        newConditionThreshold = EditorGUILayout.FloatField("阈值", newConditionThreshold);
        
        if (GUILayout.Button("添加条件") && !string.IsNullOrEmpty(newConditionParameter))
        {
            var newCondition = new AnimatorCondition
            {
                parameter = newConditionParameter,
                mode = newConditionMode,
                threshold = newConditionThreshold
            };
            batchConditions.Add(newCondition);
            EnsureParameterExists(newConditionParameter);
        }
        EditorGUILayout.EndVertical();

        if (batchConditions.Count > 0)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"当前条件列表 ({batchConditions.Count} 个):", EditorStyles.miniBoldLabel);
            
            for (int i = 0; i < batchConditions.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                var condition = batchConditions[i];
                EditorGUILayout.LabelField($"{condition.parameter} {condition.mode} {condition.threshold}", GUILayout.Width(200));
                
                if (GUILayout.Button("↑", GUILayout.Width(25)) && i > 0)
                {
                    var temp = batchConditions[i - 1];
                    batchConditions[i - 1] = batchConditions[i];
                    batchConditions[i] = temp;
                }
                
                if (GUILayout.Button("↓", GUILayout.Width(25)) && i < batchConditions.Count - 1)
                {
                    var temp = batchConditions[i + 1];
                    batchConditions[i + 1] = batchConditions[i];
                    batchConditions[i] = temp;
                }
                
                if (GUILayout.Button("×", GUILayout.Width(25)))
                {
                    batchConditions.RemoveAt(i);
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }
            
            if (GUILayout.Button("清空所有条件"))
            {
                batchConditions.Clear();
            }
        }
    }

    private void DrawAnyStateGenerationSection()
    {
        EditorGUILayout.BeginVertical("box");
        
        EditorGUILayout.HelpBox("请选择要在哪个层中生成 AnyState 过渡", MessageType.Info);
        
        anyStateCount = EditorGUILayout.IntField("生成状态数量", anyStateCount);
        anyStateCount = Mathf.Max(1, anyStateCount);
        
        anyStateParameterName = EditorGUILayout.TextField("条件参数名称", anyStateParameterName);
        
        if (GUILayout.Button("选择层并生成 AnyState 过渡"))
        {
            ShowLayerSelectionForAnyState();
        }
        
        EditorGUILayout.EndVertical();
    }

    private void ShowLayerSelectionForAnyState()
    {
        GenericMenu menu = new GenericMenu();
        
        foreach (var layer in controller.layers)
        {
            menu.AddItem(new GUIContent(layer.name), false, () => {
                GenerateAnyStateInSpecificLayer(layer);
            });
        }
        
        menu.ShowAsContext();
    }

    private void GenerateAnyStateInSpecificLayer(AnimatorControllerLayer targetLayer)
    {
        if (targetLayer == null) return;

        if (string.IsNullOrEmpty(anyStateParameterName))
        {
            EditorUtility.DisplayDialog("错误", "请输入条件参数名称", "确定");
            return;
        }

        Undo.RegisterCompleteObjectUndo(controller, $"在层 {targetLayer.name} 中生成 AnyState 过渡");
        Undo.RegisterCompleteObjectUndo(targetLayer.stateMachine, "添加状态和过渡");
        
        EnsureParameterExists(anyStateParameterName);
        
        int createdCount = 0;
        for (int i = 0; i < anyStateCount; i++)
        {
            CreateAnyStateTransition(targetLayer.stateMachine, i);
            createdCount++;
        }
        
        EditorUtility.SetDirty(controller);
        Debug.Log($"在层 '{targetLayer.name}' 中成功生成了 {createdCount} 个 AnyState 过渡");
        Repaint();
    }

    private EditorWindow GetAnimatorWindow()
    {
        var windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
        foreach (var window in windows)
        {
            if (window.GetType().Name == "AnimatorControllerWindow")
            {
                return window;
            }
        }
        return null;
    }

    private void CreateAnyStateTransition(AnimatorStateMachine stateMachine, int index)
    {
        var newState = stateMachine.AddState($"NewState_{index}");
        newState.motion = null;
        
        var transition = stateMachine.AddAnyStateTransition(newState);
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = 0.25f;
        transition.canTransitionToSelf = false;
        
        var condition = new AnimatorCondition
        {
            parameter = anyStateParameterName,
            mode = AnimatorConditionMode.Equals,
            threshold = index
        };
        
        transition.conditions = new AnimatorCondition[] { condition };
        
        Undo.RegisterCreatedObjectUndo(newState, "创建动画状态");
        Undo.RegisterCreatedObjectUndo(transition, "创建过渡");
    }

    private void EnsureParameterExists(string parameterName)
    {
        if (string.IsNullOrEmpty(parameterName)) return;
        
        bool parameterExists = controller.parameters.Any(param => param.name == parameterName);
        if (!parameterExists)
        {
            controller.AddParameter(parameterName, AnimatorControllerParameterType.Int);
            Debug.Log($"已添加新参数: {parameterName}");
        }
    }

    private string GetTransitionDescription(TransitionInfo transitionInfo)
    {
        var transition = transitionInfo.transition;
        var conditions = string.Join(" & ", transition.conditions.Select(c => $"{c.parameter} {c.mode} {c.threshold}"));
        return $"[{transitionInfo.layerName}] {transitionInfo.sourceStateName} -> {transitionInfo.destinationStateName}";
    }

    private void BatchModifySelectedTransitions()
    {
        if (selectedTransitions.Count == 0)
        {
            EditorUtility.DisplayDialog("错误", "没有选中的过渡", "确定");
            return;
        }

        // 记录撤回操作
        Undo.RegisterCompleteObjectUndo(controller, "批量修改动画过渡");
        foreach (var transitionInfo in selectedTransitions)
        {
            if (transitionInfo.transition != null)
            {
                Undo.RegisterCompleteObjectUndo(transitionInfo.transition, "修改过渡参数和条件");
            }
        }
        
        int modifiedCount = 0;
        
        foreach (var transitionInfo in selectedTransitions)
        {
            var transition = transitionInfo.transition;
            if (transition == null) continue;

            // 修改过渡参数
            transition.hasExitTime = batchHasExitTime;
            transition.exitTime = batchExitTime;
            transition.hasFixedDuration = batchFixedDuration;
            transition.duration = batchTransitionDuration;
            transition.offset = batchTransitionOffset;
            transition.canTransitionToSelf = batchCanTransitionToSelf;
            
            // 根据模式修改条件 - 确保条件数组被正确替换
            ModifyTransitionConditions(transition);
            
            modifiedCount++;
            
            // 强制标记过渡为已修改
            EditorUtility.SetDirty(transition);
        }
        
        // 确保所有参数都存在
        foreach (var condition in batchConditions)
        {
            EnsureParameterExists(condition.parameter);
        }
        
        EditorUtility.SetDirty(controller);
        Debug.Log($"成功修改了 {modifiedCount} 个过渡的参数和条件");
    }

    private void ModifyTransitionConditions(AnimatorStateTransition transition)
    {
        switch (conditionMode)
        {
            case ConditionOperationMode.Replace:
                // 创建新的条件数组并赋值
                if (batchConditions.Count > 0)
                {
                    transition.conditions = batchConditions.ToArray();
                }
                else
                {
                    // 如果批量条件列表为空，清空所有条件
                    transition.conditions = new AnimatorCondition[0];
                }
                break;
                
            case ConditionOperationMode.Add:
                // 在现有条件基础上添加新条件
                var existingConditions = transition.conditions.ToList();
                foreach (var newCondition in batchConditions)
                {
                    // 避免添加重复条件
                    if (!existingConditions.Any(c => 
                        c.parameter == newCondition.parameter && 
                        c.mode == newCondition.mode && 
                        Mathf.Approximately(c.threshold, newCondition.threshold)))
                    {
                        existingConditions.Add(newCondition);
                    }
                }
                transition.conditions = existingConditions.ToArray();
                break;
                
            case ConditionOperationMode.Remove:
                // 移除匹配的条件
                if (batchConditions.Count > 0)
                {
                    var conditionsToKeep = transition.conditions.ToList();
                    foreach (var conditionToRemove in batchConditions)
                    {
                        conditionsToKeep.RemoveAll(c => 
                            c.parameter == conditionToRemove.parameter && 
                            c.mode == conditionToRemove.mode && 
                            Mathf.Approximately(c.threshold, conditionToRemove.threshold));
                    }
                    transition.conditions = conditionsToKeep.ToArray();
                }
                break;
        }
        
        // 确保修改被应用
        if (transition.conditions == null)
        {
            transition.conditions = new AnimatorCondition[0];
        }
    }

    private void LoadFirstTransitionParameters()
    {
        if (selectedTransitions.Count > 0)
        {
            var firstTransition = selectedTransitions[0].transition;
            batchHasExitTime = firstTransition.hasExitTime;
            batchExitTime = firstTransition.exitTime;
            batchFixedDuration = firstTransition.hasFixedDuration;
            batchTransitionDuration = firstTransition.duration;
            batchTransitionOffset = firstTransition.offset;
            batchCanTransitionToSelf = firstTransition.canTransitionToSelf;
        }
    }
}