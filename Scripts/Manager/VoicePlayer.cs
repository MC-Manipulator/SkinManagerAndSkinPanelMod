using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.GameOverScreen;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using MegaCrit.Sts2.Core.Runs;
using SkinManagerAndSkinPanelMod.Scripts.Data;
using SkinManagerAndSkinPanelMod.Scripts.Helper; // NGame

namespace SkinManagerAndSkinPanelMod;

public static class VoicePlayer
{
    private static AudioStreamPlayer _playerNode;
    private static CanvasLayer _subtitleLayer;
    private static VoiceSubtitleVfx _currentSubtitle;
    private static ulong _currentAfkPlayerId = 0;
    private static string _currentAfkCharacterId = "";
    
    //"EnterCombat" "Die" "KillBoss" "KillEnemy" "TurnStart" "PlayCard_CardID" "PlayCard_Any" "Touch"
    public static void PlayEvent(ulong playerId, string characterId, string eventName)
    {
        SkinData skin = SkinApi.GetSelectedSkin(characterId);
        
        if (skin == null || skin.VoiceEvents == null || !skin.VoiceEvents.ContainsKey(eventName)) return;

        if (!UniversalSettingsManager.IsSkinVoiceEnabled(characterId, skin.SkinId))
        {
            return;
        }

        VoiceEvent voiceEvent = skin.VoiceEvents[eventName];
        if (!voiceEvent.CanPlay()) return;
        
        string selectedLangCode = UniversalSettingsManager.GetSkinLanguage(characterId, skin.SkinId);
        VoiceLine lineToPlay = voiceEvent.GetRandomLine();
        
        if (lineToPlay == null || string.IsNullOrEmpty(lineToPlay.AudioFileName)) return;
        
        string audioPath = ConstructAudioPath(skin.SkinId, selectedLangCode, lineToPlay.AudioFileName);
        
        PlayAudioPath(audioPath);
        ClearSubtitle();

        if (!string.IsNullOrEmpty(lineToPlay.Subtitle))
        {
            ShowSubtitle(playerId, lineToPlay);
        }
    }

    /// <summary>
    /// Constructs the full audio path based on character, ModId, language, and filename.
    /// </summary>
    /// <returns>Full Godot resource path or null if path cannot be constructed.</returns>
    private static string ConstructAudioPath(string skinId, string langCode, string audioFileName)
    {
        if (string.IsNullOrEmpty(skinId) || string.IsNullOrEmpty(langCode) || string.IsNullOrEmpty(audioFileName))
        {
            Log.Warn($"[VoicePlayer] Incomplete data for path construction: Lang='{langCode}', File='{audioFileName}'");
            return null;
        }
        
        string basePath = $"res://Audio/{skinId}/{langCode}/"; 

        string fullPath = Path.Combine(basePath, audioFileName).Replace("\\", "/"); // Ensure forward slashes for Godot paths

        /*
        if (!Godot.FileAccess.FileExists(fullPath))
        {
            Log.Warn($"[VoicePlayer] Expected voice file not found at: {fullPath} (Lang: {langCode}, File: {audioFileName})");
            return null;
        }*/
        
        return fullPath;
    }
    
    /// <summary>
    /// Attempts to find a VoiceLine matching the preferred language, or falls back.
    /// </summary>
    private static VoiceLine GetVoiceLineForLanguage(VoiceEvent voiceEvent, string preferredLangCode, SkinData skin)
    {
        // --- Strategy: Iterate through lines, check if path matches preferred language ---
        // This assumes VoiceLine.AudioFileName contains the base filename (e.g., "Die.ogg")
        // and VoicePlayer reconstructs the full path.
        
        foreach (var line in voiceEvent.Lines)
        {
            if (line.AudioFileName == null) continue;
            return voiceEvent.GetRandomLine();
        }

        return null;
    }

    private static void ShowSubtitle(ulong playerId, VoiceLine line)
    {
        Node2D visualNode = GetCharacterVisualNode(playerId);
        if (visualNode == null) return;

        Node safeParent = GetSafeContainerForSubtitle();
        if (safeParent == null) return;

        _currentSubtitle = new VoiceSubtitleVfx();
        _currentSubtitle.FullText = line.Subtitle; 
        
        safeParent.AddChildSafely(_currentSubtitle);

        Vector2 headGlobalPos = visualNode.GlobalPosition + new Vector2(0f, -250f);
        
        _currentSubtitle.GlobalPosition = headGlobalPos;
        
        _currentSubtitle.Position -= new Vector2(200f, 0f);

        TaskHelper.RunSafely(_currentSubtitle.PlayAnim(line.Duration));
    }
    
    private static Node GetSafeContainerForSubtitle()
    {
        var goScreen = NRun.Instance?.GlobalUi?.Overlays?.GetChildren().OfType<NGameOverScreen>().FirstOrDefault();
        if (goScreen != null) return goScreen;

        if (NCombatRoom.Instance != null && NCombatRoom.Instance.CombatVfxContainer != null)
        {
            return NCombatRoom.Instance.CombatVfxContainer;
        }

        if (NMerchantRoom.Instance != null) return NMerchantRoom.Instance;

        if (NRestSiteRoom.Instance != null) return NRestSiteRoom.Instance;

        return null;
    }
    
