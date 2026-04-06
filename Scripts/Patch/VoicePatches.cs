using HarmonyLib;
using Godot;
using System.Linq;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using SkinManagerAndSkinPanelMod.Scripts.Data;

namespace SkinManagerAndSkinPanelMod;

[HarmonyPatch]
public static class VoicePatches
{
    [HarmonyPatch(typeof(SfxCmd), nameof(SfxCmd.PlayDeath), new[] { typeof(Player) })]
    [HarmonyPostfix]
    public static bool Prefix(Player player)
    {
        if (player == null || player.Character == null) return true;
            
        string charId = player.Character.Id.Entry;
        SkinData skin = SkinApi.GetSelectedSkin(charId);
            
        if (player.Character.Id.Entry == "REGENT" && skin != null)
        {
            return false; 
        }
        
        return true; 
    }
    
    [HarmonyPatch(typeof(CombatManager), nameof(CombatManager.StartCombatInternal))]
    [HarmonyPostfix]
    public static void OnCombatStarted(CombatManager __instance)
    {
        var state = __instance.DebugOnlyGetState();
        if (state != null)
        {
            // 🌟 遍历所有活着的玩家，都可以播语音（但由于框架的防重复或几率机制，你可以做限制）
            
            int rand = (int)(GD.Randi() % state.Players.Count);
            var localPlayer = state.Players[rand];
        
            if (localPlayer != null && localPlayer.Creature.IsAlive)
            {
                VoicePlayer.PlayEvent(localPlayer.NetId, localPlayer.Character.Id.Entry, "EnterCombat");
            }
        }
    }

    [HarmonyPatch(typeof(Creature), nameof(Creature.InvokeDiedEvent))]
    [HarmonyPrefix]
    public static void OnCreatureDied(Creature __instance)
    {
        if (__instance.IsPlayer)
        {
            // VoicePlayer.Stop(); 
            // 🌟 传入 NetId
            // VoicePlayer.PlayEvent(__instance.Player.NetId, __instance.Player.Character.Id.Entry, "Die");
        }
        else if (__instance.IsEnemy || __instance.IsMonster)
        {
            var combatState = __instance.CombatState;
            if (combatState != null)
            {
                // 这里的击杀逻辑比较粗糙（只找了第一个玩家）。
                // 如果你想做到谁打死的怪谁说话，需要利用 ActionQueue 里的 dealer。
                var localPlayer = combatState.Players.FirstOrDefault();
                if (localPlayer != null)
                {
                    if (__instance.IsPrimaryEnemy && __instance.Monster != null && __instance.Monster.Id.Entry.ToLower().Contains("boss"))
                        VoicePlayer.PlayEvent(localPlayer.NetId, localPlayer.Character.Id.Entry, "KillBoss");
                    else
                        VoicePlayer.PlayEvent(localPlayer.NetId, localPlayer.Character.Id.Entry, "KillEnemy");
                }
            }
        }
    }

    [HarmonyPatch(typeof(CombatManager), "SetupPlayerTurn")]
    [HarmonyPostfix]
    public static void OnPlayerTurnStart(MegaCrit.Sts2.Core.Entities.Players.Player player)
    {
        var state = CombatManager.Instance.DebugOnlyGetState();
        if (state != null)
        {
            if (player.Creature != null && !player.Creature.IsDead)
            {
                // 🌟 传入 NetId
                VoicePlayer.ResetAfkTimer(player.NetId, player.Character.Id.Entry);
                VoicePlayer.PlayEvent(player.NetId, player.Character.Id.Entry, "TurnStart");
                return;
            }
        }
    }

    [HarmonyPatch(typeof(CombatManager), nameof(CombatManager.EndPlayerTurnPhaseOneInternal))]
    [HarmonyPrefix]
    public static void OnPlayerTurnEnded()
    {
        VoicePlayer.StopAfkTimer();
    }
    
    [HarmonyPatch(typeof(CardModel), nameof(CardModel.OnPlayWrapper))]
    [HarmonyPrefix]
    public static void OnCardPlayed(CardModel __instance) 
    {
        if (__instance.Owner == null || __instance.Owner.Character == null) return;

        string charId = __instance.Owner.Character.Id.Entry;
        ulong playerId = __instance.Owner.NetId; // 🌟 获取玩家 ID
        
        string cardEventName = "PlayCard_" + __instance.Id.Entry;

        VoicePlayer.ResetAfkTimer(playerId, charId);

        SkinData skin = SkinApi.GetSelectedSkin(charId);
        if (skin != null && skin.VoiceEvents != null)
        {
            if (skin.VoiceEvents.ContainsKey(cardEventName))
                VoicePlayer.PlayEvent(playerId, charId, cardEventName);
            else
                VoicePlayer.PlayEvent(playerId, charId, "PlayCard_Any");
        }
    }
    

    [HarmonyPatch(typeof(CombatManager), nameof(CombatManager.EndCombatInternal))]
    [HarmonyPrefix]
    public static void OnCombatEnded(CombatManager __instance)
    {
        var state = __instance.DebugOnlyGetState();
        if (state == null) return;

        var localPlayer = state.Players.FirstOrDefault();
        if (localPlayer != null && !localPlayer.Creature.IsDead)
        {
            string charId = localPlayer.Character.Id.Entry;
            ulong playerId = localPlayer.NetId;
            
            var room = state.RunState?.CurrentRoom as MegaCrit.Sts2.Core.Rooms.CombatRoom;
            bool isBoss = room != null && room.RoomType == MegaCrit.Sts2.Core.Rooms.RoomType.Boss;

            VoicePlayer.Stop();

            if (isBoss) VoicePlayer.PlayEvent(playerId, charId, "BossVictory");
            else VoicePlayer.PlayEvent(playerId, charId, "CombatVictory");
        }
    }
}