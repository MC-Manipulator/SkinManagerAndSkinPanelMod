using Godot;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Helpers; // 用于 TaskHelper

namespace SkinManagerAndSkinPanelMod;

public partial class VoiceSubtitleVfx : Label
{
    private Tween _fadeTween;
    private Tween _typewriterTween;
    private float _floatOffset = 0f; // 当前上浮的距离

    public string FullText = "";
    
    // 初始化字幕样式
    public override void _Ready()
    {
        // 1. 设置基础样式
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        
        // 2. 开启自适应换行，防止句子太长超出屏幕
        AutowrapMode = TextServer.AutowrapMode.Word;
        CustomMinimumSize = new Vector2(400f, 0f); // 限制最大宽度
        
        // 3. 设置极其优雅的文本视觉效果
        AddThemeColorOverride("font_color", new Color(1f, 1f, 1f, 1f)); // 纯白字
        AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.8f)); // 半透明黑描边
        AddThemeConstantOverride("outline_size", 4); // 描边厚度
        
        // 加一点阴影，让它在任何背景下都能看清
        AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.5f));
        AddThemeConstantOverride("shadow_offset_x", 2);
        AddThemeConstantOverride("shadow_offset_y", 2);

        // 默认透明，准备淡入
        Modulate = new Color(1, 1, 1, 0);
        // 🌟 初始化打字机设置
        Text = FullText;
        VisibleCharacters = 0; // 初始时一个字都不显示
    }
    
    public override void _Process(double delta)
    {
        // 每一帧都把 _floatOffset 应用到 Position 上
        // 这样即使外部代码强行改了 GlobalPosition (比如角色移动)，上浮效果依然是相对叠加的
        Position = new Vector2(Position.X, Position.Y - _floatOffset * (float)delta);
    }

    public async Task PlayAnim(float duration)
    {
        if (!IsInsideTree()) return;

        // --- 阶段 1：整体淡入 (框体) ---
        if (_fadeTween != null && _fadeTween.IsValid()) _fadeTween.Kill();
        _fadeTween = CreateTween();
        _fadeTween.TweenProperty(this, "modulate:a", 1f, 0.2f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        
        _floatOffset = 5f; // 微弱的上浮速度
        
        // --- 阶段 2：逐字跳出 (Typewriter Effect) ---
        if (_typewriterTween != null && _typewriterTween.IsValid()) _typewriterTween.Kill();
        _typewriterTween = CreateTween();
        
        // 计算每个字出现的速度 (假设每秒 15 个字，最多不超过总时长的一半)
        int totalChars = FullText.Length;
        float typeDuration = Mathf.Min(totalChars * 0.05f, duration * 0.5f);
        
        // 让 VisibleCharacters 从 0 匀速增加到总字数
        _typewriterTween.TweenProperty(this, "visible_characters", totalChars, typeDuration)
            .SetTrans(Tween.TransitionType.Linear);
                  
        // 等待语音总时长结束
        await Task.Delay((int)(duration * 1000));
        if (!IsInsideTree() || !IsInstanceValid(this)) return;

        // --- 阶段 3：缓缓淡出消失 ---
        _fadeTween = CreateTween();
        _fadeTween.TweenProperty(this, "modulate:a", 0f, 0.5f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
                  
        await ToSignal(_fadeTween, Tween.SignalName.Finished);
        QueueFree();
    }
}