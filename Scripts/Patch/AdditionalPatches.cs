using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using SkinManagerAndSkinPanelMod.Scripts.Data;
using SkinManagerAndSkinPanelMod.Scripts.Helper;

namespace SkinManagerAndSkinPanelMod;

[HarmonyPatch]
public static class SovereignBladeVfx
{
    [HarmonyPatch(typeof(NSovereignBladeVfx), nameof(NSovereignBladeVfx._Ready))]
    [HarmonyPostfix]
    public static void ReplaceBlade(NSovereignBladeVfx __instance)
    {
        if (__instance.Card?.Owner?.Character?.Id.Entry != "REGENT") return;
        
        SkinData skin = SkinApi.GetSelectedSkin("REGENT");

        
        if (skin == null)
        {
            LogHelper.LogNoneSkin();
            return;
        }

        LogHelper.LogReplace("君王之剑", skin);
        
        if (string.IsNullOrEmpty(skin.BladeScenePath))
        {
            LogHelper.LogEmptyPath("君王之剑", skin);
        }
        
        // 🌟 实时加载骨骼数据
        PackedScene loadedSceneData = null;
        if (!string.IsNullOrEmpty(skin.BladeScenePath))
        {
            loadedSceneData = ResourceLoader.Load<PackedScene>(skin.BladeScenePath);
        }

        if (loadedSceneData == null)
        {
            LogHelper.ErrorLoad("君王之剑",  skin);
            return;
        }
        
        
        // 2. 获取根部 Spine 节点
        var spineNode = __instance.GetNode<Node2D>("SpineSword");

        if (spineNode.HasNode("CustomSwordVisuals"))
        {
            return;
        }

        if (spineNode is CanvasItem canvasSpine)
        {
            canvasSpine.SelfModulate = new Color(1f, 1f, 1f, 0f);
        }
        string[] vanillaVisualNodes = 
        {
            "SpineSword/SwordBone/ScaleContainer/SteppedFireMix",
            "SpineSword/SwordBone/ScaleContainer/YellowDots",
            "SpineSword/SwordBone/ScaleContainer/middle spike",
            "SpineSword/SwordBone/ScaleContainer/Blade",
            "SpineSword/SwordBone/ScaleContainer/Blade2",
            "SpineSword/SwordBone/ScaleContainer/Hilt",
            "SpineSword/SwordBone/ScaleContainer/Hilt2",
            "SpineSword/SwordBone/ScaleContainer/Detail",
            "SpineSword/SwordBone/ScaleContainer/BladeGlow",
            "SpineSword/SwordBone/ScaleContainer/SpikeCircle",
            "SpineSword/SwordBone/ScaleContainer/Spikes",
            "SpineSword/SwordBone/ScaleContainer/Spikes2",
            "SpineSword/SwordBone/ScaleContainer/BladeOutline2",
            "SpineSword/SwordBone/ScaleContainer/BladeOutline"
        };
        foreach (string nodePath in vanillaVisualNodes)
        {
            var node = __instance.GetNodeOrNull<CanvasItem>(nodePath);
            if (node != null)
            {
                node.SelfModulate = new Color(1f, 1f, 1f, 0f);
                node.Visible = false;
            }
        }
        
        Node2D customSword = loadedSceneData.Instantiate<Node2D>();
        customSword.Name = "CustomSwordVisuals";
        spineNode.AddChild(customSword);
        
        // 根据你的贴图大小微调旋转角度和坐标
        customSword.Position += skin.BladeOffset; 
        customSword.Scale = skin.BladeScale;
    }
    
    
    [HarmonyPatch(typeof(NCreature), nameof(NCreature._Ready))]
    [HarmonyPostfix]
    public static void OnCombatCreatureReady(NCreature __instance)
    {
        var entity = __instance.Entity;
        if (entity?.Monster is not Osty) return;
        
        string charId = entity.PetOwner.Character.Id.Entry;
        SkinData skin = SkinApi.GetSelectedSkin(charId);

        if (skin == null || string.IsNullOrEmpty(skin.OstyCombatSpineDataPath))
        {
            LogHelper.LogNoneSkin();
            return;
        }

        LogHelper.LogReplace("奥斯提spine模型", skin);
        
        if (string.IsNullOrEmpty(skin.OstyCombatSpineDataPath))
        {
            LogHelper.LogEmptyPath("奥斯提spine模型", skin);
            return;
        }
        
        Resource loadedSpineData = null;
        if (!string.IsNullOrEmpty(skin.OstyCombatSpineDataPath))
        {
            loadedSpineData = ResourceLoader.Load<Resource>(skin.OstyCombatSpineDataPath);
        }

        if (loadedSpineData == null)
        {
            LogHelper.ErrorLoad("奥斯提spine模型",  skin);
            return;
        }

        var visuals = __instance.Visuals;
        Node2D body = VisualHelper.GetBody(visuals); // 见底部的反射辅助方法
        if (body == null || !visuals.HasSpineAnimation)
        {
            Log.Error("[皮肤管理器] 未能成功获取Body，或当前人物没有Spine动画。");
            return;
        }
        

        // 1. 替换骨骼
        
        MegaSprite spineController = VisualHelper.ReplaceSpine(body, loadedSpineData);
        
        // 2. 微调属性
        spineController.GetAnimationState().SetTimeScale(1.4f);
        body.Position += skin.OstyOffset;
        body.Scale = skin.OstyScale;

        // 3. 构建通用的状态机
        AccessTools.Field(typeof(NCreature), "_spineAnimator").SetValue(__instance, GenerateOstyUniversalAnimator(spineController, skin));

        if (skin.EnableCombatShadow)
        {
            VisualHelper.SetShadow(
                body,
                skin.OstyCombatCombatShadowOffset,
                skin.OstyCombatShadowScale
                );
        }
        
        foreach (Node2D spineNode in body.GetChildren().OfType<Node2D>())
        {
            if (spineNode.Name == "Flame")
            {
                spineNode.Visible = false;
                spineNode.SelfModulate = new Color(1f, 1f, 1f, 0f);
            }
        }
    }
    
