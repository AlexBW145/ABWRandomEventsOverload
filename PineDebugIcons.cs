using PineDebug;
using System.Collections.Generic;
using UnityEngine;

namespace ABWEvents.PineDebug;

internal static class PineDebugIcons
{
    internal static void AddPineDebugIcons()
    {
        PineDebugManager.SetIconsForRandomEvents(new Dictionary<RandomEvent, Texture2D>()
        {
            { ABWEventsPlugin.assets.Get<RandomEvent>("GnatSwarm"), ABWEventsPlugin.assets.Get<Texture2D>("BorderGnatSwarm") },
            { ABWEventsPlugin.assets.Get<RandomEvent>("TrafficTrouble"), ABWEventsPlugin.assets.Get<Texture2D>("BorderTrafficTrouble") },
            { ABWEventsPlugin.assets.Get<RandomEvent>("Nightmares"), ABWEventsPlugin.assets.Get<Texture2D>("BorderNightmares") },
            { ABWEventsPlugin.assets.Get<RandomEvent>("MissleShuffleStrike"), ABWEventsPlugin.assets.Get<Texture2D>("BorderMissleShuffleStrike") },
        });
    }
}
