using UnityEditor;

namespace net.satania.kimeramaker.editor
{
    public static class MessageBox
    {
        /// <summary>
        /// OKのみのメッセージボックス
        /// </summary>
        /// <param name="title"></param>
        /// <param name="message"></param>
        /// <param name="ok"></param>
        public static void Show(string message, string ok)
        {
            EditorUtility.DisplayDialog("Kimera Maker", message, ok);
        }
        /// <summary>
        /// 質問形メッセージボックス
        /// </summary>
        /// <param name="title"></param>
        /// <param name="message"></param>
        /// <param name="yes"></param>
        /// <param name="no"></param>
        /// <returns></returns>
        public static bool Show(string message, string yes, string no)
        {
            return EditorUtility.DisplayDialog("Kimera Maker", message, yes, no);
        }
        /// <summary>
        /// 0 = Yes,
        /// 1 = Cance,
        /// 2 = No
        /// </summary>
        /// <param name="message"></param>
        /// <param name="yes"></param>
        /// <param name="no"></param>
        /// <param name="cancel"></param>
        /// <returns></returns>
        public static int Show(string message, string yes, string no, string cancel)
        {
            return EditorUtility.DisplayDialogComplex("Kimera Maker", message, yes, cancel, no);
        }
    }
}
