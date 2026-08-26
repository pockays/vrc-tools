using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
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

                // 开关组件：挂载即启用全局优化，无需再填源对象
                var optimizers = ctx.AvatarRootObject.GetComponentsInChildren<VRCPhysBoneOptimizer>(true);
                if (optimizers.Length == 0)
                {
                    Debug.LogWarning("[PhysBone优化] 未找到 VRCPhysBoneOptimizer 开关组件，跳过处理");
                    return;
                }
                Debug.Log($"[PhysBone优化] 找到 {optimizers.Length} 个 VRCPhysBoneOptimizer 开关，开始全局优化");

                // ============================================================
                // 第一步：全局迁移 —— 扫描 avatar 下所有 PhysBone/Collider，
                // rootTransform 非空且不在自身内部的一律迁移到其 root 骨骼
                // ============================================================
                string migrateStatus;
                List<GameObject> migratedObjects;
                VRCPhysBoneAPI.MoveAllPhysBonesToRoot(ctx.AvatarRootObject, out migrateStatus, out migratedObjects);
                Debug.Log($"<color=green>[PhysBone优化-迁移] {migrateStatus}</color>");

                // ============================================================
                // 第二步：清理迁移后产生的空壳对象
                // ============================================================
                string shellStatus;
                VRCPhysBoneAPI.DeleteEmptyShells(ctx.AvatarRootObject, migratedObjects, out shellStatus);
                Debug.Log($"[PhysBone优化-删空壳] {shellStatus}");

                // ============================================================
                // 第三步：对 avatar 全局执行 PhysBone 合并
                // ============================================================
                string mergeStatus;
                VRCPhysBoneAPI.MergePhysBones(ctx.AvatarRootObject, out _, out _, out mergeStatus);
                Debug.Log($"<color=green>[PhysBone优化-合并] {mergeStatus}</color>");

                // ============================================================
                // 清理开关组件（NDMF 编辑器标记，构建后无需保留）
                // ============================================================
                foreach (var optimizer in optimizers)
                {
                    if (optimizer != null)
                        Object.DestroyImmediate(optimizer);
                }

                Debug.Log($"[PhysBone优化] ========== 插件运行完毕 ==========");
            });
    }
}
