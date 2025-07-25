﻿/*
AvatarModifyTools
https://github.com/HhotateA/AvatarModifyTools

Copyright (c) 2021 @HhotateA_xR

This software is released under the MIT License.
http://opensource.org/licenses/mit-license.php
*/
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Serialization;
#if VRC_SDK_VRCSDK3
using VRC.SDK3.Avatars.ScriptableObjects;
#endif

namespace HhotateA.AvatarModifyTools.Core
{
    [CreateAssetMenu(menuName = "HhotateA/AvatarModifyData")]
    public class AvatarModifyData : ScriptableObject
    {
        [FormerlySerializedAs("name")] public string saveName = "AvatarModifyData";
        public AnimatorController locomotion_controller;
        public AnimatorController idle_controller;
        public AnimatorController gesture_controller;
        public AnimatorController action_controller;
        public AnimatorController fx_controller;
        public Item[] items = new Item[0];
#if VRC_SDK_VRCSDK3
        public VRCExpressionParameters parameter;
        public VRCExpressionsMenu menu;
#else
        public Object parameter;
        public Object menu;
#endif
    }

    [System.Serializable]
    public class Item
    {
        public GameObject prefab;
        public HumanBodyBones target;
    }
}