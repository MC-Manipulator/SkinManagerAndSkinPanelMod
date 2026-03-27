using HarmonyLib;
using Godot;
using System.Linq;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.GameOverScreen;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Nodes.Vfx;

namespace SkinManagerAndSkinPanelMod;

[HarmonyPatch]
public static class UniversalScenePatches
{
    // ==========================================
    // ⚔️ 1. 战斗房间 (Combat Room)
    // ==========================================
    [HarmonyPatch(typeof(NCreature), nameof(NCreature._Ready))]
    [HarmonyPostfix]
    public static void OnCombatCreatureReady(NCreature __instance)
    {
        var entity = __instance.Entity;
        if (entity?.Player == null) return;
        
        string charId = entity.Player.Character.Id.Entry;
        SkinData skin = SkinApi.GetSelectedSkin(charId);
        
        if (skin == null) return; 
        
        // 🌟 实时加载骨骼数据
        Resource loadedSpineData = null;
        if (!string.IsNullOrEmpty(skin.CombatSpineDataPath))
        {
            loadedSpineData = GD.Load<Resource>(skin.CombatSpineDataPath);
        }
        
        if (loadedSpineData == null) return;

        var visuals = __instance.Visuals;
        Node2D body = GetBody(visuals); // 见底部的反射辅助方法
        if (body == null || !visuals.HasSpineAnimation) return;

        // 1. 替换骨骼
        MegaSprite spineController = new MegaSprite(body);
        spineController.SetSkeletonDataRes(new MegaSkeletonDataResource(loadedSpineData));
        
        // 2. 微调属性
        spineController.GetAnimationState().SetTimeScale(1.4f); // 默认全局加速，如果需要也可配到 SkinData 里
        body.Scale = skin.CombatScale;
        body.Position += skin.CombatOffset;

        // 3. 构建通用的状态机
        AccessTools.Field(typeof(NCreature), "_spineAnimator").SetValue(__instance, GenerateUniversalAnimator(spineController, skin));

        if (skin.EnableCombatShadow && !body.HasNode("CombatShadow"))
        {
            PackedScene combatShadow = GD.Load<PackedScene>("res://Scene/Shadow/Shadow_Combat.tscn");
            if (combatShadow != null)
            {
                Node2D node = combatShadow.Instantiate<Node2D>();

                if (__instance.Visuals != null && body != null)
                {
                    body.AddChild(node);
                    body.MoveChild(node, 0);
                    node.Name = "CombatShadow";
                    node.Position += skin.CombatShadowOffset;
                    node.Scale = skin.CombatShadowScale;
                }
            }
        }
        if (charId == "NECROBINDER")
        {
            foreach (Node2D spineNode in body.GetChildren().OfType<Node2D>())
            {
                if (spineNode.Name == "HeadBoneNode")
                {
                    foreach (var spineNode2 in spineNode.GetChildren().OfType<Node2D>())
                    {
                        if (spineNode2.Name == "SteppedFireMix_dark")
                        {
                            spineNode2.Visible = false;
                            spineNode2.SelfModulate = new Color(1f, 1f, 1f, 0f);
                        }
                    }
                }
            }
        }
    }

    
    [HarmonyPatch(typeof(NCreature), nameof(NCreature.SetAnimationTrigger))]
    [HarmonyPrefix]
    public static bool OnCombatAnimationTrigger(NCreature __instance, string trigger)
    {
        var entity = __instance.Entity;
        if (entity?.Player == null) return true;

        string charId = entity.Player.Character.Id.Entry;
        SkinData skin = SkinApi.GetSelectedSkin(charId);
        if (skin == null) return true;

        // 查找映射字典，如果有映射则替换 Trigger
        
        if (skin.CombatRandomAnimMap != null && skin.CombatRandomAnimMap.TryGetValue(trigger, out Dictionary<string, int> animMap))
        {
            if (animMap != null)
            {
                System.Random r = new System.Random();
                int total = 0;
                foreach (var weight in animMap.Values)
                {
                    total += weight;
                }
                int i = r.Next(0, total);
                string rolledKey = trigger;
                foreach (var key in animMap.Keys)
                {
                    i -= animMap[key];
                    if (i < 0)
                    {
                        rolledKey = key;
                        break;
                    }
                }
                
                Log.Info(trigger + "=>" + rolledKey);
                trigger = rolledKey;
            }
        }

        var spineAnimator = AccessTools.Field(typeof(NCreature), "_spineAnimator").GetValue(__instance) as CreatureAnimator;
        spineAnimator?.SetTrigger(trigger);
        
        return false; // 拦截原版
    }
    
