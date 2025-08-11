﻿/*
AvatarModifyTools
https://github.com/HhotateA/AvatarModifyTools

Copyright (c) 2021 @HhotateA_xR

This software is released under the MIT License.
http://opensource.org/licenses/mit-license.php
*/
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;
#if VRC_SDK_VRCSDK3
using VRC.SDK3.Avatars.Components;
#endif

namespace HhotateA.AvatarModifyTools.Core
{
    public class WindowBase : EditorWindow
    {
#if VRC_SDK_VRCSDK3
        public VRCAvatarDescriptor avatar;
#else
        public Animator avatar;
#endif
        public Animator avatarAnim;
        
        public bool expandOptions = false;
        public bool writeDefault = false;
        public bool overrideSettings = true;
        public bool renameParameters = true;
        public bool modifyOriginalAsset = true;
        public bool autoNextPage = true;
        public bool overrideNullAnimation = true;

        public void VRCErrorLabel()
        {
#if VRC_SDK_VRCSDK3
#else
            GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.alignment = TextAnchor.MiddleCenter;
            titleStyle.fontSize = 12;
            titleStyle.fontStyle = FontStyle.Normal;
            titleStyle.normal = new GUIStyleState()
            {
                textColor = Color.red
            };
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Please import VRCSDK3.0 in your project.",titleStyle);
#endif
        }

        private StatusView _status;
        public StatusView status
        {
            get
            {
                if (_status == null)
                {
                    _status = new StatusView();
                }

                return _status;
            }
        }

        public void OnFinishSetup()
        {
            status.Success("Complete Setup");
        }
        public void OnFinishRevert()
        {
            status.OK("Complete Revert");
        }
        public void OnCancel()
        {
            status.Warning("Canceled");
        }
        public void OnError(Exception e)
        {
            status.Error(e.Message);
        }
        
        public void OnSave()
        {
            status.Success("Saved");
        }
        public void OnLoad()
        {
            status.Success("Loaded");
        }

        public void AvatartField(string label = "",Action onReload = null)
        {
#if VRC_SDK_VRCSDK3
            var ava = EditorGUILayout.ObjectField(label, avatar, typeof(VRCAvatarDescriptor), true) as VRCAvatarDescriptor;
            if (ava && ava != avatar)
            {
                var anim = ava.GetComponent<Animator>();
                if (anim)
                {
                    avatar = ava;
                    avatarAnim = anim;
                    onReload?.Invoke();
                }
                else
                {
                    status.Warning("Avatar is not Humanoid");
                }
            }
#else
            avatar = EditorGUILayout.ObjectField(label, avatar, typeof(Animator), true) as Animator;
#endif
            if (!avatar)
            {
                EditorGUILayout.LabelField("アバターをドラッグドロップしてください．");
            }
        }

        public bool ShowOptions()
        {
            expandOptions = EditorGUILayout.Foldout(expandOptions,"Modify Options");
            if (expandOptions)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(" ",GUILayout.Width(20));
                    using (new EditorGUILayout.VerticalScope())
                    {
                        writeDefault = EditorGUILayout.Toggle("Override Write Default", writeDefault); 
                        // overrideNullAnimation = EditorGUILayout.Toggle("Override Null Animation", overrideNullAnimation);
                        renameParameters = EditorGUILayout.Toggle("Rename Parameters", renameParameters); 
                        //modifyOriginalAsset = EditorGUILayout.Toggle("Allow Modify Assets", modifyOriginalAsset); 
                        //overrideSettings = EditorGUILayout.Toggle("Revert Before Setup", overrideSettings); 
                        autoNextPage = EditorGUILayout.Toggle("Auto Next Page", autoNextPage);
                    }
                }
            }

            return expandOptions;
        }

#if VRC_SDK_VRCSDK3
        public AvatarModifyTool ApplySettings(AvatarModifyTool mod)
        {
            if (writeDefault)
            {
                mod.WriteDefaultOverride = true;
            }
            mod.OverrideSettings = overrideSettings;
            mod.RenameParameters = renameParameters;
            mod.ModifyOriginalAsset = modifyOriginalAsset;
            mod.AutoAddNextPage = autoNextPage;
            mod.OverrideNullAnimation = overrideNullAnimation;
            return mod;
        }
