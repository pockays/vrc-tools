using UnityEditor;
using UnityEngine;
using nadena.dev.ndmf;

[assembly: ExportsPlugin(typeof(VRCPhysBoneOptimizerPlugin))]

public class VRCPhysBoneOptimizerPlugin : Plugin<VRCPhysBoneOptimizerPlugin>
{
    public override string QualifiedName => "com.vrchat.liuris.physbone-optimizer";
    public override string DisplayName => "PhysBone Optimizer (咩卡布)";

    protected override void Configure()
    {
        InPhase(BuildPhase.Transforming)
            .Run("PhysBone Optimizer", ctx =>
            {
                Debug.Log($"[PhysBone优化] ========== 插件开始运行 ==========");
                Debug.Log($"[PhysBone优化] AvatarRoot: {ctx.AvatarRootObject.name}");

                // 尝试多种方式查找 VRCPhysBoneOptimizer 组件
                var optimizers = ctx.AvatarRootObject.GetComponentsInChildren<VRCPhysBoneOptimizer>(true);
                Debug.Log($"[PhysBone优化] GetComponentsInChildren 找到 {optimizers.Length} 个 optimizer");

                // 备用：用 FindObjectsOfType 在整个场景中找
                var allInScene = Object.FindObjectsOfType<VRCPhysBoneOptimizer>();
                Debug.Log($"[PhysBone优化] FindObjectsOfType 找到 {allInScene.Length} 个 optimizer");

                // 如果有 FindObjectsOfType 找到了但 GetComponentsInChildren 没找到，用前者
                if (optimizers.Length == 0 && allInScene.Length > 0)
                {
                    Debug.LogWarning("[PhysBone优化] GetComponentsInChildren 没找到，改用 FindObjectsOfType 的结果");
                    optimizers = allInScene;
                }

                if (optimizers.Length == 0)
                {
                    Debug.LogWarning("[PhysBone优化] 没有找到任何 VRCPhysBoneOptimizer 组件，跳过处理");
                    return;
                }

                foreach (var optimizer in optimizers)
                {
                    if (optimizer == null)
                    {
                        Debug.LogWarning("[PhysBone优化] 遇到 null optimizer，跳过");
                        continue;
                    }

                    Debug.Log($"[PhysBone优化] 处理 optimizer: {optimizer.gameObject.name}, sourcePBObjects 数量: {(optimizer.sourcePBObjects != null ? optimizer.sourcePBObjects.Count : 0)}");

                    // ============================================================
                    // 第一步：对所有源对象执行"迁移PhysBone到Root"
                    // ============================================================
                    if (optimizer.sourcePBObjects != null && optimizer.sourcePBObjects.Count > 0)
                    {
                        for (int i = optimizer.sourcePBObjects.Count - 1; i >= 0; i--)
                        {
                            var obj = optimizer.sourcePBObjects[i];
                            if (obj != null)
                            {
                                string status;
                                VRCPhysBoneAPI.MovePhysBonesToRoot(obj, out status);
                                Debug.Log($"[PhysBone优化-迁移] {obj.name}: {status}");
                            }
                        }
                    }

                    // ============================================================
                    // 第二步：删除源对象（迁移完成后安全删除）
                    // ============================================================
                    if (optimizer.sourcePBObjects != null && optimizer.sourcePBObjects.Count > 0)
                    {
                        for (int i = optimizer.sourcePBObjects.Count - 1; i >= 0; i--)
                        {
                            var obj = optimizer.sourcePBObjects[i];
                            if (obj != null)
                            {
                                string objName = obj.name;
                                VRCPhysBoneAPI.DeleteSourceObject(obj);
                                Debug.Log($"[PhysBone优化-删除] 已删除源对象: {objName}");
                            }
                        }
                    }

                    // 安全检查：DeleteSourceObject 可能删除了 optimizer 所在的 GameObject
                    if (optimizer == null || optimizer.gameObject == null)
                    {
                        Debug.LogWarning("[PhysBone优化] optimizer 的 GameObject 在删除源对象时被移除，跳过合并步骤");
                        continue;
                    }

                    // ============================================================
                    // 第三步：对挂载对象执行"合并PhysBones"
                    // ============================================================
                    Debug.Log($"[PhysBone优化] 开始合并 PhysBones，目标: {optimizer.gameObject.name}");
                    string mergeStatus;
                    VRCPhysBoneAPI.MergePhysBones(optimizer.gameObject, out _, out _, out mergeStatus);
                    Debug.Log($"<color=green>[PhysBone优化-合并] {mergeStatus}</color>");

                    // 清理已处理的组件
                    Object.DestroyImmediate(optimizer);
                    Debug.Log($"[PhysBone优化] 已销毁 optimizer 组件");
                }

                Debug.Log($"[PhysBone优化] ========== 插件运行完毕 ==========");
            });
    }
}
