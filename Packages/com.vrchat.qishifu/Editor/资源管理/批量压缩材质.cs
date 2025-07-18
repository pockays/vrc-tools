using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class 批量压缩材质 : EditorWindow
{
    private string folderPath = "Assets"; // 默认路径
    private Vector2 scrollPosition;
    private List<string> largeImages = new List<string>();

    [MenuItem("奇师傅工具箱/工具/资源管理/批量压缩材质", false, 0)]
    public static void ShowWindow()
    {
        GetWindow<批量压缩材质>("批量压缩材质").minSize = new Vector2(300, 400); // 设置最小窗口尺寸
    }

    private void OnGUI()
    {
        GUILayout.Label("指定Assets文件夹下的所有大图缩小到1024分辨率", EditorStyles.boldLabel);

        GUILayout.BeginHorizontal();
        GUILayout.Label("选择文件夹:", GUILayout.Width(80));
        folderPath = EditorGUILayout.TextField(folderPath, GUILayout.ExpandWidth(true));

        if (GUILayout.Button("选择", GUILayout.Width(60)))
        {
            string selectedPath = EditorUtility.OpenFolderPanel("选择文件夹", "Assets", "");
            if (!string.IsNullOrEmpty(selectedPath))
            {
                folderPath = "Assets" + selectedPath.Replace(Application.dataPath, "");
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(10); // 增加间距

        if (GUILayout.Button("查找大于1024的图像", GUILayout.ExpandWidth(true)))
        {
            largeImages.Clear();
            FindLargeImages(folderPath);
        }

        GUILayout.Space(10); // 增加间距

        GUILayout.Label("大图列表：", EditorStyles.boldLabel);
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));
        
        foreach (var image in largeImages)
        {
            GUILayout.Label(image);
        }

        EditorGUILayout.EndScrollView();

        GUILayout.Space(10); // 增加间距

        if (GUILayout.Button("缩小选中的图像", GUILayout.ExpandWidth(true)))
        {
            ResizeLargeImages();
        }
    }

    private void FindLargeImages(string path)
    {
        string[] files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);
        foreach (string file in files)
        {
            if (file.EndsWith(".png") || file.EndsWith(".jpg") || file.EndsWith(".jpeg") || file.EndsWith(".tga"))
            {
                string assetPath = file.Replace(Application.dataPath, "Assets");
                TextureImporter textureImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;

                if (textureImporter != null)
                {
                    Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                    if (texture != null && (texture.width > 1024 || texture.height > 1024))
                    {
                        largeImages.Add(assetPath);
                    }
                }
            }
        }
    }

    private void ResizeLargeImages()
    {
        foreach (string imagePath in largeImages)
        {
            TextureImporter textureImporter = AssetImporter.GetAtPath(imagePath) as TextureImporter;
            if (textureImporter != null)
            {
                // 注册撤销操作，以便可以撤销更改
                Undo.RegisterCompleteObjectUndo(textureImporter, "Resize Image");

                textureImporter.maxTextureSize = 1024; // 设置最大尺寸为1024
                textureImporter.SaveAndReimport();    // 重新导入图像以应用更改
            }
        }

        AssetDatabase.Refresh(); // 刷新数据库以更新资源
        largeImages.Clear();
    }
}
