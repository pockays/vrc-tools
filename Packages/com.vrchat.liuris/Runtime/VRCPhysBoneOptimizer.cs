using UnityEngine;
using nadena.dev.ndmf;

[AddComponentMenu("咩卡布/PhysBone Optimizer")]
[DefaultExecutionOrder(-32000)]
public class VRCPhysBoneOptimizer : MonoBehaviour, INDMFEditorOnly
{
    // 纯标记/作用域组件：挂载后，NDMF 构建时只处理【本对象自己的子树】下的 VRCPhysBone ——
    // 迁移到 root 骨骼、清理空壳并执行合并；不同挂载对象之间互不影响。
}
