using Godot;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Logging;

namespace SkinManagerAndSkinPanelMod;

// 权重语音条目
public class VoiceLine
{
    public string AudioPath; // "res://audio/voice/attack_1.ogg"
    public int Weight = 1;   // 权重
    public string Subtitle;    // ★新增：语音对应的台词字幕
    public float Duration = 3f; // ★新增：字幕在屏幕上停留的时间（秒）
}

// 语音事件组 (例如 "EnterCombat", "PlayCard_Strike" 等)
public class VoiceEvent
{
    public List<VoiceLine> Lines = new List<VoiceLine>();
    public float Probability = 1.0f; // 触发概率 (0.0 到 1.0)
    public float Cooldown = 0f;      // 冷却时间 (秒)
    
    private ulong _lastPlayTimeMsec = 0; // 内部记录上次播放时间
    
    // 工具方法：判断是否可以播放
    public bool CanPlay()
    {
        //Log.Info("检查是否可播放");
        // 1. 检查概率
        if (Probability < 1.0f && GD.Randf() > Probability) return false;

        // 2. 检查冷却
        if (Cooldown > 0)
        {
            ulong currentTime = Time.GetTicksMsec();
            if ((currentTime - _lastPlayTimeMsec) < (ulong)(Cooldown * 1000)) return false;
            _lastPlayTimeMsec = currentTime; // 记录本次播放时间
        }
        //Log.Info("可以播放");
        return true;
    }

    // 权重随机选择语音
    public VoiceLine GetRandomLine()
    {
        if (Lines.Count == 0) return null;
        if (Lines.Count == 1) return Lines[0];

        int totalWeight = Lines.Sum(l => l.Weight);
        int rand = (int)(GD.Randi() % totalWeight);
        
        foreach (var line in Lines)
        {
            if (rand < line.Weight) return line;
            rand -= line.Weight;
        }
        return Lines.Last();
    }
}