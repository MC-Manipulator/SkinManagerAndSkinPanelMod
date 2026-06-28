using System.Diagnostics;
using Godot;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Logging;
using SkinManagerAndSkinPanelMod.Scripts.Data;

namespace SkinManagerAndSkinPanelMod;

public partial class UniversalSkinPanel : Control
{
    private Label _skinNameLabel;
    private Node2D _currentSpineNode;
    private TextureButton _leftArrow;
    private TextureButton _rightArrow;
    
    private CheckBox _voiceCheckBox; 
    
    public string CurrentCharacterId { get; private set; } // 当前选中的角色
    private Vector2 spineNodePosition;
    private OptionButton _languageSelector; // Use OptionButton for dropdown

    // ================= 🌟 拖动逻辑所需的变量 🌟 =================
    private bool _isDragging = false;       // 记录当前是否正在被拖动
    private Vector2 _dragStartMousePos;     // 鼠标刚按下时的屏幕坐标
    private Vector2 _dragStartPanelPos;     // 鼠标刚按下时面板的坐标
    // ==========================================================
    
    
    public override void _Ready()
    {
        _leftArrow = GetNode<TextureButton>("Visuals/HBoxContainer/LeftArrow");
        _rightArrow = GetNode<TextureButton>("Visuals/HBoxContainer/RightArrow");
        _skinNameLabel = GetNode<Label>("Visuals/HBoxContainer/SkinNameLabel");
        _currentSpineNode = GetNode<Node2D>("Visuals/SpineSprite");
        spineNodePosition =  _currentSpineNode.Position;
        
        _voiceCheckBox = GetNode<CheckBox>("Visuals/VoiceSettingsContainer/CheckBox"); 
        
        _languageSelector = GetNode<OptionButton>("Visuals/LanguageSettingsContainer/LanguageOptionButton"); // Adjust path as needed
        _languageSelector.Clear(); // Clear existing options if any
        
        _leftArrow.Pressed += OnLeftClicked;
        _rightArrow.Pressed += OnRightClicked;
        
        _voiceCheckBox.Toggled += OnVoiceToggled;
        _languageSelector.ItemSelected += OnLanguageSelected; // Connect signal for language change

        // 🌟 必须设置鼠标过滤器，让面板能够接收鼠标事件！
        // 如果设置为 Ignore，鼠标事件会穿透面板，拖动逻辑将无法触发。
        this.MouseFilter = MouseFilterEnum.Stop;
    }
    
    public override void _GuiInput(InputEvent @event)
    {
        // 1. 处理鼠标按键事件 (按下与松开)
        if (@event is InputEventMouseButton mouseButton)
        {
            // 只响应鼠标左键
            if (mouseButton.ButtonIndex == MouseButton.Left)
            {
                if (mouseButton.Pressed)
                {
                    // 鼠标按下，开始拖动！
                    _isDragging = true;
                    // 记录鼠标按下时的屏幕坐标
                    _dragStartMousePos = mouseButton.GlobalPosition;
                    // 记录此时面板的坐标
                    _dragStartPanelPos = this.Position;
                }
                else
                {
                    // 鼠标松开，停止拖动
                    _isDragging = false;
                    UniversalSettingsManager.SavePanelPosition(this.Position);
                }
            }
        }
        
        // 2. 处理鼠标移动事件 (拖动过程)
        else if (@event is InputEventMouseMotion mouseMotion)
        {
            if (_isDragging)
            {
                // 计算鼠标相对于按下时移动了多少距离
                Vector2 dragOffset = mouseMotion.GlobalPosition - _dragStartMousePos;
                
                // 将面板的位置设置为初始位置加上偏移量
                this.Position = _dragStartPanelPos + dragOffset;
            }
        }
    }
    
    // --- !!! NEW: Language Selected Handler !!! ---
    private void OnLanguageSelected(long index) // index is the selected item's index
    {
        if (string.IsNullOrEmpty(CurrentCharacterId) || index < 0) return;

        // Get the language code associated with the selected index
        string selectedLangCode = _languageSelector.GetItemMetadata(Convert.ToInt32(index)).ToString();
        if (string.IsNullOrEmpty(selectedLangCode)) return;

        SkinData currentSkin = SkinApi.GetSelectedSkin(CurrentCharacterId);
        if (currentSkin == null) return;

        // !!! Save the selected language for this specific skin !!!
        UniversalSettingsManager.SetSkinLanguage(CurrentCharacterId, currentSkin.SkinId, selectedLangCode);
        
        // Play UI sound
        SfxCmd.Play("event:/sfx/ui/clicks/ui_select"); 
        
        // Optional: Immediately reload current skin's voice if needed, though usually VoicePlayer handles it dynamically.
        // For now, just saving the setting is enough.
    }

    // --- 语音开关勾选框的事件处理 ---
    private void OnVoiceToggled(bool pressed)
    {
        if (string.IsNullOrEmpty(CurrentCharacterId)) return;
        
        SkinData currentSkin = SkinApi.GetSelectedSkin(CurrentCharacterId);
        if (currentSkin == null) return;
        
        // 调用框架的通用设置管理器来更新该角色的语音状态
        UniversalSettingsManager.SetSkinVoiceEnabled(CurrentCharacterId, currentSkin.SkinId, pressed);

        
        // 播放UI音效，指示开关状态
        SfxCmd.Play(pressed ? "event:/sfx/ui/clicks/ui_checkbox_on" : "event:/sfx/ui/clicks/ui_checkbox_off");
    }
    
