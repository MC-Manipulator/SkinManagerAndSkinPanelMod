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
                Log.Error("[皮肤管理器] 初始化过程中出现未知错误。");
            }
            
            Log.Info("[皮肤管理器] 初始化完成");
        }
    }

    // 2. 注入面板
    [HarmonyPatch(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.OnSubmenuOpened))]
    [HarmonyPrefix]
    public static bool InjectPanel(NCharacterSelectScreen __instance)
    {
        var infoPanel = __instance.GetNodeOrNull<Control>("CharSelectButtons");
        if (infoPanel == null)
        {
            Log.Error("[皮肤管理器] 未能找到选择角色按钮列表，皮肤选择面板注入失败。");
            return true;
        }

        if (infoPanel.HasNode("UniversalSkinPanel"))
        {
            Log.Info("[皮肤管理器] 已经存在皮肤选择面板，不再重复操作。");
        }

        try
        {
            PackedScene panelScene = GD.Load<PackedScene>("res://Scene/UniversalSkinPanel.tscn");
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
        catch (System.Exception e)
        {
            Log.Error("[皮肤管理器] 注入皮肤选择面板失败: " + e.ToString());
        }
        
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
                UpdateBackgroundToCurrentSkin(charId); // 手动触发背景更新
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
            loadedIconData = GD.Load<PackedScene>(skin.IconScenePath);
        }
        else
        {
            Log.Info("[皮肤管理器] 当前皮肤没有可读取的Icon，停止替换。");
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