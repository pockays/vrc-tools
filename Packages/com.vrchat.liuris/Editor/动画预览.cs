using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Reflection;

public class AnimatorPreviewWindow : EditorWindow
{
    private GameObject selectedObject;
    private Animator selectedAnimator;
    private List<AnimationClip> animationClips = new List<AnimationClip>();
    private AnimationClip currentClip;
    private bool isObjectLocked = false;
    
    private Vector2 scrollPosition;
    private bool isPlaying = false;
    private float animationTime = 0f;
    private float animationLength = 0f;
    private double lastUpdateTime;
    
    [MenuItem("Tools/Animator Preview")]
    public static void ShowWindow()
    {
        GetWindow<AnimatorPreviewWindow>("Animator Preview").minSize = new Vector2(350, 400);
    }
    
    private void OnEnable()
    {
        Selection.selectionChanged += UpdateSelectedObject;
        EditorApplication.update += OnEditorUpdate;
        UpdateSelectedObject();
    }
    
    private void OnDisable()
    {
        Selection.selectionChanged -= UpdateSelectedObject;
        EditorApplication.update -= OnEditorUpdate;
        StopAnimation();
    }
    
    private void UpdateSelectedObject()
    {
        if (isObjectLocked) return;
        
        GameObject newObject = Selection.activeGameObject;
        if (newObject != null)
        {
            Animator animator = newObject.GetComponent<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                SetPreviewObject(newObject);
            }
        }
    }
    
    public void SetPreviewObject(GameObject obj)
    {
        if (obj == selectedObject) return;
        
        selectedObject = obj;
        selectedAnimator = selectedObject?.GetComponent<Animator>();
        animationClips.Clear();
        currentClip = null;
        StopAnimation();
        
        if (selectedAnimator != null && selectedAnimator.runtimeAnimatorController != null)
        {
            LoadAnimationClips();
        }
        
        Repaint();
    }
    
    private void LoadAnimationClips()
    {
        animationClips.Clear();
        
        var controller = selectedAnimator.runtimeAnimatorController;
        if (controller == null) return;
        
        foreach (var clip in controller.animationClips)
        {
            if (clip != null && !animationClips.Contains(clip))
                animationClips.Add(clip);
        }
    }
    
    private void OnGUI()
    {
        if (selectedObject == null)
        {
            EditorGUILayout.HelpBox("请选择带有Animator组件的GameObject", MessageType.Info);
            return;
        }
        
        if (selectedAnimator == null || animationClips.Count == 0)
        {
            EditorGUILayout.HelpBox("Animator中没有动画剪辑", MessageType.Warning);
            return;
        }
        
        DrawHeader();
        DrawAnimationList();
        
        if (currentClip != null)
        {
            DrawCurrentAnimationInfo();
        }
    }
    
    private void DrawHeader()
    {
        EditorGUILayout.Space(5);
        
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("预览对象:", EditorStyles.boldLabel, GUILayout.Width(70));
        GUILayout.Label(selectedObject.name);
        
        GUILayout.FlexibleSpace();
        
        if (GUILayout.Button(isObjectLocked ? "解锁" : "锁定", GUILayout.Width(50)))
        {
            isObjectLocked = !isObjectLocked;
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(5);
    }
    
    private void DrawAnimationList()
    {
        EditorGUILayout.LabelField("动画列表:", EditorStyles.boldLabel);
        
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(250));
        
        for (int i = 0; i < animationClips.Count; i++)
        {
            if (animationClips[i] == null) continue;
            
            bool isSelected = currentClip == animationClips[i];
            
            Color originalColor = GUI.backgroundColor;
            
            if (isSelected)
            {
                GUI.backgroundColor = isPlaying ? Color.green : new Color(0.8f, 0.8f, 1f);
            }
            
            string buttonText = $"{animationClips[i].name} ({animationClips[i].length:F2}s)";
            
            if (GUILayout.Button(buttonText, GUILayout.Height(28)))
            {
                if (isSelected)
                {
                    currentClip = null;
                    StopAnimation();
                }
                else
                {
                    PlayAnimation(i);
                }
            }
            
            GUI.backgroundColor = originalColor;
        }
        
        EditorGUILayout.EndScrollView();
        EditorGUILayout.Space(10);
    }
    
    private void DrawCurrentAnimationInfo()
    {
        EditorGUILayout.LabelField("当前动画:", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"{currentClip.name} ({animationLength:F2}s)");
        
        EditorGUILayout.Space(5);
        
        EditorGUILayout.LabelField($"进度: {animationTime:F2}s / {animationLength:F2}s");
        float newTime = EditorGUILayout.Slider(animationTime, 0f, animationLength);
        if (Mathf.Abs(newTime - animationTime) > 0.001f)
        {
            animationTime = newTime;
            SampleAnimationAtTime();
            isPlaying = false;
        }
        
        EditorGUILayout.Space(10);
        
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("◀ 上一帧", GUILayout.Height(25)))
        {
            animationTime = Mathf.Max(0f, animationTime - (1f / 30f));
            SampleAnimationAtTime();
            isPlaying = false;
        }
        
        if (GUILayout.Button(isPlaying ? "❚❚ 暂停" : "▶ 播放", GUILayout.Height(25)))
        {
            isPlaying = !isPlaying;
            if (isPlaying) lastUpdateTime = EditorApplication.timeSinceStartup;
        }
        
        if (GUILayout.Button("▶ 下一帧", GUILayout.Height(25)))
        {
            animationTime = Mathf.Min(animationLength, animationTime + (1f / 30f));
            SampleAnimationAtTime();
            isPlaying = false;
        }
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(10);
        
        if (GUILayout.Button("编辑此动画", GUILayout.Height(30)))
        {
            EditAnimationInAnimationWindow();
        }
    }
    
    private void PlayAnimation(int index)
    {
        if (index < 0 || index >= animationClips.Count) return;
        
        // 修复动画叠加的关键：重新启用Animator并重置
        if (selectedAnimator != null)
        {
            selectedAnimator.enabled = true;
            selectedAnimator.Rebind();
            selectedAnimator.enabled = false;
        }
        
        currentClip = animationClips[index];
        animationLength = currentClip.length;
        animationTime = 0f;
        isPlaying = true;
        lastUpdateTime = EditorApplication.timeSinceStartup;
        
        SampleAnimationAtTime();
        Repaint();
    }
    
    private void StopAnimation()
    {
        isPlaying = false;
        if (selectedAnimator != null)
        {
            selectedAnimator.enabled = true;
            selectedAnimator.Rebind();
        }
    }
    
    private void SampleAnimationAtTime()
    {
        if (currentClip == null || selectedObject == null) return;
        
        // 确保Animator被禁用，这样我们才能手动采样
        if (selectedAnimator != null && selectedAnimator.enabled)
        {
            selectedAnimator.enabled = false;
        }
        
        currentClip.SampleAnimation(selectedObject, animationTime);
    }
    
    private void OnEditorUpdate()
    {
        if (isPlaying && currentClip != null)
        {
            double currentTime = EditorApplication.timeSinceStartup;
            float deltaTime = (float)(currentTime - lastUpdateTime);
            lastUpdateTime = currentTime;
            
            animationTime += deltaTime;
            
            if (animationTime >= animationLength)
            {
                animationTime = 0f;
            }
            
            SampleAnimationAtTime();
            Repaint();
        }
    }
    
    private void EditAnimationInAnimationWindow()
    {
        if (selectedObject == null || currentClip == null) return;
        
        bool wasPlaying = isPlaying;
        if (wasPlaying) StopAnimation();
        
        Selection.activeGameObject = selectedObject;
        EditorApplication.ExecuteMenuItem("Window/Animation/Animation");
        
        EditorApplication.delayCall += () =>
        {
            var animationWindowType = System.Type.GetType("UnityEditor.AnimationWindow, UnityEditor");
            if (animationWindowType != null)
            {
                var animationWindow = EditorWindow.GetWindow(animationWindowType);
                if (animationWindow != null)
                {
                    try
                    {
                        var animationWindowStateType = System.Type.GetType("UnityEditorInternal.AnimationWindowState, UnityEditor");
                        if (animationWindowStateType != null)
                        {
                            var stateProperty = animationWindowType.GetProperty("state",
                                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (stateProperty != null)
                            {
                                var state = stateProperty.GetValue(animationWindow, null);
                                if (state != null)
                                {
                                    var activeClipProperty = animationWindowStateType.GetProperty("activeAnimationClip",
                                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                    if (activeClipProperty != null)
                                    {
                                        activeClipProperty.SetValue(state, currentClip, null);
                                    }
                                }
                            }
                        }
                        
                        animationWindow.Repaint();
                        animationWindow.Focus();
                    }
                    catch
                    {
                        Debug.Log("请在Animation窗口的下拉菜单中选择对应的动画剪辑");
                    }
                }
            }
            
            if (wasPlaying)
            {
                EditorApplication.delayCall += () =>
                {
                    isPlaying = true;
                    lastUpdateTime = EditorApplication.timeSinceStartup;
                };
            }
        };
    }
}

[CustomEditor(typeof(Animator))]
public class SimpleAnimatorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Controller"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Avatar"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_ApplyRootMotion"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_UpdateMode"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_CullingMode"));
        serializedObject.ApplyModifiedProperties();
        
        EditorGUILayout.Space(10);
        
        if (GUILayout.Button("快速预览动画", GUILayout.Height(25)))
        {
            var window = EditorWindow.GetWindow<AnimatorPreviewWindow>();
            var animator = target as Animator;
            
            if (animator != null && animator.gameObject != null)
            {
                EditorApplication.delayCall += () =>
                {
                    window.SetPreviewObject(animator.gameObject);
                };
            }
        }
    }
}