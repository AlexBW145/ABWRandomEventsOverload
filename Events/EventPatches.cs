using ABWEvents.Events;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using TMPro;
using UnityEngine;

namespace ABWEvents.Patches;

[HarmonyPatch]
class EventPatches
{
    [HarmonyPatch(typeof(Navigator), "TempOpenObstacles"), HarmonyPostfix]
    static void TempOpenTraffic(Navigator __instance)
    {
        if (__instance.passableObstacles.Contains(ABWEventsPlugin.trafficPath))
        {
            if (TrafficTroubleEvent.Instance != null)
                TrafficTroubleEvent.Instance.TempOpen();
        }
    }

    [HarmonyPatch(typeof(Navigator), "TempCloseObstacles"), HarmonyPostfix]
    static void TempCloseTraffic(Navigator __instance)
    {
        if (__instance.passableObstacles.Contains(ABWEventsPlugin.trafficPath))
        {
            if (TrafficTroubleEvent.Instance != null)
                TrafficTroubleEvent.Instance.TempClose();
        }
    }

    [HarmonyPatch(typeof(PartyEvent), nameof(PartyEvent.Begin)), HarmonyPostfix] // I cannot deal with Students bugging the party event.
    static void TrafficUndo(ref List<NavigationState_PartyEvent> ___navigationStates)
    {
        foreach (var car in ___navigationStates.Where(x => 
        x?.npc is TrafficTroubleCar ||
        x?.npc is GnatEntity ||
        x?.npc is NightmareEntity ||
        x?.npc is MissleStrikeShuffleGuy ||
        x?.npc is TokenOutrunGuy ||
        x?.npc is UFOEntity ||
        x?.npc is Balder_Entity ||
        x?.npc is Crowd_Entity ||
        x?.npc is Student ||
        x?.npc?.Character == Character.Null ||
        x?.npc == null))
        {
            if (car?.npc != null)
                car.End();
        }
        ___navigationStates.RemoveAll(x =>
        x?.npc is TrafficTroubleCar ||
        x?.npc is GnatEntity ||
        x?.npc is NightmareEntity ||
        x?.npc is MissleStrikeShuffleGuy ||
        x?.npc is TokenOutrunGuy ||
        x?.npc is UFOEntity ||
        x?.npc is Balder_Entity ||
        x?.npc is Crowd_Entity ||
        x?.npc is Student ||
        x?.npc?.Character == Character.Null ||
        x?.npc == null);
    }

