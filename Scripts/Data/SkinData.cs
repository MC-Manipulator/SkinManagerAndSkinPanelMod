using Godot;

namespace SkinManagerAndSkinPanelMod.Scripts.Data;

public class SkinData
{
    public string SkinId;
    public string SkinName;
    
    // 主要场景资源路径
    public string CombatSpineDataPath;         
    public string RestSiteSpineDataPath;
    public string MerchantSpineDataPath;
    public string BackgroundScenePath;
    public string AIBackgroundScenePath;
    
    public string ModId { get; set; }

    // 角色独特资源配置
    public string BladeScenePath; //储君君王之剑场景
    public string OstyCombatSpineDataPath; //奥斯提Spine模型路径
    public string OstyRestSiteSpineDataPath; //奥斯提Spine模型路径
    
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
    
    public Dictionary<string, string> CombatAnimMap = new Dictionary<string, string>();
    public Dictionary<string, Dictionary<string, int>> CombatRandomAnimMap = new Dictionary<string, Dictionary<string, int>>();
    public Dictionary<string, string> OstyCombatAnimMap = new Dictionary<string, string>();
    public Dictionary<string, Dictionary<string, int>> OstyCombatRandomAnimMap = new Dictionary<string, Dictionary<string, int>>();
    
    public Dictionary<string, string> MerchantAnimMap = new Dictionary<string, string>();
    
    public string RestSiteAnimName = "Sit";
    public string OstyRestSiteAnimName = "Sit";
    
    public Dictionary<string, string> CustomImpactVfxMap = new Dictionary<string, string>();
    
    public string DefaultImpactVfxPath = ""; 
    
    public Vector2 ImpactVfxScale = new Vector2(1f, 1f);
    public Vector2 ImpactVfxOffset = new Vector2(0f, 0f);

    public string IconScenePath  = "";
    public string SelectIconPath = "";
    

    public Dictionary<string, VoiceEvent> VoiceEvents = new Dictionary<string, VoiceEvent>();
    public List<string> SupportedLanguages = null;

}