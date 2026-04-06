using Godot;
using HarmonyLib; // 如果需要使用 Harmony 相关的辅助方法
using System; // For Exception
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Logging;
using SkinManagerAndSkinPanelMod.Scripts.Data; // For LINQ methods like Any()

namespace SkinManagerAndSkinPanelMod;

public static class UniversalSettingsManager
{
    // --- 配置路径 ---
    // 统一由框架管理设置文件的保存位置
    private const string SettingsPath = "user://skin_manager_universal_settings.cfg";
    
    // --- 管理的设置 ---
    // 1. !!! NEW: Skin-specific Voice Enabled !!!
    //    Key: "CharacterId/SkinId" (e.g., "REGENT/RosmontisOrigin")
    //    Value: bool (true = voice enabled for this skin, false = disabled)
    private static Dictionary<string, bool> _skinSpecificVoiceEnabled = new Dictionary<string, bool>();
    
    private static Dictionary<string, bool> _modSpecificAIBackgroundEnabled = new Dictionary<string, bool>();

    private static Dictionary<string, bool> _universalVoiceEnabled = new Dictionary<string, bool>();
    
    private static Dictionary<string, string> _skinSpecificLanguage = new Dictionary<string, string>();

    public static readonly Dictionary<string, string> AvailableLanguages = new()
    {
        {"zh", "中文"},
        {"en", "English"},
        {"ja", "日本語"}
    };
    
    // 在框架的 Initialize 方法中调用，负责加载所有通用设置
    public static void InitializeUniversalSettings()
    {
        LoadUniversalSettings();
        
        EnsureAllKnownCharacterVoiceSettings(); 
    }

    private static void LoadUniversalSettings()
    {
        try
        {
            ConfigFile config = new ConfigFile();
            Error err = config.Load(SettingsPath);
            if (err == Error.Ok)
            {                
                var voiceSectionKeys = config.GetSectionKeys("SkinVoiceSettings");
                foreach (var skinKey in voiceSectionKeys)
                {
                    _skinSpecificVoiceEnabled[skinKey] = (bool)config.GetValue("SkinVoiceSettings", skinKey, true); 
                }

                // !!! Load Mod-specific AI Background Settings !!!
                var bgSectionKeys = config.GetSectionKeys("ModAIBackgroundSettings");
                foreach (var modId in bgSectionKeys)
                {
                    _modSpecificAIBackgroundEnabled[modId] = (bool)config.GetValue("ModAIBackgroundSettings", modId, true);
                }
                
                // !!! NEW: Load Skin Language Settings !!!
                var langSectionKeys = config.GetSectionKeys("SkinLanguageSettings");
                foreach (var skinKey in langSectionKeys) // skinKey is "CharacterId/SkinId"
                {
                    string lang = (string)config.GetValue("SkinLanguageSettings", skinKey, "zh"); // Default to "zh"
                    _skinSpecificLanguage[skinKey] = lang;
                }
            }
        }
        catch (System.Exception e)
        {
            Log.Warn($"[UniversalSettings] Failed to load settings: {e.Message}. Using defaults.");
        }
        
        EnsureAllKnownCharacterVoiceSettings();
    }

    private static void SaveUniversalSettings()
    {
        try
        {
            Log.Info("[皮肤管理器] 保存数据中");
            ConfigFile config = new ConfigFile();

            // Save Character Voice Settings
            foreach (var kvp in _skinSpecificVoiceEnabled)
            {
                Log.Info($"[皮肤管理器] {kvp.Key} : {kvp.Value}");
                config.SetValue("SkinVoiceSettings", kvp.Key, kvp.Value);
            }

            // !!! Save Mod-specific AI Background Settings !!!
            foreach (var kvp in _modSpecificAIBackgroundEnabled)
            {
                config.SetValue("ModAIBackgroundSettings", kvp.Key, kvp.Value);
            }
            
            // !!! NEW: Save Skin Language Settings !!!
            foreach (var kvp in _skinSpecificLanguage)
            {
                config.SetValue("SkinLanguageSettings", kvp.Key, kvp.Value);
            }
            
            config.Save(SettingsPath);
        }
        catch (System.Exception e)
        {
            GD.PrintErr($"[皮肤管理器] 没能成功保存数据: {e.Message}");
        }
    }
    
    /// <summary>
    /// 根据角色和皮肤ID获取语音的语言设置.
    /// </summary>
    public static string GetSkinLanguage(string characterId, string skinId)
    {
        string key = $"{characterId}/{skinId}";
        
        // 如果找到了玩家保存过的语言，直接返回
        if (_skinSpecificLanguage.TryGetValue(key, out string lang))
        {
            return lang; 
        }

        // ================= 🌟 核心修复 🌟 =================
        // 如果没存过，去查一下这个皮肤到底支持什么语言，取它支持的第一个作为默认！
        var skin = SkinManagerAndSkinPanelMod.SkinApi.GetSelectedSkin(characterId);
        if (skin != null && skin.SkinId == skinId && skin.SupportedLanguages != null && skin.SupportedLanguages.Count > 0)
        {
            return skin.SupportedLanguages[0]; // 返回皮肤支持的第一个语言（比如 "ja"）
        }

        // 如果皮肤没写 SupportedLanguages，再 fallback 回 "zh"
        return "zh"; 
        // ==================================================
    }

