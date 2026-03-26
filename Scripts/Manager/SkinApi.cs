using Godot;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Logging;

namespace SkinManagerAndSkinPanelMod;

// 皮肤数据模型
public class SkinData
{
    public string SkinId;
    public string SkinName;
    // 🌟 核心改变：全换成字符串路径！不再直接存 Resource 对象！
    public string CombatSpineDataPath;         
    public string RestSiteSpineDataPath;
    public string MerchantSpineDataPath;
    public string BackgroundScenePath; 
    //动画播放速度调配
    
    //角色独特资源配置
    public string BladeScenePath; //储君君王之剑场景
    public string OstyCombatSpineDataPath; //奥斯提Spine模型路径
    public string OstyRestSiteSpineDataPath; //奥斯提Spine模型路径
    
    // --- 🌍 通用坐标与缩放微调 ---
    public Vector2 MenuScale = Vector2.One;
    public Vector2 MenuOffset = Vector2.Zero;
    
    public Vector2 CombatScale = new Vector2(1f, 1f);
    public Vector2 CombatOffset = Vector2.Zero;
    public bool EnableCombatShadow  = false;
    public Vector2 CombatShadowScale = new Vector2(1f, 1f);
    public Vector2 CombatShadowOffset = Vector2.Zero;
    
    public Vector2 MerchantScale = new Vector2(1f, 1f);
    public Vector2 MerchantOffset = Vector2.Zero;
    public bool EnableMerchantShadow  = false;
    public Vector2 MerchantShadowScale = new Vector2(1f, 1f);
    public Vector2 MerchantShadowOffset = Vector2.Zero;
    public bool EnableMerchantTouch = false;           // 是否开启商店触摸互动
    public string MerchantTouchAnimName = "Interact";     // 被触摸时播放的动画名
    public Vector2 MerchantTouchAreaSize = new Vector2(150f, 200f); // 触摸判定的热区大小 (宽, 高)
    public Vector2 MerchantTouchAreaOffset = new Vector2(-75f, -200f); // 热区相对于角色中心点的偏移量
    
    public Vector2 RestSiteScale = new Vector2(1f, 1f);
    public Vector2 RestSiteOffset = Vector2.Zero;
    
    public Vector2 GameOverScale = new Vector2(1f, 1f);
    
    public Vector2 BladeScale = new Vector2(1f, 1f);
    public Vector2 BladeOffset = Vector2.Zero;
    
    public Vector2 OstyScale = new Vector2(1f, 1f);
    public Vector2 OstyOffset = Vector2.Zero;
    public Vector2 OstyCombatShadowScale = new Vector2(1f, 1f);
    public Vector2 OstyCombatCombatShadowOffset = Vector2.Zero;
    
    // --- 🎬 动画名称映射字典 (原版Trigger -> 自定义Spine动画名) ---
    // 比如 {"Attack": "Attack_A", "Hit": "Die"}
    public Dictionary<string, string> CombatAnimMap = new Dictionary<string, string>();
    public Dictionary<string, Dictionary<string, int>> CombatRandomAnimMap = new Dictionary<string, Dictionary<string, int>>();
    public Dictionary<string, string> OstyCombatAnimMap = new Dictionary<string, string>();
    public Dictionary<string, Dictionary<string, int>> OstyCombatRandomAnimMap = new Dictionary<string, Dictionary<string, int>>();
    
    // 比如 {"relaxed_loop": "Relax"}
    public Dictionary<string, string> MerchantAnimMap = new Dictionary<string, string>();
    
    public string RestSiteAnimName = "Sit"; // 休息处默认播放的动画名
    public string OstyRestSiteAnimName = "Sit";
    
    public Dictionary<string, VoiceEvent> VoiceEvents = new Dictionary<string, VoiceEvent>();
}

// 暴露给外部调用的静态 API
public static class SkinApi
{
    private const string SavePath = "user://universal_skin_config.cfg";
    private static Dictionary<string, List<SkinData>> _characterSkins = new Dictionary<string, List<SkinData>>();
    private static Dictionary<string, int> _selectedIndices = new Dictionary<string, int>();

    public static void RegisterSkin(string characterId, SkinData skinData)
    {
        if (!_characterSkins.ContainsKey(characterId))
        {
            _characterSkins[characterId] = new List<SkinData>();
            _selectedIndices[characterId] = 0; 
        }
        _characterSkins[characterId].Add(skinData);
        Log.Info($"[通用皮肤框架] 成功注册皮肤: [{characterId}] {skinData.SkinName}");
    }

    public static bool HasSkins(string characterId) => _characterSkins.ContainsKey(characterId) && _characterSkins[characterId].Count > 0;

    public static SkinData GetSelectedSkin(string characterId)
    {
        if (!HasSkins(characterId))
        {
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
        catch (System.Exception e) { Log.Error("读取通用皮肤配置失败: " + e.Message); }
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
        catch (System.Exception e) { Log.Error("保存通用皮肤配置失败: " + e.Message); }
    }
}