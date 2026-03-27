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
using MegaCrit.Sts2.Core.Runs; // NGame

namespace SkinManagerAndSkinPanelMod;

public static class VoicePlayer
{
    private static AudioStreamPlayer _playerNode;

    // 🌟 新增：全局唯一的字幕容器和当前正在播的字幕实例
    private static CanvasLayer _subtitleLayer;
    private static VoiceSubtitleVfx _currentSubtitle;
    //"EnterCombat" "Die" "KillBoss" "KillEnemy" "TurnStart" "PlayCard_CardID" "PlayCard_Any" "Touch"
    // 播放指定角色、指定事件的语音
    
    // 🌟 升级：记录正在 AFK 计时的 玩家ID 和 对应的 角色ID
    private static ulong _currentAfkPlayerId = 0;
    private static string _currentAfkCharacterId = "";
    
    // ================= 🌟 核心播放入口 🌟 =================
    // 参数变为 playerId 和 characterId
    public static void PlayEvent(ulong playerId, string characterId, string eventName)
    {
        SkinData skin = SkinApi.GetSelectedSkin(characterId);
        if (skin == null || skin.VoiceEvents == null || !skin.VoiceEvents.ContainsKey(eventName)) return;

        VoiceEvent voiceEvent = skin.VoiceEvents[eventName];
        if (!voiceEvent.CanPlay()) return;

        var line = voiceEvent.GetRandomLine();
        string audioPath = line.AudioPath;
        if (string.IsNullOrEmpty(audioPath)) return;

        PlayAudioPath(audioPath);
        ClearSubtitle();

        if (!string.IsNullOrEmpty(line.Subtitle))
        {
            // 传入 playerId 精确定位
            ShowSubtitle(playerId, line);
        }
    }


// 🌟 核心升级：解耦挂载节点与坐标参照节点
    private static void ShowSubtitle(ulong playerId, VoiceLine line)
    {
        // 1. 寻找该角色的视觉节点 (只用来获取坐标)
        Node2D visualNode = GetCharacterVisualNode(playerId);
        if (visualNode == null) return;

        // 2. 寻找绝对安全的挂载父节点！(解耦！)
        Node safeParent = GetSafeContainerForSubtitle();
        if (safeParent == null) return;

        _currentSubtitle = new VoiceSubtitleVfx();
        _currentSubtitle.FullText = line.Subtitle; 
        
        // 挂载到安全的容器中 (这通常是当前房间的特效层，它会被游戏整体安全销毁)
        safeParent.AddChildSafely(_currentSubtitle);

        // 如果你希望特效在它自己那个容器的最上层：
        // safeParent.MoveChild(_currentSubtitle, -1);

        // 3. 将字幕设置在角色的头顶
        // 关键点：不管挂载在哪，GlobalPosition 是统一的世界坐标！
        // 找到角色 GlobalPosition，减去 250f 即为头顶
        Vector2 headGlobalPos = visualNode.GlobalPosition + new Vector2(0f, -250f);
        
        // 设置字幕的世界坐标
        _currentSubtitle.GlobalPosition = headGlobalPos;
        
        // 居中偏移 (补偿宽度)
        _currentSubtitle.Position -= new Vector2(200f, 0f);

        TaskHelper.RunSafely(_currentSubtitle.PlayAnim(line.Duration));
    }
    
    // 🌟 寻找安全的挂载容器 (专为字幕避难而生)
    private static Node GetSafeContainerForSubtitle()
    {
        // 优先挂载在结算界面本身
        var goScreen = NRun.Instance?.GlobalUi?.Overlays?.GetChildren().OfType<NGameOverScreen>().FirstOrDefault();
        if (goScreen != null) return goScreen;

        // 战斗房：挂在专门用来放伤害数字和火花的容器，极其安全
        if (NCombatRoom.Instance != null && NCombatRoom.Instance.CombatVfxContainer != null)
        {
            return NCombatRoom.Instance.CombatVfxContainer;
        }

        // 商店：挂在商店房间根节点
        if (NMerchantRoom.Instance != null) return NMerchantRoom.Instance;

        // 休息处：挂在休息处房间根节点
        if (NRestSiteRoom.Instance != null) return NRestSiteRoom.Instance;

        return null;
    }
    
    // 🌟 仅用于提供坐标的“雷达” (不再作为父节点)
    private static Node2D GetCharacterVisualNode(ulong playerId)
    {
        // 1. 找结算界面 (GameOverScreen)
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
                        
                        // 由于 GameOver 里的节点五花八门，我们尽量挖出真正的中心节点
                        if (targetNode is MegaCrit.Sts2.Core.Nodes.Combat.NCreatureVisuals vis)
                            return UniversalScenePatches.GetBody(vis) ?? vis;
                            
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
                return UniversalScenePatches.GetBody(creatureNode.Visuals) ?? creatureNode.Visuals;
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
    
    // ================= 🌟 待机计时器改造 🌟 =================
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
                // 🌟 精准检查当前计时的玩家是否存活
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