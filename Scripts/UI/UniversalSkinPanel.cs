using Godot;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Logging;

namespace SkinManagerAndSkinPanelMod;

public partial class UniversalSkinPanel : Control
{
    private Label _skinNameLabel;
    private Node2D _currentSpineNode;
    public string CurrentCharacterId { get; private set; } // 当前选中的角色
    private Vector2 spineNodePosition;
    
    
    public override void _Ready()
    {
        var leftArrow = GetNode<TextureButton>("Visuals/HBoxContainer/LeftArrow");
        var rightArrow = GetNode<TextureButton>("Visuals/HBoxContainer/RightArrow");
        _skinNameLabel = GetNode<Label>("Visuals/HBoxContainer/SkinNameLabel");
        _currentSpineNode = GetNode<Node2D>("Visuals/SpineSprite");
        spineNodePosition =  _currentSpineNode.Position;
        
        leftArrow.Pressed += OnLeftClicked;
        rightArrow.Pressed += OnRightClicked;
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
        if (!SkinApi.HasSkins(characterId)) return;
        
        SkinData currentSkin = SkinApi.GetSelectedSkin(characterId);
        _skinNameLabel.Text = currentSkin.SkinName;
        
        // 🌟 实时加载骨骼数据
        Resource loadedSpineData = null;
        if (!string.IsNullOrEmpty(currentSkin.CombatSpineDataPath))
        {
            Log.Info(currentSkin.CombatSpineDataPath);
            Log.Info("加载spine模型");
            loadedSpineData = GD.Load<Resource>(currentSkin.CombatSpineDataPath);
        }
        
        if (loadedSpineData != null)
        {
            MegaSprite previewSpine = new MegaSprite(_currentSpineNode);
            previewSpine.SetSkeletonDataRes(new MegaSkeletonDataResource(loadedSpineData));

            _currentSpineNode.Position = spineNodePosition + currentSkin.MenuOffset;
            
            MegaAnimationState animationState = previewSpine.GetAnimationState();
            MegaTrackEntry track = animationState.AddAnimation(currentSkin.CombatAnimMap["Idle"], 0f, true);
        }
        else
        {
            Log.Error($"无法加载骨骼数据，请检查路径: {currentSkin.CombatSpineDataPath}");
        }
        
        if (updateBackground)
        {
            UniversalUIPatches.UpdateBackgroundToCurrentSkin(characterId);
        }
    }
}