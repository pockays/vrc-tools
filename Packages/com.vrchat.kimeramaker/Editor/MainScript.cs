using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

#if KM_VRC_AVATARS_VRCSDK3
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using static VRC.SDK3.Avatars.Components.VRCAvatarDescriptor;
using VRC.Core;
#endif

#if KM_MODULAR_AVATAR
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
#endif

// Copyright (c) 2022 さたにあ
namespace net.satania.kimeramaker.editor
{
    public partial class KimeraScript : EditorWindow
    {
        private enum eFXLayerAppendMode
        {
            Replace,
            Combine
        }

        private const string k_PrefHeader = "Satania/Tool/KimeraMaker/";

        private static eFXLayerAppendMode LayerAppendMode
        {
            get => (eFXLayerAppendMode)EditorPrefs.GetInt(k_PrefHeader + "LayerAppendMode", 0);
            set => EditorPrefs.SetInt(k_PrefHeader + "LayerAppendMode", (int)value);
        }

        public static bool AppendExMenu
        {
            get => EditorPrefs.GetBool(k_PrefHeader + "AppendExMenu", false);
            set => EditorPrefs.SetBool(k_PrefHeader + "AppendExMenu", value);
        }

        private static Localize Localized => LanguageManager.Localized;

        /// <summary>
        /// AvatarDescriptorの内容を引用するためにVRCAvatarDescriptroで取得
        /// </summary>
        private static VRCAvatarDescriptor HeadAvatar;

        /// <summary>
        /// FXに挿入するためにVRCAvatarDescriptorで取得
        /// </summary>
        private static VRCAvatarDescriptor BodyAvatar;

        /// <summary>
        /// キメラ作る時の名前
        /// </summary>
        public static string KimeraName = "キメラ";
        /// <summary>
        /// 右寄り文字用
        /// </summary>
        public static GUIStyle MiddleRightText = new GUIStyle();
        /// <summary>
        /// ブレンドシェイプリスト
        /// </summary>
        public static string[] BlendshapeNames = { "sil", "PP", "FF", "TH", "DD", "kk", "CH", "SS", "nn", "RR", "aa", "E", "ih", "oh", "ou" };
        /// <summary>
        /// キメラ用FXの生成場所
        /// </summary>
        public static string KimeraGenerated = "Assets/Saturnian/キメラ生成/Generated/";
        /// <summary>
        /// 言語選択の番号
        /// </summary>
        public static int languageIndex = 0;

        /// <summary>
        /// 目のボーンを子入れするか
        /// </summary>
        private static bool isEyeBoneChild = true;
        /// <summary>
        /// 上級者向けオプション
        /// </summary>
        public static bool HardOptionToggle;
        /// <summary>
        /// 選択中のページ
        /// </summary>
        public static int SelectedPage;

        /// <summary>
        /// レイヤー一覧 統合する際に使う
        /// </summary>
        public static AnimatorControllerLayer[] all_layers;

        public static int selectedLayers;

        /// <summary>
        /// スクロール用
        /// </summary>
        private static Vector2 HeadAvatarScroll = Vector2.zero;
        /// <summary>
        /// 同名オブジェクトを表示するフィールドのスクロール用
        /// </summary>
        private static Vector2 SomeNameObjectsScroll = Vector2.zero;

        /// <summary>
        /// 継承レイヤーのスクロール用
        /// </summary>
        private static Vector2 head_toggles_off_scroll = Vector2.zero;

        /// <summary>
        /// 継承レイヤーのスクロール用
        /// </summary>
        private static Vector2 head_toggles_on_scroll = Vector2.zero;
        /// <summary>
        /// 継承レイヤーのスクロール用
        /// </summary>
        private static Vector2 body_toggles_off_scroll = Vector2.zero;
        /// <summary>
        /// 継承レイヤーのスクロール用
        /// </summary>
        private static Vector2 body_toggles_on_scroll = Vector2.zero;

