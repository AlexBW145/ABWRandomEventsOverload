using BaldiPlusArcade;
using HarmonyLib;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MTM101BaldAPI;

namespace ABWEvents.ArcadeEternity;

internal static class ArcadeEternityAdds
{
    internal static IEnumerator ArcadeEternityStuff()
    {
        yield return "Adding content to Arcade Eternity...";
        BaldiArcadePlugin.Instance.AddElement(new EventAndStructureElement("gnatswarm", ABWEventsPlugin.assets.Get<RandomEvent>("GnatSwarm"), -200), 600);
        BaldiArcadePlugin.Instance.AddElement(new EventAndStructureElement("traffictrouble", ABWEventsPlugin.assets.Get<RandomEvent>("TrafficTrouble"), 350), 550);
        BaldiArcadePlugin.Instance.AddElement(new EventAndStructureElement("nightmares", ABWEventsPlugin.assets.Get<RandomEvent>("Nightmares"), 300), 400);
        BaldiArcadePlugin.Instance.AddElement(new EventAddElement("missleshufflestrike", ABWEventsPlugin.assets.Get<RandomEvent>("MissleShuffleStrike"), -300), 200);
        yield return "Finalizing content to Arcade Eternity...";
        BaldiArcadePlugin.Instance.levelTemplate.SetCustomModValue(ABWEventsPlugin.PLUGIN_GUID, "hyper_events", new List<HyperEventSelection>()
        {
            new HyperEventSelection()
            {
                hyperEvent = ABWEventsPlugin.assets.Get<RandomEvent>("CrazyGravityChaos"),
                replacingExistingEvent = RandomEventType.Gravity
            },
            new HyperEventSelection()
            {
                hyperEvent = ABWEventsPlugin.assets.Get<RandomEvent>("CrazyFlood"),
                replacingExistingEvent = RandomEventType.Flood
            },
            new HyperEventSelection()
            {
                hyperEvent = ABWEventsPlugin.assets.Get<RandomEvent>("CrazyStudentShuffle"),
                replacingExistingEvent = RandomEventType.StudentShuffle
            },
            new HyperEventSelection()
            {
                hyperEvent = ABWEventsPlugin.assets.Get<RandomEvent>("CrazyBalderDash"),
                replacingExistingEvent = RandomEventType.BalderDash
            },
            new HyperEventSelection()
            {
                hyperEvent = ABWEventsPlugin.assets.Get<RandomEvent>("CrazyGnatSwarm"),
                replacingExistingEvent = ABWEventsPlugin.assets.Get<RandomEvent>("GnatSwarm").Type
            },
            new HyperEventSelection()
            {
                hyperEvent = ABWEventsPlugin.assets.Get<RandomEvent>("CrazyTrafficTrouble"),
                replacingExistingEvent = ABWEventsPlugin.assets.Get<RandomEvent>("TrafficTrouble").Type
            }
        });
        BaldiArcadePlugin.Instance.levelTemplate.SetCustomModValue(ABWEventsPlugin.PLUGIN_GUID, "hyper_event_chance", 0.5f);
        BaldiArcadePlugin.Instance.levelTemplate.SetCustomModValue(ABWEventsPlugin.PLUGIN_GUID, "bonus_events", new List<WeightedRandomEvent>()
        {
            new()
            {
                selection = ABWEventsPlugin.assets.Get<RandomEvent>("BonusMysteryEvent"),
                weight = 99
            },
            new()
            {
                selection = ABWEventsPlugin.assets.Get<RandomEvent>("TokenOutrun"),
                weight = 100
            },
            new()
            {
                selection = ABWEventsPlugin.assets.Get<RandomEvent>("UFOSmasher"),
                weight = 85
            },
            new()
            {
                selection = ABWEventsPlugin.assets.Get<RandomEvent>("TokenCollector"),
                weight = 100
            }
        });
    }
}
[ConditionalPatchMod("mtm101.rulerp.baldiplus.baldiarcade"), HarmonyPatch] // As while I was converting this into a level element, it isn't what I exactly wanted in mind and it refuses to be chosen during the process.
class CalculateCrazyChancePercentage // Don't make a patch at all for the most part since there is a way for structures to be inserted as a "challenge" encounter.
{
    [HarmonyPatch(typeof(EndlessLevelRepresentation), nameof(EndlessLevelRepresentation.SetupForFloor)), HarmonyPostfix]
    static void Postfix(EndlessLevelRepresentation __instance)
    {
        float chancePercent = __instance.floorNumber / 11f;
        chancePercent += (float)__instance.rng.NextDouble() * 100f;
        chancePercent *= __instance.wackyMultiplier;
        __instance.levelObject.SetCustomModValue(ABWEventsPlugin.PLUGIN_GUID, "hyper_event_chance", Mathf.Clamp(chancePercent / 100f, 0f, 1f));
    }
}