#endif

        public static GUIStyle TitleStyle(string title = "",int fontSize = 17,int outline = 1)
        {
            GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.alignment = TextAnchor.MiddleCenter;
            titleStyle.fontSize = fontSize;
            titleStyle.fontStyle = FontStyle.Normal;
            titleStyle.normal = new GUIStyleState()
            {
                textColor = new Color(0.9960784f,0.7254902f,0.7686275f)
            };
            
            GUIStyle outlineStyle = new GUIStyle(GUI.skin.label);
            outlineStyle.alignment = TextAnchor.MiddleCenter;
            outlineStyle.fontSize = fontSize;
            outlineStyle.fontStyle = FontStyle.Normal;
            outlineStyle.normal = new GUIStyleState()
            {
                textColor = Color.black
            };
            
            if (!String.IsNullOrWhiteSpace(title))
            {
                var rect = GUILayoutUtility.GetRect(new GUIContent(title), titleStyle);
                var r = rect;
                r.x += outline;
                EditorGUI.LabelField(r,title,outlineStyle);
                r = rect;
                r.x -= outline;
                EditorGUI.LabelField(r,title,outlineStyle);
                r = rect;
                r.y += outline;
                EditorGUI.LabelField(r,title,outlineStyle);
                r = rect;
                r.y -= outline;
                EditorGUI.LabelField(r,title,outlineStyle);
                EditorGUI.LabelField(rect,title,titleStyle);
            }
            return titleStyle;
        }
        
        public static GUIStyle DetailStyle(string title = "",string readme = "")
        {
            GUIStyle detailStyle = new GUIStyle(GUI.skin.label);
            detailStyle.alignment = TextAnchor.LowerCenter;
            detailStyle.fontSize = 10;
            detailStyle.fontStyle = FontStyle.Normal;
            detailStyle.normal = new GUIStyleState()
            {
                textColor = new Color(0.9960784f,0.7254902f,0.7686275f)
            };

            using (new EditorGUILayout.HorizontalScope())
            {
                if (!String.IsNullOrWhiteSpace(title))
                {
                    EditorGUILayout.LabelField(title,detailStyle);
                }
                if (!String.IsNullOrWhiteSpace(readme))
                {
                    if (GUILayout.Button(AssetUtility.LoadAssetAtGuid<Texture>(EnvironmentVariable.linkIcon),
                        GUILayout.Width(25),GUILayout.Height(25)))
                    {
                        var md = AssetUtility.LoadAssetAtGuid<Object>(readme);
                        Selection.objects = new Object[]{md};
                    }
                }
            }
            EditorGUILayout.Space();
            return detailStyle;
        }
        
        public static void Signature()
        {
            GUIStyle instructions = new GUIStyle(GUI.skin.label);
            instructions.fontSize = 10;
            instructions.wordWrap = true;
            
            GUIStyle signature = new GUIStyle(GUI.skin.label);
            signature.alignment = TextAnchor.LowerRight;
            signature.fontSize = 10;
            signature.wordWrap = false;
            
            EditorGUILayout.Space();
            EditorGUILayout.Space();
            
            EditorGUILayout.LabelField("ボタンを押すと，アバターのFX用AnimatorController，ExpressionParamrter，ExpressionMenuに改変を加えます．",instructions);
            EditorGUILayout.LabelField("操作は元に戻せないので，必ずバックアップをとっていることを確認してください．",instructions);
            
            EditorGUILayout.Space();
            
            var p = GUILayoutUtility.GetRect(0, 0, GUI.skin.box);
            p.height = 70;
            p.width = 70;
            EditorGUI.DrawPreviewTexture(p, 
                AssetUtility.LoadAssetAtGuid<Texture>(EnvironmentVariable.icon),
                AssetUtility.LoadAssetAtGuid<Material>(EnvironmentVariable.iconMat)); 
            
            EditorGUILayout.LabelField( "powered by AvatarModifyTool @HhotateA_xR" ,signature);
            EditorGUILayout.LabelField( "Version " + EnvironmentVariable.version ,signature);
            EditorGUILayout.LabelField( EnvironmentVariable.githubLink ,signature);
            // EditorGUILayout.LabelField( EnvironmentVariable.githubLink ,signature);
        }

        public void DetectAnimatorError()
        {
            if (!writeDefault)
            {
                var writeDefaultLayers = FindWriteDefault();
                if (writeDefaultLayers.Count > 0)
                {
                    status.Warning("Complete Setup (Detect WriteDefault Layers)");
                    foreach (var c in writeDefaultLayers)
                    {
                        Debug.LogWarning("Detect WriteDefault Layer : " + c);
                    }
                }
            }
        }
        
        List<string> FindWriteDefault()
        {
#if VRC_SDK_VRCSDK3
            AvatarModifyTool mod = new AvatarModifyTool(avatar);
            ApplySettings(mod);

            var writeDefaultLayers = mod.HasWriteDefaultLayers();
            return writeDefaultLayers;
#else
            return new List<string>();
#endif
        }

        public class StatusView
        {
            private GUIStyle msgStyle;
            private string msg;
            public StatusView()
            {
                msgStyle = new GUIStyle(GUI.skin.label);
                msgStyle.alignment = TextAnchor.UpperLeft;
                msgStyle.fontSize = 10;
                msgStyle.fontStyle = FontStyle.Normal;
                msgStyle.normal = new GUIStyleState()
                {
                    textColor = Color.gray
                };
            }
            public void Message(string m)
            {
                msgStyle.normal = new GUIStyleState()
                {
                    textColor = Color.gray
                };
                msg = m;
            }
            public void Success(string m)
            {
                msgStyle.normal = new GUIStyleState()
                {
                    textColor = Color.green
                };
                msg = m;
            }
            public void OK(string m)
            {
                msgStyle.normal = new GUIStyleState()
                {
                    textColor = Color.blue
                };
                msg = m;
            }
            public void Warning(string m)
            {
                msgStyle.normal = new GUIStyleState()
                {
                    textColor = Color.yellow
                };
                msg = m;
            }
            public void Error(string m)
            {
                msgStyle.normal = new GUIStyleState()
                {
                    textColor = Color.red
                };
                msg = m;
            }
            public void Display()
            {
                EditorGUILayout.LabelField("Status : "+msg,msgStyle);
            }
        }
    }
}