using ABWEvents.Events;
using ABWEvents.LevelStudioLoader;
using BepInEx.Bootstrap;
using HarmonyLib;
using MTM101BaldAPI;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ABWEvents.Patches;

[HarmonyPatch]
class GeneratorPatches
{
    private static MethodInfo FrameShouldEnd = AccessTools.DeclaredMethod(typeof(LevelBuilder), "FrameShouldEnd");
    private static readonly CodeMatch[] matchingTimeOut = [
        new CodeMatch(OpCodes.Ldarg_0),
        new CodeMatch(CodeInstruction.LoadField(AccessTools.Method(typeof(LevelGenerator), nameof(LevelGenerator.Generate)).GetCustomAttribute<StateMachineAttribute>().StateMachineType, "<timeOutEvent>5__12")),
        new CodeMatch(OpCodes.Ldnull),
        new CodeMatch(CodeInstruction.Call(typeof(UnityEngine.Object), "op_Inequality")),
        //new CodeMatch(x => x.opcode == OpCodes.Brfalse_S) // This does not match somehow...
        ];

    [HarmonyPatch(typeof(LevelGenerator), nameof(LevelGenerator.Generate), MethodType.Enumerator), HarmonyTranspiler]
    static IEnumerable<CodeInstruction> HyperBonusEventsAdd(IEnumerable<CodeInstruction> instructions) => new CodeMatcher(instructions)
        .Start()
        .MatchForward(false, // Commented that out because with it it won't invoke.
        //new CodeMatch(OpCodes.Ldarg_0),
        new CodeMatch(OpCodes.Ldarg_0),
        new CodeMatch(CodeInstruction.LoadField(AccessTools.Method(typeof(LevelGenerator), nameof(LevelGenerator.Generate)).GetCustomAttribute<StateMachineAttribute>().StateMachineType, "<eventsToLaunch>5__10")),
        new CodeMatch(x => x.opcode == OpCodes.Callvirt && ((MethodInfo)x.operand).Name == "GetEnumerator"),
        new CodeMatch(CodeInstruction.StoreField(AccessTools.Method(typeof(LevelGenerator), nameof(LevelGenerator.Generate)).GetCustomAttribute<StateMachineAttribute>().StateMachineType, "<>7__wrap53")),
        new CodeMatch(OpCodes.Ldarg_0),
        new CodeMatch(x => x.opcode == OpCodes.Ldc_I4_S && x.OperandIs(-3)),
        new CodeMatch(CodeInstruction.StoreField(AccessTools.Method(typeof(LevelGenerator), nameof(LevelGenerator.Generate)).GetCustomAttribute<StateMachineAttribute>().StateMachineType, "<>1__state"))
        )
        .ThrowIfInvalid("ITS WRONG. EMERGENCY WARNING!!")
        .InsertAndAdvance(
        new CodeInstruction(OpCodes.Ldloc_2),
        new CodeInstruction(OpCodes.Ldarg_0),
        CodeInstruction.LoadField(AccessTools.Method(typeof(LevelGenerator), nameof(LevelGenerator.Generate)).GetCustomAttribute<StateMachineAttribute>().StateMachineType, "<eventsToLaunch>5__10"),
        Transpilers.EmitDelegate<Action<LevelGenerator,List<RandomEvent>>>((__instance, eventsToLaunch) =>
        {
            ABWEventsPlugin.Logger.LogMessage("Inserting hyper and bonus events...");
            if (__instance.ld is CustomLevelGenerationParameters)
            {
                var ld = __instance.ld as CustomLevelGenerationParameters;
                if (ld.GetCustomModValue(ABWEventsPlugin.PLUGIN_GUID, "hyper_events") != null && ld.GetCustomModValue(ABWEventsPlugin.PLUGIN_GUID, "hyper_event_chance") != null)
                {
                    List<HyperEventSelection> hypers = ld.GetCustomModValue(ABWEventsPlugin.PLUGIN_GUID, "hyper_events") as List<HyperEventSelection>;
                    if ((hypers?.Count ?? 0) != 0)
                    {
                        for (int i = 0; i < eventsToLaunch.Count; i++)
                        {
                            if (hypers.Exists(x => x.replacingExistingEvent == eventsToLaunch[i].Type) && __instance.controlledRNG.NextDouble() < (double)(float)ld.GetCustomModValue(ABWEventsPlugin.PLUGIN_GUID, "hyper_event_chance"))
                            {
                                eventsToLaunch[i] = hypers.Find(x => x.replacingExistingEvent == eventsToLaunch[i].Type).hyperEvent;
#if DEBUG
                                ABWEventsPlugin.Logger.LogMessage($"{eventsToLaunch[i].name} hyper event is selected!");
#endif
                            }
                        }
                    }
                }
                /*if ((bool)FrameShouldEnd.Invoke(__instance, [])) // I should've not pause the enumerator...
                    yield return null;*/
                if (ld.GetCustomModValue(ABWEventsPlugin.PLUGIN_GUID, "bonus_events") != null)
                {
                    List<WeightedRandomEvent> bonuses = ld.GetCustomModValue(ABWEventsPlugin.PLUGIN_GUID, "bonus_events") as List<WeightedRandomEvent>;
                    if ((bonuses?.Count ?? 0) != 0)
                    {
                        RandomEvent randomEvent = WeightedSelection<RandomEvent>.ControlledRandomSelectionList(WeightedRandomEvent.Convert(bonuses), __instance.controlledRNG);
                        ld.SetCustomModValue(ABWEventsPlugin.PLUGIN_GUID, "bonus_event_selected", randomEvent);
#if DEBUG
                        ABWEventsPlugin.Logger.LogMessage($"{randomEvent.name} bonus event is selected!");
#endif
                    }
                }
                /*if ((bool)FrameShouldEnd.Invoke(__instance, []))
                    yield return null;*/
            }
        })
        )
        .MatchForward(false, matchingTimeOut)
        .ThrowIfInvalid("ITS WRONG PART 2. EMERGENCY WARNING!!")
        .InsertAndAdvance(
        new CodeInstruction(OpCodes.Ldloc_2),
        Transpilers.EmitDelegate<Action<LevelGenerator>>((__instance) =>
        {
            if (__instance.ld is CustomLevelGenerationParameters)
            {
                var ld = __instance.ld as CustomLevelGenerationParameters;
                if (ld.GetCustomModValue(ABWEventsPlugin.PLUGIN_GUID, "bonus_event_selected") != null)
                {
                    BonusEventBase bonusEventPrefab = ld.GetCustomModValue(ABWEventsPlugin.PLUGIN_GUID, "bonus_event_selected") as BonusEventBase;
                    var bonusEvent = GameObject.Instantiate<RandomEvent>(bonusEventPrefab, __instance.Ec.transform);
                    bonusEvent.Initialize(__instance.Ec, __instance.controlledRNG);
                    ld.SetCustomModValue(ABWEventsPlugin.PLUGIN_GUID, "bonus_event_prefab", bonusEvent);
                }
            }
        })
        )
        .MatchForward(false, matchingTimeOut)
        .InsertAndAdvance(
        new CodeInstruction(OpCodes.Ldloc_2),
        Transpilers.EmitDelegate<Action<LevelGenerator>>((__instance) =>
        {
            if (__instance.ld is CustomLevelGenerationParameters)
            {
                var ld = __instance.ld as CustomLevelGenerationParameters;
                if (ld.GetCustomModValue(ABWEventsPlugin.PLUGIN_GUID, "bonus_event_prefab") != null)
                {
                    BonusEventBase bonusEventPrefab = ld.GetCustomModValue(ABWEventsPlugin.PLUGIN_GUID, "bonus_event_prefab") as BonusEventBase;
                    bonusEventPrefab.AfterUpdateSetup(__instance.controlledRNG);
                }
            }
        })
        )
        .MatchForward(true, matchingTimeOut).Advance(2)
        .MatchForward(false,
        new CodeMatch(OpCodes.Call, AccessTools.PropertyGetter(typeof(CoreGameManager), nameof(CoreGameManager.Instance))),
        new CodeMatch(CodeInstruction.LoadField(typeof(CoreGameManager), nameof(CoreGameManager.currentMode)))
        //new CodeMatch(x => x.opcode == OpCodes.Brtrue_S) // This also does not match somehow...
            ).ThrowIfInvalid("ITS WRONG FINAL PART. EMERGENCY WARNING!!")
        .InsertAndAdvance(
        new CodeInstruction(OpCodes.Ldloc_2),
        new CodeInstruction(OpCodes.Ldarg_0),
        CodeInstruction.LoadField(AccessTools.Method(typeof(LevelGenerator), nameof(LevelGenerator.Generate)).GetCustomAttribute<StateMachineAttribute>().StateMachineType, "<currentTime>5__50"),
        Transpilers.EmitDelegate<Action<LevelGenerator, float>>((__instance, currentTime) =>
        {
            if (__instance.ld is CustomLevelGenerationParameters)
            {
                var ld = __instance.ld as CustomLevelGenerationParameters;
                if (ld.GetCustomModValue(ABWEventsPlugin.PLUGIN_GUID, "bonus_event_prefab") != null)
                {
                    BonusEventBase bonusEventPrefab = ld.GetCustomModValue(ABWEventsPlugin.PLUGIN_GUID, "bonus_event_prefab") as BonusEventBase;
                    __instance.Ec.AddEvent(bonusEventPrefab, __instance.ld.timeLimit - (bonusEventPrefab.GetEventTime() + 5f) + (currentTime * 0.01f));
                    bonusEventPrefab.SetEventTime();
                    ld.SetCustomModValue(ABWEventsPlugin.PLUGIN_GUID, "bonus_event_prefab", null); // Who even grabs that anyway at the end?
                }
            }
        })
        )
        .InstructionEnumeration();

