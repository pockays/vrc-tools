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
            .AfterPlugin("nadena.dev.modular-avatar")
            .Run("PhysBone Optimizer", ctx =>
            {
                var optimizers = ctx.AvatarRootObject.GetComponentsInChildren<VRCPhysBoneOptimizer>(true);
                if (optimizers.Length == 0) return;

                foreach (var optimizer in optimizers)
                {
                    // ============================================================
                    // 第一步：对所有源对象执行"迁移PhysBone到Root"
                    // ============================================================
                    if (optimizer.sourcePBObjects != null && optimizer.sourcePBObjects.Count > 0)
                    {
                        foreach (var obj in optimizer.sourcePBObjects)
                        {
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
                        foreach (var obj in optimizer.sourcePBObjects)
                        {
                            if (obj != null)
                            {
                                string objName = obj.name;
                                VRCPhysBoneAPI.DeleteSourceObject(obj);
                                Debug.Log($"[PhysBone优化-删除] 已删除源对象: {objName}");
                            }
                        }
                    }

                    // ============================================================
                    // 第三步：对挂载对象执行"合并PhysBones"
                    // ============================================================
                    string mergeStatus;
                    VRCPhysBoneAPI.MergePhysBones(optimizer.gameObject, out _, out _, out mergeStatus);
                    Debug.Log($"<color=green>[PhysBone优化-合并] {mergeStatus}</color>");

                    // 清理已处理的组件
                    Object.DestroyImmediate(optimizer);
                }
            });
    }
}