    private static FieldInfo _fleeStates = AccessTools.DeclaredField(typeof(TapePlayer), "fleeStates");
    [HarmonyPatch(typeof(TapePlayer), "Cooldown", MethodType.Enumerator), HarmonyTranspiler]
    static IEnumerable<CodeInstruction> TapeUndo(IEnumerable<CodeInstruction> instructions) => new CodeMatcher(instructions) // There will be a null error exception in base game because of the students, balders, and even worse...
        .Start()
        .MatchForward(true,
        new CodeMatch(OpCodes.Ldloc_3),
        new CodeMatch(CodeInstruction.LoadField(typeof(NPC), nameof(NPC.navigationStateMachine))),
        new CodeMatch(x => x.opcode == OpCodes.Ldloc_S),
        new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(NavigationStateMachine), nameof(NavigationStateMachine.ChangeState)))
        ).ThrowIfInvalid("What?? The?? Fuck??").Advance(4) // Get out of the loop.
        .InsertAndAdvance(
        new CodeInstruction(OpCodes.Ldloc_1),
        Transpilers.EmitDelegate<Action<TapePlayer>>((__instance) =>
        {
            var fleeStates = _fleeStates.GetValue(__instance) as List<NavigationState_WanderFleeOverride>;
            foreach (var car in fleeStates.Where(x =>
        x?.npc is TrafficTroubleCar ||
        x?.npc is GnatEntity ||
        x?.npc is NightmareEntity ||
        x?.npc is MissleStrikeShuffleGuy ||
        x?.npc is TokenOutrunGuy ||
        x?.npc is UFOEntity ||
        x?.npc is Balder_Entity ||
        x?.npc is Crowd_Entity ||
        x?.npc is Student ||
        x?.npc?.Character == Character.Null ||
        x?.npc == null))
        {
            if (car?.npc != null)
                car.End();
        }
            fleeStates.RemoveAll(x =>
        x?.npc is TrafficTroubleCar ||
        x?.npc is GnatEntity ||
        x?.npc is NightmareEntity ||
        x?.npc is MissleStrikeShuffleGuy ||
        x?.npc is TokenOutrunGuy ||
        x?.npc is UFOEntity ||
        x?.npc is Balder_Entity ||
        x?.npc is Crowd_Entity ||
        x?.npc is Student ||
        x?.npc?.Character == Character.Null ||
        x?.npc == null);
        })
        )
        .InstructionEnumeration();

    [HarmonyPatch(typeof(Navigator), "Update"), HarmonyPostfix]
    static void TheyWalkThroughWalls(Navigator __instance, ref List<Vector3> ___destinationPoints)
    {
        if (__instance.npc is NightmareEntity && ___destinationPoints.Count > 1)
            __instance.SkipCurrentDestinationPoint();
    }

    [HarmonyPatch(typeof(Looker), nameof(Looker.Raycast), 
        [typeof(Transform), typeof(float), typeof(PlayerManager), typeof(LayerMask), typeof(bool)], 
        [ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out]), HarmonyPrefix]
    static bool Wrathed(Transform target, float rayDistance, PlayerManager player, LayerMask mask, out bool targetSighted)
    {
        targetSighted = false;
        return target.GetComponent<NightmareEntity>() == null;
    }

    [HarmonyPatch(typeof(EnvironmentController), "EventTimer", MethodType.Enumerator), HarmonyTranspiler]
    static IEnumerable<CodeInstruction> TheTimeOfNightmares(IEnumerable<CodeInstruction> instructions) => new CodeMatcher(instructions)
        .Start()
        .MatchForward(true,
        new CodeMatch(OpCodes.Ldarg_0),
        new CodeMatch(OpCodes.Ldarg_0),
        new CodeMatch(CodeInstruction.LoadField(AccessTools.Method(typeof(EnvironmentController), "EventTimer").GetCustomAttribute<StateMachineAttribute>().StateMachineType, "time")),
        new CodeMatch(OpCodes.Call, AccessTools.PropertyGetter(typeof(Time), nameof(Time.deltaTime))),
        new CodeMatch(OpCodes.Ldloc_1),
        new CodeMatch(OpCodes.Call, AccessTools.PropertyGetter(typeof(EnvironmentController), nameof(EnvironmentController.EnvironmentTimeScale))),
        new CodeMatch(OpCodes.Mul),
        new CodeMatch(OpCodes.Sub),
        new CodeMatch(CodeInstruction.StoreField(AccessTools.Method(typeof(EnvironmentController), "EventTimer").GetCustomAttribute<StateMachineAttribute>().StateMachineType, "time"))
        ).ThrowIfInvalid("Really??").Advance(1)
        .InsertAndAdvance(
        new CodeInstruction(OpCodes.Ldarg_0),
        CodeInstruction.LoadField(AccessTools.Method(typeof(EnvironmentController), "EventTimer").GetCustomAttribute<StateMachineAttribute>().StateMachineType, "randomEvent"),
        new CodeInstruction(OpCodes.Ldarg_0),
        CodeInstruction.LoadField(AccessTools.Method(typeof(EnvironmentController), "EventTimer").GetCustomAttribute<StateMachineAttribute>().StateMachineType, "time"),
        Transpilers.EmitDelegate<Action<RandomEvent, float>>((_event, time) =>
        {
            if (time > 15f && time <= 90f && _event is NightmaresEvent)
            {
                var nightmare = _event.GetComponent<NightmaresEvent>();
                if (nightmare.phase != NightmareEventPhase.Warning)
                    nightmare.Warning();
            }
        })
        )
        .InstructionEnumeration();

    [HarmonyPatch(typeof(Entity), "OnTriggerEnter")]
    [HarmonyPatch(typeof(Entity), "OnTriggerStay")]
    [HarmonyPatch(typeof(Entity), "OnTriggerExit")]
    [HarmonyPrefix]
    static bool CantTouchThis(Collider other) => other.GetComponent<NightmareEntity>() == null;

    private static bool isGeneratingUseless = false;
    [HarmonyPatch(typeof(LevelBuilder), nameof(LevelBuilder.StartGenerate)), HarmonyPostfix]
    static void BooleanUselessSet() => isGeneratingUseless = false;

    [HarmonyPatch(typeof(LevelBuilder), "Update"), HarmonyPostfix]
    static void NotCosmeticUsage(LevelBuilder __instance)
    {
        if (!isGeneratingUseless && !__instance.levelInProgress && __instance.levelCreated)
        {
            isGeneratingUseless = true; // Don't be a fool, they are not meant to be decorations without their respective events.
            foreach (var point in GameObject.FindObjectsOfType<TileBasedObject>(false).Where(x => x is IEventSpawnPlacement))
                GameObject.Destroy(point.gameObject);
        }
    }

    [HarmonyPatch(typeof(HudManager), nameof(HudManager.SetItemSelect)), HarmonyPostfix]
    static void SpikeBallCounts(int value, string key, HudManager __instance, ref TMP_Text ___itemTitle)
    {
        if (CoreGameManager.Instance.GetPlayer(__instance.hudNum) != null)
        {
            var item = CoreGameManager.Instance.GetPlayer(__instance.hudNum).itm.items[value].item;
            if (item is ITM_SpikedBall)
                ___itemTitle.text = string.Format(LocalizationManager.Instance.GetLocalizedText(key), (((ITM_SpikedBall)item).stacks + 1).ToString());
        }
    }

    private static MethodInfo _IncreaseItemSelection = AccessTools.Method(typeof(ItemManager), "IncreaseItemSelection");
    [HarmonyPatch(typeof(ItemManager), nameof(ItemManager.AddItem), [typeof(ItemObject), typeof(Pickup)]), HarmonyPrefix]
    static bool SpikeBallPickups(ItemObject item, Pickup pickup, ItemManager __instance, ref bool __result, ref bool[] ___slotLocked)
    {
        if (__instance.maxItem >= 0 && item.item is ITM_SpikedBall)
        {
            if (!__instance.InventoryFull())
            {
                __instance.AddItem(item);
                __result = true;
                return false;
            }

            int num = 0;
            while (___slotLocked[__instance.selectedItem] && num <= __instance.maxItem)
            {
                _IncreaseItemSelection.Invoke(__instance, []);
                num++;
            }

            int stacks = ((ITM_SpikedBall)item.item).stacks;
            int otherstacks = 0;
            if (__instance.items[__instance.selectedItem].itemType == item.itemType)
                otherstacks = ((ITM_SpikedBall)__instance.items[__instance.selectedItem].item).stacks;
            int totalstacks = stacks + otherstacks;
            if (!___slotLocked[__instance.selectedItem])
            {
                if (__instance.items[__instance.selectedItem].itemType == item.itemType && totalstacks < ITM_SpikedBall.stacksItems.Length - 1)
                {
                    __instance.AddItem(item);
                    __result = true;
                    return false;
                }
                else
                {
                    for (int i = 0; i < __instance.maxItem; i++)
                    {
                        if (__instance.items[i].itemType == item.itemType)
                        {
                            otherstacks = ((ITM_SpikedBall)__instance.items[i].item).stacks;
                            totalstacks = stacks + otherstacks;
                            if (totalstacks < ITM_SpikedBall.stacksItems.Length - 1)
                            {
                                __instance.AddItem(item);
                                __result = true;
                                return false;
                            }
                        }
                    }
                }
                pickup.AssignItem(__instance.items[__instance.selectedItem]);
                CoreGameManager.Instance.GetHud(__instance.pm.playerNumber).inventory.LoseItem(__instance.selectedItem, __instance.items[__instance.selectedItem]);
                __instance.AddItem(item);
                __result = true;
                return false;
            }
        }
        return true;
    }
    [HarmonyPatch(typeof(ItemManager), nameof(ItemManager.AddItem), [typeof(ItemObject)]), HarmonyPrefix]
    static bool isSpikedBall(ItemObject item, ItemManager __instance)
    {
        if (__instance.maxItem >= 0 && item.item is ITM_SpikedBall)
        {
            int stacks = ((ITM_SpikedBall)item.item).stacks;
            int otherstacks = 0;
            if (__instance.items[__instance.selectedItem].itemType == item.itemType)
                otherstacks = ((ITM_SpikedBall)__instance.items[__instance.selectedItem].item).stacks;
            int totalstacks = stacks + otherstacks;
            if (__instance.items[__instance.selectedItem].itemType == item.itemType && totalstacks < ITM_SpikedBall.stacksItems.Length - 1)
            {
                __instance.SetItem(ITM_SpikedBall.stacksItems[totalstacks + 1], __instance.selectedItem);
                return false;
            }
            else
            {
                for (int i = 0; i < __instance.maxItem; i++)
                {
                    if (__instance.items[i].itemType == item.itemType)
                    {
                        otherstacks = ((ITM_SpikedBall)__instance.items[i].item).stacks;
                        totalstacks = stacks + otherstacks;
                        if (totalstacks < ITM_SpikedBall.stacksItems.Length - 1)
                        {
                            __instance.SetItem(ITM_SpikedBall.stacksItems[totalstacks + 1], i);
                            return false;
                        }
                    }
                }
            }
        }
        return true;
    }
}
