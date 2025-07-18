using UnityEngine;
using UnityEditor;
using System.IO;

public class UpdateTool
{
    // 使用完整的源路径
    private static readonly string SOURCE_PATH = Path.Combine(
        System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments),
        "奇师傅工具箱"
    );
    private const string TARGET_PATH = "Assets/XR";
    private const string OLD_FOLDER_NAME1 = "改模小工具箱";
    private const string OLD_FOLDER_NAME2 = "奇师傅改模小箱";
    private const string OLD_FOLDER_NAME3 = "奇师傅工具箱";


    public static void ShowUpdateConfirmation()
    {
        // 首先检查源文件夹是否存在
        if (!Directory.Exists(SOURCE_PATH))
        {
            EditorUtility.DisplayDialog("错误", 
                $"源文件夹不存在：\n{SOURCE_PATH}\n请确保该文件夹存在后再试。", 
                "确定");
            return;
        }

        if (EditorUtility.DisplayDialog("更新确认", 
            $"确定要更新工具箱吗？\n\n源文件夹：\n{SOURCE_PATH}\n\n这将删除当前的工具箱并从文档文件夹复制新版本。", 
            "确定", "取消"))
        {
            PerformUpdate();
        }
    }

    private static void PerformUpdate()
    {
        try
        {
            // 删除旧文件夹
            string oldPath1 = Path.Combine(TARGET_PATH, OLD_FOLDER_NAME1).Replace("/", "\\");
            string oldPath2 = Path.Combine(TARGET_PATH, OLD_FOLDER_NAME2).Replace("/", "\\");
            string oldPath3 = Path.Combine(TARGET_PATH, OLD_FOLDER_NAME3).Replace("/", "\\");

            if (Directory.Exists(oldPath1))
            {
                AssetDatabase.DeleteAsset(oldPath1);
            }
            if (Directory.Exists(oldPath2))
            {
                AssetDatabase.DeleteAsset(oldPath2);
            }
            if (Directory.Exists(oldPath3))
            {
                AssetDatabase.DeleteAsset(oldPath3);
            }

            // 复制新文件夹
            string targetPath = Path.Combine(TARGET_PATH, "奇师傅工具箱").Replace("/", "\\");
            
            // 确保目标路径的父文件夹存在
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath));

            // 使用AssetDatabase的方法来复制文件夹
            foreach (string dirPath in Directory.GetDirectories(SOURCE_PATH, "*", SearchOption.AllDirectories))
            {
                string relativePath = dirPath.Substring(SOURCE_PATH.Length + 1);
                string targetDirPath = Path.Combine(targetPath, relativePath);
                Directory.CreateDirectory(targetDirPath);
            }

            foreach (string filePath in Directory.GetFiles(SOURCE_PATH, "*.*", SearchOption.AllDirectories))
            {
                string relativePath = filePath.Substring(SOURCE_PATH.Length + 1);
                string targetFilePath = Path.Combine(targetPath, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(targetFilePath));
                File.Copy(filePath, targetFilePath, true);
            }

            // 刷新Unity资源
            AssetDatabase.Refresh();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"更新过程中发生错误：{e}");
            EditorUtility.DisplayDialog("错误", 
                $"更新过程中发生错误：\n{e.Message}\n\n详细错误信息已写入Console窗口。", 
                "确定");
        }
    }
}