    private static Node2D GetCharacterVisualNode(ulong playerId)
    {
        if (NRun.Instance?.GlobalUi?.Overlays?.GetChildren().OfType<NGameOverScreen>().FirstOrDefault() is NGameOverScreen goScreen)
        {
            var container = goScreen.GetNodeOrNull<Control>("%CreatureContainer");
            if (container != null)
            {
                var runState = AccessTools.Field(typeof(NGameOverScreen), "_runState").GetValue(goScreen) as RunState;
                if (runState != null)
                {
                    int playerIndex = runState.Players.ToList().FindIndex(p => p.NetId == playerId);
                    if (playerIndex >= 0 && playerIndex < container.GetChildCount())
                    {
                        var targetNode = container.GetChild(playerIndex);
                        
                        if (targetNode is MegaCrit.Sts2.Core.Nodes.Combat.NCreatureVisuals vis)
                            return VisualHelper.GetBody(vis) ?? vis;
                            
                        if (targetNode is MegaCrit.Sts2.Core.Nodes.Screens.Shops.NMerchantCharacter merchVis)
                            return merchVis;

                        if (targetNode is Node2D n2d) return n2d;
                    }
                }
            }
        }

        // 2. 找战斗房间
        if (NCombatRoom.Instance != null)
        {
            var creatureNode = NCombatRoom.Instance.CreatureNodes.FirstOrDefault(c => c.Entity.Player?.NetId == playerId);
            if (creatureNode != null && creatureNode.Visuals != null)
            {
                return VisualHelper.GetBody(creatureNode.Visuals) ?? creatureNode.Visuals;
            }
        }

        // 3. 找商店房间
        if (NMerchantRoom.Instance != null)
        {
            foreach (var visual in NMerchantRoom.Instance.PlayerVisuals)
            {
                if (visual.HasMeta("UniversalSkinPlayerId") && visual.GetMeta("UniversalSkinPlayerId").AsUInt64() == playerId)
                {
                    return visual;
                }
            }
        }

        // 4. 找休息处
        if (NRestSiteRoom.Instance != null)
        {
            var restChar = NRestSiteRoom.Instance.Characters.FirstOrDefault(c => c.Player?.NetId == playerId);
            if (restChar != null)
            {
                return restChar;
            }
        }

        return null;
    }
    
    // 底层播放逻辑
    private static void PlayAudioPath(string path)
    {
        // 动态加载音频文件
        if (string.IsNullOrEmpty(path))
        {
            MegaCrit.Sts2.Core.Logging.Log.Error($"[皮肤管理器] 无法加载音频文件: {path}");
            return;
        }
        
        AudioStream stream = ResourceLoader.Load<AudioStream>(path);

        // 确保播放器节点存在
        if (_playerNode == null || !GodotObject.IsInstanceValid(_playerNode))
        {
            _playerNode = new AudioStreamPlayer();
            _playerNode.Name = "SkinModVoicePlayer";
            _playerNode.Bus = "Master";
            _playerNode.VolumeLinear = 0.3f;
            
            // 挂载到游戏的全局根节点，保证场景切换声音不断
            NGame.Instance.AddChild(_playerNode);
        }

        _playerNode.Stream = stream;
        _playerNode.Play();
        
        MegaCrit.Sts2.Core.Logging.Log.Info($"[皮肤管理器] 正在播放: {path}");
    }

    // 强行停止当前语音 (例如死亡时打断说话)
    public static void Stop()
    {
        if (_playerNode != null && GodotObject.IsInstanceValid(_playerNode) && _playerNode.Playing)
        {
            _playerNode.Stop();
        }
        ClearSubtitle(); // 语音停止时同步清空字幕
    }
    
    private static void ClearSubtitle()
    {
        if (_currentSubtitle != null && GodotObject.IsInstanceValid(_currentSubtitle))
        {
            _currentSubtitle.QueueFree();
            _currentSubtitle = null;
        }
    }

    
    private static Godot.Timer _afkTimer;
    private static string _currentCharacterId;
    // 每次玩家有操作（打牌、回合开始）时调用此方法，重置计时器
    
    public static void ResetAfkTimer(ulong playerId, string characterId)
    {
        _currentAfkPlayerId = playerId;
        _currentAfkCharacterId = characterId;

        if (_afkTimer == null || !GodotObject.IsInstanceValid(_afkTimer))
        {
            _afkTimer = new Godot.Timer();
            _afkTimer.Name = "SkinModAfkTimer";
            _afkTimer.OneShot = true;     
            _afkTimer.WaitTime = 15.0f;   
            _afkTimer.Timeout += OnAfkTimeout;

            if (NGame.Instance != null && GodotObject.IsInstanceValid(NGame.Instance))
            {
                NGame.Instance.AddChild(_afkTimer);
            }
            else return;
        }
        _afkTimer.Start();
    }

    public static void StopAfkTimer()
    {
        if (_afkTimer != null && GodotObject.IsInstanceValid(_afkTimer))
        {
            _afkTimer.Stop();
        }
    }

    private static void OnAfkTimeout()
    {
        if (CombatManager.Instance != null && CombatManager.Instance.IsInProgress && CombatManager.Instance.IsPlayPhase)
        {
            var state = CombatManager.Instance.DebugOnlyGetState();
            if (state != null)
            {
                var player = state.Players.FirstOrDefault(p => p.NetId == _currentAfkPlayerId);
                if (player != null && player.Creature != null && player.Creature.IsDead)
                {
                    StopAfkTimer();
                    return;
                }
            }

            PlayEvent(_currentAfkPlayerId, _currentAfkCharacterId, "IdleWait");
            _afkTimer.Start(); 
        }
    }
}