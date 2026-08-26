using UnityEngine;
using nadena.dev.ndmf;

[AddComponentMenu("咩卡布/PhysBone Optimizer")]
[DefaultExecutionOrder(-32000)]
public class VRCPhysBoneOptimizer : MonoBehaviour, INDMFEditorOnly
{
    // 纯标记/开关组件：挂载后，NDMF 构建时会自动扫描 avatar 下所有 VRCPhysBone，
    // 将 rootTransform 非空的 PhysBone 迁移到其 root 骨骼、清理空壳并执行合并。
}
