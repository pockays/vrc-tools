
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

#if KM_VRC_AVATARS_VRCSDK3
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using static VRC.SDK3.Avatars.Components.VRCAvatarDescriptor;
#endif

#if KM_MODULAR_AVATAR
using nadena.dev.modular_avatar.core;
#endif

namespace net.satania.kimeramaker.editor
{
    public static class ExtensionClass
    {
#if KM_MODULAR_AVATAR
        //AvatarParametersEditor.cs L:178
        public static void ImportValues(this ModularAvatarParameters maParameters, VRCExpressionParameters importProp)
        {
            var known = new HashSet<string>();

            var target = maParameters;
            foreach (var parameter in target.parameters)
            {
                if (!parameter.isPrefix)
                {
                    known.Add(parameter.nameOrPrefix);
                }
            }

            Undo.RecordObject(target, "Import parameters");

            foreach (var parameter in importProp.parameters)
            {
                if (!known.Contains(parameter.name))
                {
                    ParameterSyncType pst;

                    switch (parameter.valueType)
                    {
                        case VRCExpressionParameters.ValueType.Bool: pst = ParameterSyncType.Bool; break;
                        case VRCExpressionParameters.ValueType.Float: pst = ParameterSyncType.Float; break;
                        case VRCExpressionParameters.ValueType.Int: pst = ParameterSyncType.Int; break;
                        default: pst = ParameterSyncType.Float; break;
                    }

                    target.parameters.Add(new ParameterConfig()
                    {
                        internalParameter = false,
                        nameOrPrefix = parameter.name,
                        isPrefix = false,
                        remapTo = "",
                        syncType = pst,
                        localOnly = !parameter.networkSynced,
                        defaultValue = parameter.defaultValue,
                        saved = parameter.saved,
                    });
                }
            }
        }
#endif

        public static void SetController(this VRCAvatarDescriptor avatarDescriptor, AnimatorController controller, AnimLayerType layerType)
        {
            if (avatarDescriptor == null || !avatarDescriptor.customizeAnimationLayers)
                return;

            for (int i = 0; i < avatarDescriptor.baseAnimationLayers.Length; i++)
            {
                var layer = avatarDescriptor.baseAnimationLayers[i];
                if (layer.type != layerType)
                    continue;

                if (controller != null)
                {
                    layer.animatorController = controller;
                }
                else
                {
                    //入力されたコントローラーがnullだった場合は、isDefaultフラグを立てる
                    layer.animatorController = null;
                    layer.isDefault = true;
                    layer.isEnabled = false;
                }
                avatarDescriptor.baseAnimationLayers[i] = layer;
            }

            EditorUtility.SetDirty(avatarDescriptor);
        }

        public static AnimatorController GetController(this VRCAvatarDescriptor avatarDescriptor, AnimLayerType layerType)
        {
            if (!avatarDescriptor.customizeAnimationLayers)
                return new AnimatorController();

            var runtimeController = avatarDescriptor.baseAnimationLayers.Where(x => x.type == layerType).Select(x => x.animatorController).FirstOrDefault();

            return runtimeController == null ? null : (AnimatorController)runtimeController;
        }

        public static bool IsPrefab(this UnityEngine.GameObject self)
        {
            var type = PrefabUtility.GetPrefabAssetType(self);

            return
                type == PrefabAssetType.Model ||
                type == PrefabAssetType.MissingAsset ||
                type == PrefabAssetType.Regular ||
                type == PrefabAssetType.Variant;
        }

