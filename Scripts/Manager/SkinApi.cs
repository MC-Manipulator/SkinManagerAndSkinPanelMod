using Godot;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Logging;
using SkinManagerAndSkinPanelMod.Scripts.Data;

namespace SkinManagerAndSkinPanelMod;

// 皮肤数据模型


// 暴露给外部调用的静态 API
public static class SkinApi
{
    private const string SavePath = "user://universal_skin_config.cfg";
    private static Dictionary<string, List<SkinData>> _characterSkins = new Dictionary<string, List<SkinData>>();
    private static Dictionary<string, int> _selectedIndices = new Dictionary<string, int>();

    public static void RegisterSkin(string characterId, SkinData skinData)
    {
        if (string.IsNullOrEmpty(skinData.ModId))
            Log.Info("[皮肤管理器] 该皮肤没有配置ModID，可能会有影响 :" + skinData.SkinId);
        
        if (!_characterSkins.ContainsKey(characterId))
        {
            _characterSkins[characterId] = new List<SkinData>();
            _selectedIndices[characterId] = 0; 
        }
        _characterSkins[characterId].Add(skinData);
        Log.Info($"[皮肤管理器] 成功注册皮肤: [{characterId}] {skinData.SkinName}");
    }

    
    public static void RegisterSkin(string modId, string characterId, SkinData skinData)
    {
        skinData.ModId = modId;

        if (!_characterSkins.ContainsKey(characterId))
        {
            _characterSkins[characterId] = new List<SkinData>();
            _selectedIndices[characterId] = 0;
        }
        _characterSkins[characterId].Add(skinData);
        Log.Info($"[皮肤管理器] 成功注册皮肤: [{characterId}] {skinData.SkinName} 来自 '{modId}'");
    }
    
    public static bool HasSkins(string characterId) => _characterSkins.ContainsKey(characterId) && _characterSkins[characterId].Count > 0;

    public static SkinData GetSelectedSkin(string characterId)
    {
        if (!HasSkins(characterId))
        {
            Log.Info($"[皮肤管理器] {characterId} 该角色没有皮肤");
            return null;
        }
        return _characterSkins[characterId][_selectedIndices[characterId]];
    }

    public static List<SkinData> GetAllSkinsFor(string characterId)
    {
        if (!HasSkins(characterId)) return new List<SkinData>();
        return _characterSkins[characterId];
    }

    public static int GetCurrentIndex(string characterId)
    {
        return _selectedIndices.ContainsKey(characterId) ? _selectedIndices[characterId] : 0;
    }

    public static void NextSkin(string characterId)
    {
        if (!HasSkins(characterId)) return;
        _selectedIndices[characterId]++;
        if (_selectedIndices[characterId] >= _characterSkins[characterId].Count)
            _selectedIndices[characterId] = 0;
        SaveSkinChoice(characterId);
    }

    public static void PrevSkin(string characterId)
    {
        if (!HasSkins(characterId)) return;
        _selectedIndices[characterId]--;
        if (_selectedIndices[characterId] < 0)
            _selectedIndices[characterId] = _characterSkins[characterId].Count - 1;
        SaveSkinChoice(characterId);
    }
    
    public static List<string> GetAllCharacterIdsWithVoiceData()
    {
        var idsWithVoice = new List<string>();
        foreach (var characterId in _characterSkins.Keys) 
        {
            List<SkinData> skins = _characterSkins[characterId];
            if (skins.Any(skin => skin.VoiceEvents != null && skin.VoiceEvents.Count > 0))
            {
                idsWithVoice.Add(characterId);
            }
        }
        return idsWithVoice;
    }
    public static bool HasMethod(string methodName) { return true; /* Placeholder */ }
    
    // --- 本地配置读写 ---
    public static void LoadAllSkinChoices()
    {
        try 
        {
            ConfigFile config = new ConfigFile();
            Error err = config.Load(SavePath);
            if (err == Error.Ok)
            {
                // 遍历已注册的所有角色，读取它们的索引
                List<string> keys = new List<string>(_characterSkins.Keys);
                foreach (string charId in keys)
                {
                    int savedIndex = (int)config.GetValue(charId, "Index", 0);
                    if (savedIndex >= 0 && savedIndex < _characterSkins[charId].Count)
                    {
                        _selectedIndices[charId] = savedIndex;
                    }
                }
            }
        }
        catch (System.Exception e) { Log.Error("[皮肤管理器] 读取通用皮肤配置失败: " + e.Message); }
    }

    private static void SaveSkinChoice(string characterId)
    {
        try 
        {
            ConfigFile config = new ConfigFile();
            config.Load(SavePath); // 先读取现有配置，防止覆盖其他角色的数据
            config.SetValue(characterId, "Index", _selectedIndices[characterId]);
            config.Save(SavePath);
        }
        catch (System.Exception e) { Log.Error("[皮肤管理器] 保存通用皮肤配置失败: " + e.Message); }
    }
    
    /// <summary>
    /// 用于 UniversalSettingsManager 检查哪些 Mod 提供了 AI 背景图。
    /// </summary>
    /// <returns>一个包含所有已注册 SkinData 对象的列表。</returns>
    public static List<SkinData> GetAllRegisteredSkins()
    {
        List<SkinData> allSkins = new List<SkinData>();
        // 遍历所有角色
        foreach (var skinsList in _characterSkins.Values)
        {
            // 将每个角色的所有皮肤添加到总列表中
            allSkins.AddRange(skinsList);
        }
        return allSkins;
    }
}