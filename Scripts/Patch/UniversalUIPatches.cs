using HarmonyLib;
using Godot;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;

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
            Callable.From(() => {
                SkinApi.LoadAllSkinChoices();
            }).CallDeferred();
            _hasLoadedConfig = true;
        }
    }

    // 2. 注入面板
    [HarmonyPatch(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.OnSubmenuOpened))]
    [HarmonyPrefix]
    public static bool InjectPanel(NCharacterSelectScreen __instance)
    {
        var infoPanel = __instance.GetNodeOrNull<Control>("CharSelectButtons");
        if (infoPanel == null || infoPanel.HasNode("UniversalSkinPanel")) return true;

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
                UpdateBackgroundToCurrentSkin(charId); // 手动触发背景更新
            }
            else
            {
                skinPanel.Visible = false;
            }
        }
    }

    // 4. 更新大背景
    public static void UpdateBackgroundToCurrentSkin(string characterId)
    {
        if (CurrentScreenInstance == null || !GodotObject.IsInstanceValid(CurrentScreenInstance)) return;
        var bgContainer = AccessTools.Field(typeof(NCharacterSelectScreen), "_bgContainer").GetValue(CurrentScreenInstance) as Control;
        if (bgContainer == null || !GodotObject.IsInstanceValid(bgContainer)) return;

        foreach (Node child in bgContainer.GetChildren())
        {
            if (child is CanvasItem) child.QueueFree();
        }

        SkinData currentSkin = SkinApi.GetSelectedSkin(characterId);
        if (currentSkin != null && !string.IsNullOrEmpty(currentSkin.BackgroundScenePath))
        {
            PackedScene myBgScene = GD.Load<PackedScene>(currentSkin.BackgroundScenePath);
            if (myBgScene != null)
            {
                Control myBg = myBgScene.Instantiate<Control>();
                myBg.Name = $"CustomBG_{characterId}_{currentSkin.SkinId}";
                bgContainer.AddChild(myBg);
            }
        }
    }
}