    [HarmonyPatch(typeof(NRestSiteCharacter), nameof(NRestSiteCharacter._Ready))]
    [HarmonyPostfix]
    public static void OnRestSiteReady(NRestSiteCharacter __instance)
    {
        if (__instance.Player == null) return;
        
        string charId = __instance.Player.Character.Id.Entry;
        if (charId != "NECROBINDER") return;
        
        SkinData skin = SkinApi.GetSelectedSkin(charId);

        if (skin == null)
        {
            LogHelper.LogNoneSkin();
            return;
        }

        if (string.IsNullOrEmpty(skin.OstyRestSiteSpineDataPath))
        {
            LogHelper.LogEmptyPath("奥斯提spine资源", skin);
        }
        
        LogHelper.LogReplace("奥斯提spine资源", skin);
        Resource loadedSpineData = null;
        if (!string.IsNullOrEmpty(skin.OstyRestSiteSpineDataPath))
        {
            loadedSpineData = ResourceLoader.Load<Resource>(skin.OstyRestSiteSpineDataPath);
        }

        if (loadedSpineData == null)
        {
            LogHelper.ErrorLoad("奥斯提spine资源", skin);
            return;
        }

        Log.Info($"[皮肤管理器] {skin.SkinId} 正在尝试寻找并替换奥斯提spine模型。");
        foreach (Node2D spineNode in __instance.GetChildren().OfType<Node2D>())
        {
            if (spineNode.GetClass() == "SpineSprite"  && spineNode.Name == "Osty")
            {
                MegaSprite spineController = new MegaSprite(spineNode);
                spineController.SetSkeletonDataRes(new MegaSkeletonDataResource(loadedSpineData));
                spineNode.Scale = skin.RestSiteScale;
                spineNode.Position += skin.RestSiteOffset;
                spineController.GetAnimationState().AddAnimation(skin.OstyRestSiteAnimName, 0f, true);
                
                foreach (Node2D spineNode2 in spineNode.GetChildren().OfType<Node2D>())
                {
                    if (spineNode2.Name == "SpineSlotNode")
                    {
                        spineNode2.Visible = false;
                    }
                }
            }
        }
    }
    
    private static CreatureAnimator GenerateOstyUniversalAnimator(MegaSprite controller, SkinData skin)
    {
        Log.Info($"[皮肤管理器] {skin.SkinId} 加载奥斯提动画。");
        AnimState idleState = null;
        AnimState deadState = null;
        AnimState deadLoopState = null;
        if (skin.OstyCombatAnimMap.ContainsKey("Idle"))
        {
            idleState = new AnimState(skin.OstyCombatAnimMap["Idle"], isLooping: true);
        }

        if (skin.OstyCombatAnimMap.ContainsKey("Dead"))
        {
            deadState = new AnimState(skin.OstyCombatAnimMap["Dead"]);
        }
        if (skin.OstyCombatAnimMap.ContainsKey("dead_loop"))
        {
            deadLoopState = new AnimState(skin.OstyCombatAnimMap["dead_loop"]);
        }

        CreatureAnimator animator = new CreatureAnimator(idleState, controller);
        animator.AddAnyState("Idle", idleState);
        animator.AddAnyState("Dead", deadState);
        deadState.NextState = deadLoopState;

        foreach (var mappedAnim in skin.OstyCombatAnimMap.Keys.Distinct())
        {
            if (mappedAnim == "Idle" || mappedAnim == "Dead" || mappedAnim == "dead_loop" ) continue;
            Log.Info($"[皮肤管理器] {skin.SkinId} 奥斯提游戏内动画trigger : " + mappedAnim);
            Log.Info($"[皮肤管理器] {skin.SkinId} 奥斯提Spine动画名称 : " + skin.OstyCombatAnimMap[mappedAnim]);
            AnimState newState = new AnimState(skin.OstyCombatAnimMap[mappedAnim]);
            newState.NextState = idleState; // 播完回退到 Idle
            animator.AddAnyState(mappedAnim, newState);
        }
        
        foreach (var key in skin.OstyCombatRandomAnimMap.Keys)
        {
            Dictionary<string, int> mappedAnim = skin.OstyCombatRandomAnimMap[key];
            foreach (var mappedAnim2 in mappedAnim.Keys)
            {
                Log.Info($"[皮肤管理器] {skin.SkinId} 奥斯提游戏内动画trigger : " + key);
                Log.Info($"[皮肤管理器] {skin.SkinId} 奥斯提Spine动画名称 : " + mappedAnim2);
                AnimState newState = new AnimState(mappedAnim2);
                newState.NextState = idleState; // 播完回退到 Idle
                animator.AddAnyState(mappedAnim2, newState);
            }
        }

        Log.Info($"[皮肤管理器] {skin.SkinId} 奥斯提动画加载完成");
        return animator;
    }
}