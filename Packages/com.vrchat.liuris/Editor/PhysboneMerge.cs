using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using VRC.SDK3.Dynamics.PhysBone.Components;
using System.Collections.Generic;

public class VRCPhysBoneHandler
{
    [MenuItem("Tools/处理VRCPhysBone组件 _r")]
    private static void ProcessSelectedGameObject()
    {
        GameObject selectedObject = Selection.activeGameObject;
        if (selectedObject == null)
        {
            Debug.LogError("未选中任何GameObject！");
            return;
        }

        // 查找第一个子对象中的VRCPhysBone组件
        VRCPhysBone sourcePhysBone = null;
        foreach (Transform child in selectedObject.transform)
        {
            sourcePhysBone = child.GetComponent<VRCPhysBone>();
            if (sourcePhysBone != null)
                break;
        }

        if (sourcePhysBone == null)
        {
            Debug.Log("选中对象的子对象中未找到VRCPhysBone组件！", selectedObject);
            return;
        }

        // 开始撤销记录
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("处理VRCPhysBone组件");

        // 记录原始对象状态
        Undo.RegisterCompleteObjectUndo(selectedObject, "复制VRCPhysBone到选中对象");
        
        // 使用Unity内置组件复制工具
        ComponentUtility.CopyComponent(sourcePhysBone);
        VRCPhysBone newPhysBone = selectedObject.AddComponent<VRCPhysBone>();
        ComponentUtility.PasteComponentValues(newPhysBone);
        
        // 记录并删除选中GameObject的子集中的VRCPhysBone组件
        List<Object> objectsToDelete = new List<Object>();
        foreach (Transform child in selectedObject.transform)
        {
            VRCPhysBone[] physBones = child.GetComponents<VRCPhysBone>();
            foreach (VRCPhysBone physBone in physBones)
            {
                objectsToDelete.Add(physBone);
            }
        }
        
        // 批量删除并记录撤销操作
        if (objectsToDelete.Count > 0)
        {
            Undo.RegisterCompleteObjectUndo(objectsToDelete.ToArray(), "删除子级VRCPhysBone组件");
            foreach (Object obj in objectsToDelete)
            {
                Undo.DestroyObjectImmediate(obj);
            }
        }

        // 完成撤销记录
        Undo.CollapseUndoOperations(undoGroup);

        Debug.Log($"已成功将VRCPhysBone组件从 {sourcePhysBone.gameObject.name} 复制到 {selectedObject.name}，并删除了{objectsToDelete.Count}个子对象中的VRCPhysBone组件", selectedObject);
    }

    // 验证菜单项是否可用
    [MenuItem("Tools/处理VRCPhysBone组件 _r", true)]
    private static bool ValidateProcessSelectedGameObject()
    {
        GameObject selectedObject = Selection.activeGameObject;
        if (selectedObject == null)
            return false;

        foreach (Transform child in selectedObject.transform)
        {
            if (child.GetComponent<VRCPhysBone>() != null)
                return true;
        }

        return false;
    }
}    
