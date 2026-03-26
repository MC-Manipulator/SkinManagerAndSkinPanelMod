using Godot;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.GameOverScreen;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops; // NGame

namespace SkinManagerAndSkinPanelMod;

public static class VoicePlayer
{
    private static AudioStreamPlayer _playerNode;

    // 🌟 新增：全局唯一的字幕容器和当前正在播的字幕实例
    private static CanvasLayer _subtitleLayer;
    private static VoiceSubtitleVfx _currentSubtitle;
    //"EnterCombat" "Die" "KillBoss" "KillEnemy" "TurnStart" "PlayCard_CardID" "PlayCard_Any" "Touch"
    // 播放指定角色、指定事件的语音
    public static void PlayEvent(string characterId, string eventName)
    {
        Log.Info("触发语音事件" +  eventName);
        SkinData skin = SkinApi.GetSelectedSkin(characterId);
        
        if (skin == null || !skin.VoiceEvents.ContainsKey(eventName)) return;

        VoiceEvent voiceEvent = skin.VoiceEvents[eventName];
        
        // 检查概率和冷却
        if (!voiceEvent.CanPlay()) return;

        var line = voiceEvent.GetRandomLine();
        string audioPath = line.AudioPath;
        Log.Info("语音路径" +  audioPath);
        if (string.IsNullOrEmpty(audioPath)) return;

        PlayAudioPath(audioPath);
        // 🌟 修复 3：打断上一句字幕
        if (!string.IsNullOrEmpty(line.Subtitle))
        {
            ShowSubtitle(characterId, line);
        }
        else
        {
            // 如果新语音没有字幕，清空屏幕上的旧字幕
            ClearSubtitle();
        }
    }

    // 🌟 核心升级：动态寻找合适的父节点挂载字幕
    private static void ShowSubtitle(string characterId, VoiceLine line)
    {
        // 尝试获取角色的视觉根节点 (NCreatureVisuals 或 NMerchantCharacter 等)
        Node parentNode = GetCharacterVisualNode(characterId, out Vector2 localOffset);
        if (parentNode == null) return;

        _currentSubtitle = new VoiceSubtitleVfx();
        _currentSubtitle.FullText = line.Subtitle; 
        
        // ★关键：直接把字幕挂在角色视觉节点内部
        // 这样不管这个角色节点被移到哪个界面（Reparent），字幕都会跟着走，且层级与角色平齐！
        parentNode.AddChildSafely(_currentSubtitle);
        
        // 保证字幕在角色节点内部处于最上层（盖住角色的头，但不盖住外部的系统 UI）
        parentNode.MoveChild(_currentSubtitle, -1);

        // 设置在头顶
        // 注意：因为现在是作为子节点挂载，坐标直接写局部偏移即可，不用再加 GlobalPosition！
        Vector2 headPosition = localOffset + new Vector2(0f, -250f);
        _currentSubtitle.Position = headPosition;
        
        // 居中偏移 (补偿宽度)
        _currentSubtitle.Position -= new Vector2(200f, 0f);

        TaskHelper.RunSafely(_currentSubtitle.PlayAnim(line.Duration));
    }
    
