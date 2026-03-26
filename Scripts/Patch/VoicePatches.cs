using HarmonyLib;
using Godot;
using System.Linq;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace SkinManagerAndSkinPanelMod;

[HarmonyPatch]
public static class VoicePatches
{
    // ==========================================
    // 1. 进战斗触发 (EnterCombat)
    // 拦截 CombatManager.StartCombatInternal
    // ==========================================
    [HarmonyPatch(typeof(CombatManager), nameof(CombatManager.StartCombatInternal))]
    [HarmonyPostfix]
    public static void OnCombatStarted(CombatManager __instance)
    {
        // DebugOnlyGetState() 可以获取到当前的 CombatState
        var state = __instance.DebugOnlyGetState();
        if (state != null)
        {
            var localPlayer = state.Players.FirstOrDefault();
            if (localPlayer != null)
            {
                VoicePlayer.PlayEvent(localPlayer.Character.Id.Entry, "EnterCombat");
            }
        }
    }

    // ==========================================
    // 2. 角色死亡 (Die) 与击杀敌人 (KillEnemy)
    // 拦截 Creature.InvokeDiedEvent()，这是实体判定死亡发信号的地方
    // ==========================================
    [HarmonyPatch(typeof(Creature), nameof(Creature.InvokeDiedEvent))]
    [HarmonyPrefix]
    public static void OnCreatureDied(Creature __instance)
    {
        if (__instance.IsPlayer)
        {
            /*
            // 玩家角色死了
            VoicePlayer.Stop(); // 打断遗言或者大招语音
            VoicePlayer.PlayEvent(__instance.Player.Character.Id.Entry, "Die");*/
        }
        else if (__instance.IsEnemy || __instance.IsMonster)
        {
            // 敌人死了，找出场上的玩家
            var combatState = __instance.CombatState;
            if (combatState != null)
            {
                var localPlayer = combatState.Players.FirstOrDefault();
                if (localPlayer != null)
                {
                    // 可以根据是否是首脑怪来区分击杀语音
                    if (__instance.IsPrimaryEnemy && __instance.Monster != null && __instance.Monster.Id.Entry.ToLower().Contains("boss"))
                    {
                        VoicePlayer.PlayEvent(localPlayer.Character.Id.Entry, "KillBoss");
                    }
                    else
                    {
                        VoicePlayer.PlayEvent(localPlayer.Character.Id.Entry, "KillEnemy");
                    }
                }
            }
        }
    }

    // ==========================================
    // 3. 新的回合 (TurnStart)
    // ==========================================
    // 注意：我们拦截 CombatManager 私有的 SetupPlayerTurn
    // 这里能精准知道是哪个玩家开始回合，防止联机干扰
    [HarmonyPatch(typeof(CombatManager), "SetupPlayerTurn")]
    [HarmonyPostfix]
    public static void OnPlayerTurnStart(MegaCrit.Sts2.Core.Entities.Players.Player player)
    {
        if (player != null && player.Character != null)
        {
            // 🌟 开启待机计时
            VoicePlayer.ResetAfkTimer(player.Character.Id.Entry);
            VoicePlayer.PlayEvent(player.Character.Id.Entry, "TurnStart");
        }
    }

    // 回合结束时：停止计时 (拦截 EndPlayerTurnPhaseOneInternal)
    [HarmonyPatch(typeof(CombatManager), nameof(CombatManager.EndPlayerTurnPhaseOneInternal))]
    [HarmonyPrefix]
    public static void OnPlayerTurnEnded()
    {
        // 🌟 停止待机计时
        VoicePlayer.StopAfkTimer();
    }
    
    // ==========================================
    // 4. 打出卡牌 (PlayCard)
    // 拦截 CardModel.OnPlayWrapper
    // ==========================================
    [HarmonyPatch(typeof(CardModel), nameof(CardModel.OnPlayWrapper))]
    [HarmonyPrefix]
    public static void OnCardPlayed(CardModel __instance)
    {
        Log.Info("玩家出牌");
        if (__instance.Owner == null || __instance.Owner.Character == null) return;

        string charId = __instance.Owner.Character.Id.Entry;
        
        // 🌟 打牌了，重新开始计算 15 秒
        VoicePlayer.ResetAfkTimer(charId);
        
        // 拼接一个带卡牌 ID 的特定事件名，比如 "PlayCard_Strike"
        Log.Info("玩家打出" + __instance.Id.Entry);
        string cardEventName = "PlayCard_" + __instance.Id.Entry;

        SkinData skin = SkinApi.GetSelectedSkin(charId);
        if (skin != null && skin.VoiceEvents != null)
        {
            if (skin.VoiceEvents.ContainsKey(cardEventName))
            {
                // 如果这皮肤配置了这张牌的专属语音，就播专属的
                VoicePlayer.PlayEvent(charId, cardEventName);
            }
            else
            {
                // 否则播通用的出牌语音
                VoicePlayer.PlayEvent(charId, "PlayCard_Any");
            }
        }
    }
    

    // ==========================================
    // 7. 战斗胜利 (CombatVictory / BossVictory)
    // 拦截 CombatManager.EndCombatInternal
    // ==========================================
    [HarmonyPatch(typeof(CombatManager), nameof(CombatManager.EndCombatInternal))]
    [HarmonyPrefix]
    public static void OnCombatEnded(CombatManager __instance)
    {
        var state = __instance.DebugOnlyGetState();
        if (state == null) return;

        // 检查是不是胜利 (玩家没死)
        var localPlayer = state.Players.FirstOrDefault();
        if (localPlayer != null && !localPlayer.Creature.IsDead)
        {
            string charId = localPlayer.Character.Id.Entry;
            
            // 判断是不是 Boss 战
            var room = state.RunState?.CurrentRoom as MegaCrit.Sts2.Core.Rooms.CombatRoom;
            bool isBoss = room != null && room.RoomType == MegaCrit.Sts2.Core.Rooms.RoomType.Boss;

            // 停止之前的语音 (比如最后一击的杀敌语音)，优先播放胜利结算语音
            VoicePlayer.Stop();

            if (isBoss)
            {
                VoicePlayer.PlayEvent(charId, "BossVictory");
            }
            else
            {
                VoicePlayer.PlayEvent(charId, "CombatVictory");
            }
        }
    }
}