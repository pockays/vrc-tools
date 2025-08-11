using System.IO;
using UnityEditor;
using UnityEngine;

namespace net.satania.kimeramaker.editor
{
    public static class LanguageManager
    {
        private const string k_PrefHeader = "Satania/Tool/KimeraMaker/";

        public static string[] fileGUIDs = new string[] { "9540400dc0f7bcd42890c73826a265ca", "f5b48c6cf8f5cec47be862561685dd5e", "e39ddc0c6a6d6054c8ac88a00973b473" };

        /// <summary>
        /// 言語、選択士
        /// </summary>
        private static string[] languageArray = new string[] { "日本語", "한국어", "English" };
        public static Localize Localized = new Localize();

        public static int LanguageIndex
        {
            get => EditorPrefs.GetInt(k_PrefHeader + "LanguageIndex", 0);
            set => EditorPrefs.SetInt(k_PrefHeader + "LanguageIndex", value);
        }


        /// <summary>
        /// 指定されたオブジェクトがプレハブの場合 true を返します
        /// </summary>

        public static Localize LoadLocalizeFile(int index)
        {
            string filename = AssetDatabase.GUIDToAssetPath(fileGUIDs[index]);
            if (!File.Exists(filename))
                return new Localize();

            string jsonText = File.ReadAllText(filename);
            var FromJson = JsonUtility.FromJson<Localize>(jsonText);

            if (FromJson == null)
                return new Localize();

            return FromJson;
        }

        public static void DrawLanguage()
        {
            var newLanguageIndex = EditorGUILayout.Popup("Language", LanguageIndex, languageArray);

            if (newLanguageIndex != LanguageIndex)
            {
                LanguageIndex = newLanguageIndex;
                Localized = LoadLocalizeFile(LanguageIndex);
            }
        }
    }
}
