using ABWEvents.Events;
using BepInEx.Bootstrap;
using HarmonyLib;
using MonoMod.Utils;
using MTM101BaldAPI;
using MTM101BaldAPI.AssetTools;
using MTM101BaldAPI.Registers;
using PlusLevelStudio;
using PlusLevelStudio.Editor;
using PlusLevelStudio.Editor.GlobalSettingsMenus;
using PlusLevelStudio.Editor.Tools;
using PlusStudioLevelFormat;
using PlusStudioLevelLoader;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ABWEvents.LevelStudio;

internal static class StudioAdds
{
    internal static List<string> editorHyperEvents { get; private set; } = new List<string>();
    internal static List<string> editorBonusEvents { get; private set; } = new List<string>();

    internal static void AddStuffToLevelStudio()
    {
        var modpath = AssetLoader.GetModPath(Chainloader.PluginInfos[ABWEventsPlugin.PLUGIN_GUID].Instance);
        EditorMissleStrikeShuffleChaos shuffleEditor = UnityEngine.Object.Instantiate(ABWEventsPlugin.assets.Get<MissleStrikeShuffleGameManager>("MissleShuffleChaos"), MTM101BaldiDevAPI.prefabTransform).gameObject.SwapComponent<MissleStrikeShuffleGameManager, EditorMissleStrikeShuffleChaos>();
        shuffleEditor.name = "Missle Strike Chaos Level Studio Edition";
        shuffleEditor.timeoverPre = ABWEventsPlugin.assets.Get<RandomEvent>("TimeOver/MissleShuffleChaos") as TimeOut_Shuffle;
        LevelStudioPlugin.Instance.gameModeAliases.Add("missleshufflechaos", new EditorGameMode
        {
            prefab = shuffleEditor,
            nameKey = "Ed_GameMode_MissleShuffleChaos",
            descKey = "Ed_GameMode_MissleShuffleChaos_Desc",
        });
        var placeholdsprite = Resources.FindObjectsOfTypeAll<Sprite>().Last(x => x.name == "Icon_Item");
        LevelStudioPlugin.Instance.eventSprites.AddRange(new Dictionary<string, Sprite>()
        {
            { "gnatswarm", ABWEventsPlugin.assets.Get<Sprite>("gnatswarm") },
            { "traffictrouble", ABWEventsPlugin.assets.Get<Sprite>("traffictrouble") },
            { "nightmares", ABWEventsPlugin.assets.Get<Sprite>("nightmares") },
            { "missleshufflestrike", ABWEventsPlugin.assets.Get<Sprite>("missleshufflestrike") },

            { "hyper_gravitychaos", LevelStudioPlugin.Instance.eventSprites["gravitychaos"] },
            { "hyper_flood", LevelStudioPlugin.Instance.eventSprites["flood"] },
            { "hyper_studentshuffle", LevelStudioPlugin.Instance.eventSprites["studentshuffle"] },
            { "hyper_balderdash", LevelStudioPlugin.Instance.eventSprites["balderdash"] },
            { "hyper_gnatswarm", ABWEventsPlugin.assets.Get<Sprite>("gnatswarm") },
            { "hyper_traffictrouble", ABWEventsPlugin.assets.Get < Sprite >("traffictrouble") },

            { "bonus_randomevent", ABWEventsPlugin.assets.Get<Sprite>("bonus_randomevent") },
            { "bonus_tokenoutrun", placeholdsprite }
        });
        LevelStudioPlugin.Instance.structureTypes.Add("gnatswarm_placement", typeof(EventSpawnPlacementData));
        var visual = EditorInterface.AddStructureGenericVisual("gnatswarm_placement", LevelLoaderPlugin.Instance.tileBasedObjectPrefabs["gnatswarm_placement"].gameObject);
        visual.AddComponent<CapsuleCollider>().height = 5f;
        visual.GetComponent<CapsuleCollider>().center = Vector3.up * 5f;
        LevelStudioPlugin.Instance.structureTypes.Add("traffictrouble_placement", typeof(EventSpawnPlacementData));
        visual = EditorInterface.AddStructureGenericVisual("traffictrouble_placement", LevelLoaderPlugin.Instance.tileBasedObjectPrefabs["traffictrouble_placement"].gameObject);
        visual.AddComponent<BoxCollider>().size = new Vector3(0.5f, 5f, 5f);
        visual.GetComponent<BoxCollider>().center = new Vector3(0f, 5f, 5f);
        LevelStudioPlugin.Instance.structureTypes.Add("nightmares_placement", typeof(EventSpawnPlacementData));
        visual = EditorInterface.AddStructureGenericVisual("nightmares_placement", LevelLoaderPlugin.Instance.tileBasedObjectPrefabs["nightmares_placement"].gameObject);
        visual.AddComponent<SphereCollider>().radius = 5f;
        visual.GetComponent<SphereCollider>().center = Vector3.up * 1f;
        EditorInterfaceModes.AddModeCallback((mode, isVanillaComplaint) =>
        {
            if (mode.id == "full")
                mode.availableGameModes.Add("missleshufflechaos");
            if (mode.id != "rooms")
            {
                EditorInterfaceModes.AddToolsToCategory(mode, "tools", [
                    new GnatSwarmHousingPlacement("gnatswarm_placement", placeholdsprite),
                    new TrafficTroubleTunnelTool("traffictrouble_placement", ABWEventsPlugin.assets.Get<Sprite>("traffictrouble_placement")),
                    new GnatSwarmHousingPlacement("nightmares_placement", ABWEventsPlugin.assets.Get<Sprite>("nightmares_placement")),
                    ]);
            }

            mode.availableRandomEvents.AddRange([
                "gnatswarm",
                "traffictrouble",
                "nightmares",
                "missleshufflestrike"
                ]);
            mode.pages.AddRange([
            new EditorGlobalPage()
            {
                managerType = typeof(CrazyEventsStudioPage),
                filePath = Path.Combine(modpath, "UI", "CrazyEventsGlobalPage.json"),
                pageName = "CrazyEvents",
                pageKey = "Ed_GlobalPage_CrazyEvents"
            },
            new EditorGlobalPage()
            {
                managerType = typeof(BonusEventsStudioPage),
                filePath = Path.Combine(modpath, "UI", "CrazyEventsGlobalPage.json"),
                pageName = "BonusEvents",
                pageKey = "Ed_GlobalPage_BonusEvents"
            }]);
        });
    }