    private void OnLeftClicked()
    {
        if (string.IsNullOrEmpty(CurrentCharacterId)) return;
        SkinApi.PrevSkin(CurrentCharacterId);
        RefreshPanel(CurrentCharacterId, updateBackground: true);
    }

    private void OnRightClicked()
    {
        if (string.IsNullOrEmpty(CurrentCharacterId)) return;
        SkinApi.NextSkin(CurrentCharacterId);
        RefreshPanel(CurrentCharacterId, updateBackground: true);
    }

    public void RefreshPanel(string characterId, bool updateBackground = true)
    {
        CurrentCharacterId = characterId;
        
        if (!SkinApi.HasSkins(characterId)) 
        {
            this.Visible = false; 
            return;
        }
        this.Visible = true; 

        SkinData currentSkin = SkinApi.GetSelectedSkin(characterId);
        if (currentSkin == null)
        {
            Log.Info($"[皮肤管理器] 未能加载当前角色的皮肤:{characterId}");
            return;
        }

        _skinNameLabel.Text = currentSkin.SkinName;
        
        bool hasVoiceEvents = currentSkin.VoiceEvents != null && currentSkin.VoiceEvents.Count > 0;
        _voiceCheckBox.Visible = hasVoiceEvents;

        if (hasVoiceEvents)
        {
            _voiceCheckBox.ButtonPressed = UniversalSettingsManager.IsSkinVoiceEnabled(characterId, currentSkin.SkinId);
        }
        
        // --- Update Language Selector ---
        PopulateLanguageSelectorForSkin(currentSkin);
        // Set the correct language based on saved settings
        string currentLangCode = UniversalSettingsManager.GetSkinLanguage(characterId, currentSkin.SkinId);
        SetLanguageSelectorSelection(currentLangCode, characterId, currentSkin.SkinId);
        
        Resource loadedSpineData = null;
        if (!string.IsNullOrEmpty(currentSkin.CombatSpineDataPath))
        {
            loadedSpineData = ResourceLoader.Load<Resource>(currentSkin.CombatSpineDataPath);
        }
        if (loadedSpineData != null)
        {
            MegaSprite previewSpine = new MegaSprite(_currentSpineNode);
            previewSpine.SetSkeletonDataRes(new MegaSkeletonDataResource(loadedSpineData));
            _currentSpineNode.Position = spineNodePosition + currentSkin.MenuOffset;
            MegaAnimationState animationState = previewSpine.GetAnimationState();
            if (currentSkin.CombatAnimMap != null && currentSkin.CombatAnimMap.ContainsKey("Idle"))
            {
                animationState.AddAnimation(currentSkin.CombatAnimMap["Idle"], 0f, true);
            }
            else
            {
                Log.Error($"[皮肤管理器] '{characterId}'角色皮肤'{currentSkin.SkinId}'未能加载Idle动画.");
            }
        }
        else 
        {
            Log.Error($"[皮肤管理器] 未能加载角色spine资源: {currentSkin.CombatSpineDataPath}");
        }
        
        if (updateBackground)
        {
            UniversalUIPatches.UpdateBackgroundToCurrentSkin(characterId);
            UniversalUIPatches.UpdateSelectButtonIconToCurrentSkin(characterId);
        }
    }
    
    
    // !!! NEW: Helper to populate the language dropdown based on skin support !!!
    private void PopulateLanguageSelectorForSkin(SkinData skin)
    {
        _languageSelector.Clear(); // Clear previous entries

        // Determine which languages are supported by this skin
        List<string> skinSupportedLangs = skin.SupportedLanguages;
        
        // If skin doesn't explicitly list supported languages, assume all are potentially supported
        // The actual check happens in VoicePlayer when trying to load the file.
        if (skinSupportedLangs == null || skinSupportedLangs.Count == 0)
        {
            skinSupportedLangs = UniversalSettingsManager.AvailableLanguages.Keys.ToList();
        }

        int currentLangIndex = 0;
        // Populate with available languages
        foreach (var kvp in UniversalSettingsManager.AvailableLanguages)
        {
            string langCode = kvp.Key;
            string langName = kvp.Value;

            // Only add if the skin supports this language OR if the skin doesn't specify, we add all available.
            if (skinSupportedLangs.Contains(langCode)) 
            {
                _languageSelector.AddItem(langName);
                _languageSelector.SetItemMetadata(currentLangIndex++, langCode);

            }
        }
        
        // Ensure there's at least one option if list ended up empty (shouldn't happen with default logic)
        if (_languageSelector.ItemCount == 0)
        {
            _languageSelector.AddItem("中文"); // Fallback
            _languageSelector.SetItemMetadata(0, "zh"); 
        }
    }

    // !!! NEW: Helper to set the correct language in the dropdown !!!
    private void SetLanguageSelectorSelection(string langCode, string characterId, string skinId)
    {
        for (int i = 0; i < _languageSelector.ItemCount; i++)
        {
            string itemLangCode = _languageSelector.GetItemMetadata(i).ToString();
            if (itemLangCode == langCode)
            {
                _languageSelector.Select(i);
                return;
            }
        }
        
        // ================= 🌟 核心修复 🌟 =================
        // 如果存档里存的语言（或者 fallback 的语言）不在这个皮肤支持的列表里
        // 我们强制选中下拉框里的第 0 项（这个皮肤实际支持的第一个语言）
        if (_languageSelector.ItemCount > 0)
        {
            _languageSelector.Select(0); 
            
            // 并且！必须主动通知 SettingsManager 保存这个真正的语言！
            string actualLangCode = _languageSelector.GetItemMetadata(0).ToString();
            UniversalSettingsManager.SetSkinLanguage(characterId, skinId, actualLangCode);
        }
        // ==================================================
    }
}