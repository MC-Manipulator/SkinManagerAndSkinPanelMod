using HarmonyLib;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.ModdingScreen;
using MegaCrit.sts2.Core.Nodes.TopBar;
using SkinManagerAndSkinPanelMod.Scripts.Data;

namespace SkinManagerAndSkinPanelMod;

[HarmonyPatch]
public static class UniversalUIPatches
{
    private static bool _hasLoadedConfig = false;
    public static NCharacterSelectScreen CurrentScreenInstance { get; private set; }

    // 1. 主菜单读取存档
    [HarmonyPatch(typeof(NMainMenu), nameof(NMainMenu._Ready))]
    [HarmonyPostfix]
    public static void MainMenuReady()
    {
        if (!_hasLoadedConfig)
        {
            Log.Info("[皮肤管理器] 初始化中");

            try
            {
                UniversalSettingsManager.InitializeUniversalSettings(); 

                Callable.From(() => {
                    SkinApi.LoadAllSkinChoices();
                }).CallDeferred();
            
                _hasLoadedConfig = true;
            }
            catch (Exception e)
            {
                Log.Error($"[皮肤管理器] 初始化过程中出现未知错误:{e}");
            }
            
            Log.Info("[皮肤管理器] 初始化完成");
        }
    }

    // 2. 注入面板
    [HarmonyPatch(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.OnSubmenuOpened))]
    [HarmonyPrefix]
    public static bool InjectPanel(NCharacterSelectScreen __instance)
    {
        // 🌟 1. 记录当前界面的实例，因为马上要用到它来获取 _charButtonContainer
        CurrentScreenInstance = __instance;

        // 🌟 2. 获取包含所有选人按钮的容器
        var buttonContainer = __instance.GetNodeOrNull<Control>("CharSelectButtons/ButtonContainer");
        if (buttonContainer != null)
        {
            // 遍历所有的选人按钮
            foreach (Node child in buttonContainer.GetChildren())
            {
                if (child is NCharacterSelectButton button && button.Character != null)
                {
                    string charId = button.Character.Id.Entry;
                    
                    // 如果这个角色有皮肤，就强制刷一次它的头像
                    if (SkinApi.HasSkins(charId))
                    {
                        // 借用我们之前写好的更新单个按钮头像的公共方法
                        UpdateSelectButtonIconToCurrentSkin(charId);
                    }
                }
            }
        }
        else
        {
            Log.Warn("[皮肤管理器] 无法找到选人按钮容器，无法在进入界面时初始化皮肤头像。");
        }

        // =========================================================
        // 下面是原本注入皮肤选择面板的代码，保持不变
        // =========================================================
        var infoPanel = __instance.GetNodeOrNull<Control>("CharSelectButtons");
        if (infoPanel == null)
        {
            Log.Error("[皮肤管理器] 未能找到选择角色按钮列表，皮肤选择面板注入失败。");
            return true;
        }

        if (infoPanel.HasNode("UniversalSkinPanel"))
        {
            Log.Info("[皮肤管理器] 已经存在皮肤选择面板，不再重复操作。");
            return true; // 🌟 修复：如果有就不加了，直接 return
        }

        try
        {
            PackedScene panelScene = ResourceLoader.Load<PackedScene>("res://Scene/UniversalSkinPanel.tscn");
            if (panelScene != null)
            {
                Control panelVisuals = panelScene.Instantiate<Control>();
                panelVisuals.Name = "Visuals";
                
                UniversalSkinPanel panelLogic = new UniversalSkinPanel();
                panelLogic.Name = "UniversalSkinPanel";
                panelLogic.Visible = false;

                panelLogic.AddChild(panelVisuals);
                infoPanel.AddChildSafely(panelLogic);
            }
            else
            {
                Log.Error("[皮肤管理器] 未能成功加载皮肤选择面板。");
            }
        }
        catch (System.Exception e) { Log.Error("框架注入面板失败: " + e.ToString()); }
        
        return true;
    }

    // 3. 点击角色时，切换面板和背景
    [HarmonyPatch(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.SelectCharacter))]
    [HarmonyPostfix]
    public static void OnSelectCharacter(NCharacterSelectScreen __instance, CharacterModel characterModel)
    {
        CurrentScreenInstance = __instance;
        string charId = characterModel.Id.Entry;

        var skinPanel = __instance.GetNodeOrNull<UniversalSkinPanel>("CharSelectButtons/UniversalSkinPanel");
        if (skinPanel != null)
        {
            if (SkinApi.HasSkins(charId))
            {
                skinPanel.Visible = true;
                skinPanel.RefreshPanel(charId, false); // 刷新但不触发自身背景更新
                
                // 🌟 同步更新大背景和小头像
                UpdateBackgroundToCurrentSkin(charId);
                UpdateSelectButtonIconToCurrentSkin(charId); 
            }
            else
            {
                skinPanel.Visible = false;
            }
        }
    }

    [HarmonyPatch(typeof(NTopBarPortrait), nameof(NTopBarPortrait.Initialize))]
    [HarmonyPrefix]
    public static bool ReplaceIcon(NTopBarPortrait __instance, Player player)
    {
        if (player == null || player.Character == null)
        {
            Log.Warn("[皮肤管理器] 替换Icon时，未读取到玩家或玩家角色。");
            return true;
        }
        
        string charId = player.Character.Id.Entry;
        SkinData skin = SkinApi.GetSelectedSkin(charId);

        if (skin == null)
        {
            Log.Info("[皮肤管理器] 替换Icon时，未读取到皮肤信息。");
            return true;
        } 
        
        // 🌟 实时加载骨骼数据
        PackedScene loadedIconData = null;
        if (!string.IsNullOrEmpty(skin.IconScenePath))
        {
            Log.Info($"[皮肤管理器] 尝试从如下路径读取Icon。{ skin.IconScenePath }");
            loadedIconData = ResourceLoader.Load<PackedScene>(skin.IconScenePath);
        }
        else
        {
            Log.Info("[皮肤管理器] 当前皮肤没有可读取的Icon，停止替换。");
            return true;
        }
        
        if (loadedIconData == null)
        {
            Log.Error("[皮肤管理器] 未能加载皮肤Icon。");
            return true;
        }

        Log.Info("[皮肤管理器] 替换角色Icon。");
        Control node = loadedIconData.Instantiate<Control>();
        __instance.AddChildSafely(node);
        return false;
    }
    
    
    // 🌟 新增：动态更新选人按钮头像
    public static void UpdateSelectButtonIconToCurrentSkin(string characterId)
    {
        if (CurrentScreenInstance == null || !GodotObject.IsInstanceValid(CurrentScreenInstance)) return;

        // 获取选人界面的按钮容器
        var buttonContainer = AccessTools.Field(typeof(NCharacterSelectScreen), "_charButtonContainer").GetValue(CurrentScreenInstance) as Control;
        if (buttonContainer == null || !GodotObject.IsInstanceValid(buttonContainer)) return;

        // 获取当前选中的皮肤数据
        SkinData currentSkin = SkinApi.GetSelectedSkin(characterId);
        if (currentSkin == null) return;

        // 遍历所有角色按钮，找到对应当前角色的那个按钮
        foreach (Node child in buttonContainer.GetChildren())
        {
            if (child is NCharacterSelectButton button && button.Character.Id.Entry == characterId)
            {
                // 如果角色被锁定，就不替换头像 (保持一把锁的样子)
                if (button.IsLocked) return;

                // 尝试加载皮肤专属的头像
                Texture2D newIconTexture = null;
                if (!string.IsNullOrEmpty(currentSkin.SelectIconPath))
                {
                    newIconTexture = ResourceLoader.Load<Texture2D>(currentSkin.SelectIconPath);
                }

                // 如果皮肤没有配置专属头像，就退回原版的头像
                if (newIconTexture == null)
                {
                    newIconTexture = button.Character.CharacterSelectIcon;
                }

                // 获取按钮内部的 _icon 和 _iconAdd 节点并替换贴图
                var iconRect = AccessTools.Field(typeof(NCharacterSelectButton), "_icon").GetValue(button) as TextureRect;
                var iconAddRect = AccessTools.Field(typeof(NCharacterSelectButton), "_iconAdd").GetValue(button) as TextureRect;

                if (iconRect != null && GodotObject.IsInstanceValid(iconRect))
                {
                    iconRect.Texture = newIconTexture;
                }
                
                if (iconAddRect != null && GodotObject.IsInstanceValid(iconAddRect))
                {
                    iconAddRect.Texture = newIconTexture;
                }

                break; // 找到了就跳出循环
            }
        }
    }

    /// <summary>
    /// 更新角色选择界面的大背景图。
    /// 根据玩家的 Mod 设置和当前皮肤的配置来决定使用哪个背景图。
    /// </summary>
    /// <param name="characterId">当前选择的角色 ID。</param>
    public static void UpdateBackgroundToCurrentSkin(string characterId)
    {
        try
        {
            if (CurrentScreenInstance == null || !GodotObject.IsInstanceValid(CurrentScreenInstance))
            {
                Log.Error("[皮肤管理器] 未找到选择角色界面的背景");
                return;
            }
            // 获取背景图容器节点
            var bgContainer = AccessTools.Field(typeof(NCharacterSelectScreen), "_bgContainer").GetValue(CurrentScreenInstance) as Control;
            if (bgContainer == null || !GodotObject.IsInstanceValid(bgContainer))
            {
                Log.Error("[皮肤管理器] 未找到选择角色界面的背景图容器");
                return;
            }

            // 清理旧的背景图节点
            foreach (Node child in bgContainer.GetChildren())
            {
                if (child is CanvasItem)
                    child.QueueFree();
            }

            // 获取当前角色的选中皮肤数据
            SkinData currentSkin = SkinApi.GetSelectedSkin(characterId);
            if (currentSkin == null)
            {
                Log.Info("[皮肤管理器] 皮肤选择界面未读取到当前角色的皮肤。");
                return; // 如果皮肤数据为空则返回
            }

            string backgroundPathToUse = null; // 要使用的背景图路径
            bool useAI = true; // 是否最终决定使用 AI 背景图

            if (!string.IsNullOrEmpty(currentSkin.ModId)) // 确保 SkinData 中有 ModId
            {
                useAI = UniversalSettingsManager.IsModAIBackgroundEnabled(currentSkin.ModId);
            }
            else
            {
                Log.Warn($"[皮肤管理器] 角色 '{characterId}' 的皮肤 '{currentSkin.SkinId}' 未设置 ModId。");
            }

            // 根据 useAI 状态和皮肤是否提供了 AI 背景图路径来决定使用哪个背景
            if (useAI && !string.IsNullOrEmpty(currentSkin.AIBackgroundScenePath))
            {
                backgroundPathToUse = currentSkin.AIBackgroundScenePath; // 使用 AI 背景图路径
            }
            else if (!string.IsNullOrEmpty(currentSkin.BackgroundScenePath))
            {
                backgroundPathToUse = currentSkin.BackgroundScenePath; // 使用默认背景图路径
            }

            // 加载并实例化选定的背景图场景
            if (!string.IsNullOrEmpty(backgroundPathToUse))
            {
                PackedScene myBgScene = ResourceLoader.Load<PackedScene>(backgroundPathToUse);
                if (myBgScene != null)
                {
                    Control myBg = myBgScene.Instantiate<Control>(); // 实例化场景
                    myBg.Name = $"CustomBG_{characterId}_{currentSkin.SkinId}"; // 设置节点名称，便于追踪
                    bgContainer.AddChild(myBg); // 添加到背景容器中
                } 
                else 
                {
                     Log.Error($"[皮肤管理器] 无法加载背景场景: {backgroundPathToUse}，角色 {characterId}");
                }
            }
        }
        catch (Exception e)
        {
            Log.Error($"[皮肤管理器] 加载背景时出错");
        }
    }
    
    
    // --- 补丁：NModInfoContainer.Fill ---
    // 为每个 Mod 信息条目添加自定义设置（如 AI 背景图开关）。
    [HarmonyPatch(typeof(NModInfoContainer), nameof(NModInfoContainer.Fill))]
    [HarmonyPostfix]
    public static void AddUniversalModSettings(NModInfoContainer __instance, Mod mod)
    {
        // 此补丁会在 Mod 列表中渲染每个 Mod 时被调用。
        string currentModId = mod.manifest.id; // 获取当前 Mod 的 ID

        if (__instance.HasNode("ModSettingsContainer"))
        {
            var targetNode = __instance.GetNode<HBoxContainer>("ModSettingsContainer");
            if (targetNode != null)
            {
                targetNode.QueueFree();
            }
        }
        
        if (!mod.manifest.dependencies.Contains("SkinManagerAndSkinPanelMod")) return;
        
        // 检查当前 Mod 是否提供了 AI 背景图能力
        // 使用 UniversalSettingsManager 中提供的方法来获取所有提供 AI 背景的 Mod ID 列表
        List<string> modsWithAIBackgrounds = UniversalSettingsManager.GetModsWithAIBackgrounds();

        // 如果当前 Mod 在列表中，则添加 AI 背景开关 UI
        if (modsWithAIBackgrounds.Contains(currentModId))
        {
            // 创建一个容器来容纳该 Mod 的设置项
            HBoxContainer modSettingsContainer = new HBoxContainer();
            modSettingsContainer.Name = "ModSettingsContainer"; // 节点名称，包含 Mod ID
            modSettingsContainer.Alignment = BoxContainer.AlignmentMode.Center; // 水平居中对齐内容
            modSettingsContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; // 允许容器水平扩展以填充可用空间
            modSettingsContainer.Position = new Vector2(20, 560);
            
            // --- AI 背景图勾选框 ---
            Label aiBgLabel = new Label();
            aiBgLabel.Text = "AI背景图："; // 标签文本
            aiBgLabel.AddThemeColorOverride("font_color", new Color(1, 0.84f, 0)); // 设置为金色字体
            aiBgLabel.VerticalAlignment = VerticalAlignment.Center; // 垂直居中文本

            CheckBox aiBgCheckBox = new CheckBox();
            aiBgCheckBox.VerticalIconAlignment = VerticalAlignment.Center; // 垂直居中勾选框
            // 设置勾选框的初始状态，根据 Mod 的 AI 背景启用设置
            aiBgCheckBox.ButtonPressed = UniversalSettingsManager.IsModAIBackgroundEnabled(currentModId);

            // 连接勾选框的 Toggled 信号到处理函数
            aiBgCheckBox.Toggled += (bool pressed) =>
            {
                // 更新 Mod 的 AI 背景启用设置，并保存
                UniversalSettingsManager.SetModAIBackgroundEnabled(currentModId, pressed);
                // 播放 UI 音效
                SfxCmd.Play(pressed ? "event:/sfx/ui/clicks/ui_checkbox_on" : "event:/sfx/ui/clicks/ui_checkbox_off");
            };

            modSettingsContainer.AddChild(aiBgLabel); // 添加标签到容器
            modSettingsContainer.AddChild(aiBgCheckBox); // 添加勾选框到容器
            // 如果设置容器中有任何子节点（即添加了 AI 背景开关），则将其添加到 Mod 信息面板
            if (modSettingsContainer.GetChildCount() > 0)
            {
                __instance.AddChild(modSettingsContainer); // 将设置容器添加到 Mod 信息面板
            }
        }
    }
}

