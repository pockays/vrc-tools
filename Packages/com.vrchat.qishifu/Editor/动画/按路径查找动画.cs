using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;
using System.Linq;

public class 按路径查找动画 : EditorWindow
{
    private GameObject targetObject;
    private Animator referenceAnimator;
    private Vector2 scrollPosition;
    private List<AnimationClip> foundClips = new List<AnimationClip>();
    private Dictionary<AnimationClip, List<string>> clipControllerMap = new Dictionary<AnimationClip, List<string>>();
    private string objectPath = "";

    [MenuItem("奇师傅工具箱/工具/动画/按路径查找动画", false, 0)]
    static void ShowWindow()
    {
        var window = GetWindow<按路径查找动画>();
        window.titleContent = new GUIContent("按路径查找动画");
        window.Show();
    }

    void OnGUI()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("基础设置", EditorStyles.boldLabel);

        // Animator引用字段
        EditorGUI.BeginChangeCheck();
        referenceAnimator = EditorGUILayout.ObjectField("参考Animator", referenceAnimator, typeof(Animator), true) as Animator;
        if (EditorGUI.EndChangeCheck() && referenceAnimator != null)
        {
            targetObject = null;
            objectPath = "";
        }

        EditorGUILayout.Space(5);
        EditorGUI.BeginChangeCheck();
        using (new EditorGUI.DisabledGroupScope(referenceAnimator == null))
        {
            targetObject = EditorGUILayout.ObjectField("目标对象", targetObject, typeof(GameObject), true) as GameObject;
        }
        if (EditorGUI.EndChangeCheck() && targetObject != null && referenceAnimator != null)
        {
            objectPath = GetGameObjectPath(targetObject, referenceAnimator.gameObject);
        }

        EditorGUILayout.Space(5);
        if (!string.IsNullOrEmpty(objectPath))
        {
            EditorGUILayout.LabelField("相对路径:", objectPath);
        }

        EditorGUILayout.Space(10);
        using (new EditorGUI.DisabledGroupScope(referenceAnimator == null))
        {
            if (GUILayout.Button("查找包含该对象的动画片段"))
            {
                FindAnimationClips();
            }
            
            EditorGUILayout.Space(5);
            
            using (new EditorGUI.DisabledGroupScope(!Application.isPlaying))
            {
                if (GUILayout.Button("查找当前正在播放的动画（不兼容GestureManager）"))
                {
                    FindCurrentPlayingAnimations();
                }
                
            }
        }

        EditorGUILayout.Space(10);
        if (foundClips.Count > 0)
        {
            EditorGUILayout.LabelField($"找到 {foundClips.Count} 个相关动画片段:", EditorStyles.boldLabel);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            foreach (var clip in foundClips)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField(clip, typeof(AnimationClip), false);
                
                if (clipControllerMap.ContainsKey(clip))
                {
                    var layerInfo = GetClipLayerInfo(clip);
                    EditorGUILayout.LabelField(layerInfo, GUILayout.Width(200));
                }
                
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }
            
