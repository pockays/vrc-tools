using System;
using UnityEditor;
using UnityEngine;
using VRC.SDK3A.Editor;
using VRC.SDKBase.Editor.Api;

[InitializeOnLoad]
public class SimpleOperationSoundPlayer : EditorWindow
{
    private static AudioClip operationSound;
    private static bool isInitialized = false;
    private static IVRCSdkAvatarBuilderApi currentBuilder;
    private static double lastOperationTime;
    private const double OPERATION_COOLDOWN = 0.5f;
    private static float volume = 1.0f;

    [InitializeOnLoadMethod]
    private static void Initialize()
    {
        if (isInitialized) return;
        
        LoadSettings();
        SetupEventListeners();
        isInitialized = true;
        
        Debug.Log("🔊 操作音效插件已初始化");
    }

    private static void SetupEventListeners()
    {
        VRCSdkControlPanel.OnSdkPanelEnable += OnSdkPanelEnable;
        EditorApplication.projectChanged += OnProjectChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    #region VRChat SDK事件处理
    private static void OnSdkPanelEnable(object sender, EventArgs e)
    {
        if (!VRCSdkControlPanel.TryGetBuilder<IVRCSdkAvatarBuilderApi>(out var builder)) 
            return;

        if (currentBuilder != null)
        {
            RemoveBuilderEventListeners(currentBuilder);
        }

        currentBuilder = builder;
        
        AddBuilderEventListeners(builder);
    }

    private static void AddBuilderEventListeners(IVRCSdkAvatarBuilderApi builder)
    {
        if (builder == null) return;

        builder.OnSdkUploadSuccess += OnUploadSuccess;
        builder.OnSdkUploadError += OnUploadError;
    }

    private static void RemoveBuilderEventListeners(IVRCSdkAvatarBuilderApi builder)
    {
        if (builder == null) return;

        builder.OnSdkUploadSuccess -= OnUploadSuccess;
        builder.OnSdkUploadError -= OnUploadError;
    }

    private static void OnUploadSuccess(object sender, string result)
    {
        PlaySound("VRChat上传成功");
    }

    private static void OnUploadError(object sender, string error)
    {
        PlaySound("VRChat上传错误");
    }
    #endregion

    #region Unity编辑器事件处理
    private static void OnProjectChanged()
    {
        double currentTime = EditorApplication.timeSinceStartup;
        if (currentTime - lastOperationTime < OPERATION_COOLDOWN) return;
        
        lastOperationTime = currentTime;
        
        EditorApplication.delayCall += () =>
        {
            PlaySound("资源操作完成");
        };
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            PlaySound("进入Play模式");
        }
    }
    #endregion

    #region 音效播放核心逻辑（修复版）
    private static AudioSource audioSource;
    
    private static void EnsureAudioSource()
    {
        if (audioSource == null)
        {
            CleanupOrphanedPlayers();
            GameObject soundPlayerObject = new GameObject("EditorSoundPlayer");
            soundPlayerObject.hideFlags = HideFlags.HideAndDontSave;
            audioSource = soundPlayerObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.volume = volume;
        }
    }

    private static void CleanupOrphanedPlayers()
    {
        var existing = GameObject.Find("EditorSoundPlayer");
        if (existing != null)
        {
            UnityEngine.Object.DestroyImmediate(existing);
        }
    }
    private static void PlaySound(string operationType)
    {
        if (operationSound == null)
        {
            Debug.LogWarning($"🔊 {operationType} - 未设置音效文件");
            return;
        }

        try
        {
            EnsureAudioSource();
            
            // 停止当前播放
            if (audioSource.isPlaying)
            {
                audioSource.Stop();
            }
            
            // 设置音量和播放
            audioSource.volume = volume;
            audioSource.clip = operationSound;
            audioSource.Play();
            
            Debug.Log($"🔊 {operationType} - 播放音效 (音量: {volume * 100}%)");
        }
        catch (Exception ex)
        {
            Debug.LogError($"🔊 播放音效时出错: {ex.Message}");
        }
    }