// =====================================================================
// 🌟 新增：处理多人联机读取存档界面 (NMultiplayerLoadGameScreen) 的背景替换
// =====================================================================

[HarmonyPatch]
public static class MultiplayerLoadGameUIPatches
{
    // 保存当前读取存档界面的实例
    public static NMultiplayerLoadGameScreen CurrentLoadScreenInstance { get; private set; }

    [HarmonyPatch(typeof(NMultiplayerLoadGameScreen), "AfterMultiplayerStarted")]
    [HarmonyPostfix]
    public static void OnMultiplayerLoadGameStarted(NMultiplayerLoadGameScreen __instance)
    {
        CurrentLoadScreenInstance = __instance;

        // 1. 获取当前玩家的角色 ID
        // 通过反射获取私有的 _runLobby
        var runLobby = AccessTools.Field(typeof(NMultiplayerLoadGameScreen), "_runLobby").GetValue(__instance) as MegaCrit.Sts2.Core.Multiplayer.Game.Lobby.LoadRunLobby;
        if (runLobby == null) return;

        // 获取当前本地玩家的数据
        var localPlayer = runLobby.Run.Players.FirstOrDefault(p => p.NetId == runLobby.NetService.NetId);
        if (localPlayer == null) return;

        string charId = localPlayer.CharacterId.Entry; // 注意在 SerializablePlayer 中叫 CharacterId

        // 2. 调用专门为读取界面写的背景更新方法
        UpdateLoadScreenBackgroundToCurrentSkin(charId);
    }