        /// <summary>
        /// Animatorを取得してisHuman判定する
        /// </summary>
        /// <param name="Avatar"></param>
        public static bool IsHumanoidAvatar(this VRCAvatarDescriptor Avatar)
        {
            //アニメータを取得
            Animator HeadAvatarAnimator = Avatar.GetComponent<Animator>();
            if (HeadAvatarAnimator == null)
            {
                return false;
            }

            return HeadAvatarAnimator.isHuman;
        }
    }
    public static class AnimatorControllerUtility
    {
        public static void CombineAnimatorController(AnimatorController srcController, AnimatorController dstController)
        {
            var dstControllerPath = AssetDatabase.GetAssetPath(dstController);

            for (int i = 0; i < srcController.layers.Length; i++)
            {
                AddLayer(dstController, srcController.layers[i], i == 0, dstControllerPath);
            }

            foreach (var parameter in srcController.parameters)
            {
                AddParameter(dstController, parameter);
            }

            EditorUtility.SetDirty(dstController);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static AnimatorControllerLayer AddLayer(AnimatorController controller, AnimatorControllerLayer srcLayer, bool setWeightTo1 = false, string controllerPath = "")
        {
            if (string.IsNullOrEmpty(controllerPath))
            {
                controllerPath = AssetDatabase.GetAssetPath(controller);
            }

            var newLayer = DuplicateLayer(srcLayer, controller.MakeUniqueLayerName(srcLayer.name), controllerPath, setWeightTo1);
            controller.AddLayer(newLayer);

            // Unity再起動後も保持するためにアセットにオブジェクトを追加する必要がある
            AddObjectsInStateMachineToAnimatorController(newLayer.stateMachine, controllerPath);

            return newLayer;
        }

        public static AnimatorControllerParameter AddParameter(AnimatorController controller, AnimatorControllerParameter srcParameter)
        {
            // 同じParameterがあれば追加しない
            if (controller.parameters.Any(p => p.name == srcParameter.name))
                return null;

            var parameter = new AnimatorControllerParameter
            {
                defaultBool = srcParameter.defaultBool,
                defaultFloat = srcParameter.defaultFloat,
                defaultInt = srcParameter.defaultInt,
                name = srcParameter.name,
                type = srcParameter.type
            };

            controller.AddParameter(parameter);
            return parameter;
        }

        private static AnimatorControllerLayer DuplicateLayer(AnimatorControllerLayer srcLayer, string dstLayerName, string controllerPath, bool firstLayer = false)
        {
            var newLayer = new AnimatorControllerLayer()
            {
                avatarMask = srcLayer.avatarMask,
                blendingMode = srcLayer.blendingMode,
                defaultWeight = srcLayer.defaultWeight,
                iKPass = srcLayer.iKPass,
                name = dstLayerName,
                // 新しく作らないとLayer削除時にコピー元LayerのStateが消える
                stateMachine = DuplicateStateMachine(srcLayer.stateMachine, controllerPath),
                syncedLayerAffectsTiming = srcLayer.syncedLayerAffectsTiming,
                syncedLayerIndex = srcLayer.syncedLayerIndex
            };

            // 最初のレイヤーはdefaultWeightがどんなものでも自動的にweightが1扱いになっているので
            // defaultWeightを1にする
            if (firstLayer) newLayer.defaultWeight = 1f;

            // StateとStateMachineをすべて追加後に遷移を設定
            // 親階層へ伸びているっぽい遷移もある?
            CopyTransitions(srcLayer.stateMachine, newLayer.stateMachine);

            return newLayer;
        }

        private static AnimatorStateMachine DuplicateStateMachine(AnimatorStateMachine srcStateMachine, string controllerPath)
        {
            var dstStateMachine = new AnimatorStateMachine
            {
                anyStatePosition = srcStateMachine.anyStatePosition,
                entryPosition = srcStateMachine.entryPosition,
                exitPosition = srcStateMachine.exitPosition,
                hideFlags = srcStateMachine.hideFlags,
                name = srcStateMachine.name,
                parentStateMachinePosition = srcStateMachine.parentStateMachinePosition,
                stateMachines = srcStateMachine.stateMachines
                                    .Select(cs =>
                                        new ChildAnimatorStateMachine
                                        {
                                            position = cs.position,
                                            stateMachine = DuplicateStateMachine(cs.stateMachine, controllerPath)
                                        })
                                    .ToArray(),
                states = DuplicateChildStates(srcStateMachine.states, controllerPath),
            };

            // behaivoursを設定
            foreach (var srcBehaivour in srcStateMachine.behaviours)
            {
                var behaivour = dstStateMachine.AddStateMachineBehaviour(srcBehaivour.GetType());
                DeepCopy(srcBehaivour, behaivour);
            }

            // defaultStateの設定
            if (srcStateMachine.defaultState != null)
            {
                var defaultStateIndex = srcStateMachine.states
                                    .Select((value, index) => new { Value = value.state, Index = index })
                                    .Where(s => s.Value == srcStateMachine.defaultState)
                                    .Select(s => s.Index).SingleOrDefault();
                dstStateMachine.defaultState = dstStateMachine.states[defaultStateIndex].state;
            }

            return dstStateMachine;
        }

        private static ChildAnimatorState[] DuplicateChildStates(ChildAnimatorState[] srcChildStates, string controllerPath)
        {
            var dstStates = new ChildAnimatorState[srcChildStates.Length];

            for (int i = 0; i < srcChildStates.Length; i++)
            {
                var srcState = srcChildStates[i].state;
                dstStates[i] = new ChildAnimatorState
                {
                    position = srcChildStates[i].position,
                    state = DuplicateAnimatorState(srcState)
                };

                AssetDatabase.AddObjectToAsset(dstStates[i].state, controllerPath);

                // behavioursを設定
                foreach (var srcBehaivour in srcChildStates[i].state.behaviours)
                {
                    var behaivour = dstStates[i].state.AddStateMachineBehaviour(srcBehaivour.GetType());
                    DeepCopy(srcBehaivour, behaivour);
                }
            }

            return dstStates;
        }

        private static AnimatorState DuplicateAnimatorState(AnimatorState srcState)
        {
            return new AnimatorState
            {
                cycleOffset = srcState.cycleOffset,
                cycleOffsetParameter = srcState.cycleOffsetParameter,
                cycleOffsetParameterActive = srcState.cycleOffsetParameterActive,
                hideFlags = srcState.hideFlags,
                iKOnFeet = srcState.iKOnFeet,
                mirror = srcState.mirror,
                mirrorParameter = srcState.mirrorParameter,
                mirrorParameterActive = srcState.mirrorParameterActive,
                motion = srcState.motion,
                name = srcState.name,
                speed = srcState.speed,
                speedParameter = srcState.speedParameter,
                speedParameterActive = srcState.speedParameterActive,
                tag = srcState.tag,
                timeParameter = srcState.timeParameter,
                timeParameterActive = srcState.timeParameterActive,
                writeDefaultValues = srcState.writeDefaultValues
            };
        }

        private static void CopyTransitions(AnimatorStateMachine srcStateMachine, AnimatorStateMachine dstStateMachine)
        {
            var srcStates = GetAllStates(srcStateMachine);
            var dstStates = GetAllStates(dstStateMachine);
            var srcStateMachines = GetAllStateMachines(srcStateMachine);
            var dstStateMachines = GetAllStateMachines(dstStateMachine);

            // StateからのTransitionを設定
            for (int i = 0; i < srcStates.Length; i++)
            {
                foreach (var srcTransition in srcStates[i].transitions)
                {
                    AnimatorStateTransition dstTransition;

                    if (srcTransition.isExit)
                    {
                        dstTransition = dstStates[i].AddExitTransition();
                    }
                    else if (srcTransition.destinationState != null)
                    {
                        var stateIndex = Array.IndexOf(srcStates, srcTransition.destinationState);
                        dstTransition = dstStates[i].AddTransition(dstStates[stateIndex]);
                    }
                    else if (srcTransition.destinationStateMachine != null)
                    {
                        var stateMachineIndex = Array.IndexOf(srcStateMachines, srcTransition.destinationStateMachine);
                        dstTransition = dstStates[i].AddTransition(dstStateMachines[stateMachineIndex]);
                    }
                    else continue;

                    CopyTransitionParameters(srcTransition, dstTransition);
                }
            }

            // SubStateMachine, EntryState, AnyStateからのTransitionを設定
            for (int i = 0; i < srcStateMachines.Length; i++)
            {
                // SubStateMachineからのTransitionを設定
                CopyTransitionOfSubStateMachine(srcStateMachines[i], dstStateMachines[i],
                                                srcStates, dstStates,
                                                srcStateMachines, dstStateMachines);

                // AnyStateからのTransitionを設定
                foreach (var srcTransition in srcStateMachines[i].anyStateTransitions)
                {
                    AnimatorStateTransition dstTransition;

                    // AnyStateからExitStateへの遷移は存在しないはず
                    if (srcTransition.isExit)
                    {
                        Debug.LogError($"Unknown transition:{srcStateMachines[i].name}.AnyState->Exit");
                        continue;
                    }
                    else if (srcTransition.destinationState != null)
                    {
                        var stateIndex = Array.IndexOf(srcStates, srcTransition.destinationState);
                        dstTransition = dstStateMachines[i].AddAnyStateTransition(dstStates[stateIndex]);
                    }
                    else if (srcTransition.destinationStateMachine != null)
                    {
                        var stateMachineIndex = Array.IndexOf(srcStateMachines, srcTransition.destinationStateMachine);
                        dstTransition = dstStateMachines[i].AddAnyStateTransition(dstStateMachines[stateMachineIndex]);
                    }
                    else continue;

                    CopyTransitionParameters(srcTransition, dstTransition);
                }

                // EntryStateからのTransitionを設定
                foreach (var srcTransition in srcStateMachines[i].entryTransitions)
                {
                    AnimatorTransition dstTransition;

                    // EntryStateからExitStateへの遷移は存在しないはず
                    if (srcTransition.isExit)
                    {
                        Debug.LogError($"Unknown transition:{srcStateMachines[i].name}.Entry->Exit");
                        continue;
                    }
                    else if (srcTransition.destinationState != null)
                    {
                        var stateIndex = Array.IndexOf(srcStates, srcTransition.destinationState);
                        dstTransition = dstStateMachines[i].AddEntryTransition(dstStates[stateIndex]);
                    }
                    else if (srcTransition.destinationStateMachine != null)
                    {
                        var stateMachineIndex = Array.IndexOf(srcStateMachines, srcTransition.destinationStateMachine);
                        dstTransition = dstStateMachines[i].AddEntryTransition(dstStateMachines[stateMachineIndex]);
                    }
                    else continue;

                    CopyTransitionParameters(srcTransition, dstTransition);
                }
            }

        }

        private static void CopyTransitionOfSubStateMachine(AnimatorStateMachine srcParentStateMachine, AnimatorStateMachine dstParentStateMachine,
                                                     AnimatorState[] srcStates, AnimatorState[] dstStates,
                                                     AnimatorStateMachine[] srcStateMachines, AnimatorStateMachine[] dstStateMachines)
        {
            // SubStateMachineからのTransitionを設定
            for (int i = 0; i < srcParentStateMachine.stateMachines.Length; i++)
            {
                var srcSubStateMachine = srcParentStateMachine.stateMachines[i].stateMachine;
                var dstSubStateMachine = dstParentStateMachine.stateMachines[i].stateMachine;

                foreach (var srcTransition in srcParentStateMachine.GetStateMachineTransitions(srcSubStateMachine))
                {
                    AnimatorTransition dstTransition;

                    if (srcTransition.isExit)
                    {
                        dstTransition = dstParentStateMachine.AddStateMachineExitTransition(dstSubStateMachine);
                    }
                    else if (srcTransition.destinationState != null)
                    {
                        var stateIndex = Array.IndexOf(srcStates, srcTransition.destinationState);
                        dstTransition = dstParentStateMachine.AddStateMachineTransition(dstSubStateMachine, dstStates[stateIndex]);
                    }
                    else if (srcTransition.destinationStateMachine != null)
                    {
                        var stateMachineIndex = Array.IndexOf(srcStateMachines, srcTransition.destinationStateMachine);
                        dstTransition = dstParentStateMachine.AddStateMachineTransition(dstSubStateMachine, dstStateMachines[stateMachineIndex]);
                    }
                    else continue;

                    CopyTransitionParameters(srcTransition, dstTransition);
                }
            }
        }

        private static AnimatorState[] GetAllStates(AnimatorStateMachine stateMachine)
        {
            var stateList = stateMachine.states.Select(sc => sc.state).ToList();
            foreach (var subStatetMachine in stateMachine.stateMachines)
            {
                stateList.AddRange(GetAllStates(subStatetMachine.stateMachine));
            }
            return stateList.ToArray();
        }

        private static AnimatorStateMachine[] GetAllStateMachines(AnimatorStateMachine stateMachine)
        {
            var stateMachineList = new List<AnimatorStateMachine>
            {
                stateMachine
            };

            foreach (var subStateMachine in stateMachine.stateMachines)
            {
                stateMachineList.AddRange(GetAllStateMachines(subStateMachine.stateMachine));
            }

            return stateMachineList.ToArray();
        }

        private static void CopyTransitionParameters(AnimatorStateTransition srcTransition, AnimatorStateTransition dstTransition)
        {
            dstTransition.canTransitionToSelf = srcTransition.canTransitionToSelf;
            dstTransition.duration = srcTransition.duration;
            dstTransition.exitTime = srcTransition.exitTime;
            dstTransition.hasExitTime = srcTransition.hasExitTime;
            dstTransition.hasFixedDuration = srcTransition.hasFixedDuration;
            dstTransition.hideFlags = srcTransition.hideFlags;
            dstTransition.isExit = srcTransition.isExit;
            dstTransition.mute = srcTransition.mute;
            dstTransition.name = srcTransition.name;
            dstTransition.offset = srcTransition.offset;
            dstTransition.interruptionSource = srcTransition.interruptionSource;
            dstTransition.orderedInterruption = srcTransition.orderedInterruption;
            dstTransition.solo = srcTransition.solo;
            foreach (var srcCondition in srcTransition.conditions)
            {
                dstTransition.AddCondition(srcCondition.mode, srcCondition.threshold, srcCondition.parameter);
            }
        }

        private static void CopyTransitionParameters(AnimatorTransition srcTransition, AnimatorTransition dstTransition)
        {
            dstTransition.hideFlags = srcTransition.hideFlags;
            dstTransition.isExit = srcTransition.isExit;
            dstTransition.mute = srcTransition.mute;
            dstTransition.name = srcTransition.name;
            dstTransition.solo = srcTransition.solo;
            foreach (var srcCondition in srcTransition.conditions)
            {
                dstTransition.AddCondition(srcCondition.mode, srcCondition.threshold, srcCondition.parameter);
            }

        }

        private static void AddObjectsInStateMachineToAnimatorController(AnimatorStateMachine stateMachine, string controllerPath)
        {
            AssetDatabase.AddObjectToAsset(stateMachine, controllerPath);

            foreach (var transition in stateMachine.anyStateTransitions)
            {
                AssetDatabase.AddObjectToAsset(transition, controllerPath);
            }
            foreach (var transition in stateMachine.entryTransitions)
            {
                AssetDatabase.AddObjectToAsset(transition, controllerPath);
            }
            foreach (var behaviour in stateMachine.behaviours)
            {
                AssetDatabase.AddObjectToAsset(behaviour, controllerPath);
            }
            foreach (var SubStateMachine in stateMachine.stateMachines)
            {
                foreach (var transition in stateMachine.GetStateMachineTransitions(SubStateMachine.stateMachine))
                {
                    AssetDatabase.AddObjectToAsset(transition, controllerPath);
                }
                AddObjectsInStateMachineToAnimatorController(SubStateMachine.stateMachine, controllerPath);
            }
        }

        public static AnimatorController DuplicateAnimationLayerController(string originalControllerPath, string outputFolderPath, string avatarName)
        {
            var controllerName = $"{Path.GetFileNameWithoutExtension(originalControllerPath)}_{avatarName}.controller";
            var controllerPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(outputFolderPath, controllerName));
            AssetDatabase.CopyAsset(originalControllerPath, controllerPath);
            return AssetDatabase.LoadAssetAtPath(controllerPath, typeof(AnimatorController)) as AnimatorController;
        }