    // ==========================================
    // 💰 2. 商店 (Merchant Room)
    // ==========================================
    [HarmonyPatch(typeof(NMerchantRoom), "AfterRoomIsLoaded")]
    [HarmonyPostfix]
    public static void OnMerchantRoomLoaded(NMerchantRoom __instance)
    {
        var playersList = AccessTools.Field(typeof(NMerchantRoom), "_players").GetValue(__instance) as List<Player>;
        var visualsList = __instance.PlayerVisuals;
        if (playersList == null || visualsList == null) return;

        for (int i = 0; i < playersList.Count; i++)
        {
            if (i >= visualsList.Count) break;
            
            string charId = playersList[i].Character.Id.Entry;
            ulong playerId = playersList[i].NetId; // 🌟 拿到玩家 ID
            SkinData skin = SkinApi.GetSelectedSkin(charId);
            
            if (skin == null) continue;
            
            // 🌟 实时加载骨骼数据
            Resource loadedSpineData = null;
            if (!string.IsNullOrEmpty(skin.MerchantSpineDataPath))
            {
                loadedSpineData = GD.Load<Resource>(skin.MerchantSpineDataPath);
            }
            
            if (loadedSpineData == null) continue;

            NMerchantCharacter characterVisual = visualsList[i];
            Node2D targetSpineNode = characterVisual.GetChildren().OfType<Node2D>().FirstOrDefault(c => c.GetClass() == "SpineSprite");

            if (targetSpineNode != null)
            {
                MegaSprite spineController = new MegaSprite(targetSpineNode);
                spineController.SetSkeletonDataRes(new MegaSkeletonDataResource(loadedSpineData));
                targetSpineNode.Scale = skin.MerchantScale;
                targetSpineNode.Position += skin.MerchantOffset;

                // 打上专属标签，记录它是哪个角色，方便后面播动画
                characterVisual.SetMeta("UniversalSkinCharId", charId);
                characterVisual.SetMeta("UniversalSkinPlayerId", playerId); // 🌟 注入玩家ID元数据
                
                if (skin.MerchantAnimMap.ContainsKey("relaxed_loop"))
                    spineController.GetAnimationState().AddAnimation(skin.MerchantAnimMap["relaxed_loop"], 0f, true); // 默认初始动作

                // ================= 🌟 核心重构：Viewport 渲染法 🌟 =================
                if (skin.EnableMerchantTouch)
                {
                    Node parent = targetSpineNode.GetParent();

                    // 1. 创建“摄影棚” (SubViewport)
                    // 透明背景非常关键，否则描边会变成黑框！
                    SubViewport viewport = new SubViewport();
                    viewport.Name = "SpineViewport";
                    viewport.TransparentBg = true; 
                    viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
                    // 设置画布大小，必须能装得下整个 Spine 模型 (商店模型一般 512x512 足够)
                    viewport.Size = new Vector2I(1024, 1024);

                    // 2. 把 Spine 模型放进摄影棚
                    parent.RemoveChild(targetSpineNode);
                    viewport.AddChild(targetSpineNode);
                    
                    // 让 Spine 在摄影棚里居中
                    // 注意：这里抵消了原本在主场景里的 Scale 和 Position
                    targetSpineNode.Scale = skin.MerchantScale;
                    targetSpineNode.Position = new Vector2(512, 512) + skin.MerchantOffset; 

                    // 3. 将摄影棚作为一个挂载点放回原父节点
                    // SubViewport 必须在场景树中才能渲染
                    parent.AddChild(viewport);

                    // 4. 创建“幕布” (TextureRect) 来显示摄影棚拍到的画面
                    TextureRect displayRect = new TextureRect();
                    displayRect.Name = "SpineDisplay";
                    // 将幕布的纹理设置为摄影棚的实时输出！
                    displayRect.Texture = viewport.GetTexture(); 
                    
                    // 将幕布放回原本 Spine 该呆的位置 (调整居中偏移)
                    displayRect.Position = new Vector2(-512, -512);
                    
                    parent.AddChild(displayRect);
                    parent.MoveChild(displayRect, 0); // 确保幕布在底层

                    // 5. 创建隐形的触摸热区
                    Control touchArea = new Control();
                    touchArea.Name = "SkinTouchArea";
                    touchArea.CustomMinimumSize = skin.MerchantTouchAreaSize;
                    touchArea.Size = skin.MerchantTouchAreaSize;
                    touchArea.Position = skin.MerchantTouchAreaOffset;
                    touchArea.MouseFilter = Control.MouseFilterEnum.Stop;
                    
                    parent.AddChild(touchArea);

                    // 6. 绑定悬停事件，这次我们把材质挂在“幕布”上！
                    touchArea.GuiInput += (InputEvent e) => OnMerchantCharacterTouched(e, characterVisual, targetSpineNode, skin, playerId); // 🌟 传入 playerId
                    touchArea.MouseEntered += () => OnMerchantCharacterHovered(displayRect);
                    touchArea.MouseExited += () => OnMerchantCharacterUnhovered(displayRect);
                }
                else
                {
                    // 如果没开启触碰，按原样处理
                    targetSpineNode.Scale = skin.MerchantScale;
                    targetSpineNode.Position += skin.MerchantOffset;
                }
                
                
                if (charId == "NECROBINDER")
                {
                    foreach (Node2D spineNode in targetSpineNode.GetChildren().OfType<Node2D>())
                    {
                        if (spineNode.Name == "HeadBoneNode")
                        {
                            foreach (var spineNode2 in spineNode.GetChildren().OfType<Node2D>())
                            {
                                if (spineNode2.Name == "SteppedFireMix_dark")
                                {
                                    spineNode2.Visible = false;
                                    spineNode2.SelfModulate = new Color(1f, 1f, 1f, 0f);
                                }
                            }
                        }
                    }
                }
                
                if (skin.EnableMerchantShadow)
                {
                    PackedScene merchantShadow = GD.Load<PackedScene>("res://Scene/Shadow/Shadow_Merchant.tscn");
                    if (merchantShadow != null && !characterVisual.HasNode("MerchantShadow"))
                    {
                        Node2D node = merchantShadow.Instantiate<Node2D>();
                        node.Position = skin.MerchantShadowOffset; 
                        node.Scale = skin.MerchantShadowScale;
                        node.Name = "MerchantShadow";

                        characterVisual.AddChild(node);
                        characterVisual.MoveChild(node, 0);
                    }
                }
            }
        }
    }

