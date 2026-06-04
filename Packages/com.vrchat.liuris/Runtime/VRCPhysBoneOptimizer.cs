using UnityEngine;
using System.Collections.Generic;
using nadena.dev.ndmf;

[AddComponentMenu("咩卡布/PhysBone Optimizer")]
[DefaultExecutionOrder(-32000)]
public class VRCPhysBoneOptimizer : MonoBehaviour, INDMFEditorOnly
{
    [Header("动骨优化")]
    [Tooltip("需要迁移PhysBone的源对象列表（可为空），将对每个对象执行\"迁移PhysBone到Root\"，完成后删除源对象")]
    public List<GameObject> sourcePBObjects = new List<GameObject>();
}
