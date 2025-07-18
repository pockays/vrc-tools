using UnityEngine;
using UnityEditor;
using System.IO;

public class CopyFullFolderPathMenuItem
{
    [MenuItem("Assets/复制完整路径", false, 19)]
    private static void CopyFullFolderPath()
    {
        string path = AssetDatabase.GetAssetPath(Selection.activeObject);
        string fullPath;

        if (string.IsNullOrEmpty(path))
        {
            // 未选中时使用当前project窗口路径
            var projectWindow = EditorWindow.focusedWindow;
            if (projectWindow != null && projectWindow.GetType().Name == "ProjectBrowser")
            {
                var getActiveFolderPathMethod = projectWindow.GetType().GetMethod("GetActiveFolderPath",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (getActiveFolderPathMethod != null)
                {
                    var folderPath = getActiveFolderPathMethod.Invoke(projectWindow, null) as string;
                    if (!string.IsNullOrEmpty(folderPath))
                    {
                        fullPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), folderPath);
                    }
                    else
                    {
                        fullPath = Path.GetDirectoryName(Application.dataPath);
                    }
                }
                else
                {
                    fullPath = Path.GetDirectoryName(Application.dataPath);
                }
            }
            else
            {
                fullPath = Path.GetDirectoryName(Application.dataPath);
            }
        }
        else
        {
            string projectPath = Path.GetDirectoryName(Application.dataPath);
            if (File.Exists(Path.Combine(projectPath, path)))
            {
                // 如果是文件，获取其目录
                fullPath = Path.GetDirectoryName(Path.Combine(projectPath, path));
            }
            else
            {
                // 如果是目录，直接使用
                fullPath = Path.Combine(projectPath, path);
            }
        }
        
        EditorGUIUtility.systemCopyBuffer = fullPath;
        Debug.Log("文件夹完整路径已复制到剪贴板: " + fullPath);
    }

    [MenuItem("Assets/复制完整路径", true)]
    private static bool ValidateCopyFullFolderPath()
    {
        // 始终启用此菜单项
        return true;
    }
}
