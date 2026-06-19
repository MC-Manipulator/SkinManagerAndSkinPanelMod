using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Events.Custom;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using SkinManagerAndSkinPanelMod.Scripts.Data;
using SkinManagerAndSkinPanelMod.Scripts.Helper;

namespace SkinManagerAndSkinPanelMod;

[HarmonyPatch]
public static class FakeMerchantPatches
{
    
    // ==========================================
    // 💰 2. 商店 (Merchant Room)
    // ==========================================
    [HarmonyPatch(typeof(NFakeMerchant), "AfterRoomIsLoaded")]
    [HarmonyPostfix]
    public static void OnMerchantRoomLoaded(NFakeMerchant __instance)
    {
        var playersList = AccessTools.Field(typeof(NFakeMerchant), "_players").GetValue(__instance) as List<Player>;
        var characterContainer = AccessTools.Field(typeof(NFakeMerchant), "_characterContainer").GetValue(__instance) as Control;
        
        if (playersList == null)
        {        
            Log.Warn("[皮肤管理器] 未能获取商店内数据，停止替换玩家角色spine模型。");
            return;
        }

        for (int i = 0; i < playersList.Count; i++)
        {
            
            string charId = playersList[i].Character.Id.Entry;
            ulong playerId = playersList[i].NetId; // 🌟 拿到玩家 ID
            SkinData skin = SkinApi.GetSelectedSkin(charId);
            
            if (skin == null || string.IsNullOrEmpty(skin.MerchantSpineDataPath))
            {
                Log.Info("[皮肤管理器] 未读取到皮肤，或玩家角色spine路径为空，停止商店的spine模型替换。");
                continue;
            }
            
            // 🌟 实时加载骨骼数据
            Resource loadedSpineData = null;
            if (!string.IsNullOrEmpty(skin.MerchantSpineDataPath))
            {
                loadedSpineData = ResourceLoader.Load<Resource>(skin.MerchantSpineDataPath);
            }

            if (loadedSpineData == null)
            {
                Log.Error($"[皮肤管理器] 玩家角色spine资源未能成功加载。{skin.ModId} {skin.SkinId}");
                continue;
            }

            NCreatureVisuals characterVisual = characterContainer.GetChild<NCreatureVisuals>(playersList.Count - 1 - i);;
            Node2D targetSpineNode = characterVisual.GetChildren().OfType<Node2D>().FirstOrDefault(c => c.GetClass() == "SpineSprite");

            if (targetSpineNode != null)
            {
                MegaSprite spineController = VisualHelper.ReplaceSpine(targetSpineNode, loadedSpineData);
                targetSpineNode.Scale = skin.MerchantScale;
                targetSpineNode.Position += skin.MerchantOffset;

                // 打上专属标签，记录它是哪个角色，方便后面播动画
                characterVisual.SetMeta("UniversalSkinCharId", charId);
                characterVisual.SetMeta("UniversalSkinPlayerId", playerId); // 🌟 注入玩家ID元数据
                characterVisual.Scale *= 0.6f;
                if (skin.MerchantAnimMap.ContainsKey("relaxed_loop"))
                    spineController.GetAnimationState().AddAnimation(skin.MerchantAnimMap["relaxed_loop"], 0f, true); // 默认初始动作
                
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
                    VisualHelper.SetShadow(characterVisual, skin.MerchantOffset, skin.MerchantShadowScale);
                }
            }
        }
    }

    private static Material _defaultMaterial = null;

    private static Material GetGoldOutlineMaterial()
    {
        return ResourceLoader.Load<ShaderMaterial>("res://Materials/GoldOutlineMaterial.tres");
    }
    
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
            displayRect.Material = _defaultMaterial;
        }
    }
    
    private static void OnMerchantCharacterTouched(InputEvent e, NCreatureVisuals characterVisual, Node2D spineNode, SkinData skin, ulong playerId)
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
}