    /// <summary>
    /// Sets the selected language code for a specific character and skin, then saves settings.
    /// </summary>
    /// <param name="characterId">Character ID.</param>
    /// <param name="skinId">Skin ID.</param>
    /// <param name="language">The language code to set (e.g., "zh", "en").</param>
    public static void SetSkinLanguage(string characterId, string skinId, string language)
    {
        string key = $"{characterId}/{skinId}";
        _skinSpecificLanguage[key] = language;
        SaveUniversalSettings();
    }
    
    /// <summary>
    /// Sets the voice enabled state for a specific character and skin, then saves settings.
    /// </summary>
    /// <param name="characterId">The character's ID.</param>
    /// <param name="skinId">The skin's ID.</param>
    /// <param name="enabled">Whether to enable voice.</param>
    public static void SetSkinVoiceEnabled(string characterId, string skinId, bool enabled)
    {
        string key = $"{characterId}/{skinId}";
        _skinSpecificVoiceEnabled[key] = enabled;
        SaveUniversalSettings(); // Save immediately
    }
    
    /// <summary>
    /// Checks if voice is enabled for a specific character and skin.
    /// </summary>
    /// <param name="characterId">The character's ID.</param>
    /// <param name="skinId">The skin's ID.</param>
    /// <returns>True if voice is enabled for this skin, false otherwise.</returns>
    public static bool IsSkinVoiceEnabled(string characterId, string skinId)
    {
        string key = $"{characterId}/{skinId}";
        // Default to true (voice enabled) if setting doesn't exist for this skin.
        if (!_skinSpecificVoiceEnabled.TryGetValue(key, out bool enabled))
        {
            return true; 
        }
        return enabled;
    }

    public static void EnsureAllKnownCharacterVoiceSettings()
    {
        List<string> characterIdsToProcess;
        
        // 尝试从 SkinApi 获取注册的角色 ID 列表（这是最理想的方式）
        if (SkinApi.HasMethod(nameof(SkinApi.GetAllCharacterIdsWithVoiceData))) // 假设 SkinApi 有此方法
        {
            characterIdsToProcess = SkinApi.GetAllCharacterIdsWithVoiceData();
        }
        else 
        {
            // 如果 SkinApi 没有直接提供，可以使用一个备用列表（例如，列出所有已知的玩家角色 ID）。
            // 注意：这个备用列表需要根据游戏实际情况更新。
            string[] fallbackKnownCharacterIds = {"REGENT", "IRONCLAD", "SILENT", "DEFECT", "NECROBINDER"}; // 示例，根据游戏角色 ID 调整
            characterIdsToProcess = fallbackKnownCharacterIds.ToList();
        }

        foreach (string charId in characterIdsToProcess)
        {
            if (!_universalVoiceEnabled.ContainsKey(charId))
            {
                _universalVoiceEnabled[charId] = true;
            }
        }
    }

    // 查询特定角色的语音是否启用
    public static bool IsCharacterVoiceEnabled(string characterId)
    {
        // 如果某个角色 ID 不在字典中，我们默认它是启用的（以防万一）。
        if (!_universalVoiceEnabled.TryGetValue(characterId, out bool enabled))
        {
            // Log.Warning($"[UniversalSettings] 角色 '{characterId}' 未找到语音设置，默认开启。"); // 可选日志
            return true; 
        }
        return enabled;
    }

    // 设置特定角色的语音启用状态，并保存
    public static void SetCharacterVoiceEnabled(string characterId, bool enabled)
    {
        _universalVoiceEnabled[characterId] = enabled;
        SaveUniversalSettings(); // 每次改变都立即保存
    }

    public static bool IsModAIBackgroundEnabled(string modId)
    {
        if (!_modSpecificAIBackgroundEnabled.TryGetValue(modId, out bool enabled))
        {
            return true;
        }
        return enabled;
    }

    public static void SetModAIBackgroundEnabled(string modId, bool enabled)
    {
        _modSpecificAIBackgroundEnabled[modId] = enabled;
        SaveUniversalSettings(); // Save immediately
    }
    
    public static List<string> GetModsWithAIBackgrounds()
    {
        HashSet<string> modsWithAI = new HashSet<string>(); // 使用 HashSet 来避免重复的 Mod ID

        // 遍历所有已注册的皮肤，收集那些定义了 ModId 且 AI 背景图路径不为空的 Mod 的 ID。
        // 这里假设 SkinApi 提供了获取所有已注册皮肤的方法。
        List<SkinData> allRegisteredSkins = SkinApi.GetAllRegisteredSkins(); // 调用 SkinApi 的新方法

        if (allRegisteredSkins != null)
        {
            foreach (SkinData skin in allRegisteredSkins)
            {
                // 检查 ModId 是否有效，AI 背景图路径是否有效
                if (!string.IsNullOrEmpty(skin.ModId) && !string.IsNullOrEmpty(skin.AIBackgroundScenePath))
                {
                    modsWithAI.Add(skin.ModId); // 添加到 HashSet 中
                }
            }
        }
        else
        {
            // 如果无法获取所有注册皮肤，打印警告信息
            GD.PrintErr($"[皮肤管理器] 无法从 SkinApi 获取所有已注册皮肤，可能无法正确识别所有提供 AI 背景图的 Mod。");
        }
        
        return modsWithAI.ToList();
    }
}