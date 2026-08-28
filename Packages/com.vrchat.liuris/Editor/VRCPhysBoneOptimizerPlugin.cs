using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
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

                // 作用域组件：挂载后仅处理【挂载对象自己的子树】，不同对象互不影响
                var allOptimizers = ctx.AvatarRootObject.GetComponentsInChildren<VRCPhysBoneOptimizer>(true);
                if (allOptimizers.Length == 0)
                {
                    Debug.LogWarning("[PhysBone优化] 未找到 VRCPhysBoneOptimizer 开关组件，跳过处理");
                    return;
                }

                // 只处理最外层标记：若标记嵌套在另一个标记的子树内，则交给外层统一处理，避免重复
                var optimizers = allOptimizers
                    .Where(o => o != null && !allOptimizers.Any(p => p != null && p != o && o.transform.IsChildOf(p.transform)))
                    .ToArray();

                Debug.Log($"[PhysBone优化] 找到 {allOptimizers.Length} 个开关，其中 {optimizers.Length} 个顶层标记，逐个处理其子树");

                foreach (var optimizer in optimizers)
                {
                    var go = optimizer.gameObject;
                    Debug.Log($"<color=cyan>[PhysBone优化] 处理对象: {go.name}</color>");

                    // ============================================================
                    // 第一步：迁移 —— 只迁移 go 子树下的 PhysBone/Collider 到其 root 骨骼
                    // ============================================================
                    string migrateStatus;
                    List<GameObject> migratedObjects;
                    VRCPhysBoneAPI.MoveAllPhysBonesToRoot(go, out migrateStatus, out migratedObjects);
                    Debug.Log($"[PhysBone优化-迁移] {go.name}: {migrateStatus}");

                    // ============================================================
                    // 第二步：清理迁移后产生的空壳对象
                    // ============================================================
                    string shellStatus;
                    VRCPhysBoneAPI.DeleteEmptyShells(go, migratedObjects, out shellStatus);
                    Debug.Log($"[PhysBone优化-删空壳] {go.name}: {shellStatus}");

                    // ============================================================
                    // 第三步：合并 —— 只合并 go 子树内同父级同配置的 PhysBone
                    // ============================================================
                    string mergeStatus;
                    VRCPhysBoneAPI.MergePhysBones(go, out _, out _, out mergeStatus);
                    Debug.Log($"<color=green>[PhysBone优化-合并] {go.name}: {mergeStatus}</color>");
                }

                // ============================================================
                // 清理开关组件（NDMF 编辑器标记，构建后无需保留）
                // ============================================================
                foreach (var optimizer in allOptimizers)
                {
                    if (optimizer != null)
                        Object.DestroyImmediate(optimizer);
                }

                Debug.Log($"[PhysBone优化] ========== 插件运行完毕 ==========");
            });
    }
}