        private static T DeepCopy<T>(T src, T dst)
        {
            var srcFields = src.GetType().GetFields();
            var dstFields = dst.GetType().GetFields();
            foreach (var srcField in srcFields)
            {
                foreach (var dstField in dstFields)
                {
                    if (srcField.Name != dstField.Name || srcField.FieldType != dstField.FieldType) continue;
                    dstField.SetValue(dst, srcField.GetValue(src));
                    break;
                }
            }

            var srcProperties = src.GetType().GetProperties();
            var dstProperties = dst.GetType().GetProperties();
            foreach (var srcProperty in srcProperties)
            {
                foreach (var dstProperty in dstProperties)
                {
                    if (srcProperty.Name != dstProperty.Name || srcProperty.PropertyType != dstProperty.PropertyType) continue;
                    dstProperty.SetValue(dst, srcProperty.GetValue(src));
                    break;
                }
            }
            return src;
        }

        public static void InjectLayers(AnimatorControllerLayer[] dest, AnimatorController to)
        {
            var copySakiControllerPath = AssetDatabase.GetAssetPath(to);

            for (int i = 0; i < dest.Length; i++)
                AddLayer(to, dest[i], i == 0, copySakiControllerPath);

            EditorUtility.SetDirty(to);
            //AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }

    public partial class KimeraScript : EditorWindow
    {
        public static Dictionary<AnimatorControllerLayer, bool> Head_Toggles = new Dictionary<AnimatorControllerLayer, bool>();

        public static Dictionary<AnimatorControllerLayer, bool> Body_Toggles = new Dictionary<AnimatorControllerLayer, bool>();

        /// <summary>
        /// 継承レイヤーを保存
        /// </summary>
        public static AnimatorControllerLayer[,] Add_Layers;

        /// <summary>
        /// 頭の継承レイヤー
        /// </summary>
        public static AnimatorControllerLayer[] Head_Add_Layers;

        /// <summary>
        /// 体の継承レイヤー
        /// </summary>
        public static AnimatorControllerLayer[] Body_Add_Layers;


        public static string RandomTexts(int length)
        {
            // 文字列を用意
            var txt = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            string result = "";

            for (int i = 0; i < length; i++)
            {
                result += txt[UnityEngine.Random.Range(0, txt.Length - 1)]; // 文字数の範囲内で乱数を生成
            }

            return result;
        }
        public static List<GameObject> SameNameObjects(Transform head, Transform body)
        {
            List<GameObject> objects = new List<GameObject>();
            for (int hc = 0; hc < head.childCount; hc++)
            {
                GameObject headChildGO = head.GetChild(hc).gameObject;
                for (int bc = 0; bc < body.childCount; bc++)
                {
                    GameObject bodyChildGO = body.GetChild(bc).gameObject;

                    if (headChildGO.name == bodyChildGO.name && !headChildGO.name.Contains("Armature") && !headChildGO.name.Contains("armature"))
                    {
                        if (!objects.Contains(bodyChildGO))
                            objects.Add(bodyChildGO);
                    }
                }
            }

            return objects;
        }
        /// <summary>
        /// 0 = Left 1 = Right
        /// </summary>
        /// <param name="Avatar"></param>
        /// <returns></returns>
        public static AnimatorControllerLayer[] SearchLayerfromName(VRCAvatarDescriptor Avatar, string left = "Left Hand", string right = "Right Hand")
        {
            AnimatorControllerLayer[] retLayers = new AnimatorControllerLayer[2];
            if (!Avatar.customizeAnimationLayers)
                return null;

            var FXLayer = Avatar.baseAnimationLayers[(int)AnimLayerType.FX - 1];

            if (FXLayer.isDefault || FXLayer.animatorController == null)
                return null;

            var FXLayerController = (AnimatorController)FXLayer.animatorController;

            if (FXLayerController == null)
                return null;

            for (int i = 0; i < FXLayerController.layers.Length; i++)
            {
                AnimatorControllerLayer Layer = FXLayerController.layers[i];
                if (Layer.name == left)
                {
                    retLayers[0] = Layer;
                }

                if (Layer.name == right)
                {
                    retLayers[1] = Layer;
                }
            }

            return retLayers;
        }

        public static void LipsyncCopy(VRCAvatarDescriptor HeadAvatar, VRCAvatarDescriptor BodyAvatar)
        {
            BodyAvatar.lipSync = HeadAvatar.lipSync;
            if (HeadAvatar.lipSync == VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.JawFlapBone)
            {
                //顎のボーンをコピー
                BodyAvatar.lipSyncJawBone = HeadAvatar.lipSyncJawBone;
                //顎の閉じてる場所
                BodyAvatar.lipSyncJawClosed = HeadAvatar.lipSyncJawClosed;
                //顎の開いてる場所
                BodyAvatar.lipSyncJawOpen = HeadAvatar.lipSyncJawOpen;
            }
            else if (HeadAvatar.lipSync == VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.JawFlapBlendShape)
            {
                //顎用のメッシュをコピー
                BodyAvatar.VisemeSkinnedMesh = HeadAvatar.VisemeSkinnedMesh;
                //顎用のブレンドシェイプ名をコピー
                BodyAvatar.MouthOpenBlendShapeName = HeadAvatar.MouthOpenBlendShapeName;
            }
            else if (HeadAvatar.lipSync == VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShape)
            {
                //口のメッシュをコピー
                BodyAvatar.VisemeSkinnedMesh = HeadAvatar.VisemeSkinnedMesh;
                //口のブレンドシェイプをコピー
                BodyAvatar.VisemeBlendShapes = HeadAvatar.VisemeBlendShapes;
            }
            else if (HeadAvatar.lipSync == VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.VisemeParameterOnly)
            {
                //中身が無いのでスキップ
            }
            else if (HeadAvatar.lipSync == VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.Default)
            {

            }
        }
        public static void EyeLookCopy(VRCAvatarDescriptor HeadAvatar, VRCAvatarDescriptor BodyAvatar, bool swapEyeBone)
        {
            if (!HeadAvatar.enableEyeLook)
            {
                BodyAvatar.enableEyeLook = false;
                return;
            }

            BodyAvatar.enableEyeLook = HeadAvatar.enableEyeLook;

            //[General]
            //eye Movementをコピー
            BodyAvatar.customEyeLookSettings.eyeMovement = HeadAvatar.customEyeLookSettings.eyeMovement;


            //[Eyes]
            //左目と右目をコピー
            if (swapEyeBone)
            {
                BodyAvatar.customEyeLookSettings.leftEye = HeadAvatar.customEyeLookSettings.leftEye;
                BodyAvatar.customEyeLookSettings.rightEye = HeadAvatar.customEyeLookSettings.rightEye;
            }

            //ちらちら見る位置をコピー
            BodyAvatar.customEyeLookSettings.eyesLookingStraight = HeadAvatar.customEyeLookSettings.eyesLookingStraight;
            BodyAvatar.customEyeLookSettings.eyesLookingUp = HeadAvatar.customEyeLookSettings.eyesLookingUp;
            BodyAvatar.customEyeLookSettings.eyesLookingDown = HeadAvatar.customEyeLookSettings.eyesLookingDown;
            BodyAvatar.customEyeLookSettings.eyesLookingLeft = HeadAvatar.customEyeLookSettings.eyesLookingLeft;
            BodyAvatar.customEyeLookSettings.eyesLookingRight = HeadAvatar.customEyeLookSettings.eyesLookingRight;

            //[Eyelids]
            BodyAvatar.customEyeLookSettings.eyelidType = HeadAvatar.customEyeLookSettings.eyelidType;
            if (BodyAvatar.customEyeLookSettings.eyelidType == VRCAvatarDescriptor.EyelidType.Blendshapes)
            {
                //メッシュをコピー
                BodyAvatar.customEyeLookSettings.eyelidsSkinnedMesh = HeadAvatar.customEyeLookSettings.eyelidsSkinnedMesh;

                //選択されたブレンドシェイプをコピー
                BodyAvatar.customEyeLookSettings.eyelidsBlendshapes = HeadAvatar.customEyeLookSettings.eyelidsBlendshapes;
            }
            else if (BodyAvatar.customEyeLookSettings.eyelidType == VRCAvatarDescriptor.EyelidType.Bones)
            {
                //目のローテンションをコピー
                BodyAvatar.customEyeLookSettings.upperLeftEyelid = HeadAvatar.customEyeLookSettings.upperLeftEyelid;
                BodyAvatar.customEyeLookSettings.upperRightEyelid = HeadAvatar.customEyeLookSettings.upperRightEyelid;

                BodyAvatar.customEyeLookSettings.lowerLeftEyelid = HeadAvatar.customEyeLookSettings.lowerLeftEyelid;
                BodyAvatar.customEyeLookSettings.lowerRightEyelid = HeadAvatar.customEyeLookSettings.lowerRightEyelid;

                BodyAvatar.customEyeLookSettings.eyelidsDefault = HeadAvatar.customEyeLookSettings.eyelidsDefault;
                BodyAvatar.customEyeLookSettings.eyelidsClosed = HeadAvatar.customEyeLookSettings.eyelidsClosed;
                BodyAvatar.customEyeLookSettings.eyelidsLookingUp = HeadAvatar.customEyeLookSettings.eyelidsLookingUp;
                BodyAvatar.customEyeLookSettings.eyelidsLookingDown = HeadAvatar.customEyeLookSettings.eyelidsLookingDown;
            }
        }

        [Obsolete("こっちではLeft HandとRight Handしか継承されません。FXLayersCopy_newを使ってください")]
        public static void FXLayersCopy(VRCAvatarDescriptor HeadAvatar, VRCAvatarDescriptor BodyAvatar, string left = "Left Hand", string right = "Right Hand")
        {
            if (!Directory.Exists(KimeraGenerated))
                Directory.CreateDirectory(KimeraGenerated);

            //名前を時刻に
            DateTime dt = DateTime.Now;
            string GeneratePath = KimeraGenerated + KimeraName + "-" + dt.ToString("yyyy-MM-dd-HH-mm-ss") + ".controller";

            //左手と右手のレイヤーを取得出来なかった場合
            var HeadLayers = SearchLayerfromName(HeadAvatar, left, right);
            if (HeadLayers == null) return;

            //左手と右手のレイヤーを取得出来なかった場合
            var BodyLayers = SearchLayerfromName(BodyAvatar, left, right);
            if (BodyLayers == null) return;

            var BodyFX = (AnimatorController)BodyAvatar.baseAnimationLayers[(int)AnimLayerType.FX - 1].animatorController;
            var AssetPath = AssetDatabase.GetAssetPath(BodyFX);
            if (string.IsNullOrEmpty(AssetPath))
                return;

            AssetDatabase.CopyAsset(AssetPath, GeneratePath);
            AssetDatabase.Refresh();
            var CopiedFX = AssetDatabase.LoadAssetAtPath<AnimatorController>(GeneratePath);
            if (CopiedFX == null)
                return;

            BodyAvatar.baseAnimationLayers[(int)AnimLayerType.FX - 1].animatorController = CopiedFX;

            List<AnimatorControllerLayer> newlayers = new List<AnimatorControllerLayer>();
            for (int i = 0; i < CopiedFX.layers.Length; i++)
            {
                if (CopiedFX.layers[i].name == left)
                {
                    if (HeadLayers[0] != null)
                        newlayers.Add(HeadLayers[0]);
                }
                else if (CopiedFX.layers[i].name == right)
                {
                    if (HeadLayers[1] != null)
                        newlayers.Add(HeadLayers[1]);
                }
                else
                {
                    newlayers.Add(CopiedFX.layers[i]);
                }
            }

            CopiedFX.layers = newlayers.ToArray();
            EditorUtility.SetDirty(CopiedFX);
        }
        public static void FXLayersCopy_new(VRCAvatarDescriptor BodyAvatar, VRCAvatarDescriptor HeadAvatar,
            AnimatorControllerLayer[] headlayers, AnimatorControllerLayer[] bodylayers)
        {
            //保存用のディレクトリが無い場合生成
            if (!Directory.Exists(KimeraGenerated))
                Directory.CreateDirectory(KimeraGenerated);

            //名前を時刻に
            DateTime dt = DateTime.Now;
            string GeneratePath = KimeraGenerated + KimeraName + "-" + dt.ToString("yyyy-MM-dd-HH-mm-ss") + ".controller";

            //体側のFXを取得
            var BodyFX = BodyAvatar.GetController(AnimLayerType.FX);
            var HeadFX = HeadAvatar.GetController(AnimLayerType.FX);

            if (BodyFX == null || HeadFX == null)
            {
                BodyAvatar.SetController(null, AnimLayerType.FX);
                return;
            }

            //FXのパスを取得
            var AssetPath = AssetDatabase.GetAssetPath(BodyFX);

            //パスが存在しない場合は返す
            if (string.IsNullOrEmpty(AssetPath))
                return;

            //アセットを保存用ディレクトリにコピー
            AssetDatabase.CopyAsset(AssetPath, GeneratePath);

            //アセットデータベースを更新
            AssetDatabase.Refresh();

            //コピーしたアセットを再読み込み
            var CopiedFX = AssetDatabase.LoadAssetAtPath<AnimatorController>(GeneratePath);
            if (CopiedFX == null)
                return;

            //コピーしたFXをキメラに設定
            BodyAvatar.SetController(CopiedFX, AnimLayerType.FX);

            //差し替えるレイヤーを作成
            List<AnimatorControllerLayer> newlayers = new List<AnimatorControllerLayer>();


            //先にFXを書き換え
            CopiedFX.layers = newlayers.ToArray();

            //チェックしたレイヤーだけ入れる
            AnimatorControllerUtility.InjectLayers(headlayers, CopiedFX);
            AnimatorControllerUtility.InjectLayers(bodylayers, CopiedFX);

            foreach (var headparam in HeadFX.parameters)
            {
                AnimatorControllerUtility.AddParameter(CopiedFX, headparam);
            }

            foreach (var bodyparam in BodyFX.parameters)
            {
                AnimatorControllerUtility.AddParameter(CopiedFX, bodyparam);
            }

            EditorUtility.SetDirty(CopiedFX);
            AssetDatabase.Refresh();
        }
    }
}

