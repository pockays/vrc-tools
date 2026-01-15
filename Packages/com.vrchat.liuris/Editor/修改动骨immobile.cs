using UnityEngine;

#if VRC_SDK_VRCSDK3
using VRC.SDK3.Dynamics.PhysBone.Components;
#endif

public class ModifyVRCPhysBoneImmobile : MonoBehaviour
{
    [Header("PhysBone 设置")]
    [Tooltip("要设置的 Immobile 值 (0=完全灵活, 1=完全固定)")]
    [Range(0f, 1f)]
    public float immobileValue = 0.7f;
    
    [Tooltip("是否在游戏开始时自动修改")]
    public bool runOnStart = true;
    
    [Tooltip("是否包含子物体中的 PhysBone")]
    public bool includeChildren = true;
    
    [Tooltip("是否显示修改日志")]
    public bool showDebugLogs = true;

    void Start()
    {
        if (runOnStart)
        {
            ModifyAllPhysBones();
        }
    }
    
    /// <summary>
    /// 修改所有 VRCPhysBone 组件的 Immobile 值
    /// </summary>
    public void ModifyAllPhysBones()
    {
#if VRC_SDK_VRCSDK3
        // 检查 SDK 环境
        if (!CheckSDKEnvironment())
        {
            return;
        }
        
        // 获取所有 PhysBone 组件
        var physBones = includeChildren ? 
            GetComponentsInChildren<VRCPhysBone>(true) : 
            GetComponents<VRCPhysBone>();
        
        if (physBones == null || physBones.Length == 0)
        {
            LogWarning($"在对象 '{gameObject.name}' 上未找到 VRCPhysBone 组件");
            return;
        }
        
        // 修改每个 PhysBone 的 Immobile 值
        int modifiedCount = 0;
        foreach (var physBone in physBones)
        {
            if (physBone != null)
            {
                physBone.immobile = immobileValue;
                modifiedCount++;
                
                if (showDebugLogs)
                {
                    Debug.Log($"已修改 '{physBone.gameObject.name}' 的 Immobile 值为 {immobileValue}", 
                             physBone.gameObject);
                }
            }
        }
        
        LogSuccess($"修改完成！共修改了 {modifiedCount} 个 VRCPhysBone 组件");
#else
        LogError("VRChat SDK 未找到！请确保已导入 VRChat SDK 3.0+ 版本");
#endif
    }
    
    /// <summary>
    /// 快捷方法：设置所有 PhysBone 为 0.7
    /// </summary>
    public void SetTo07()
    {
        immobileValue = 0.7f;
        ModifyAllPhysBones();
    }
    
    /// <summary>
    /// 快捷方法：设置所有 PhysBone 为 0（完全灵活）
    /// </summary>
    public void SetTo0()
    {
        immobileValue = 0f;
        ModifyAllPhysBones();
    }
    
    /// <summary>
    /// 快捷方法：设置所有 PhysBone 为 1（完全固定）
    /// </summary>
    public void SetTo1()
    {
        immobileValue = 1f;
        ModifyAllPhysBones();
    }
    
    /// <summary>
    /// 检查 SDK 环境
    /// </summary>
    private bool CheckSDKEnvironment()
    {
#if VRC_SDK_VRCSDK3
        return true;
#else
        return false;
#endif
    }
    
    private void LogWarning(string message)
    {
        if (showDebugLogs)
        {
            Debug.LogWarning(message, gameObject);
        }
    }
    
    private void LogError(string message)
    {
        if (showDebugLogs)
        {
            Debug.LogError(message, gameObject);
        }
    }
    
    private void LogSuccess(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"<color=green>{message}</color>", gameObject);
        }
    }
    
#if UNITY_EDITOR
    // 在 Tools 菜单中添加菜单项
    [UnityEditor.MenuItem("Tools/修改 PhysBone Immobile 值", false, 100)]
    static void AddModifierToSelected()
    {
        var selected = UnityEditor.Selection.activeGameObject;
        if (selected != null)
        {
            var modifier = selected.GetComponent<ModifyVRCPhysBoneImmobile>();
            if (modifier == null)
            {
                modifier = selected.AddComponent<ModifyVRCPhysBoneImmobile>();
            }
            
            modifier.immobileValue = 0.7f;
            modifier.ModifyAllPhysBones();
            
            Debug.Log($"已为 '{selected.name}' 添加并执行 PhysBone 修改器");
        }
        else
        {
            Debug.LogWarning("请先选中一个 GameObject");
        }
    }
    
    // 验证菜单项是否可用
    [UnityEditor.MenuItem("Tools/修改 PhysBone Immobile 值", true)]
    static bool ValidateAddModifierToSelected()
    {
        return UnityEditor.Selection.activeGameObject != null;
    }
#endif
}