    internal static void InsertCrazysIntoList()
    {
        // Some mods may not have editor support but also has crazy event variants...
        foreach (var crazy in ABWEventsPlugin.hyperEvents)
        {
            foreach (var alias in LevelLoaderPlugin.Instance.randomEventAliases)
            {
                if (alias.Value == crazy)
                {
                    editorHyperEvents.Add(alias.Key);
                    break;
                }
            }
        }
        // Same with bonus events...
        foreach (var bonus in RandomEventMetaStorage.Instance.All().Select(x => x.value).Where(x => x.GetType().IsSubclassOf(typeof(BonusEventBase))))
        {
            foreach (var alias in LevelLoaderPlugin.Instance.randomEventAliases)
            {
                if (alias.Value == bonus)
                {
                    editorBonusEvents.Add(alias.Key);
                    break;
                }
            }
        }
    }
}

internal class EventSpawnPlacementData : HallDoorStructureLocation
{
    public override StructureInfo Compile(EditorLevelData data, BaldiLevel level)
    {
        for (int i = 0; i < myChildren.Count; i++)
        {
            level.tileObjects.Add(new TileObjectInfo()
            {
                prefab = myChildren[i].prefab,
                direction = (PlusDirection)myChildren[i].direction,
                position = EditorExtensions.ToByte(myChildren[i].position)
            });
        }
        StructureInfo dummyInfo = new StructureInfo(); // Dummy structure info containing the dummy structure game object.
        dummyInfo.type = "dummystructure_eventsoverload";
        return dummyInfo;
    }
}