    /// <summary>
    /// 更新多人联机读取存档界面的大背景图。
    /// </summary>
    public static void UpdateLoadScreenBackgroundToCurrentSkin(string characterId)
    {
        try
        {
            if (CurrentLoadScreenInstance == null || !GodotObject.IsInstanceValid(CurrentLoadScreenInstance)) return;

            // 获取背景图容器节点 (_bgContainer)
            var bgContainer = AccessTools.Field(typeof(NMultiplayerLoadGameScreen), "_bgContainer").GetValue(CurrentLoadScreenInstance) as Control;
            if (bgContainer == null || !GodotObject.IsInstanceValid(bgContainer)) return;

            // 检查当前角色是否有选中皮肤
            SkinData currentSkin = SkinApi.GetSelectedSkin(characterId);
            if (currentSkin == null) return;

            // 确定要加载的背景图路径
            string backgroundPathToUse = null;
            bool useAI = true;

            if (!string.IsNullOrEmpty(currentSkin.ModId))
            {
                useAI = UniversalSettingsManager.IsModAIBackgroundEnabled(currentSkin.ModId);
            }

            if (useAI && !string.IsNullOrEmpty(currentSkin.AIBackgroundScenePath))
            {
                backgroundPathToUse = currentSkin.AIBackgroundScenePath;
            }
            else if (!string.IsNullOrEmpty(currentSkin.BackgroundScenePath))
            {
                backgroundPathToUse = currentSkin.BackgroundScenePath;
            }

            // 加载并替换背景
            if (!string.IsNullOrEmpty(backgroundPathToUse))
            {
                PackedScene myBgScene = ResourceLoader.Load<PackedScene>(backgroundPathToUse);
                if (myBgScene != null)
                {
                    // 先清理原版的旧背景
                    foreach (Node child in bgContainer.GetChildren())
                    {
                        if (child is CanvasItem)
                            child.QueueFree();
                    }

                    Control myBg = myBgScene.Instantiate<Control>();
                    myBg.Name = $"CustomLoadBG_{characterId}_{currentSkin.SkinId}";
                    bgContainer.AddChild(myBg);
                    
                    MegaCrit.Sts2.Core.Logging.Log.Info($"[皮肤管理器] 成功替换联机读取界面的背景图: {characterId}");
                }
            }
        }
        catch (System.Exception e)
        {
            MegaCrit.Sts2.Core.Logging.Log.Error($"[皮肤管理器] 加载联机读取界面背景时出错: {e}");
        }
    }
}