    private static Material _defaultMaterial = null;

    private static Material GetGoldOutlineMaterial()
    {
        return GD.Load<ShaderMaterial>("res://Materials/GoldOutlineMaterial.tres");
    }

// 参数改接收 TextureRect
    private static void OnMerchantCharacterHovered(TextureRect displayRect)
    {
        if (displayRect != null)
        {
            if (_defaultMaterial == null)
            {
                _defaultMaterial = displayRect.Material;
            }

            var outlineMat = GetGoldOutlineMaterial();
            if (outlineMat != null)
            {
                // 给幕布挂上描边着色器
                displayRect.Material = outlineMat;
                MegaCrit.Sts2.Core.Commands.SfxCmd.Play("event:/sfx/ui/clicks/ui_hover");
            }
        }
    }

    private static void OnMerchantCharacterUnhovered(TextureRect displayRect)
    {
        if (displayRect != null)
        {
            // 移开鼠标，撤下着色器
            displayRect.Material = _defaultMaterial;
        }
    }
    
    private static void OnMerchantCharacterTouched(InputEvent e, NMerchantCharacter characterVisual, Node2D spineNode, SkinData skin, ulong playerId)
    {
        if (e is InputEventMouseButton mouseBtn && mouseBtn.ButtonIndex == MouseButton.Left && mouseBtn.Pressed)
        {
            MegaSprite spineController = new MegaSprite(spineNode);
            MegaAnimationState animationState = spineController.GetAnimationState();

            animationState.SetAnimation(skin.MerchantTouchAnimName, loop: false);
            string idleAnim = skin.MerchantAnimMap.ContainsKey("relaxed_loop") ? skin.MerchantAnimMap["relaxed_loop"] : "relaxed_loop";
            animationState.AddAnimation(idleAnim, 0f, true);
        
            // 🌟 精准呼叫 VoicePlayer
            VoicePlayer.PlayEvent(playerId, characterVisual.GetMeta("UniversalSkinCharId").AsString(), "Touch");
        }
    }
    