internal class GnatSwarmHousingPlacement : PointTool // Workaround: tile based objects are not implemented in studio yet, but I have to do a 100% worst way of implementing.
{
    public override string id => type;
    public string type;

    internal GnatSwarmHousingPlacement(string id, Sprite icon)
    {
        type = id;
        sprite = icon;
    }

    protected override bool TryPlace(IntVector2 position)
    {
        EditorController.Instance.AddUndo();
        EventSpawnPlacementData location = (EventSpawnPlacementData)EditorController.Instance.AddOrGetStructureToData(type, true);
        SimpleLocation simpleLocation = location.CreateNewChild();
        simpleLocation.position = position;
        simpleLocation.direction = Direction.North;
        simpleLocation.prefab = type;
        location.myChildren.Add(simpleLocation);
        EditorController.Instance.UpdateVisual(location);
        return true;
    }
}

internal class TrafficTroubleTunnelTool : PlaceAndRotateTool // Workaround: Is a poster but can only be placed if the behind cell is not an existing cell.
{
    public override string id => type;
    public string type;

    internal TrafficTroubleTunnelTool(string id, Sprite icon)
    {
        type = id;
        sprite = icon;
    }

    protected override bool TryPlace(IntVector2 position, Direction dir)
    {
        var cell = EditorController.Instance.levelData.GetCellSafe(position + dir.ToIntVector2());
        if (!EditorController.Instance.levelData.WallFree(position, dir, false) || cell == null || cell?.roomId != 0)
            return false;
        EditorController.Instance.AddUndo();
        EventSpawnPlacementData location = (EventSpawnPlacementData)EditorController.Instance.AddOrGetStructureToData(type, true);
        SimpleLocation simpleLocation = location.CreateNewChild();
        simpleLocation.position = position;
        simpleLocation.direction = dir;
        simpleLocation.prefab = type;
        location.myChildren.Add(simpleLocation);
        EditorController.Instance.UpdateVisual(location);
        return true;
    }

    public override bool ValidLocation(IntVector2 position)
    {
        if (base.ValidLocation(position))
        {
            for (int i = 0; i < 4; i++)
                if (EditorController.Instance.levelData.WallFree(position, (Direction)i, false) && EditorController.Instance.levelData.GetCellSafe(position + ((Direction)i).ToIntVector2())?.roomId == 0)
                    return true;
        }
        return false;
    }
}

public class EditorMissleStrikeShuffleChaos : MissleStrikeShuffleGameManager
{
    [SerializeField] internal TimeOut_Shuffle timeoverPre;

    public override void LoadNextLevel() => EditorPlayModeManager.Instance.Win(); // That's that.

    public override void BeginSpoopMode()
    {
        base.BeginSpoopMode();
        ec.SpawnNPCs(); // The only part that isn't in the original gameplay because NPCs are absent and are not in the super schoolhouse this late.
    }

    public override void PrepareLevelData(LevelData data)
    {
        base.PrepareLevelData(data);
        if (data.extraData is ExtendedExtraLevelData)
        {
            var extraData = (ExtendedExtraLevelData)data.extraData;
            if (extraData.timeOutEvent != null && extraData.timeOutTime > 0f)
                extraData.timeOutEvent = timeoverPre;
        }
    }
}

public class CrazyEventsStudioPage : GlobalSettingsUIExchangeHandler // Why copy and paste?? I don't know either!
{
    private StandardMenuButton[] randomEventButtons;
    private int randomEventViewOffset;

    public override bool GetStateBoolean(string key) => false;