        //頭用
        /// <summary>
        /// キメラ用頭の左手のレイヤーを自動取得するため
        /// </summary>
        public static string HeadLeftHandLayerName = "Left Hand";
        /// <summary>
        /// キメラ用頭の右手のレイヤーを自動取得するため
        /// </summary>
        public static string HeadRightLayerName = "Right Hand";
        /// <summary>
        /// 頭のFXレイヤー取得用
        /// </summary>
        public static AnimatorControllerLayer[] HeadLayers = new AnimatorControllerLayer[2];

        //体用
        /// <summary>
        /// キメラ体の左手のレイヤーを自動取得するため
        /// </summary>
        public static string BodyLeftHandLayerName = "Left Hand";
        /// <summary>
        /// キメラ体の右手のレイヤーを自動取得するため
        /// </summary>
        public static string BodyRightHandLayerName = "Right Hand";
        /// <summary>
        /// 体のFXレイヤー取得用
        /// </summary>
        public static AnimatorControllerLayer[] BodyLayers = new AnimatorControllerLayer[2];

        /// <summary>
        /// ウィンドウを開いたときのイベント
        /// </summary>
        [MenuItem("さたにあしょっぴんぐ/Kimera Maker", false, 2)]
        private static void Init()
        {
            //ウィンドウのインスタンスを生成
            KimeraScript window = GetWindow<KimeraScript>("キメラメーカー");

            //ウィンドウサイズを固定
            window.maxSize = window.minSize = new Vector2(980, 540);

            //右寄りに設定
            MiddleRightText.alignment = TextAnchor.MiddleRight;
        }

        /// <summary>
        /// ページ毎に処理が違うと困るのでメソッド化
        /// </summary>
        private static void InitializeForHeadAvatar()
        {
            //中身をリセット
            Head_Toggles.Clear();

            //中身がある場合
            if (HeadAvatar != null)
            {
                //Humanoid判定をとってHumanoidでない場合nullに
                if (!HeadAvatar.IsHumanoidAvatar())
                {
                    HeadAvatar = null;
                    MessageBox.Show(Localized.HumanoidError, Localized.Yes);
                }
            }

            if (HeadAvatar == null)
                return;

            //FXコントローラー取得
            if (HeadAvatar.baseAnimationLayers[(int)AnimLayerType.FX - 1].animatorController != null)
            {
                var Controller = (AnimatorController)HeadAvatar.baseAnimationLayers[(int)AnimLayerType.FX - 1].animatorController;

                //継承するレイヤーを選択する用
                if (Controller.layers.Length > 0)
                {
                    for (int i = 0; i < Controller.layers.Length; i++)
                    {
                        //表情レイヤーのみをtrueに
                        bool isGestureLayer = Controller.layers[i].name == BodyLeftHandLayerName || Controller.layers[i].name == BodyRightHandLayerName;

                        Head_Toggles.Add(Controller.layers[i], isGestureLayer);
                    }
                }
            }
        }
        /// <summary>
        /// ページ毎に処理が違うと困るのでメソッド化
        /// </summary>
        private static void InitializeForBodyAvatar()
        {
            //中身をリセット
            Body_Toggles.Clear();

            //中身がある場合
            if (BodyAvatar != null)
            {
                //Humanoid判定をとってHumanoidでない場合nullに
                if (!BodyAvatar.IsHumanoidAvatar())
                {
                    BodyAvatar = null;
                    MessageBox.Show(Localized.HumanoidError, Localized.Yes);
                }
            }

            if (BodyAvatar == null)
                return;

            //FXコントローラー取得
            var FX = BodyAvatar.baseAnimationLayers[(int)AnimLayerType.FX - 1];

            if (FX.animatorController != null)
            {
                var Controller = (AnimatorController)FX.animatorController;

                //継承するレイヤーを選択する用
                if (Controller.layers.Length > 0)
                {
                    for (int i = 0; i < Controller.layers.Length; i++)
                    {
                        //表情レイヤーは予めfalseに
                        bool isGestureLayer = Controller.layers[i].name == BodyLeftHandLayerName || Controller.layers[i].name == BodyRightHandLayerName;

                        Body_Toggles.Add(Controller.layers[i], !isGestureLayer);
                    }
                }
            }
        }