    [HarmonyPatch(typeof(NMerchantCharacter), nameof(NMerchantCharacter.PlayAnimation))]
    [HarmonyPrefix]
    public static bool OnMerchantPlayAnimation(NMerchantCharacter __instance, string anim, bool loop)
    {
        if (__instance.HasMeta("UniversalSkinCharId"))
        {
            string charId = __instance.GetMeta("UniversalSkinCharId").AsString();
            SkinData skin = SkinApi.GetSelectedSkin(charId);
            if (skin == null) return true;

            var node = __instance.GetNodeOrNull<SubViewport>("SpineViewport");
            if (node != null)
            {
                Log.Info("替换中");
                Node2D targetSpineNode = node.GetChildren().OfType<Node2D>().FirstOrDefault(c => c.GetClass() == "SpineSprite");
                if (targetSpineNode != null)
                {
                    MegaSprite spineController = new MegaSprite(targetSpineNode);
                    // 翻译动画名称
                    string targetAnim = skin.MerchantAnimMap.ContainsKey(anim) ? skin.MerchantAnimMap[anim] : "relax_loop";
                    spineController.GetAnimationState().AddAnimation(targetAnim, 0f, loop);
                    return false; 
                }
            }
            else
            {
                Log.Info("替换中2");
                Node2D targetSpineNode = __instance.GetChildren().OfType<Node2D>().FirstOrDefault(c => c.GetClass() == "SpineSprite");
                if (targetSpineNode != null)
                {
                    MegaSprite spineController = new MegaSprite(targetSpineNode);
                    // 翻译动画名称
                    string targetAnim = skin.MerchantAnimMap.ContainsKey(anim) ? skin.MerchantAnimMap[anim] : "relax_loop";
                    spineController.GetAnimationState().AddAnimation(targetAnim, 0f, loop);
                    return false; 
                }
            }
        }
        return true; 
    }

    // ==========================================
    // 🏕️ 3. 休息处 (Rest Site Room)
    // ==========================================
    [HarmonyPatch(typeof(NRestSiteCharacter), nameof(NRestSiteCharacter._Ready))]
    [HarmonyPostfix]
    public static void OnRestSiteReady(NRestSiteCharacter __instance)
    {
        if (__instance.Player == null) return;
        string charId = __instance.Player.Character.Id.Entry;
        SkinData skin = SkinApi.GetSelectedSkin(charId);
        
        if (skin == null) return;
        
        // 🌟 实时加载骨骼数据
        Resource loadedSpineData = null;
        if (!string.IsNullOrEmpty(skin.RestSiteSpineDataPath))
        {
            loadedSpineData = GD.Load<Resource>(skin.RestSiteSpineDataPath);
        }
        
        if (loadedSpineData == null) return;

        if (charId != "NECROBINDER")
        {
            foreach (Node2D spineNode in __instance.GetChildren().OfType<Node2D>())
            {
                if (spineNode.GetClass() == "SpineSprite")
                {
                    MegaSprite spineController = new MegaSprite(spineNode);
                    spineController.SetSkeletonDataRes(new MegaSkeletonDataResource(loadedSpineData));
                    spineNode.Scale = skin.RestSiteScale;
                    spineNode.Position += skin.RestSiteOffset;
                    spineController.GetAnimationState().AddAnimation(skin.RestSiteAnimName, 0f, true);
                }
            }
        }
        else
        {
            foreach (Node2D spineNode in __instance.GetChildren().OfType<Node2D>())
            {
                if (spineNode.GetClass() == "SpineSprite" && spineNode.Name == "Necro")
                {
                    MegaSprite spineController = new MegaSprite(spineNode);
                    spineController.SetSkeletonDataRes(new MegaSkeletonDataResource(loadedSpineData));
                    spineNode.Scale = skin.RestSiteScale;
                    spineNode.Position += skin.RestSiteOffset;
                    spineController.GetAnimationState().AddAnimation(skin.RestSiteAnimName, 0f, true);
                    
                    foreach (Node2D spineNode2 in spineNode.GetChildren().OfType<Node2D>())
                    {
                        if (spineNode2.Name == "SpineBoneNode")
                        {
                            spineNode2.Visible = false;
                        }
                    }
                }
            }
        }
    }