    public override void OnElementsCreated()
    {
        randomEventButtons = [
            transform.Find("Event0").GetComponent<StandardMenuButton>(),
            transform.Find("Event1").GetComponent<StandardMenuButton>(),
            transform.Find("Event2").GetComponent<StandardMenuButton>(),
            transform.Find("Event3").GetComponent<StandardMenuButton>(),
            transform.Find("Event4").GetComponent<StandardMenuButton>(),
            transform.Find("Event5").GetComponent<StandardMenuButton>(),
            transform.Find("Event6").GetComponent<StandardMenuButton>()
        ];
        for (int i = 0; i < randomEventButtons.Length; i++)
        {
            int index = i;
            if (i >= StudioAdds.editorHyperEvents.Count)
            {
                randomEventButtons[i].gameObject.SetActive(false);
                transform.Find("RandomEventLeft").gameObject.SetActive(false);
                transform.Find("RandomEventRight").gameObject.SetActive(false);
                continue;
            }
            randomEventButtons[i].eventOnHigh = true;
            randomEventButtons[i].OnHighlight.AddListener(() => EventHighlight(index));
            randomEventButtons[i].OffHighlight.AddListener(EditorController.Instance.tooltipController.CloseTooltip);
        }
    }

    public void EventHighlight(int index)
    {
        if (index + randomEventViewOffset < StudioAdds.editorHyperEvents.Count)
            EditorController.Instance.tooltipController.UpdateTooltip("Ed_RandomEvent_" + StudioAdds.editorHyperEvents[index + randomEventViewOffset]);
    }

    public void RefreshEventView()
    {
        for (int i = 0; i < randomEventButtons.Length; i++)
        {
            if (i + randomEventViewOffset >= StudioAdds.editorHyperEvents.Count)
            {
                randomEventButtons[i].image.color = Color.clear;
                continue;
            }

            randomEventButtons[i].image.sprite = LevelStudioPlugin.Instance.eventSprites[StudioAdds.editorHyperEvents[i + randomEventViewOffset]];
            if (EditorController.Instance.levelData.randomEvents.Contains(StudioAdds.editorHyperEvents[i + randomEventViewOffset]))
                randomEventButtons[i].image.color = Color.white;
            else
                randomEventButtons[i].image.color = Color.black;
        }
    }

    public override void Refresh() => RefreshEventView();

    public void ToggleEvent(string evnt)
    {
        var regular = evnt.Replace("hyper_", "");
        if (EditorController.Instance.levelData.randomEvents.Contains(regular))
            EditorController.Instance.levelData.randomEvents.Remove(regular);

        if (EditorController.Instance.levelData.randomEvents.Contains(evnt))
            EditorController.Instance.levelData.randomEvents.Remove(evnt);
        else
            EditorController.Instance.levelData.randomEvents.Add(evnt);

        handler.somethingChanged = true;
        RefreshEventView();
    }

    public override void SendInteractionMessage(string message, object data = null)
    {

        if (message.StartsWith("event"))
        {
            int num2 = int.Parse(message.Replace("event", ""));
            if (num2 + randomEventViewOffset >= StudioAdds.editorHyperEvents.Count)
                return;

            ToggleEvent(StudioAdds.editorHyperEvents[num2 + randomEventViewOffset]);
        }

        switch (message)
        {
            case "nextEvent":
                randomEventViewOffset = Mathf.Clamp(randomEventViewOffset + 1, 0, StudioAdds.editorHyperEvents.Count - randomEventButtons.Length);
                RefreshEventView();
                break;
            case "prevEvent":
                randomEventViewOffset = Mathf.Clamp(randomEventViewOffset - 1, 0, StudioAdds.editorHyperEvents.Count - randomEventButtons.Length);
                RefreshEventView();
                break;
        }
    }
}

public class BonusEventsStudioPage : GlobalSettingsUIExchangeHandler // Again?
{
    private StandardMenuButton[] randomEventButtons;
    private int randomEventViewOffset;

    public override bool GetStateBoolean(string key) => false;

