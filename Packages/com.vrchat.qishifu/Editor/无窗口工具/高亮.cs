using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;

/// <summary>
/// 资源导入文件夹高亮工具
/// 功能：自动高亮导入Unitypackage时的顶层文件夹并选中所有相关文件夹
/// </summary>
public class ImportFolderHighlighter : AssetPostprocessor
{
    private static bool IsImportingPackage()
    {
        // Unity在导入package时会创建临时文件夹
        string tempPath = Path.Combine(Path.GetTempPath(), "Unity");
        if (!Directory.Exists(tempPath))
            return false;

        try
        {
            // 检查是否存在Unity package导入相关的临时文件
            var files = Directory.GetFiles(tempPath, "*.manifest", SearchOption.AllDirectories);
            return files.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
    {
        // 只在导入package时处理
        if (!IsImportingPackage())
            return;

        if (importedAssets.Length == 0)
            return;

        // 获取并过滤顶层文件夹
        var topFolders = importedAssets
            .Select(path => path.Split('/'))
            .Where(parts => parts.Length > 1)
            .Select(parts => parts[1])
            .Distinct()
            .OrderBy(folder => folder)
            .ToList();

        if (topFolders.Count == 0)
            return;

        // 加载文件夹对象
        var folderObjects = topFolders
            .Select(folder => AssetDatabase.LoadAssetAtPath<Object>("Assets/" + folder))
            .Where(obj => obj != null)
            .ToArray();

        if (folderObjects.Length > 0)
        {
            // 选中所有相关文件夹
            Selection.objects = folderObjects;
            
            // 高亮第一个文件夹
            EditorUtility.FocusProjectWindow();
            EditorGUIUtility.PingObject(folderObjects[0]);
        }
    }
}