        /// <summary>
        /// キメラ元を取得するためのフィールドを描画
        /// </summary>
        private static void DrawAvatarDescriptorField()
        {
            using (new GUILayout.HorizontalScope())
            {
                //頭 用フィールド
                using (new GUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(485), GUILayout.ExpandHeight(true)))
                {
                    DrawHeadDescriptor();
                }

                //体 用フィールド
                using (new GUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(485), GUILayout.ExpandHeight(true)))
                {
                    DrawBodyDescriptor();
                }
            }
        }

        private static void DrawHeadDescriptor()
        {
            var newHeadAvatar = EditorGUILayout.ObjectField(Localized.Head_Avatar, HeadAvatar, typeof(VRCAvatarDescriptor), true) as VRCAvatarDescriptor;
            if (newHeadAvatar != HeadAvatar)
            {
                HeadAvatar = newHeadAvatar;
                //頭用アバターが変わった際の処理
                InitializeForHeadAvatar();
            }

            if (HeadAvatar != null)
            {
                HeadAvatarScroll = EditorGUILayout.BeginScrollView(HeadAvatarScroll);

                if (HeadAvatar.gameObject.IsPrefab())
                {
                    if (GUILayout.Button(Localized.Unpack_Prefab))
                    {
                        PrefabUtility.UnpackPrefabInstance(HeadAvatar.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    }
                }
                else
                {
                    if (LayerAppendMode == eFXLayerAppendMode.Combine)
                    {
                        //左手と右手のレイヤーを取得出来なかった場合エラー
                        var Layers = SearchLayerfromName(HeadAvatar, HeadLeftHandLayerName, HeadRightLayerName);
                        if (Layers == null || Layers[0] == null || Layers[1] == null)
                        {
                            EditorGUILayout.HelpBox(Localized.CantFindGestureLayer, MessageType.Error);
                        }
                    }

                    //リップシンクがデフォルトの場合エラー
                    if (HeadAvatar.lipSync == VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.Default)
                    {
                        EditorGUILayout.HelpBox(Localized.DontLipsync, MessageType.Error);
                    }

                    //まばたき判定
                    if (!HeadAvatar.enableEyeLook)
                    {
                        EditorGUILayout.HelpBox(Localized.DontEyelook, MessageType.Error);
                    }
                    else
                    {
                        if (HeadAvatar.customEyeLookSettings.eyelidType == VRCAvatarDescriptor.EyelidType.None)
                        {
                            EditorGUILayout.HelpBox(Localized.DontEyelids, MessageType.Error);
                        }
                    }

                    EditorGUI.BeginDisabledGroup(true);
                    //LipSync情報表示用
                    //LipSync Modeを表示して、ブレンドシェイプタイプの場合のみリストを表示



                    GUILayout.Label("[LipSync]", EditorStyles.boldLabel);
                    EditorGUI.indentLevel++;

                    if (HeadAvatar.lipSync == VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.Default)
                        EditorGUILayout.TextField("LipSync Mode", Localized.NotHere);
                    else if (HeadAvatar.lipSync == VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.JawFlapBone)
                        EditorGUILayout.TextField("LipSync Mode", "JawFlapBone");
                    else if (HeadAvatar.lipSync == VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.JawFlapBlendShape)
                        EditorGUILayout.TextField("LipSync Mode", "JawFlapBlendShape");
                    else if (HeadAvatar.lipSync == VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShape)
                    {
                        EditorGUILayout.TextField("LipSync Mode", "VisemeBlendShape");
                        EditorGUILayout.ObjectField("Face Mesh", HeadAvatar.VisemeSkinnedMesh, typeof(SkinnedMeshRenderer), true);
                        GUILayout.Space(10);

                        //Viseme Blendshapesのリストを表示
                        for (int i = 0; i < BlendshapeNames.Length; i++)
                        {
                            EditorGUILayout.TextField($"Viseme :{BlendshapeNames[i]}", HeadAvatar.VisemeBlendShapes[i]);
                        }
                    }
                    else if (HeadAvatar.lipSync == VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.VisemeParameterOnly)
                        GUILayout.Label("LipSync Mode : VisemeParameterOnly");
                    EditorGUI.indentLevel--;

                    GUILayout.Space(15);


                    GUILayout.Label("[EyeLook]", EditorStyles.boldLabel);
                    EditorGUI.indentLevel++;
                    if (HeadAvatar.enableEyeLook)
                    {
                        EditorGUILayout.TextField($"Eye Look", "Enable");
                        EditorGUILayout.ObjectField("Left Eye Bone", HeadAvatar.customEyeLookSettings.leftEye, typeof(Transform), true);
                        EditorGUILayout.ObjectField("Right Eye Bone", HeadAvatar.customEyeLookSettings.rightEye, typeof(Transform), true);

                    }
                    else
                        EditorGUILayout.TextField($"Eye Look", "Disable");

                    GUILayout.Space(10);

                    GUILayout.Label("[Eyelids]", EditorStyles.boldLabel);
                    if (HeadAvatar.customEyeLookSettings.eyelidType == EyelidType.None)
                        EditorGUILayout.TextField("Eyelid Type", Localized.NotHere);
                    else if (HeadAvatar.customEyeLookSettings.eyelidType == EyelidType.Blendshapes)
                    {
                        EditorGUILayout.TextField("Eyelid Type", "Blendshapes");
                        EditorGUILayout.ObjectField("Eyelid Mesh", HeadAvatar.customEyeLookSettings.eyelidsSkinnedMesh, typeof(SkinnedMeshRenderer), true);

                    }
                    else if (HeadAvatar.customEyeLookSettings.eyelidType == EyelidType.Bones)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.TextField("Eyelid Type", "Bones");
                        EditorGUI.indentLevel--;
                    }
                    EditorGUI.indentLevel--;
                    EditorGUI.EndDisabledGroup();
                }

                EditorGUILayout.EndScrollView();
            }
        }
        private static void DrawBodyDescriptor()
        {
            var newBodyAvatar = EditorGUILayout.ObjectField(Localized.Body_Avatar, BodyAvatar, typeof(VRCAvatarDescriptor), true) as VRCAvatarDescriptor;
            if (newBodyAvatar != BodyAvatar)
            {
                BodyAvatar = newBodyAvatar;
                InitializeForBodyAvatar();
            }

            if (BodyAvatar != null)
            {
                if (BodyAvatar.gameObject.IsPrefab())
                {
                    if (GUILayout.Button(Localized.Unpack_Prefab))
                    {
                        PrefabUtility.UnpackPrefabInstance(BodyAvatar.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    }
                }
                else
                {
                    if (LayerAppendMode == eFXLayerAppendMode.Combine)
                    {
                        //左手と右手のレイヤーを取得出来なかった場合エラー
                        var Layers = SearchLayerfromName(BodyAvatar, BodyLeftHandLayerName, BodyRightHandLayerName);
                        if (Layers == null || Layers[0] == null || Layers[1] == null)
                        {
                            EditorGUILayout.HelpBox(Localized.CantFindGestureLayer, MessageType.Error);
                        }
                    }

                    if (HeadAvatar != null)
                    {
                        var SameObjects = SameNameObjects(HeadAvatar.transform, BodyAvatar.transform);
                        if (SameObjects.Count > 0)
                        {
                            GUILayout.Label(Localized.FoundSomeName);
                            GUILayout.Space(10);

                            using (new GUILayout.HorizontalScope())
                            {
                                if (GUILayout.Button(Localized.AllChangeName))
                                {
                                    foreach (var obj in SameObjects)
                                    {
                                        string name = obj.name + "[Rename]" + RandomTexts(2);
                                        obj.name = name;
                                    }
                                }
                            }

                            SomeNameObjectsScroll = EditorGUILayout.BeginScrollView(SomeNameObjectsScroll);
                            foreach (var obj in SameObjects)
                            {
                                using (new GUILayout.HorizontalScope())
                                {
                                    var _obj = EditorGUILayout.ObjectField(Localized.SomeNameObject, obj, typeof(VRCAvatarDescriptor), true) as VRCAvatarDescriptor;
                                    GUILayout.Space(3);
                                    if (GUILayout.Button(Localized.Deleting))
                                    {
                                        DestroyImmediate(obj);
                                    }
                                }
                            }
                            EditorGUILayout.EndScrollView();
                        }
                    }
                }
            }
        }

        private void DrawAdvancedField()
        {
            using (new GUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
            {
                DrawFXAppendModePopup();

                if (LayerAppendMode == eFXLayerAppendMode.Combine)
                {
                    EditorGUILayout.HelpBox(Localized.Obsolute_CombineFX, MessageType.Warning);

                    DrawAdvancedCombine();
                }
            }
        }

        private static void DrawAdvancedCombine()
        {
            using (new GUILayout.HorizontalScope())
            {
                //頭 用フィールド
                using (new GUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(485), GUILayout.ExpandHeight(true)))
                {
                    var newHeadAvatar = EditorGUILayout.ObjectField(Localized.Head_Avatar, HeadAvatar, typeof(VRCAvatarDescriptor), true) as VRCAvatarDescriptor;
                    if (newHeadAvatar != HeadAvatar)
                    {
                        HeadAvatar = newHeadAvatar;
                        InitializeForHeadAvatar();
                    }


                    if (HeadAvatar != null)
                    {
                        if (HeadAvatar.gameObject.IsPrefab())
                        {
                            if (GUILayout.Button(Localized.Unpack_Prefab))
                            {
                                PrefabUtility.UnpackPrefabInstance(HeadAvatar.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                            }
                        }
                        else
                        {
                            if (LayerAppendMode == eFXLayerAppendMode.Combine)
                            {

                                GUILayout.Label($"【{Localized.Inherited_layers}】");

                                //継承するレイヤーを選択するフィールドを描画
                                using (new GUILayout.HorizontalScope())
                                {
                                    List<AnimatorControllerLayer> layers = new List<AnimatorControllerLayer>(Head_Toggles.Keys);

                                    //OFFを描画
                                    using (new GUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(240), GUILayout.Height(250)))
                                    {
                                        head_toggles_off_scroll = EditorGUILayout.BeginScrollView(head_toggles_off_scroll);

                                        foreach (var layer in layers)
                                        {
                                            if (!Head_Toggles[layer])
                                                Head_Toggles[layer] = EditorGUILayout.Toggle(layer.name, Head_Toggles[layer]);
                                        }

                                        EditorGUILayout.EndScrollView();
                                    }

                                    //ONを描画
                                    using (new GUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(240), GUILayout.Height(250)))
                                    {
                                        head_toggles_on_scroll = EditorGUILayout.BeginScrollView(head_toggles_on_scroll);

                                        foreach (var layer in layers)
                                        {
                                            if (Head_Toggles[layer])
                                                Head_Toggles[layer] = EditorGUILayout.Toggle(layer.name, Head_Toggles[layer]);
                                        }

                                        EditorGUILayout.EndScrollView();
                                    }
                                }
                            }
                        }
                    }
                }

                //体 用フィールド
                using (new GUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(485), GUILayout.ExpandHeight(true)))
                {
                    var newBodyAvatar = EditorGUILayout.ObjectField(Localized.Body_Avatar, BodyAvatar, typeof(VRCAvatarDescriptor), true) as VRCAvatarDescriptor;
                    if (newBodyAvatar != BodyAvatar)
                    {
                        BodyAvatar = newBodyAvatar;
                        InitializeForBodyAvatar();
                    }

                    if (BodyAvatar != null)
                    {
                        if (BodyAvatar.gameObject.IsPrefab())
                        {
                            if (GUILayout.Button(Localized.Unpack_Prefab))
                            {
                                PrefabUtility.UnpackPrefabInstance(BodyAvatar.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                            }
                        }
                        else
                        {
                            if (LayerAppendMode == eFXLayerAppendMode.Combine)
                            {
                                GUILayout.Label($"【{Localized.Inherited_layers}】");

                                //継承するレイヤーを選択するフィールドを描画
                                using (new GUILayout.HorizontalScope())
                                {
                                    List<AnimatorControllerLayer> layers = new List<AnimatorControllerLayer>(Body_Toggles.Keys);

                                    //OFFを描画
                                    using (new GUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(240), GUILayout.Height(250)))
                                    {
                                        body_toggles_off_scroll = EditorGUILayout.BeginScrollView(body_toggles_off_scroll);

                                        foreach (var layer in layers)
                                        {
                                            if (!Body_Toggles[layer])
                                                Body_Toggles[layer] = EditorGUILayout.Toggle(layer.name, Body_Toggles[layer]);
                                        }

                                        EditorGUILayout.EndScrollView();
                                    }

                                    //ONを描画
                                    using (new GUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(240), GUILayout.Height(250)))
                                    {
                                        body_toggles_on_scroll = EditorGUILayout.BeginScrollView(body_toggles_on_scroll);

                                        foreach (var layer in layers)
                                        {
                                            if (Body_Toggles[layer])
                                                Body_Toggles[layer] = EditorGUILayout.Toggle(layer.name, Body_Toggles[layer]);
                                        }

                                        EditorGUILayout.EndScrollView();
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private bool Prepare(out VRCAvatarDescriptor headavatar, out VRCAvatarDescriptor bodyavatar)
        {
            headavatar = HeadAvatar;
            bodyavatar = BodyAvatar;

            //0 = Yes, 2 = No, 1 = Cancel & X
            int MessageBoxValue = MessageBox.Show(Localized.IsNeedBackup, Localized.Yes, Localized.No, Localized.Cancel);

            //キャンセルする場合
            if (MessageBoxValue == 1)
                return false;

            //はいを押した場合
            if (MessageBoxValue == 0)
            {
                headavatar = Instantiate(headavatar);
                bodyavatar = Instantiate(bodyavatar);

                EditorUtility.SetDirty(headavatar);
                EditorUtility.SetDirty(bodyavatar);

                HeadAvatar.gameObject.SetActive(false);
                BodyAvatar.gameObject.SetActive(false);
                headavatar.gameObject.SetActive(true);
                bodyavatar.gameObject.SetActive(true);
            }

            return true;
        }

        private void PrepareProcess()
        {
            if (Prepare(out VRCAvatarDescriptor headavatar, out VRCAvatarDescriptor bodyavatar))
            {
                //生成
                Process(headavatar, bodyavatar);
            }
        }
        private void Process(VRCAvatarDescriptor headAvatar, VRCAvatarDescriptor bodyAvatar)
        {
            Animator HeadAnimator = headAvatar.GetComponent<Animator>();
            Animator BodyAnimator = bodyAvatar.GetComponent<Animator>();

            //頭のボーンから推定する
            if (KimeraUtils.TryGetEstimationViewpoint(BodyAnimator, HeadAnimator, headAvatar.ViewPosition, out var viewPoint))
            {
                bodyAvatar.ViewPosition = new Vector3()
                {
                    x = headAvatar.ViewPosition.x,
                    y = viewPoint.y,
                    z = headAvatar.ViewPosition.z,
                };
            }
            else
            {
                //推定出来なかった場合はそのまま入れる
                bodyAvatar.ViewPosition = headAvatar.ViewPosition;
            }

            //リップシンクのステータスをコピー
            LipsyncCopy(headAvatar, bodyAvatar);

            //目 ステータスをコピー
            EyeLookCopy(headAvatar, bodyAvatar, true);

            //ボーンを全てくっつける
            for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
            {
                if (!isEyeBoneChild && ((HumanBodyBones)i == HumanBodyBones.LeftEye || (HumanBodyBones)i == HumanBodyBones.RightEye))
                    continue;

                Transform HeadAvatarBone = HeadAnimator.GetBoneTransform((HumanBodyBones)i);
                Transform BodyAvatarBone = BodyAnimator.GetBoneTransform((HumanBodyBones)i);

                if (HeadAvatarBone == null || BodyAvatarBone == null)
                    continue;

                HeadAvatarBone.transform.SetParent(BodyAvatarBone.transform);
            }

            int headAvatarLen = headAvatar.gameObject.transform.childCount;

            //0になるまでルートに展開
            while (headAvatarLen > 0)
            {
                Transform AllObject = headAvatar.transform.GetChild(0);
                AllObject.transform.SetParent(bodyAvatar.transform);

                headAvatarLen = headAvatar.gameObject.transform.childCount;
            }


            if (LayerAppendMode == eFXLayerAppendMode.Replace)
            {
                //置き換え
                AnimatorController headFX = HeadAvatar.GetController(AnimLayerType.FX);
                VRCExpressionsMenu headMenu = HeadAvatar.expressionsMenu;
                VRCExpressionParameters headParameter = HeadAvatar.expressionParameters;

                if (headFX != null)
                {
                    if (!bodyAvatar.customizeAnimationLayers)
                        bodyAvatar.customizeAnimationLayers = true;

                    bodyAvatar.SetController(headFX, AnimLayerType.FX);
                }

                if (headMenu != null && headParameter != null)
                {
                    if (!bodyAvatar.customExpressions)
                        bodyAvatar.customExpressions = true;

                    bodyAvatar.expressionsMenu = headMenu;
                    bodyAvatar.expressionParameters = headParameter;
                }

                EditorUtility.SetDirty(bodyAvatar);
            }
            else if (LayerAppendMode == eFXLayerAppendMode.Combine)
            {
                //合成

                List<AnimatorControllerLayer> headLayers = new List<AnimatorControllerLayer>();
                foreach (var headlayer in Head_Toggles)
                {
                    //継承レイヤーがチェックされていた場合のみ追加
                    if (headlayer.Value)
                        headLayers.Add(headlayer.Key);
                }

                List<AnimatorControllerLayer> bodyLayers = new List<AnimatorControllerLayer>();
                foreach (var bodylayer in Body_Toggles)
                {
                    //継承レイヤーがチェックされていた場合のみ追加
                    if (bodylayer.Value)
                        bodyLayers.Add(bodylayer.Key);
                }

                //FXレイヤーをコピー (上級者向けオプションで選択したレイヤーのみ)
                FXLayersCopy_new(bodyAvatar, headAvatar, headLayers.ToArray(), bodyLayers.ToArray());
            }

            //名前を変更
            bodyAvatar.name = KimeraName;

            //顔の元オブジェクトを削除
            DestroyImmediate(headAvatar.gameObject);

            //キメラをアクティブに
            bodyAvatar.gameObject.SetActive(true);

            MessageBox.Show(Localized.Complete, Localized.Yes);
        }

#if KM_MODULAR_AVATAR
        private void PrepareProcessMA()
        {
            if (Prepare(out VRCAvatarDescriptor headavatar, out VRCAvatarDescriptor bodyavatar))
            {
                //生成
                ProcessMA(headavatar, bodyavatar);
            }
        }

        private static void ProcessMA(VRCAvatarDescriptor headAvatar, VRCAvatarDescriptor bodyAvatar)
        {
            Animator HeadAnimator = headAvatar.GetComponent<Animator>();
            Animator BodyAnimator = bodyAvatar.GetComponent<Animator>();

            Transform headAvatarTransform = headAvatar.transform;
            GameObject headAvatarGO = headAvatar.gameObject;

            //頭のボーンから推定する
            if (KimeraUtils.TryGetEstimationViewpoint(BodyAnimator, HeadAnimator, headAvatar.ViewPosition, out var viewPoint))
            {
                bodyAvatar.ViewPosition = new Vector3()
                {
                    x = headAvatar.ViewPosition.x,
                    y = viewPoint.y,
                    z = headAvatar.ViewPosition.z,
                };
            }
            else
            {
                //推定出来なかった場合はそのまま入れる
                bodyAvatar.ViewPosition = headAvatar.ViewPosition;
            }

            AnimatorController headFX = headAvatar.GetController(AnimLayerType.FX);
            VRCExpressionsMenu headMenu = headAvatar.expressionsMenu;
            VRCExpressionParameters headParameters = headAvatar.expressionParameters;

            //リップシンクのステータスをコピー
            LipsyncCopy(headAvatar, bodyAvatar);

            //目 ステータスをコピー
            EyeLookCopy(headAvatar, bodyAvatar, false);

            headAvatarTransform.SetParent(bodyAvatar.transform);

            PipelineManager pipelineManager = headAvatar.GetComponent<PipelineManager>();
            if (pipelineManager != null)
                DestroyImmediate(pipelineManager);

            if (headAvatar != null)
                DestroyImmediate(headAvatar);

            SetupOutfit.SetupOutfitUI(headAvatarGO);

            if (headFX != null)
            {
                ModularAvatarMergeAnimator mergeAnimator = headAvatarGO.AddComponent<ModularAvatarMergeAnimator>();
                mergeAnimator.animator = headFX;
                mergeAnimator.mergeAnimatorMode = MergeAnimatorMode.Append;
                mergeAnimator.matchAvatarWriteDefaults = true;
                mergeAnimator.layerType = AnimLayerType.FX;
                mergeAnimator.layerPriority = 0;
                mergeAnimator.pathMode = MergeAnimatorPathMode.Relative;
                mergeAnimator.relativePathRoot.Set(headAvatarGO);
                mergeAnimator.deleteAttachedAnimator = false;

                EditorUtility.SetDirty(mergeAnimator);
            }

            if (headMenu != null && headParameters != null)
            {
                ModularAvatarMenuInstaller menuInstaller = headAvatarGO.AddComponent<ModularAvatarMenuInstaller>();
                menuInstaller.menuToAppend = headMenu;

                ModularAvatarParameters maParameters = headAvatarGO.AddComponent<ModularAvatarParameters>();
                maParameters.ImportValues(headParameters);

                EditorUtility.SetDirty(menuInstaller);
                EditorUtility.SetDirty(maParameters);
            }

            //名前を変更
            bodyAvatar.name = KimeraName + " (MA)";

            //キメラをアクティブに
            bodyAvatar.gameObject.SetActive(true);

            MessageBox.Show(Localized.Complete, Localized.Yes);
        }
#endif

        /// <summary>
        /// 有効化された時
        /// </summary>
        private void OnEnable()
        {
            HeadAvatar = null;
            BodyAvatar = null;

            HeadLeftHandLayerName = "Left Hand";
            BodyLeftHandLayerName = "Left Hand";
            HeadLayers = new AnimatorControllerLayer[2];

            HeadRightLayerName = "Right Hand";
            BodyRightHandLayerName = "Right Hand";
            BodyLayers = new AnimatorControllerLayer[2];

        }
        private void Update()
        {
            if (EditorApplication.isPlaying)
            {
                this.Close();
            }
        }

        private void DrawFXAppendModePopup()
        {
            var newLayerAppendMode = (eFXLayerAppendMode)EditorGUILayout.Popup(Localized.FXAppendMode, (int)LayerAppendMode,
                new string[]
                {
                    Localized.Replace,
                    Localized.Combine
                });

            if (newLayerAppendMode != LayerAppendMode)
            {
                LayerAppendMode = newLayerAppendMode;
            }
        }

        /// <summary>
        /// エディタ描画用
        /// </summary>
        private void OnGUI()
        {
            LanguageManager.DrawLanguage();

            //ページ選択用
            SelectedPage = GUILayout.Toolbar(SelectedPage, new string[] { Localized.AvatarInfo, Localized.For_advanced_users }, EditorStyles.toolbarButton);

            switch (SelectedPage)
            {
                case 0:
                    //キメラ元を取得するためのフィールドを描画
                    DrawAvatarDescriptorField(); break;

                case 1: DrawAdvancedField(); break;
            }

            DrawFXAppendModePopup();

            isEyeBoneChild = EditorGUILayout.ToggleLeft(Localized.EyeBoneParent, isEyeBoneChild);

            //両方中身がある場合
            if (HeadAvatar != null && BodyAvatar != null)
            {
                if (HeadAvatar == BodyAvatar)
                    EditorGUILayout.HelpBox(Localized.BodyHeadSome, MessageType.Error);
                else
                {
                    using (new GUILayout.HorizontalScope())
                    {
                        //名前用フィールドを表示
                        KimeraName = EditorGUILayout.TextField(Localized.Kimera_Name, KimeraName);

                        //キメラ作るボタン
                        if (GUILayout.Button(Localized.Kimera_Create))
                        {
                            PrepareProcess();
                        }

#if KM_MODULAR_AVATAR
                        if (GUILayout.Button(Localized.SetupByModularAvatar))
                        {
                            PrepareProcessMA();
                        }
#endif
                    }

                }
            }
        }
    }
}