    // ==========================================
    // ☠️ 4. 结算画面 (Game Over Screen)
    // ==========================================
    [HarmonyPatch(typeof(NGameOverScreen), "MoveCreaturesToDifferentLayerAndDisableUi")]
    [HarmonyPostfix]
    public static void OnGameOverMoveCreatures(NGameOverScreen __instance)
    {
        var runState = AccessTools.Field(typeof(NGameOverScreen), "_runState").GetValue(__instance) as MegaCrit.Sts2.Core.Runs.RunState;
        var creatureContainer = AccessTools.Field(typeof(NGameOverScreen), "_creatureContainer").GetValue(__instance) as Control;
        if (runState == null || creatureContainer == null) return;

        foreach (Player player in runState.Players)
        {
            string charId = player.Character.Id.Entry;
            ulong playerID = player.NetId;
            SkinData skin = SkinApi.GetSelectedSkin(charId);
            
            if (skin == null) continue;
            
            // 🌟 实时加载骨骼数据
            Resource loadedSpineData = null;
            if (!string.IsNullOrEmpty(skin.CombatSpineDataPath))
            {
                loadedSpineData = GD.Load<Resource>(skin.CombatSpineDataPath);
            }
            
            if (loadedSpineData == null) continue;

            foreach (Node child in creatureContainer.GetChildren())
            {
                if (child is NCreatureVisuals visuals && NCombatRoom.Instance == null && NMerchantRoom.Instance == null)
                {
                    Node2D body = GetBody(visuals);
                    if (body != null)
                    {
                        MegaSprite spineController = new MegaSprite(body);
                        spineController.SetSkeletonDataRes(new MegaSkeletonDataResource(loadedSpineData));
                        
                        if (skin.CombatAnimMap.ContainsKey("Dead"))
                            spineController.GetAnimationState().SetAnimation(skin.CombatAnimMap["Dead"], loop: false); // 通用死亡动画名
                        
                        body.Scale = skin.GameOverScale;
                    }
                }
            }
            // ================= 🌟 核心修复：转场后的字幕保障 🌟 =================
            // 在角色被强行搬家到 GameOver 界面的容器后，
            // 立即停止上一句（可能还在老容器里放着）的语音和字幕，并强制在这个新环境里重播一句遗言！
            VoicePlayer.Stop();
            VoicePlayer.PlayEvent(playerID, charId, "Die");
            // =================================================================
        }
    }