            EditorGUILayout.EndScrollView();
        }
        else if (targetObject != null)
        {
            EditorGUILayout.HelpBox("未找到包含该对象的动画片段", MessageType.Info);
        }
    }

    private void FindCurrentPlayingAnimations()
    {
        if (referenceAnimator == null) return;

        Debug.Log("================开始查找当前播放动画================");
        
        // 先获取所有可能的动画片段
        List<AnimationClip> allPossibleClips = new List<AnimationClip>();
        Dictionary<AnimationClip, List<string>> tempClipControllerMap = new Dictionary<AnimationClip, List<string>>();

        RuntimeAnimatorController runtimeController = referenceAnimator.runtimeAnimatorController;
        if (runtimeController == null)
        {
            Debug.LogError("未找到RuntimeAnimatorController");
            return;
        }

        Debug.Log($"开始扫描AnimatorController: {runtimeController.name}");

        if (runtimeController is AnimatorController animController)
        {
            HashSet<AnimationClip> processedClips = new HashSet<AnimationClip>();

            for (int i = 0; i < animController.layers.Length; i++)
            {
                var layer = animController.layers[i];
                CollectAnimationClipsFromStateMachine(layer.stateMachine, processedClips, i, layer.name);
            }

            foreach (var clip in processedClips)
            {
                if (targetObject == null || ContainsObjectPath(clip, objectPath))
                {
                    allPossibleClips.Add(clip);
                }
            }
        }

        Debug.Log($"找到所有可能的动画片段数量: {allPossibleClips.Count}");

        // 获取当前正在播放的动画
        foundClips.Clear();
        clipControllerMap.Clear();

        for (int i = 0; i < referenceAnimator.layerCount; i++)
        {
            string layerName = referenceAnimator.GetLayerName(i);
            Debug.Log($"检查层 {i}: {layerName}");

            try
            {
                var clipInfo = referenceAnimator.GetCurrentAnimatorClipInfo(i);
                
                if (clipInfo.Length > 0)
                {
                    foreach (var info in clipInfo)
                    {
                        var clip = info.clip;
                        if (clip != null)
                        {
                            Debug.Log($"在层 {layerName} 找到正在播放的动画: {clip.name} (权重: {info.weight})");
                            
                            if (allPossibleClips.Contains(clip))
                            {
                                foundClips.Add(clip);
                                if (!clipControllerMap.ContainsKey(clip))
                                {
                                    clipControllerMap[clip] = new List<string> { $"层 {i}: {layerName} (正在播放, 权重: {info.weight:F2})" };
                                }
                                else
                                {
                                    clipControllerMap[clip].Add($"层 {i}: {layerName} (正在播放, 权重: {info.weight:F2})");
                                }
                            }
                            else
                            {
                                Debug.LogWarning($"动画 {clip.name} 正在播放但不在控制器中或不符合筛选条件");
                            }
                        }
                    }
                }
                else
                {
                    Debug.Log($"层 {layerName} 没有正在播放的动画");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"获取层 {layerName} 的动画信息时出错: {e.Message}");
            }
        }

        Debug.Log($"最终找到正在播放的动画数量: {foundClips.Count}");
        
        if (foundClips.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "未找到正在播放的动画", "确定");
            Debug.LogWarning("未找到任何正在播放的动画");
        }

        Debug.Log("================查找结束================");
    }

    private void FindAnimationClips()
    {
        if (targetObject == null || referenceAnimator == null) return;

        foundClips.Clear();
        clipControllerMap.Clear();

        RuntimeAnimatorController runtimeController = referenceAnimator.runtimeAnimatorController;
        if (runtimeController == null)
        {
            EditorUtility.DisplayDialog("错误", "指定的Animator没有设置AnimatorController", "确定");
            return;
        }

        if (runtimeController is AnimatorController animController)
        {
            HashSet<AnimationClip> processedClips = new HashSet<AnimationClip>();

            for (int i = 0; i < animController.layers.Length; i++)
            {
                var layer = animController.layers[i];
                CollectAnimationClipsFromStateMachine(layer.stateMachine, processedClips, i, layer.name);
            }

            foreach (var clip in processedClips)
            {
                if (ContainsObjectPath(clip, objectPath))
                {
                    foundClips.Add(clip);
                }
            }
        }
    }

    private void CollectAnimationClipsFromStateMachine(AnimatorStateMachine stateMachine, 
        HashSet<AnimationClip> clips, int layerIndex, string layerName)
    {
        foreach (var state in stateMachine.states)
        {
            if (state.state.motion is AnimationClip clip)
            {
                clips.Add(clip);
                if (!clipControllerMap.ContainsKey(clip))
                {
                    clipControllerMap[clip] = new List<string> { $"层 {layerIndex}: {layerName}" };
                }
            }
            else if (state.state.motion is BlendTree blendTree)
            {
                CollectAnimationClipsFromBlendTree(blendTree, clips, layerIndex, layerName);
            }
        }

        foreach (var childStateMachine in stateMachine.stateMachines)
        {
            CollectAnimationClipsFromStateMachine(childStateMachine.stateMachine, clips, layerIndex, layerName);
        }
    }

    private void CollectAnimationClipsFromBlendTree(BlendTree blendTree, 
        HashSet<AnimationClip> clips, int layerIndex, string layerName)
    {
        var children = blendTree.children;
        foreach (var child in children)
        {
            if (child.motion is AnimationClip clip)
            {
                clips.Add(clip);
                if (!clipControllerMap.ContainsKey(clip))
                {
                    clipControllerMap[clip] = new List<string> { $"层 {layerIndex}: {layerName}" };
                }
            }
            else if (child.motion is BlendTree childBlendTree)
            {
                CollectAnimationClipsFromBlendTree(childBlendTree, clips, layerIndex, layerName);
            }
        }
    }

    private string GetClipLayerInfo(AnimationClip clip)
    {
        if (clipControllerMap.ContainsKey(clip) && clipControllerMap[clip].Count > 0)
        {
            return string.Join(", ", clipControllerMap[clip]);
        }
        return "未找到层信息";
    }

    private bool ContainsObjectPath(AnimationClip clip, string searchPath)
    {
        var bindings = AnimationUtility.GetCurveBindings(clip);
        return bindings.Any(binding => binding.path.Contains(searchPath));
    }

    private string GetGameObjectPath(GameObject obj, GameObject relativeTo)
    {
        if (obj == null || relativeTo == null) return "";

        List<string> path = new List<string>();
        Transform current = obj.transform;
        Transform relativeTransform = relativeTo.transform;

        while (current != null && current != relativeTransform)
        {
            path.Insert(0, current.name);
            current = current.parent;
        }

        if (current != relativeTransform)
        {
            return "";
        }

        return string.Join("/", path);
    }
}
