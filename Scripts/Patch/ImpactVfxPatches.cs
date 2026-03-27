using HarmonyLib;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace SkinManagerAndSkinPanelMod;

[HarmonyPatch]
public static class ImpactVfxPatches
{
    [HarmonyPatch(typeof(CardModel), nameof(CardModel.OnPlayWrapper))]
    [HarmonyPrefix]
    // 🌟 修复：参数名和类型必须与原版 OnPlayWrapper 完美对应！
    public static void OnCardPlayed(CardModel __instance, Creature target)
    {
        if (__instance.Owner == null || __instance.Owner.Character == null) return;

        if (__instance.Type != CardType.Attack) return;

        // 此时我们就可以安全地使用 target 了
        if (target == null || target.IsDead) return;

        string charId = __instance.Owner.Character.Id.Entry;
        SkinData skin = SkinApi.GetSelectedSkin(charId);
        if (skin == null) return;

        string vfxPathToPlay = null;

        if (charId == "REGENT" && __instance.Id.Entry == "SOVEREIGN_BLADE") return;
        
        if (skin.CustomImpactVfxMap != null && skin.CustomImpactVfxMap.TryGetValue(__instance.Id.Entry, out string specificVfx))
        {
            vfxPathToPlay = specificVfx;
        }
        else if (!string.IsNullOrEmpty(skin.DefaultImpactVfxPath))
        {
            vfxPathToPlay = skin.DefaultImpactVfxPath;
        }

        if (string.IsNullOrEmpty(vfxPathToPlay)) return;

        // 动态加载需要使用正确的路径并加特效，如果你之前写了延迟逻辑或其他的也可以保留
        PackedScene vfxScene = GD.Load<PackedScene>(vfxPathToPlay);
        if (vfxScene != null)
        {
            Node2D customVfx = vfxScene.Instantiate<Node2D>();
            
            var vfxContainer = NCombatRoom.Instance?.CombatVfxContainer;
            if (vfxContainer != null)
            {
                vfxContainer.AddChildSafely(customVfx);

                NCreature targetNode = NCombatRoom.Instance.GetCreatureNode(target);
                if (targetNode != null)
                {
                    // 放到目标的身体位置（可以微调Y轴）
                    customVfx.GlobalPosition = targetNode.GlobalPosition + new Vector2(0f, -100f) + skin.ImpactVfxOffset; 
                }

                customVfx.Scale = skin.ImpactVfxScale;

                /*
                NGame.Instance?.ScreenShake(
                    MegaCrit.Sts2.Core.Nodes.Vfx.Utilities.ShakeStrength.Strong, 
                    MegaCrit.Sts2.Core.Nodes.Vfx.Utilities.ShakeDuration.Short);*/
            }
        }
        else
        {
            MegaCrit.Sts2.Core.Logging.Log.Error($"[框架报错] 无法加载打击特效，路径: {vfxPathToPlay}");
        }
    }
}