    // 🌟 定位角色视觉节点，并返回它，用于直接作为字幕的父节点
    private static Node GetCharacterVisualNode(string characterId, out Vector2 localOffset)
    {
        localOffset = Vector2.Zero;

        // 1. 优先找结算界面 (GameOverScreen) 里的角色节点
        if (NRun.Instance?.GlobalUi?.Overlays?.GetChildren().OfType<NGameOverScreen>().FirstOrDefault() is NGameOverScreen goScreen)
        {
            var container = goScreen.GetNodeOrNull<Control>("%CreatureContainer");
            if (container != null)
            {
                foreach (Node child in container.GetChildren())
                {
                    // 在 GameOver 里，我们要么找到 NCreatureVisuals，要么找到 NMerchantCharacter
                    if (child is NCreatureVisuals vis)
                    {
                        return vis; // 直接返回这个视觉节点
                    }
                    if (child is MegaCrit.Sts2.Core.Nodes.Screens.Shops.NMerchantCharacter merchVis)
                    {
                        if (merchVis.HasMeta("UniversalSkinCharId") && merchVis.GetMeta("UniversalSkinCharId").AsString() == characterId)
                        {
                            return merchVis;
                        }
                    }
                }
            }
        }

        // 2. 找战斗房间
        if (NCombatRoom.Instance != null)
        {
            var creatureNode = NCombatRoom.Instance.CreatureNodes.FirstOrDefault(c => c.Entity.Player?.Character.Id.Entry == characterId);
            if (creatureNode != null && creatureNode.Visuals != null)
            {
                return creatureNode.Visuals; // 返回 NCreatureVisuals
            }
        }

        // 3. 找商店房间
        if (NMerchantRoom.Instance != null)
        {
            foreach (var visual in NMerchantRoom.Instance.PlayerVisuals)
            {
                if (visual.HasMeta("UniversalSkinCharId") && visual.GetMeta("UniversalSkinCharId").AsString() == characterId)
                {
                    return visual; // 返回 NMerchantCharacter
                }
            }
        }

        // 4. 找休息处
        if (NRestSiteRoom.Instance != null)
        {
            var restChar = NRestSiteRoom.Instance.Characters.FirstOrDefault(c => c.Player?.Character.Id.Entry == characterId);
            if (restChar != null)
            {
                return restChar; // 返回 NRestSiteCharacter
            }
        }

        return null;
    }
    
    // 底层播放逻辑
    private static void PlayAudioPath(string path)
    {
        // 动态加载音频文件
        AudioStream stream = GD.Load<AudioStream>(path);
        if (stream == null)
        {
            MegaCrit.Sts2.Core.Logging.Log.Error($"[语音系统] 无法加载音频文件: {path}");
            return;
        }

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
        
        MegaCrit.Sts2.Core.Logging.Log.Info($"[语音系统] 正在播放: {path}");
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
    
    public static void ResetAfkTimer(string characterId)
    {
        _currentCharacterId = characterId;

        // 如果没有初始化 Timer，就挂载一个到全局
        if (_afkTimer == null || !GodotObject.IsInstanceValid(_afkTimer))
        {
            _afkTimer = new Godot.Timer();
            _afkTimer.Name = "SkinModAfkTimer";
            _afkTimer.OneShot = true;     // 不循环，等下次重置
            _afkTimer.WaitTime = 15.0f;   // 默认 15 秒没操作视为待机
            
            _afkTimer.Timeout += OnAfkTimeout;

            if (NGame.Instance != null && GodotObject.IsInstanceValid(NGame.Instance))
            {
                NGame.Instance.AddChild(_afkTimer);
            }
            else return;
        }

        // 重新开始计时
        _afkTimer.Start();
    }

    // 当玩家回合结束、或者离开战斗时调用此方法，停止计时
    public static void StopAfkTimer()
    {
        if (_afkTimer != null && GodotObject.IsInstanceValid(_afkTimer))
        {
            _afkTimer.Stop();
        }
    }

    private static void OnAfkTimeout()
    {
        // 检查战斗状态
        if (CombatManager.Instance != null && 
            CombatManager.Instance.IsInProgress && 
            CombatManager.Instance.IsPlayPhase)
        {
            // 🌟 修复 1：检查角色是否还活着
            var state = CombatManager.Instance.DebugOnlyGetState();
            if (state != null)
            {
                var player = state.Players.FirstOrDefault(p => p.Character.Id.Entry == _currentCharacterId);
                if (player != null && player.Creature != null && player.Creature.IsDead)
                {
                    // 如果角色死了，直接停止计时，不再播待机语音
                    StopAfkTimer();
                    return;
                }
            }

            PlayEvent(_currentCharacterId, "IdleWait");
            _afkTimer.Start(); // 播完重置
        }
    }
}