    internal static SceneObject missleShuffleObject;
    [HarmonyPatch(typeof(GameInitializer), nameof(GameInitializer.Initialize)), HarmonyPrefix]
    static void MissleModeAssign()
    {
        if (CoreGameManager.Instance.sceneObject.name == "MainLevel_5"
            && new System.Random(CoreGameManager.Instance.Seed() + CoreGameManager.Instance.sceneObject.levelNo).NextDouble() < (double)0.1f) // 10% chance for doomsday
            CoreGameManager.Instance.sceneObject = missleShuffleObject;
    }

    private static FieldInfo _events = AccessTools.Field(typeof(EnvironmentController), "events");
    private static FieldInfo _eventTimes = AccessTools.Field(typeof(EnvironmentController), "eventTimes");
    [HarmonyPatch(typeof(LevelLoader), "Load", MethodType.Enumerator), HarmonyTranspiler]
    static IEnumerable<CodeInstruction> BonusEventSet(IEnumerable<CodeInstruction> instructions)
    {
        var newInstructions = new CodeMatcher(instructions); // The pain of obtaining labels...
        newInstructions.Start().MatchForward(true,
            new CodeMatch(OpCodes.Ldloc_S, name: "V_57"), // List<RandomEvent> list3
            new CodeMatch(OpCodes.Ldloc_S, name: "V_56"), // List<RandomEvent> list2
            new CodeMatch(OpCodes.Ldloc_S, name: "V_58"), // Index
            new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(List<RandomEvent>), "get_Item", [typeof(int)])),
            new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(List<RandomEvent>), nameof(List<RandomEvent>.Add)))
            ).ThrowIfInvalid("That did not work??");
        var v_58 = newInstructions.Advance(-2).Operand;
        var v_56 = newInstructions.Advance(-1).Operand;
        var v_57 = newInstructions.Advance(-1).Operand;

        return newInstructions.End()
        .MatchBack(false,
        new CodeMatch(OpCodes.Ldloc_S, v_56),
        new CodeMatch(OpCodes.Ldloc_S, v_58),
        new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(List<RandomEvent>), nameof(List<RandomEvent>.RemoveAt), [typeof(int)]))
        ).ThrowIfInvalid("That's an issue...")
        .InsertAndAdvance(
            new CodeInstruction(OpCodes.Ldloc_S, v_57),

            new CodeInstruction(OpCodes.Ldloc_S, v_56),
            new CodeInstruction(OpCodes.Ldloc_S, v_58),
            new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(List<RandomEvent>), "get_Item", [typeof(int)])),

            new CodeInstruction(OpCodes.Ldloc_2),

            //new CodeInstruction(OpCodes.Ldloc_2),
            //CodeInstruction.LoadField(AccessTools.Method(typeof(LevelLoader), "Load", [typeof(LevelData)]).GetCustomAttribute<StateMachineAttribute>().StateMachineType, "data"),
            Transpilers.EmitDelegate<Action<List<RandomEvent>, RandomEvent, LevelLoader>>((list3, _event, levelloader) =>
            {
                if (_event.GetType().IsSubclassOf(typeof(BonusEventBase)))
                {
#if DEBUG
                    ABWEventsPlugin.Logger.LogInfo($"Preloading bonus event: {_event.gameObject.name}");
#endif
                    list3.Remove(_event);
                    var bonus = GameObject.Instantiate(_event, levelloader.Ec.transform) as BonusEventBase;
                    bonus.Initialize(levelloader.Ec, levelloader.controlledRNG);
                    bonus.PremadeSetup();
                    bonus.SetEventTime();
                    if (Chainloader.PluginInfos.ContainsKey("mtm101.rulerp.baldiplus.levelstudioloader"))
                        levelloader.Ec.AddEvent(bonus, LoaderAdds.GetTimeLimit(levelloader.extraAsset) - (bonus.GetEventTime() + 5f) + ((levelloader.levelAsset.events.Count * levelloader.extraAsset.initialEventGap) * 0.1f));
                    else
                        levelloader.Ec.AddEvent(bonus, 120f - (bonus.GetEventTime() + 5f) + ((levelloader.levelAsset.events.Count * levelloader.extraAsset.initialEventGap) * 0.1f));
                }
            })
        )
        .InstructionEnumeration();
    }
}