    public override void OnElementsCreated()
    {
        randomEventButtons = [
            transform.Find("Event0").GetComponent<StandardMenuButton>(),
            transform.Find("Event1").GetComponent<StandardMenuButton>(),
            transform.Find("Event2").GetComponent<StandardMenuButton>(),
            transform.Find("Event3").GetComponent<StandardMenuButton>(),
            transform.Find("Event4").GetComponent<StandardMenuButton>(),
            transform.Find("Event5").GetComponent<StandardMenuButton>(),
            transform.Find("Event6").GetComponent<StandardMenuButton>()
        ];
        for (int i = 0; i < randomEventButtons.Length; i++)
        {
            int index = i;
            if (i >= StudioAdds.editorBonusEvents.Count)
            {
                randomEventButtons[i].gameObject.SetActive(false);
                transform.Find("RandomEventLeft").gameObject.SetActive(false);
                transform.Find("RandomEventRight").gameObject.SetActive(false);
                continue;
            }
            randomEventButtons[i].eventOnHigh = true;
            randomEventButtons[i].OnHighlight.AddListener(() => EventHighlight(index));
            randomEventButtons[i].OffHighlight.AddListener(EditorController.Instance.tooltipController.CloseTooltip);
        }
    }

    public void EventHighlight(int index)
    {
        if (index + randomEventViewOffset < StudioAdds.editorBonusEvents.Count)
            EditorController.Instance.tooltipController.UpdateTooltip("Ed_RandomEvent_" + StudioAdds.editorBonusEvents[index + randomEventViewOffset]);
    }

    public void RefreshEventView()
    {
        for (int i = 0; i < randomEventButtons.Length; i++)
        {
            if (i + randomEventViewOffset >= StudioAdds.editorBonusEvents.Count)
            {
                randomEventButtons[i].image.color = Color.clear;
                continue;
            }

            randomEventButtons[i].image.sprite = LevelStudioPlugin.Instance.eventSprites[StudioAdds.editorBonusEvents[i + randomEventViewOffset]];
            if (EditorController.Instance.levelData.randomEvents.Contains(StudioAdds.editorBonusEvents[i + randomEventViewOffset]))
                randomEventButtons[i].image.color = Color.white;
            else
                randomEventButtons[i].image.color = Color.black;
        }
    }

    public override void Refresh() => RefreshEventView();

    public void ToggleEvent(string evnt)
    {
        if (EditorController.Instance.levelData.randomEvents.Contains(evnt))
            EditorController.Instance.levelData.randomEvents.Remove(evnt);
        else
        {
            EditorController.Instance.levelData.randomEvents.RemoveAll(StudioAdds.editorBonusEvents.Contains); // There can be only one bonus event at a time...
            EditorController.Instance.levelData.randomEvents.Add(evnt);
        }

        handler.somethingChanged = true;
        RefreshEventView();
    }

    public override void SendInteractionMessage(string message, object data = null)
    {

        if (message.StartsWith("event"))
        {
            int num2 = int.Parse(message.Replace("event", ""));
            if (num2 + randomEventViewOffset >= StudioAdds.editorBonusEvents.Count)
                return;

            ToggleEvent(StudioAdds.editorBonusEvents[num2 + randomEventViewOffset]);
        }

        switch (message)
        {
            case "nextEvent":
                randomEventViewOffset = Mathf.Clamp(randomEventViewOffset + 1, 0, StudioAdds.editorBonusEvents.Count - randomEventButtons.Length);
                RefreshEventView();
                break;
            case "prevEvent":
                randomEventViewOffset = Mathf.Clamp(randomEventViewOffset - 1, 0, StudioAdds.editorBonusEvents.Count - randomEventButtons.Length);
                RefreshEventView();
                break;
        }
    }
}

[HarmonyPatch]
class StudioPatches
{
    [HarmonyPatch(typeof(LevelSettingsExchangeHandler), nameof(LevelSettingsExchangeHandler.ToggleEvent)), HarmonyPrefix]
    static void IfHyperExists(string evnt)
    {
        var hyper = "hyper_" + evnt;
        if (EditorController.Instance.levelData.randomEvents.Contains(hyper)) // Worst check but it would still work...
            EditorController.Instance.levelData.randomEvents.Remove(hyper);
    }
}