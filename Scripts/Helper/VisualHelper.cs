using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace SkinManagerAndSkinPanelMod.Scripts.Helper;

public static class VisualHelper
{
    public static void SetShadow(Node2D body, Vector2 offset, Vector2 scale)
    {
        if (body.HasNode("Shadow")) return;
        
        PackedScene shadow = GD.Load<PackedScene>("res://Scene/Shadow/Shadow_Combat.tscn");
        if (shadow != null)
        {
            Node2D node = shadow.Instantiate<Node2D>();

            body.AddChild(node);
            body.MoveChild(node, 0);
            node.Name = "Shadow";
            node.Position += offset;
            node.Scale = scale;
        }
    }
    
    public static MegaSprite ReplaceSpine(Node2D body, Resource loadedSpineData)
    {
        MegaSprite spineController = new MegaSprite(body);
        spineController.SetSkeletonDataRes(new MegaSkeletonDataResource(loadedSpineData));

        return spineController;
    }
    
    public static Node2D GetBody(NCreatureVisuals visuals)
    {
        // 兼容0.99.1版本与0.100.0版本的恐惧症更新
        var traverse = Traverse.Create(visuals);
        
        // 0.99.1版本
        var bodyProp = traverse.Property("Body");
        if (bodyProp.PropertyExists()) return bodyProp.GetValue<Node2D>();
        
        // 0.100.0版本
        var bodyField = traverse.Field("_body");
        if (bodyField.FieldExists()) return bodyField.GetValue<Node2D>();
        
        return null; 
    }
}