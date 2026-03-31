using HarmonyLib;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using SkinManagerAndSkinPanelMod.Scripts.Data;

namespace SkinManagerAndSkinPanelMod;

[HarmonyPatch]
public static class ImpactVfxPatches
{
    [HarmonyPatch(typeof(CardModel), nameof(CardModel.OnPlayWrapper))]
    [HarmonyPrefix]
    public static void OnCardPlayed(CardModel __instance, Creature target)
    {
        if (__instance.Owner == null || __instance.Owner.Character == null) return;

        if (__instance.Type != CardType.Attack) return;

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
                    customVfx.GlobalPosition = targetNode.GlobalPosition + new Vector2(0f, -100f) + skin.ImpactVfxOffset; 
                }

                customVfx.Scale = skin.ImpactVfxScale;
            }
        }
        else
        {
            MegaCrit.Sts2.Core.Logging.Log.Error($"[皮肤管理器] 无法加载打击特效，路径: {vfxPathToPlay}");
        }
    }
}