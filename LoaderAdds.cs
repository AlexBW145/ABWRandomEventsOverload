using ABWEvents.Events;
using MonoMod.Utils;
using MTM101BaldAPI;
using PlusStudioLevelLoader;
using System.Collections.Generic;
using UnityEngine;

namespace ABWEvents.LevelStudioLoader;

internal static class LoaderAdds
{
    internal static void AddLevelLoaderStuff()
    {
        LevelLoaderPlugin.Instance.randomEventAliases.AddRange(new Dictionary<string, RandomEvent>()
        {
            { "gnatswarm", ABWEventsPlugin.assets.Get<RandomEvent>("GnatSwarm") },
            { "traffictrouble", ABWEventsPlugin.assets.Get<RandomEvent>("TrafficTrouble") },
            { "nightmares", ABWEventsPlugin.assets.Get<RandomEvent>("Nightmares") },
            { "missleshufflestrike", ABWEventsPlugin.assets.Get<RandomEvent>("MissleShuffleStrike") },

            { "hyper_gravitychaos", ABWEventsPlugin.assets.Get<RandomEvent>("CrazyGravityChaos") },
            { "hyper_flood", ABWEventsPlugin.assets.Get<RandomEvent>("CrazyFlood") },
            { "hyper_studentshuffle", ABWEventsPlugin.assets.Get<RandomEvent>("CrazyStudentShuffle") },
            { "hyper_balderdash", ABWEventsPlugin.assets.Get<RandomEvent>("CrazyBalderDash") },
            { "hyper_gnatswarm", ABWEventsPlugin.assets.Get<RandomEvent>("CrazyGnatSwarm") },
            { "hyper_traffictrouble", ABWEventsPlugin.assets.Get<RandomEvent>("CrazyTrafficTrouble") },

            { "bonus_randomevent", ABWEventsPlugin.assets.Get<RandomEvent>("BonusMysteryEvent") },
            { "bonus_tokenoutrun", ABWEventsPlugin.assets.Get<RandomEvent>("TokenOutrun") },
            { "bonus_ufosmasher", ABWEventsPlugin.assets.Get<RandomEvent>("UFOSmasher") }
        });
        var gnatplacement = new GameObject("Gnat Housing Placement", typeof(GnatSwarm.HousingPlacement));
        gnatplacement.ConvertToPrefab(true);
        var rend = GameObject.Instantiate(((GnatSwarm)ABWEventsPlugin.assets.Get<RandomEvent>("GnatSwarm")).housePrefab, gnatplacement.transform, false);
        rend.transform.localPosition = Vector3.up * 5f;
        LevelLoaderPlugin.Instance.tileBasedObjectPrefabs.Add("gnatswarm_placement", gnatplacement.GetComponent<GnatSwarm.HousingPlacement>());
        var trafficplacement = new GameObject("Traffic Tunnel Placement", typeof(TrafficTroubleEvent.TunnelPlacement));
        trafficplacement.ConvertToPrefab(true);
        var trafficevent = (TrafficTroubleEvent)ABWEventsPlugin.assets.Get<RandomEvent>("TrafficTrouble");
        var quad = GameObject.Instantiate(trafficevent.tunnel.walls[0], trafficplacement.transform, false);
        quad.SetMaterial(trafficevent.tunnel.overlayShut[0]);
        quad.transform.localPosition = new Vector3(0f, 5f, 5f);
        LevelLoaderPlugin.Instance.tileBasedObjectPrefabs.Add("traffictrouble_placement", trafficplacement.GetComponent<TrafficTroubleEvent.TunnelPlacement>());
        var fissurePlacement = new GameObject("Nightmares Placement", typeof(NightmaresEvent.FissurePlacement));
        fissurePlacement.ConvertToPrefab(true);
        var fissure = GameObject.Instantiate(((NightmaresEvent)ABWEventsPlugin.assets.Get<RandomEvent>("Nightmares")).fissuresPre.GetComponentInChildren<MeshRenderer>().gameObject, fissurePlacement.transform, false);
        fissure.transform.localPosition = Vector3.up * 0.01f;
        var glowlything = GameObject.Instantiate(((NightmaresEvent)ABWEventsPlugin.assets.Get<RandomEvent>("Nightmares")).fissuresPre.GetComponentInChildren<SpriteRenderer>().gameObject, fissurePlacement.transform, false);
        glowlything.transform.localPosition = Vector3.up * 5f;
        glowlything.GetComponent<SpriteRenderer>().color = new Color(0.8980392157f, 0.2235294118f, 0.2666666667f);
        LevelLoaderPlugin.Instance.tileBasedObjectPrefabs.Add("nightmares_placement", fissurePlacement.GetComponent<NightmaresEvent.FissurePlacement>());
        // Dummy structure because Studio does not have a way of tile objects.
        var dummy = new GameObject("Dummy Structure", typeof(StructureBuilder));
        dummy.ConvertToPrefab(true);
        LevelLoaderPlugin.Instance.structureAliases.Add("dummystructure_eventsoverload", new LoaderStructureData()
        {
            structure = dummy.GetComponent<StructureBuilder>(),
            prefabAliases = []
        });
    }

    internal static float GetTimeLimit(ExtraLevelDataAsset extraleveldata)
    {
        if (extraleveldata is ExtendedExtraLevelDataAsset)
        {
            var extradata = extraleveldata as ExtendedExtraLevelDataAsset;
            return extradata.timeOutTime;
        }
        return 120f;
    }
}