    [HarmonyPatch(typeof(NGameOverScreen), "MoveCreaturesToDifferentLayerAndDisableUi")]
    [HarmonyPrefix]
    public static bool OnGameOverMoveCreatures_Prefix(NGameOverScreen __instance)
    {
        if (NMerchantRoom.Instance != null)
        {
            Log.Info("商店中死亡");
            foreach (NMerchantCharacter playerVisual in NMerchantRoom.Instance.PlayerVisuals)
            {
                Log.Info("检查" + playerVisual.Name);
                if (playerVisual.HasMeta("UniversalSkinCharId"))
                {
                    Log.Info("替换模型");
                    string charId = playerVisual.GetMeta("UniversalSkinCharId").AsString();
                    SkinData skin = SkinApi.GetSelectedSkin(charId);
                    if (skin == null) return true;

                    // 🌟 实时加载骨骼数据
                    Resource loadedSpineData = null;
                    if (!string.IsNullOrEmpty(skin.CombatSpineDataPath))
                    {
                        loadedSpineData = GD.Load<Resource>(skin.CombatSpineDataPath);
                    }
                    
                    if (loadedSpineData == null) continue;

                    
                    var node = playerVisual.GetNodeOrNull<SubViewport>("SpineViewport");
                    if (node != null)
                    {
                        Log.Info("替换中");
                        Node2D targetSpineNode = node.GetChildren().OfType<Node2D>().FirstOrDefault(c => c.GetClass() == "SpineSprite");
                        if (targetSpineNode != null)
                        {
                            Log.Info("正在替换模型...");
                            MegaSprite spineController = new MegaSprite(targetSpineNode);
                            spineController.SetSkeletonDataRes(new MegaSkeletonDataResource(loadedSpineData));
                        }
                    }
                    else
                    {
                        Log.Info("替换中2");
                        Node2D targetSpineNode = playerVisual.GetChildren().OfType<Node2D>().FirstOrDefault(c => c.GetClass() == "SpineSprite");
                        if (targetSpineNode != null)
                        {
                            Log.Info("正在替换模型...");
                            MegaSprite spineController = new MegaSprite(targetSpineNode);
                            spineController.SetSkeletonDataRes(new MegaSkeletonDataResource(loadedSpineData));
                        }
                    }
                }
            }
        }
        
        return true;
    }
    
    // ==========================================
    // 🛠️ 辅助方法
    // ==========================================
    public static Node2D GetBody(NCreatureVisuals visuals)
    {
        // 兼容0.99.1版本与0.100.0版本的恐惧症更新
        var traverse = Traverse.Create(visuals);
        
        // 0.99.1版本
        var bodyProp = traverse.Property("Body");
        if (bodyProp.PropertyExists()) return bodyProp.GetValue<Node2D>();
        
        // 0.100.0版本
        var bodyField = traverse.Field("_body");
        if (bodyField.FieldExists()) return bodyField.GetValue<Node2D>();
        
        return null; 
    }

    public static CreatureAnimator GenerateUniversalAnimator(MegaSprite controller, SkinData skin)
    {
        // 提取所有映射到的值，构建一个极其包容的状态机
        // 这样只要内容 Mod 在 CombatAnimMap 填了映射，状态机里就一定有这个状态
        // Idle状态作为其他状态的返回状态，需要先声明
        // Dead状态是唯一不需要返回Idle的状态，也需要单独声明

        AnimState idleState = null;
        AnimState deadState = null;
        if (skin.CombatAnimMap.ContainsKey("Idle"))
        {
            idleState = new AnimState(skin.CombatAnimMap["Idle"], isLooping: true);
        }

        if (skin.CombatAnimMap.ContainsKey("Dead"))
        {
            deadState = new AnimState(skin.CombatAnimMap["Dead"]);
        }

        CreatureAnimator animator = new CreatureAnimator(idleState, controller);
        animator.AddAnyState("Idle", idleState);
        animator.AddAnyState("Dead", deadState);

        foreach (var mappedAnim in skin.CombatAnimMap.Keys.Distinct())
        {
            if (mappedAnim == "Idle" || mappedAnim == "Dead") continue;
            Log.Info("游戏内动画trigger : " + mappedAnim);
            Log.Info("Spine动画名称 : " + skin.CombatAnimMap[mappedAnim]);
            AnimState newState = new AnimState(skin.CombatAnimMap[mappedAnim]);
            newState.NextState = idleState; // 播完回退到 Idle
            animator.AddAnyState(mappedAnim, newState);
        }
        
        foreach (var key in skin.CombatRandomAnimMap.Keys)
        {
            Dictionary<string, int> mappedAnim = skin.CombatRandomAnimMap[key];
            foreach (var mappedAnim2 in mappedAnim.Keys)
            {
                Log.Info("游戏内动画trigger : " + key);
                Log.Info("Spine动画名称 : " + mappedAnim2);
                AnimState newState = new AnimState(mappedAnim2);
                newState.NextState = idleState; // 播完回退到 Idle
                animator.AddAnyState(mappedAnim2, newState);
            }
        }

        return animator;
    }
}