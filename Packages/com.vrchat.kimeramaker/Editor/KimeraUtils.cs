using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if KM_VRC_AVATARS_VRCSDK3
using VRC.SDK3.Avatars.Components;
#endif

namespace net.satania.kimeramaker.editor
{
    public static class KimeraUtils
    {
        /// <summary>
        /// �f�t�H���g�̃r���[�|�C���g�̒l�Ɠ��{�[�����g�p���āA�L������̃r���[�|�C���g�̈ʒu�𐄒肷��֐�
        /// </summary>
        /// <param name="bodyAvatar"></param>
        /// <param name="headAvatar"></param>
        /// <param name="defaultHeadAvatarViewpoint"></param>
        /// <param name="EstimationViewpoint"></param>
        /// <returns></returns>
        public static bool TryGetEstimationViewpoint(Animator bodyAvatar, Animator headAvatar, Vector3 defaultHeadAvatarViewpoint, out Vector3 EstimationViewpoint)
        {
            EstimationViewpoint = new Vector3(0, 0, 0);

            Transform headBone = headAvatar.GetBoneTransform(HumanBodyBones.Head);
            if (headBone == null)
                return false;

            //���X�̃r���[�|�C���g��Head�{�[���̍����擾���Ă����A�����BodyAvatar���ɉ��Z����
            Vector3 localHeadPosition = headAvatar.transform.InverseTransformPoint(headBone.position);
            Vector3 diff = defaultHeadAvatarViewpoint - localHeadPosition;

            EstimationViewpoint = headBone.position - bodyAvatar.transform.position + diff;
            return true;
        }
    }
}
