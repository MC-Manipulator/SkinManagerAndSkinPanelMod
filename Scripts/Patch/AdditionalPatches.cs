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

namespace SkinManagerAndSkinPanelMod;

[HarmonyPatch]
public static class SovereignBladeVfx
{
    [HarmonyPatch(typeof(NSovereignBladeVfx), nameof(NSovereignBladeVfx._Ready))]
    [HarmonyPostfix]
    public static void ReplaceBlade(NSovereignBladeVfx __instance)
    {
        if (__instance.Card?.Owner?.Character?.Id.Entry != "REGENT") return;
        
        Log.Info("替换君王之剑");
        
        SkinData skin = SkinApi.GetSelectedSkin("REGENT");
        
        if (skin == null) return;
        
        // 🌟 实时加载骨骼数据
        PackedScene loadedSceneData = null;
        if (!string.IsNullOrEmpty(skin.BladeScenePath))
        {
            loadedSceneData = GD.Load<PackedScene>(skin.BladeScenePath);
        }
        
        if (loadedSceneData == null) return;
        
        
        // 2. 获取根部 Spine 节点
        var spineNode = __instance.GetNode<Node2D>("SpineSword");
        
        if (spineNode.HasNode("CustomSwordVisuals")) return;

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
        
        if (skin == null || string.IsNullOrEmpty(skin.OstyCombatSpineDataPath)) return; 
        
        // 🌟 实时加载骨骼数据
        Resource loadedSpineData = null;
        if (!string.IsNullOrEmpty(skin.OstyCombatSpineDataPath))
        {
            loadedSpineData = GD.Load<Resource>(skin.OstyCombatSpineDataPath);
        }
        
        if (loadedSpineData == null) return;

        var visuals = __instance.Visuals;
        Node2D body = UniversalScenePatches.GetBody(visuals); // 见底部的反射辅助方法
        if (body == null || !visuals.HasSpineAnimation) return;

        // 1. 替换骨骼
        MegaSprite spineController = new MegaSprite(body);
        spineController.SetSkeletonDataRes(new MegaSkeletonDataResource(loadedSpineData));
        
        // 2. 微调属性
        spineController.GetAnimationState().SetTimeScale(1.4f);
        body.Position += skin.OstyOffset;
        body.Scale = skin.OstyScale;

        // 3. 构建通用的状态机
        AccessTools.Field(typeof(NCreature), "_spineAnimator").SetValue(__instance, GenerateOstyUniversalAnimator(spineController, skin));

        if (skin.EnableCombatShadow)
        {
            PackedScene combatShadow = GD.Load<PackedScene>("res://Scene/Shadow/Shadow_Combat.tscn");
            if (combatShadow != null)
            {
                Node2D node = combatShadow.Instantiate<Node2D>();

                if (__instance.Visuals != null && body != null)
                {
                    body.AddChild(node);
                    body.MoveChild(node, 0);
                    node.Position += skin.OstyCombatCombatShadowOffset;
                    node.Scale = skin.OstyCombatShadowScale;
                }
            }
        }
        
        foreach (Node2D spineNode in body.GetChildren().OfType<Node2D>())
        {
            if (spineNode.Name == "Flame")
            {
                spineNode.Visible = false;
                spineNode.SelfModulate = new Color(1f, 1f, 1f, 0f);
            }
        }
        /*
        // 4. 挂载额外场景 (例如 Blade)
        if (skin.BladeScene != null)
        {
            
        }*/
    }
    