    // 停止所有音效
    public static void StopAllSounds()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
            Debug.Log("🔊 音效已停止");
        }
    }

    // 检查是否有音效正在播放
    private static bool IsSoundPlaying()
    {
        return audioSource != null && audioSource.isPlaying;
    }

    // 清理资源
    [UnityEditor.Callbacks.DidReloadScripts]
    private static void OnScriptsReloaded()
    {
        // 重新初始化音频源
        if (audioSource != null)
        {
            GameObject.DestroyImmediate(audioSource.gameObject);
            audioSource = null;
        }
    }
    #endregion

    #region 设置管理
    private static void LoadSettings()
    {
        string soundPath = EditorPrefs.GetString("SimpleOperationSoundPlayer_SoundPath", "");
        if (!string.IsNullOrEmpty(soundPath))
        {
            operationSound = AssetDatabase.LoadAssetAtPath<AudioClip>(soundPath);
        }

        volume = EditorPrefs.GetFloat("SimpleOperationSoundPlayer_Volume", 0.7f);
    }

    private static void SaveSettings()
    {
        if (operationSound != null)
        {
            EditorPrefs.SetString("SimpleOperationSoundPlayer_SoundPath", AssetDatabase.GetAssetPath(operationSound));
        }
        else
        {
            EditorPrefs.DeleteKey("SimpleOperationSoundPlayer_SoundPath");
        }

        EditorPrefs.SetFloat("SimpleOperationSoundPlayer_Volume", volume);
        
        // 立即更新AudioSource的音量
        if (audioSource != null)
        {
            audioSource.volume = volume;
        }
    }
    #endregion

    #region 编辑器界面
    [MenuItem("Tools/操作音效播放器")]
    public static void ShowWindow()
    {
        GetWindow<SimpleOperationSoundPlayer>("操作音效", true).minSize = new Vector2(350, 280);
    }

    private void OnGUI()
    {
        DrawHeader();
        DrawSoundSettings();
        DrawVolumeControl();
        DrawPlaybackControls();
        DrawStatusInfo();
        DrawActionButtons();
    }

    private void DrawHeader()
    {
        GUILayout.Label("操作音效播放器", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        
        EditorGUILayout.HelpBox("在以下操作时播放音效：\n• 导入/删除/修改文件\n• 进入Play模式\n• VRChat上传成功\n• VRChat上传错误", MessageType.Info);
        EditorGUILayout.Space();
    }

    private void DrawSoundSettings()
    {
        GUILayout.Label("音效设置", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        AudioClip newSound = (AudioClip)EditorGUILayout.ObjectField("操作音效", operationSound, typeof(AudioClip), false);
        
        if (newSound != operationSound)
        {
            operationSound = newSound;
            SaveSettings();
        }
        
        if (operationSound != null && GUILayout.Button("测试", GUILayout.Width(50)))
        {
            PlaySound("测试");
        }
        
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();
    }

    private void DrawVolumeControl()
    {
        GUILayout.Label("音量控制", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        
        // 音量滑块
        float newVolume = EditorGUILayout.Slider("音量", volume, 0f, 1f);
        if (Math.Abs(newVolume - volume) > 0.01f)
        {
            volume = newVolume;
            SaveSettings(); // 保存时会自动更新AudioSource音量
        }
        
        // 音量百分比显示
        GUILayout.Label($"{volume * 100:0}%", GUILayout.Width(40));
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space();
    }

    private void DrawPlaybackControls()
    {
        GUILayout.Label("播放控制", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        
        // 测试播放按钮
        if (operationSound != null)
        {
            if (GUILayout.Button("测试播放", GUILayout.Height(25)))
            {
                PlaySound("测试");
            }
        }
        else
        {
            GUI.enabled = false;
            GUILayout.Button("测试播放", GUILayout.Height(25));
            GUI.enabled = true;
        }
        
        // 暂停播放按钮
        bool isPlaying = IsSoundPlaying();
        if (isPlaying)
        {
            if (GUILayout.Button("暂停播放", GUILayout.Height(25)))
            {
                StopAllSounds();
            }
        }
        else
        {
            GUI.enabled = false;
            GUILayout.Button("无音效播放", GUILayout.Height(25));
            GUI.enabled = true;
        }
        
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();
    }

    private void DrawStatusInfo()
    {
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("状态信息", EditorStyles.miniBoldLabel);
        
        GUILayout.Label($"插件状态: {(isInitialized ? "✅ 已初始化" : "❌ 未初始化")}");
        GUILayout.Label($"SDK连接: {(currentBuilder != null ? "✅ 已连接" : "❌ 未连接")}");
        GUILayout.Label($"音效设置: {(operationSound != null ? "✅ 已设置" : "❌ 未设置")}");
        GUILayout.Label($"播放状态: {(IsSoundPlaying() ? "🔊 播放中" : "⏸️ 静音")}");
        GUILayout.Label($"当前音量: {volume * 100:0}%");
        
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();
    }

    private void DrawActionButtons()
    {
        GUILayout.Label("操作", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("保存设置"))
        {
            SaveSettings();
            Debug.Log("🔊 音效设置已保存");
        }
        
        if (GUILayout.Button("重新连接SDK"))
        {
            ReconnectToSDK();
        }
        
        EditorGUILayout.EndHorizontal();
    }

    private static void ReconnectToSDK()
    {
        if (currentBuilder != null)
        {
            RemoveBuilderEventListeners(currentBuilder);
            currentBuilder = null;
        }
        
        
        if (VRCSdkControlPanel.TryGetBuilder<IVRCSdkAvatarBuilderApi>(out var builder))
        {
            currentBuilder = builder;
            AddBuilderEventListeners(builder);
            Debug.Log("🔊 已重新连接到VRChat SDK");
        }
        else
        {
            Debug.LogWarning("🔊 无法连接到VRChat SDK，请确保SDK面板已打开");
        }
    }

    private void OnDestroy()
    {
        if (currentBuilder != null)
        {
            RemoveBuilderEventListeners(currentBuilder);
        }
    }
    #endregion
}