    [HarmonyPatch(typeof(NRestSiteCharacter), nameof(NRestSiteCharacter._Ready))]
    [HarmonyPostfix]
    public static void OnRestSiteReady(NRestSiteCharacter __instance)
    {
        if (__instance.Player == null) return;
        string charId = __instance.Player.Character.Id.Entry;
        if (charId != "NECROBINDER") return;
        SkinData skin = SkinApi.GetSelectedSkin(charId);
        
        if (skin == null || string.IsNullOrEmpty(skin.OstyRestSiteSpineDataPath) ) return;
        
        // 🌟 实时加载骨骼数据
        Resource loadedSpineData = null;
        if (!string.IsNullOrEmpty(skin.OstyRestSiteSpineDataPath))
        {
            loadedSpineData = GD.Load<Resource>(skin.OstyRestSiteSpineDataPath);
        }
        
        if (loadedSpineData == null) return;

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
    
    /*
    [HarmonyPatch(typeof(OstyCmd), nameof(OstyCmd.Summon))]
    [HarmonyPostfix]
    public static void OstyFlameDisable(PlayerChoiceContext choiceContext, Player summoner, decimal amount, AbstractModel? source)
    {
        if (summoner == null) return;
        string charId = summoner.Character.Id.Entry;
        if (charId != "NECROBINDER") return;
        SkinData skin = SkinApi.GetSelectedSkin(charId);
        
        if (skin == null || string.IsNullOrEmpty(skin.OstyRestSiteSpineDataPath) ) return;
        Log.Load("Flame Disable");
        CombatState combatState = summoner.Creature.CombatState;
        Creature osty = combatState.Allies.FirstOrDefault((Creature c) => c.Monster is Osty && c.PetOwner == summoner);
        NCreature nCreature = NCombatRoom.Instance?.GetCreatureNode(osty);
        foreach (Node2D spineNode in UniversalScenePatches.GetBody(nCreature.Visuals).GetChildren().OfType<Node2D>())
        {
            if (spineNode.GetClass() == "SpineSprite"  && spineNode.Name == "Osty")
            {
                foreach (Node2D spineNode2 in spineNode.GetChildren().OfType<Node2D>())
                {
                    if (spineNode2.Name == "Flame")
                    {
                        spineNode2.Visible = false;
                    }
                }
            }
        }
    }*/
    
    private static CreatureAnimator GenerateOstyUniversalAnimator(MegaSprite controller, SkinData skin)
    {
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
            Log.Info("Osty游戏内动画trigger : " + mappedAnim);
            Log.Info("Osty Spine动画名称 : " + skin.OstyCombatAnimMap[mappedAnim]);
            AnimState newState = new AnimState(skin.OstyCombatAnimMap[mappedAnim]);
            newState.NextState = idleState; // 播完回退到 Idle
            animator.AddAnyState(mappedAnim, newState);
        }
        
        foreach (var key in skin.OstyCombatRandomAnimMap.Keys)
        {
            Dictionary<string, int> mappedAnim = skin.OstyCombatRandomAnimMap[key];
            foreach (var mappedAnim2 in mappedAnim.Keys)
            {
                Log.Info("Osty游戏内动画trigger : " + key);
                Log.Info("Osty Spine动画名称 : " + mappedAnim2);
                AnimState newState = new AnimState(mappedAnim2);
                newState.NextState = idleState; // 播完回退到 Idle
                animator.AddAnyState(mappedAnim2, newState);
            }
        }

        return animator;
    }
}



/*
[HarmonyPatch(typeof(NSovereignBladeVfx), nameof(NSovereignBladeVfx.Forge))]
public static class Patch_NSovereignBladeVfx_Forge
{
    public static void Postfix(NSovereignBladeVfx __instance, float bladeDamage, bool showFlames)
    {
        if (__instance.Card?.Owner?.Character?.Id.Entry == "REGENT")
        {
            var spineNode = __instance.GetNode<Node2D>("SpineSword");
            
            if (spineNode is CanvasItem canvasSpine)
            {
                canvasSpine.SelfModulate = new Color(1f, 1f, 1f, 0f);
            }

            var glowT = AccessTools.Field(typeof(NSovereignBladeVfx), "_glowTween").GetValue(__instance) as Tween;
            if (glowT != null)
            {
                glowT.Kill();
            }
            
            // 3. 隐藏所有原版用于拼接武器的贴图节点
            // 如果不隐藏，充能时原版的剑柄和发光特效会突然冒出来
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
                    // 使用 SelfModulate 免疫原版 Tween 动画对透明度的覆盖
                    node.SelfModulate = new Color(1f, 1f, 1f, 0f);
                }
            }
            
        }
    }
}
*/