using ABWEvents.Events;
using ABWEvents.LevelStudio;
using ABWEvents.LevelStudioLoader;
using ABWEvents.Patches;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using MonoMod.Utils;
using MTM101BaldAPI;
using MTM101BaldAPI.AssetTools;
using MTM101BaldAPI.Components;
using MTM101BaldAPI.ObjectCreation;
using MTM101BaldAPI.PlusExtensions;
using MTM101BaldAPI.Reflection;
using MTM101BaldAPI.Registers;
using MTM101BaldAPI.SaveSystem;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ABWEvents;

[BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
[BepInDependency("mtm101.rulerp.bbplus.baldidevapi", "9.1.0.0")]
[BepInDependency("mtm101.rulerp.baldiplus.levelstudioloader", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("mtm101.rulerp.baldiplus.levelstudio", BepInDependency.DependencyFlags.SoftDependency)]
public class ABWEventsPlugin : BaseUnityPlugin
{
    internal const string PLUGIN_GUID = "alexbw145.bbplus.eventsoverload";
    private const string PLUGIN_NAME = "AlexBW145's Events Overload";
    private const string PLUGIN_VERSION = "1.0.0";

    internal static new ManualLogSource Logger;
    internal static AssetManager assets = new AssetManager();
    internal static PassableObstacle trafficPath;
    public static SoundObject _bonusJingle => assets.Get<SoundObject>("EventJingles/Bonus");
    public static SoundObject _hyperJingle => assets.Get<SoundObject>("EventJingles/Hyper");
    internal static ConfigEntry<bool> instantHyper { get; private set; }
    internal static ConfigEntry<bool> youtuberMode { get; private set; }
    public static List<RandomEvent> hyperEvents { get; private set; } = new List<RandomEvent>();

    private void Awake()
    {
        // Plugin startup logic
        Logger = base.Logger;
        instantHyper = Config.Bind("Generation Settings", "Force Crazy Events", false, "If set to 'true', then crazy event will always replace specific regular events regardless of rng and chance percentage. (This config is mainly be paired with Youtuber Mode)");
        youtuberMode = Config.Bind("Generation Settings", "Youtuber Mode", false, "If set to 'true', then regular events from this mod will always be forced! (But not bonus events, forcing crazy events are a separate option.)");
        new Harmony(PLUGIN_GUID).PatchAllConditionals();
        LoadingEvents.RegisterOnAssetsLoaded(Info, AssetsLoad, LoadingEventOrder.Start);
        LoadingEvents.RegisterOnAssetsLoaded(Info, PreLoad(), LoadingEventOrder.Pre);
        LoadingEvents.RegisterOnAssetsLoaded(Info, PostLoad(), LoadingEventOrder.Post);
        LoadingEvents.RegisterOnAssetsLoaded(Info, FinalLoad, LoadingEventOrder.Final);
        ModdedSaveGame.AddSaveHandler(new ABWEventsOverloadSaveIO());
        MTM101BaldiDevAPI.AddWarningScreen(@"This ABW's Events Overload build is given out to a discord server and some people.

This is actually an early access release...", false);
    }

    private void FinalLoad()
    {
        if (Chainloader.PluginInfos.ContainsKey("mtm101.rulerp.baldiplus.levelstudio"))
            StudioAdds.InsertCrazysIntoList();
    }

    private IEnumerator PostLoad()
    {
        yield return 3;
        yield return "Extra Stuff...";
        foreach (var useless in ItemMetaStorage.Instance.FindAll(x => x.tags.Contains("lost_item") || x.tags.Contains("shape_key") || x.tags.Contains("shop_dummy") || x.flags.HasFlag(ItemFlags.Unobtainable)))
            useless.tags.Add("abw_eventsoverload_isnotufosmasherloot");
        NightmareEntity.snds = Resources.FindObjectsOfTypeAll<SoundObject>().Where(x => x.soundClip.length < 3f && x.soundType == SoundType.Voice && !x.soundKey.ToLower().StartsWith("sfx_")).ToArray();
        UFOEntity.regularItem.AddRange(ItemMetaStorage.Instance.FindAll(x => x.value.price >= 200 && x.value.price < 750 && x.value.itemType != Items.None && !x.tags.Contains("abw_eventsoverload_isnotufosmasherloot")).Select(x => x.value)); // Lowest price is principal's keys and techno-boots.
        UFOEntity.goodItem.AddRange(ItemMetaStorage.Instance.FindAll(x => x.value.price >= 750 && x.value.price <= 2500 && x.value.itemType != Items.None && !x.tags.Contains("abw_eventsoverload_isnotufosmasherloot")).Select(x => x.value)); // Lowest expensive price is the grappling hook.
        yield return "Adding events to level loader...";
        if (Chainloader.PluginInfos.ContainsKey("mtm101.rulerp.baldiplus.levelstudioloader"))
            LoaderAdds.AddLevelLoaderStuff();
        yield return "Adding content to level studio...";
        if (Chainloader.PluginInfos.ContainsKey("mtm101.rulerp.baldiplus.levelstudio"))
            StudioAdds.AddStuffToLevelStudio();
    }

    private void AssetsLoad()
    {
        #region LOCALIZATION
        AssetLoader.LocalizationFromFunction((lang) =>
        {
            return new Dictionary<string, string>()
            {
                { "Sfx_GnatIdling", "*BUZZING SOUNDS*" },
                { "Sfx_Gnattack", "*TACKLE!*" },
                { "Sfx_TrafficTrouble_Horn", "*BEEP*" },
                { "Sfx_TrafficTrouble_IMPACT", "*BAOMP!*" },
                { "Sfx_MissleShuffleStrike_IncomingPre", "*BLING!*" },
                { "Sfx_MissleShuffleStrike_Incoming", "*Missile target incoming...*" },
                { "Sfx_MissleShuffleStrike_Exploded", "*BOOM!*" },
                { "Sfx_UFOSmasher_UFOIdle", "*Humming noises*" },
                { "Sfx_UFOSmasher_SpikeballImpact", "*BUMP!*" },
                { "Sfx_UFOSmasher_UFOSpawn", "*Swooshing humming noises*" },
                { "Sfx_UFOSmasher_UFODespawn", "*Swooshing humming noises*" },
                { "Sfx_UFOSmasher_SpikeballRoll", "*Rolling...*" },
                { "Sfx_UFOSmasher_UFODies", "*Malfunction noises!*" },

                { "Itm_SpikedBallBonus", "Spiked Ball ({0})" },
                { "Desc_SpikedBallBonus", "Spiked Ball\nA spiked ball that can destroy ufos.\nYou can also use it towards Baldi and his friends too!" },

                { "Vfx_ABW_GnatSwarm", "Well this is a problem." },
                { "Vfx_ABW_GnatSwarm1", "A bunch of gnats has came out of their houses" },
                { "Vfx_ABW_GnatSwarm2", "and are now swarming the entire schoolhouse!" },
                { "Vfx_ABW_GnatSwarm3", "Beware or else your vision will die of swarms..." },

                { "Vfx_ABW_HyperGnatSwarm", "The gnats are restless," },
                { "Vfx_ABW_HyperGnatSwarm1", "they will always be multiplying..." },

                { "Vfx_ABW_TrafficTrouble", "Now it's that time already for traffic to slowly pick up!" },
                { "Vfx_ABW_TrafficTrouble1", "If one of the drivers ran into you, then uhh..." },
                { "Vfx_ABW_TrafficTrouble2", "I think you'll get flunged, I guess." },

                { "Vfx_ABW_HyperTrafficTrouble", "Traffic is starting to pick up more and more drivers." },
                { "Vfx_ABW_HyperTrafficTrouble1", "..." },
                { "Vfx_ABW_HyperTrafficTrouble2", "...wait" },
                { "Vfx_ABW_HyperTrafficTrouble3", "Look both ways and good luck because they're extremely crazy!" },
                { "Vfx_ABW_HyperTrafficTrouble4", "Oh my..." },

                { "Vfx_ABW_Nightmares", "VEhFIE5JR0hUTUFSRVMgQVJFIENPTUlORy4=" },
                { "Vfx_ABW_Nightmares1", "SXMgdGhpcyBldmVuIHBhcnQgb2YgdGhlIHNjcmlwdD8=" },

                { "Vfx_ABW_MissleShuffleStrike", "Uh noes, we have sighted a random UFO and is now sending missile strikes towards the schoolhouse?!" },
                { "Vfx_ABW_MissleShuffleStrike1", "Avoid them or else you'll get pushed away!" },

                { "Vfx_ABW_BonusMysteryEvent", "Hmm... A mystery event has appeared." },
                { "Vfx_ABW_BonusMysteryEvent1", "I wonder what action will it throw towards you?" },

                { "Vfx_ABW_TokenOutrun", "Some random thief has stolen all of the banks containing You Thought Points!" },
                { "Vfx_ABW_TokenOutrun1", "But they're accidentally dropping some," },
                { "Vfx_ABW_TokenOutrun2", "grab them before its too late!" },

                { "Vfx_ABW_UFOSmasher", "A bunch of UFOs has appeared out of nowhere!" },
                { "Vfx_ABW_UFOSmasher1", "I've placed in spiked balls around the hallways so that you can fight back!" },

                { "Vfx_ABW_TokenCollector", "Tokens are starting to appear around the hallways," },
                { "Vfx_ABW_TokenCollector1", "collect them before others do!" },

                { "Ed_GlobalPage_CrazyEvents", "<color=#FF0200>C</color><color=#26FF2B>R</color><color=#2412FB>A</color><color=#FEFE2B>Z</color><color=#D987FF>Y</color>\nEvents" },
                { "Ed_GlobalPage_BonusEvents", "Bonus\nEvents" },

                {"Ed_GameMode_MissleShuffleChaos", "Missile Shuffle Chaos"},
                {"Ed_GameMode_MissleShuffleChaos_Desc", "An unidentified flying object has appeared and is now launching missile strikes towards the students!\nMake sure to not stay inside of the blast for too long or else its game over!"},

                {"Ed_RandomEvent_gnatswarm", "Gnat Swarm\nA bunch of gnats came out of their houses and are now swarming the entire schoolhouse!\nGnats blinds NPCs completely but also hinders the player's stamina."},
                {"Ed_RandomEvent_traffictrouble", "Traffic Trouble\nTraffic is slowly starting to pick up drivers.\nTry not to get too close to the drivers or else you'll get completely flinged in front of them."},
                {"Ed_RandomEvent_nightmares", "Nightmares\nTHE NIGHTMARES ARE COMING.\nNightmare creatures can walk through walls and will only target the player,\nwith Cloudy Claustrophobia decreasing the player's stamina by -10 of the amount\nand Terrorcrafters alerting Baldi to the player's location."},
                {"Ed_RandomEvent_missleshufflestrike", "Missile Shuffle Strike\nAn unidentified flying object has appeared and is now launching missile strikes towards the students!\nmissile strikes will push away anyone and anything on touch!"},

                {"Ed_RandomEvent_hyper_gravitychaos", "<color=#FF0200>C</color><color=#26FF2B>R</color><color=#2412FB>A</color><color=#FEFE2B>Z</color><color=#D987FF>Y</color> Gravity Chaos\nShapes are appearing more and more often...\nGravity flippers spawn rate and max amount has been increased!"},
                {"Ed_RandomEvent_hyper_flood", "<color=#FF0200>C</color><color=#26FF2B>R</color><color=#2412FB>A</color><color=#FEFE2B>Z</color><color=#D987FF>Y</color> Flood\nThe next leak is massive!\nWhirlpools has been buffed significally and will appear more often\nplus the max amount of whirlpools has been increased!"},
                {"Ed_RandomEvent_hyper_studentshuffle", "<color=#FF0200>C</color><color=#26FF2B>R</color><color=#2412FB>A</color><color=#FEFE2B>Z</color><color=#D987FF>Y</color> Student Shuffle\nSome influencer has gotten their post about the super schoolhouse into trending\nand has boosted the open house visitors.\nCrowds of students will appear more and more and more and the line keeps on going."},
                {"Ed_RandomEvent_hyper_balderdash", "<color=#FF0200>C</color><color=#26FF2B>R</color><color=#2412FB>A</color><color=#FEFE2B>Z</color><color=#D987FF>Y</color> Balder Dash\nThe balders has gone super crazy and are now terrorizing the super schoolhouse!\nBalders will have the increased speed boost while their spawn cooldown is always one second."},
                {"Ed_RandomEvent_hyper_gnatswarm", "<color=#FF0200>C</color><color=#26FF2B>R</color><color=#2412FB>A</color><color=#FEFE2B>Z</color><color=#D987FF>Y</color> Gnat Swarm\nThe gnats are multiplying...\nMore gnats will spawn in every 15 seconds!"},
                {"Ed_RandomEvent_hyper_traffictrouble", "<color=#FF0200>C</color><color=#26FF2B>R</color><color=#2412FB>A</color><color=#FEFE2B>Z</color><color=#D987FF>Y</color> Traffic Trouble\nThe drivers has gone crazy and more of them kept on appearing into the traffic scene!\nDrivers will have increased speed, less spawn cooldown, and will terrorize the super schoolhouse!"},

                {"Ed_RandomEvent_bonus_randomevent", "Mystery Event\nA random event will be summonned and will end by the time class is dismissed!\nRNG is dependent for this event."},
                {"Ed_RandomEvent_bonus_tokenoutrun", "Token Outrun\nSome random guy appeared and is running away but is accidently dropping the tokens.\nEach token is worth 15 YTPs and will disappear quickly. The runner will also run away from the players so be quick!"},
                {"Ed_RandomEvent_bonus_ufosmasher", "UFO Smasher\nA bunch of UFOs have appeared out of nowhere with randomized loot!\nSpiked balls will appear in random hallway positions and throwing enough of them will destroy the UFO!\nDropping its loot it has.\nSpiked balls can also be used against Baldi and him friends!"},
                {"Ed_RandomEvent_bonus_tokencollector", "Token Collector\nRandom tokens have appeared out of nowhere and will disappear soon.\nTokens are valued by each existing YTP items, other characters can collect the tokens."},

                { "Ed_Tool_gnatswarm_placement_Title", "Gnat House" },
                { "Ed_Tool_gnatswarm_placement_Desc", "Houses a bunch of gnats." },
                { "Ed_Tool_traffictrouble_placement_Title", "Traffic Tunnel" },
                { "Ed_Tool_traffictrouble_placement_Desc", "Constructs a tunnel for traffic to flow." },
                { "Ed_Tool_nightmares_placement_Title", "Nightmare Fissure" },
                { "Ed_Tool_nightmares_placement_Desc", "Evil residue containment and will always be real." }
            };
        });
        #endregion
        #region TRAFFIC TROUBLE CARS
        List<Sprite[]> spritesheets = new List<Sprite[]>();
        foreach (var spritesheet in Directory.GetFiles(Path.Combine(AssetLoader.GetModPath(this), "Texture2D", "TrafficTrouble", "Cars"), "*.png"))
        {
            var sheet = AssetLoader.SpritesFromSpritesheet(12, 1, 26, Vector2.one / 2f, AssetLoader.TextureFromFile(spritesheet));
            spritesheets.Add(sheet);
        }
        assets.Add("TrafficTrouble/CarSheets", spritesheets);
        assets.Add("SelfInsertGuy", AssetLoader.SpritesFromSpritesheet(5, 1, 1f, Vector2.one / 2f, AssetLoader.TextureFromMod(this, "Texture2D", "selfinsertguy_tvsheet.png")));
        #endregion
        #region SOUND ASSETS
        Color him = new Color(0.1921568627f, 0.5411764706f, 1f);
        assets.AddRange<SoundObject>([
            ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromMod(this, "AudioClip", "EventJingleABW.wav"), "EventJingleABW", SoundType.Effect, Color.white, 0f),
            ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromMod(this, "AudioClip", "EventJingleABWHyper.wav"), "EventJingleABWHyper", SoundType.Effect, Color.white, 0f),
            ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromMod(this, "AudioClip", "HyperEventJingle.wav"), "EventJingleHyper", SoundType.Effect, Color.white, 0f),
            ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromMod(this, "AudioClip", "BonusEventJingle.wav"), "EventJingleBonus", SoundType.Effect, Color.white, 0f),
            ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromMod(this, "AudioClip", "EventJingleNightmares.wav"), "EventJingleNightmares", SoundType.Effect, Color.white, 0f),

            ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromMod(this, "AudioClip", "Intros", "GnatSwarmIntro.wav"), "Vfx_ABW_GnatSwarm", SoundType.Voice, him),
            ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromMod(this, "AudioClip", "Intros", "GnatSwarmHyperIntro.wav"), "Vfx_ABW_HyperGnatSwarm", SoundType.Voice, him),
            ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromMod(this, "AudioClip", "Intros", "TrafficTroubleIntro.wav"), "Vfx_ABW_TrafficTrouble", SoundType.Voice, him),
            ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromMod(this, "AudioClip", "Intros", "TrafficTroubleHyperIntro.wav"), "Vfx_ABW_HyperTrafficTrouble", SoundType.Voice, him),
            ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromMod(this, "AudioClip", "Intros", "NightmaresIntro.wav"), "Vfx_ABW_Nightmares", SoundType.Voice, new Color(0.01568627451f, 0.04705882353f, 0.0862745098f)),
            ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromMod(this, "AudioClip", "Intros", "MissleStrikeShuffleIntro.wav"), "Vfx_ABW_MissleShuffleStrike", SoundType.Voice, him),
            ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromMod(this, "AudioClip", "Intros", "MysteryEventIntro.wav"), "Vfx_ABW_BonusMysteryEvent", SoundType.Voice, him),
            ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromMod(this, "AudioClip", "Intros", "TokenOutrunIntro.wav"), "Vfx_ABW_TokenOutrun", SoundType.Voice, him),
            ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromMod(this, "AudioClip", "Intros", "UFOSmasherIntro.wav"), "Vfx_ABW_UFOSmasher", SoundType.Voice, him),
            ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromMod(this, "AudioClip", "Intros", "TokenCollectorIntro.wav"), "Vfx_ABW_TokenCollector", SoundType.Voice, him),

            ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromMod(this, "AudioClip", "GnatSwarm", "GnatIdling.wav"), "Sfx_GnatIdling", SoundType.Effect, Color.white),
            ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromMod(this, "AudioClip", "GnatSwarm", "Gnattack.wav"), "Sfx_Gnattack", SoundType.Effect, Color.white),

            ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromMod(this, "AudioClip", "TrafficTrouble", "CarImpact.wav"), "Sfx_TrafficTrouble_IMPACT", SoundType.Effect, Color.white),

            ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromMod(this, "AudioClip", "Nightmares", "nightmare_spawner_close.wav"), "Sfx_NightmareSpawner_Close", SoundType.Effect, Color.white, 0f),
            ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromMod(this, "AudioClip", "Nightmares", "nightmare_spawner_LP.wav"), "Sfx_NightmareSpawner_FX", SoundType.Effect, Color.white, 0f),
            ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromMod(this, "AudioClip", "Nightmares", "nightmare_spawner_open.wav"), "Sfx_NightmareSpawner_Open", SoundType.Effect, Color.white, 0f),
            ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromMod(this, "AudioClip", "Nightmares", "nightmare_spawner_warnopen.wav"), "Sfx_NightmareSpawner_WarningOpen", SoundType.Effect, Color.white, 0f),
            ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromMod(this, "AudioClip", "Nightmares", "nightmare_statechange.wav"), "Sfx_NightmareEvent_Phase", SoundType.Effect, Color.white, 0f),

            ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromMod(this, "AudioClip", "MissleStrikeShuffle", "MissleStrikeIncomingPre.wav"), "Sfx_MissleShuffleStrike_IncomingPre", SoundType.Effect, Color.white),
            ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromMod(this, "AudioClip", "MissleStrikeShuffle", "MissleStrikeIncoming.wav"), "Sfx_MissleShuffleStrike_Incoming", SoundType.Effect, Color.white),
            ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromMod(this, "AudioClip", "MissleStrikeShuffle", "MissleStrikeExplosion.wav"), "Sfx_MissleShuffleStrike_Exploded", SoundType.Effect, Color.white),

            ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromMod(this, "AudioClip", "UFOSmasher", "UFOidle.wav"), "Sfx_UFOSmasher_UFOIdle", SoundType.Effect, Color.white),
            ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromMod(this, "AudioClip", "UFOSmasher", "SpikeBallImpact.wav"), "Sfx_UFOSmasher_SpikeballImpact", SoundType.Effect, Color.white),
            ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromMod(this, "AudioClip", "UFOSmasher", "SpikeBallRoll.wav"), "Sfx_UFOSmasher_SpikeballRoll", SoundType.Effect, Color.white),
            ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromMod(this, "AudioClip", "UFOSmasher", "UFOspawn.wav"), "Sfx_UFOSmasher_UFOSpawn", SoundType.Effect, Color.white),
            ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromMod(this, "AudioClip", "UFOSmasher", "UFOdespawn.wav"), "Sfx_UFOSmasher_UFODespawn", SoundType.Effect, Color.white),
            ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromMod(this, "AudioClip", "UFOSmasher", "UFOdies.wav"), "Sfx_UFOSmasher_UFODies", SoundType.Effect, Color.white)
            ], [
                "EventJingles/ABW",
                "EventJingles/HyperABW",
                "EventJingles/Hyper",
                "EventJingles/Bonus",
                "EventJingles/Nightmares",

                "EventIntros/GnatSwarm",
                "EventIntros/HyperGnatSwarm",
                "EventIntros/TrafficTrouble",
                "EventIntros/HyperTrafficTrouble",
                "EventIntros/Nightmares",
                "EventIntros/MissleStrikeShuffle",
                "EventIntros/BonusRandomEvent",
                "EventIntros/TokenOutrun",
                "EventIntros/UFOSmasher",
                "EventIntros/TokenCollector",

                "GnatSwarm/Gnat",
                "GnatSwarm/Gnattack",

                "TrafficTrouble/CarImpact",

                "Nightmares/SpawnerClose",
                "Nightmares/SpawnerFX",
                "Nightmares/SpawnerOpen",
                "Nightmares/SpawnerWarningOpen",
                "Nightmares/PhaseChange",

                "MissleStrikeShuffle/IncomingPre",
                "MissleStrikeShuffle/Incoming",
                "MissleStrikeShuffle/Exploded",

                "UFOSmasher/Idling",
                "UFOSmasher/BallImpact",
                "UFOSmasher/BallRolling",
                "UFOSmasher/Spawn",
                "UFOSmasher/Despawn",
                "UFOSmasher/Die"
                ]);
        assets.Get<SoundObject>("EventIntros/GnatSwarm").additionalKeys = [
            new()
            {
                time = 1.181f,
                key = "Vfx_ABW_GnatSwarm1"
            },
            new()
            {
                time = 3.25f,
                key = "Vfx_ABW_GnatSwarm2"
            },
            new()
            {
                time = 6f,
                key = "Vfx_ABW_GnatSwarm3"
            },
            ];
        assets.Get<SoundObject>("EventIntros/HyperGnatSwarm").additionalKeys = [
            new()
            {
                time = 1.368f,
                key = "Vfx_ABW_HyperGnatSwarm1"
            }
            ];
        assets.Get<SoundObject>("EventIntros/TrafficTrouble").additionalKeys = [
            new()
            {
                time = 3.25f,
                key = "Vfx_ABW_TrafficTrouble1"
            },
            new()
            {
                time = 5.85f,
                key = "Vfx_ABW_TrafficTrouble2"
            }
            ];
        assets.Get<SoundObject>("EventIntros/HyperTrafficTrouble").additionalKeys = [
            new()
            {
                time = 2.8f,
                key = "Vfx_ABW_HyperTrafficTrouble1"
            },
            new()
            {
                time = 3.43f,
                key = "Vfx_ABW_HyperTrafficTrouble2"
            },
            new()
            {
                time = 5.79f,
                key = "Vfx_ABW_HyperTrafficTrouble3"
            },
            new()
            {
                time = 9.8f,
                key = "Vfx_ABW_HyperTrafficTrouble4"
            }
            ];
        assets.Get<SoundObject>("EventIntros/Nightmares").additionalKeys = [
            new()
            {
                time = 4.4f,
                key = "Vfx_ABW_Nightmares1"
            },
            ];
        assets.Get<SoundObject>("EventIntros/MissleStrikeShuffle").additionalKeys = [
            new()
            {
                time = 5.22f,
                key = "Vfx_ABW_MissleShuffleStrike1"
            }
            ];
        assets.Get<SoundObject>("EventIntros/BonusRandomEvent").additionalKeys = [
            new()
            {
                time = 2.85f,
                key = "Vfx_ABW_BonusMysteryEvent1"
            }
            ];
        assets.Get<SoundObject>("EventIntros/TokenOutrun").additionalKeys = [
            new()
            {
                time = 4.38f,
                key = "Vfx_ABW_TokenOutrun1"
            },
            new()
            {
                time = 6.35f,
                key = "Vfx_ABW_TokenOutrun2"
            }
            ];
        assets.Get<SoundObject>("EventIntros/UFOSmasher").additionalKeys = [
            new()
            {
                time = 2.655f,
                key = "Vfx_ABW_UFOSmasher1"
            }
            ];
        assets.Get<SoundObject>("EventIntros/TokenCollector").additionalKeys = [
            new()
            {
                time = 2.445f,
                key = "Vfx_ABW_TokenCollector1"
            }
            ];
        #endregion
        #region TEXTURES & SPRITES
        assets.AddRange<Texture2D>([
            AssetLoader.TextureFromMod(this, "Texture2D", "TrafficTrouble", "Open.png"),
            AssetLoader.TextureFromMod(this, "Texture2D", "TrafficTrouble", "Corner.png"),
            AssetLoader.TextureFromMod(this, "Texture2D", "TrafficTrouble", "Straight.png"),
            AssetLoader.TextureFromMod(this, "Texture2D", "TrafficTrouble", "ThreeWay.png"),
            AssetLoader.TextureFromMod(this, "Texture2D", "TrafficTrouble", "End.png"),
            AssetLoader.TextureFromMod(this, "Texture2D", "TrafficTrouble", "TrafficTunnel.png"),
            AssetLoader.TextureFromMod(this, "Texture2D", "TrafficTrouble", "TrafficTunnelMask.png"),

            AssetLoader.TextureFromMod(this, "Texture2D", "Nightmares", "Fissure.png"),

            AssetLoader.TextureFromMod(this, "Texture2D", "MissleStrikeShuffle", "MissleShuffleFlames.png"),
            AssetLoader.TextureFromMod(this, "Texture2D", "MissleStrikeShuffle", "MissleShuffleIndication.png")
        ], ["TrafficTrouble/Roads/Open", "TrafficTrouble/Roads/Corner", "TrafficTrouble/Roads/Straight", "TrafficTrouble/Roads/ThreeWay", "TrafficTrouble/Roads/End",
        "TrafficTrouble/Tunnel", "TrafficTrouble/TunnelMask",
        
        "Nightmares/Fissure",

        "MissleShuffleStrike/Flames",
        "MissleShuffleStrike/Indication"]);
        assets.AddRange([
            AssetLoader.SpriteFromMod(this, Vector2.one/2f, 64f, "Texture2D", "Nightmares", "FissureGlow.png"),
            AssetLoader.SpriteFromMod(this, Vector2.one/2f, 16f, "Texture2D", "MissleStrikeShuffle", "ARocketThatStartsWithR.png"),
            AssetLoader.SpriteFromMod(this, Vector2.one/2f, 50f, "Texture2D", "UFOSmasher", "SpikedBall_Large.png"),
            AssetLoader.SpriteFromMod(this, Vector2.one/2f, 1f, "Texture2D", "UFOSmasher", "SpikedBall_Small.png")
            ], [
                "Nightmares/FissureGlow",
                "MissleShuffleStrike/Rocket",
                "UFOSmasher/SpikedBall_Large",
                "UFOSmasher/SpikedBall_Small"
                ]);
        #endregion
        #region TRAFFIC TROUBLE HORNS
        List<SoundObject> sounds = new List<SoundObject>();
        foreach (var sound in Directory.GetFiles(Path.Combine(AssetLoader.GetModPath(this), "AudioClip", "TrafficTrouble", "Horns"), "*.wav"))
        {
            var hornSound = ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromFile(sound), "Sfx_TrafficTrouble_Horn", SoundType.Effect, Color.white);
            sounds.Add(hornSound);
        }
        assets.Add("TrafficTrouble/Horns", sounds);
        #endregion
        #region SPIKED BALL
        float sprSize = 12f * 2f;
        assets.AddRange<Sprite[]>([
            AssetLoader.SpritesFromSpritesheet(6, 1, sprSize, Vector2.one / 2f, AssetLoader.TextureFromMod(this, "Texture2D", "UFOSmasher", "spikedball_0.png")),
            AssetLoader.SpritesFromSpritesheet(6, 1, sprSize, Vector2.one / 2f, AssetLoader.TextureFromMod(this, "Texture2D", "UFOSmasher", "spikedball_30.png")),
            AssetLoader.SpritesFromSpritesheet(6, 1, sprSize, Vector2.one / 2f, AssetLoader.TextureFromMod(this, "Texture2D", "UFOSmasher", "spikedball_60.png")),
            AssetLoader.SpritesFromSpritesheet(6, 1, sprSize, Vector2.one / 2f, AssetLoader.TextureFromMod(this, "Texture2D", "UFOSmasher", "spikedball_90.png")),
            AssetLoader.SpritesFromSpritesheet(6, 1, sprSize, Vector2.one / 2f, AssetLoader.TextureFromMod(this, "Texture2D", "UFOSmasher", "spikedball_120.png")),
            AssetLoader.SpritesFromSpritesheet(6, 1, sprSize, Vector2.one / 2f, AssetLoader.TextureFromMod(this, "Texture2D", "UFOSmasher", "spikedball_150.png")),
            AssetLoader.SpritesFromSpritesheet(6, 1, sprSize, Vector2.one / 2f, AssetLoader.TextureFromMod(this, "Texture2D", "UFOSmasher", "spikedball_180.png")),
            AssetLoader.SpritesFromSpritesheet(6, 1, sprSize, Vector2.one / 2f, AssetLoader.TextureFromMod(this, "Texture2D", "UFOSmasher", "spikedball_210.png"))
            ], [
                "UFOSmasher/Dir1",
                "UFOSmasher/Dir2",
                "UFOSmasher/Dir3",
                "UFOSmasher/Dir4",
                "UFOSmasher/Dir5",
                "UFOSmasher/Dir6",
                "UFOSmasher/Dir7",
                "UFOSmasher/Dir8",
                ]);
        #endregion
        #region TOKEN OUTRUN THIEF
        float theifSize = 25f;
        assets.AddRange([
            AssetLoader.SpritesFromSpritesheet(7, 1, theifSize, Vector2.one / 2f, AssetLoader.TextureFromMod(this, "Texture2D", "TokenOutrun", "TOThiefBack.png")),
            AssetLoader.SpritesFromSpritesheet(7, 1, theifSize, Vector2.one / 2f, AssetLoader.TextureFromMod(this, "Texture2D", "TokenOutrun", "TOThiefFront.png")),
            ], ["TokenOutrun/ThiefBackfacing", "TokenOutrun/ThiefFrontfacing"]);
        #endregion
        #region MODELS
        var elv0 = Resources.FindObjectsOfTypeAll<Material>().Last(x => x.name == "El4"); // I AM ALMOST AT MY LIMIT, TILEBASE MATERIAL USES A DIFFERENT SHADER.
        var ufo = AssetLoader.ModelFromMod(this, "Models", "UFOSmashable.obj");
        var ufoMat = Instantiate(elv0);
        ufoMat.name = "UFOSmashable";
        ufoMat.SetMainTexture(AssetLoader.TextureFromMod(this, "Models", "UFOSmashable.png"));
        ufoMat.SetTexture("_LightGuide", AssetLoader.TextureFromMod(this, "Models", "UFOlightmap.png"));
        ufo.transform.GetChild(0).GetComponent<Renderer>().SetMaterial(ufoMat);
        ufo.ConvertToPrefab(true);
        assets.Add("UFOSmasher/UFOModel", ufo);
        var gnathouse = AssetLoader.ModelFromMod(this, "Models", "gnathouse.obj");
        gnathouse.transform.GetChild(0).GetComponent<Renderer>().SetMaterial(Resources.FindObjectsOfTypeAll<Material>().Last(x => x.name == "DarkWood"));
        gnathouse.ConvertToPrefab(true);
        assets.Add("GnatSwarm/HouseModel", gnathouse);
        var token = AssetLoader.ModelFromMod(this, "Models", "PlusToken.obj");
        token.ConvertToPrefab(true);
        var baseToken = Instantiate(elv0);
        baseToken.name = "YTPTokenGold";
        baseToken.SetMainTexture(AssetLoader.TextureFromMod(this, "Models", "PlusTokenGold.png"));
        var basicToken = Instantiate(elv0);
        basicToken.name = "YTPTokenGreen";
        basicToken.SetMainTexture(AssetLoader.TextureFromMod(this, "Models", "PlusTokenGreen.png"));
        var moderateToken = Instantiate(elv0);
        moderateToken.name = "YTPTokenSilver";
        moderateToken.SetMainTexture(AssetLoader.TextureFromMod(this, "Models", "PlusTokenSilver.png"));
        assets.AddRange([basicToken,  moderateToken, baseToken], ["TokenGreen", "TokenSilver", "TokenGold"]);
        token.transform.GetChild(0).GetComponent<Renderer>().SetMaterial(baseToken);
        assets.Add("TokenModel", token);
        #endregion
        if (Chainloader.PluginInfos.ContainsKey("mtm101.rulerp.baldiplus.levelstudio"))
        {
            string[] files = Directory.GetFiles(Path.Combine(AssetLoader.GetModPath(this), "Texture2D", "EditorUI"));
            for (int i = 0; i < files.Length; i++)
                assets.Add(Path.GetFileNameWithoutExtension(files[i]), AssetLoader.SpriteFromTexture2D(AssetLoader.TextureFromFile(files[i]), 1f));
        }
    }

    private static FieldInfo _eventJingleOverride = AccessTools.Field(typeof(RandomEvent), "eventJingleOverride");
    private static FieldInfo _eventIntro = AccessTools.Field(typeof(RandomEvent), "eventIntro");

    private IEnumerator PreLoad()
    {
        yield return 4;
        yield return "Self-inserting that person...";
        var abw = new SimpleBaldiTVCharacter(assets.GetAll<SoundObject>().Where(x => x.name.StartsWith("Vfx_ABW_")).ToList(), assets.Get<Sprite[]>("SelfInsertGuy"), 1.5f);
        BaldiTVExtensionHandler.AddCharacter("abw", abw);
        yield return "Initializing Regular Events";
        var _soundOnStart = AccessTools.DeclaredField(typeof(AudioManager), "soundOnStart");
        var _loopOnStart = AccessTools.DeclaredField(typeof(AudioManager), "loopOnStart");
        var abwJingle = assets.Get<SoundObject>("EventJingles/ABW");
        // Gnat Swarm
        var gnatSwarm = new RandomEventBuilder<GnatSwarm>(Info)
            .SetName("Event_GnatSwarm")
            .SetEnum("GnatSwarm")
            .SetMinMaxTime(150, 165)
            .SetJingle(abwJingle)
            .SetSound(assets.Get<SoundObject>("EventIntros/GnatSwarm"))
            .SetMeta(RandomEventFlags.AffectsGenerator)
            .Build();
        gnatSwarm.housePrefab = Instantiate(assets.Get<GameObject>("GnatSwarm/HouseModel").transform.GetChild(0).gameObject, MTM101BaldiDevAPI.prefabTransform, false);
        gnatSwarm.housePrefab.transform.localScale = Vector3.one * 2f;

        GnatEntity gnat = new NPCBuilder<GnatEntity>(Info)
            .SetName("Gnat")
            .SetEnum(Character.Null)
            .AddTrigger()
            .AddLooker()
            .SetAudioTimescaleType(TimeScaleType.Npc)
            .SetWanderEnterRooms()
            .SetAirborne()
            .EnableAcceleration()
            .Build();
        gnat.attack = assets.Get<SoundObject>("GnatSwarm/Gnattack");
        gnat.spriteRenderer[0].color = Color.clear;
        gnat.spriteRenderer = gnat.spriteRenderer.AddToArray(Instantiate(gnat.spriteRenderer[0], gnat.spriteBase.transform, false));
        gnat.spriteRenderer[1].gameObject.name = "Clouds";
        {
            var particle = gnat.spriteRenderer[0].gameObject.AddComponent<ParticleSystem>();
            var main = particle.main;

            main.duration = 0.5f;
            main.maxParticles = 555;
            main.startLifetime = 0.1f;
            main.startSpeed = -95f;
            main.startSize = 0.1f;
            //main.startRotation = 10f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.playOnAwake = true;
            main.startColor = Color.black;
            main.simulationSpeed = 1f;
            main.loop = true;
            main.emitterVelocityMode = ParticleSystemEmitterVelocityMode.Rigidbody;
            main.cullingMode = ParticleSystemCullingMode.Automatic;
            main.ringBufferMode = ParticleSystemRingBufferMode.Disabled;

            var emission = particle.emission;
            emission.rateOverTime = 55f;

            var shape = particle.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 5f;
            shape.radiusThickness = 0f;
            shape.radiusSpread = 5f;
            shape.radiusMode = ParticleSystemShapeMultiModeValue.Random;
            shape.scale = Vector3.one;

            var render = particle.gameObject.GetComponent<ParticleSystemRenderer>();
            render.renderMode = ParticleSystemRenderMode.Billboard;
            var particlemat = Instantiate(Resources.FindObjectsOfTypeAll<Material>().ToList().Find(x => x.name == "DustTest"));
            particlemat.name = "Gnat";
            particlemat.SetMainTexture(Resources.FindObjectsOfTypeAll<Material>().ToList().Find(x => x.name == "BlackTexture").mainTexture);
            render.SetMaterial(particlemat);
            render.normalDirection = 1f;
            render.sortMode = ParticleSystemSortMode.Distance;
            render.alignment = ParticleSystemRenderSpace.View;
            render.minParticleSize = 0f;
            render.maxParticleSize = 0.5f;
            render.SetActiveVertexStreams([ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Normal, ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV, ParticleSystemVertexStream.UV2, ParticleSystemVertexStream.Center]);

            var alphaTime = particle.colorOverLifetime;
            alphaTime.enabled = false;
        }
        {
            var particle = gnat.spriteRenderer[1].gameObject.AddComponent<ParticleSystem>();
            var main = particle.main;

            main.duration = 1f;
            main.maxParticles = 35;
            main.startLifetime = 0.5f;
            main.startSpeed = -10f;
            main.startSize = 1f;
            //main.startRotation = 10f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.playOnAwake = true;
            main.startColor = Color.white;
            main.simulationSpeed = 1f;
            main.loop = true;
            main.emitterVelocityMode = ParticleSystemEmitterVelocityMode.Rigidbody;
            main.cullingMode = ParticleSystemCullingMode.Automatic;
            main.ringBufferMode = ParticleSystemRingBufferMode.Disabled;

            var emission = particle.emission;
            emission.rateOverTime = 15f;

            var shape = particle.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 2.5f;
            shape.radiusThickness = 0f;
            shape.radiusSpread = 2.5f;
            shape.radiusMode = ParticleSystemShapeMultiModeValue.Random;
            shape.scale = Vector3.one;

            var render = particle.gameObject.GetComponent<ParticleSystemRenderer>();
            render.renderMode = ParticleSystemRenderMode.Billboard;
            render.SetMaterial(Resources.FindObjectsOfTypeAll<Material>().ToList().Find(x => x.name == "DustTest"));
            render.normalDirection = 1f;
            render.sortMode = ParticleSystemSortMode.Distance;
            render.alignment = ParticleSystemRenderSpace.View;
            render.minParticleSize = 0f;
            render.maxParticleSize = 0.5f;
            render.SetActiveVertexStreams([ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Normal, ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV, ParticleSystemVertexStream.UV2, ParticleSystemVertexStream.Center]);

            var alphaTime = particle.colorOverLifetime;
            alphaTime.color = new ParticleSystem.MinMaxGradient(new Gradient()
            {
                colorKeys = [
                    new GradientColorKey()
                    {
                        color = Color.white,
                        time = 0f
                    },
                    new GradientColorKey()
                    {
                        color = Color.white,
                        time = 1f
                    }
                    ],
                alphaKeys = [
                    new GradientAlphaKey()
                    {
                        alpha = 1f,
                        time = 0f
                    },
                    new GradientAlphaKey()
                    {
                        alpha = 0f,
                        time = 1f
                    }
                    ]
            });
            alphaTime.enabled = true;

            gnat.clouds = particle;
        }
        gnat.audMan = gnat.gameObject.GetComponent<AudioManager>();

        PropagatedAudioManager gnatThing = gnat.gameObject.AddComponent<PropagatedAudioManager>();
        gnatThing.audioDevice = gnat.gameObject.AddComponent<AudioSource>();
        gnatThing.audioDevice.spatialBlend = 1;
        gnatThing.audioDevice.rolloffMode = AudioRolloffMode.Custom;
        gnatThing.audioDevice.maxDistance = 150;
        gnatThing.audioDevice.dopplerLevel = 0;
        gnatThing.audioDevice.spread = 0;
        gnatThing.ReflectionSetVariable("maxDistance", 250f);
        _soundOnStart.SetValue(gnatThing, new SoundObject[]
            {
                assets.Get<SoundObject>("GnatSwarm/Gnat")
            });
        _loopOnStart.SetValue(gnatThing, true);

        var lookerLayerMask = AccessTools.DeclaredField(typeof(Looker), "layerMask");
        lookerLayerMask.SetValue(gnat.looker, (LayerMask)LayerMask.GetMask("Default", "Block Raycast", "Player", "NPCs", "Windows"));

        gnatSwarm.gnatPrefab = gnat;

        // Traffic Trouble
        var trafficTrouble = new RandomEventBuilder<TrafficTroubleEvent>(Info)
            .SetName("Event_TrafficTrouble")
            .SetEnum("TrafficTrouble")
            .SetMinMaxTime(120, 125)
            .SetJingle(abwJingle)
            .SetSound(assets.Get<SoundObject>("EventIntros/TrafficTrouble"))
            .SetMeta(RandomEventFlags.AffectsGenerator)
            .Build();
        trafficTrouble.roadPrefab = Instantiate(Resources.FindObjectsOfTypeAll<PowerLeverCable>().Last(), MTM101BaldiDevAPI.prefabTransform).gameObject;
        DestroyImmediate(trafficTrouble.roadPrefab.GetComponent<PowerLeverCable>());
        DestroyImmediate(trafficTrouble.roadPrefab.transform.GetChild(0).GetComponent<MeshCollider>());
        trafficTrouble.roadPrefab.transform.GetChild(0).position += Vector3.down * 10f;
        trafficTrouble.roadPrefab.transform.GetChild(0).rotation = Quaternion.Euler(90f, 0f, 0f);
        var trafficTunnel = Instantiate(Resources.FindObjectsOfTypeAll<StandardDoor>().Last(), MTM101BaldiDevAPI.prefabTransform).gameObject.AddComponent<TrafficTroubleTunnel>();
        trafficTunnel.gameObject.name = "TrafficTroubleTunnel";
        var otherdoor = trafficTunnel.GetComponent<StandardDoor>();
        trafficTunnel.walls = otherdoor.doors;
        DestroyImmediate(otherdoor);
        var window = Resources.FindObjectsOfTypeAll<Window>().Last();
        trafficTunnel.mapSprite = window.ReflectionGetVariable("mapClosedSprite") as Sprite;
        Material tunnelMat = Instantiate(Resources.FindObjectsOfTypeAll<WindowObject>().Last().overlay[0]);
        tunnelMat.name = "TTTunnelOverlay";
        tunnelMat.SetMainTexture(assets.Get<Texture2D>("TrafficTrouble/Tunnel"));
        trafficTunnel.overlayOpen = [tunnelMat, tunnelMat];
        trafficTunnel.overlayShut = trafficTunnel.overlayOpen;
        Material tunnelMask = Instantiate(Resources.FindObjectsOfTypeAll<WindowObject>().Last().mask);
        tunnelMask.name = "TTTunnelMask";
        tunnelMask.SetMaskTexture(assets.Get<Texture2D>("TrafficTrouble/TunnelMask"));
        trafficTunnel.mask = [tunnelMask, tunnelMask];
        trafficTunnel.makesNoise = false;
        trafficTrouble.tunnel = trafficTunnel;

        TrafficTroubleCar trafficCar = new NPCBuilder<TrafficTroubleCar>(Info)
            .SetName("Traffic Trouble Car")
            .SetEnum(Character.Null)
            .SetAudioTimescaleType(TimeScaleType.Environment)
            .AddTrigger()
            .AddLooker()
            .SetFOV(40f)
            .SetMaxSightDistance(1000f)
            .IgnorePlayerVisibility()
            .SetMinMaxAudioDistance(5f, 500f) // It's that loud!!
            .EnableAcceleration()
            .Build();
        trafficCar.audMan = trafficCar.GetComponent<AudioManager>();
        trafficCar.push = assets.Get<SoundObject>("TrafficTrouble/CarImpact");
        trafficCar.horn = assets.Get<List<SoundObject>>("TrafficTrouble/Horns").ToArray();
        
        lookerLayerMask.SetValue(trafficCar.looker, (LayerMask)LayerMask.GetMask("Default", "Block Raycast", "Player", "NPCs", "Windows"));
        trafficCar.Navigator.maxSpeed = 35f;
        trafficCar.Navigator.accel = 35f;
        trafficPath = EnumExtensions.ExtendEnum<PassableObstacle>("TrafficTroubleRoad");
        trafficCar.Navigator.passableObstacles.AddRange([trafficPath, PassableObstacle.Bully, PassableObstacle.LockedDoor]);
        PropagatedAudioManager motorAudMan = trafficCar.gameObject.AddComponent<PropagatedAudioManager>();
        motorAudMan.audioDevice = trafficCar.gameObject.AddComponent<AudioSource>();
        motorAudMan.audioDevice.spatialBlend = 1;
        motorAudMan.audioDevice.rolloffMode = AudioRolloffMode.Custom;
        motorAudMan.audioDevice.maxDistance = 150;
        motorAudMan.audioDevice.dopplerLevel = 0;
        motorAudMan.audioDevice.spread = 0;
        motorAudMan.ReflectionSetVariable("maxDistance", 250f);
        _soundOnStart.SetValue(motorAudMan, new SoundObject[]
            {
                Resources.FindObjectsOfTypeAll<SoundObject>().Last(x => x.name == "BusLoop")
            });
        _loopOnStart.SetValue(motorAudMan, true);
        trafficCar.motorAudMan = motorAudMan;
        
        trafficCar.deathSignal = Resources.FindObjectsOfTypeAll<QuickExplosion>().Last(x => x.name == "QuickExplosion");
        AnimatedSpriteRotator roatingSprite = trafficCar.spriteBase.AddComponent<AnimatedSpriteRotator>();
        var _renderer = AccessTools.DeclaredField(typeof(AnimatedSpriteRotator), "renderer");
        _renderer.SetValue(roatingSprite, trafficCar.spriteRenderer[0]);
        var sheets = assets.Get<List<Sprite[]>>("TrafficTrouble/CarSheets");
        var _spriteSheet = AccessTools.DeclaredField(typeof(SpriteRotationMap), "spriteSheet");
        foreach (var _sheet in sheets)
        {
            SpriteRotationMap idleRotationMap = new SpriteRotationMap()
            {
                angleCount = 12,
            };
            _spriteSheet.SetValue(idleRotationMap, _sheet);
            trafficCar.sheetSelection.Add(idleRotationMap);
        }
        var sheet = AccessTools.DeclaredField(typeof(AnimatedSpriteRotator), "spriteMap");
        sheet.SetValue(roatingSprite, new SpriteRotationMap[] { trafficCar.sheetSelection[0] });
        trafficCar.rotatorMan = roatingSprite;
        trafficCar.spriteBase.transform.position = Vector3.down * 0.75f;

        trafficTrouble.trafficCarPrefab = trafficCar;

        Material roadPre = trafficTrouble.roadPrefab.transform.GetChild(0).GetComponent<MeshRenderer>().material;
        var road = Instantiate(roadPre);
        road.name = "TT_StraightRoad";
        road.SetMainTexture(assets.Get<Texture2D>("TrafficTrouble/Roads/Straight"));
        trafficTrouble.roads[5] = road;
        trafficTrouble.roads[10] = road;
        road = Instantiate(roadPre);
        road.name = "TT_OpenRoad";
        road.SetMainTexture(assets.Get<Texture2D>("TrafficTrouble/Roads/Open"));
        trafficTrouble.roads[0] = road;
        road = Instantiate(roadPre);
        road.name = "TT_CornerRoad";
        road.SetMainTexture(assets.Get<Texture2D>("TrafficTrouble/Roads/Corner"));
        trafficTrouble.roads[3] = road;
        trafficTrouble.roads[6] = road;
        trafficTrouble.roads[9] = road;
        trafficTrouble.roads[12] = road;
        road = Instantiate(roadPre);
        road.name = "TT_ThreeWayRoad";
        road.SetMainTexture(assets.Get<Texture2D>("TrafficTrouble/Roads/ThreeWay"));
        trafficTrouble.roads[1] = road;
        trafficTrouble.roads[2] = road;
        trafficTrouble.roads[4] = road;
        trafficTrouble.roads[8] = road;
        road = Instantiate(roadPre);
        road.name = "TT_EndRoad";
        road.SetMainTexture(assets.Get<Texture2D>("TrafficTrouble/Roads/End"));
        trafficTrouble.roads[7] = road;
        trafficTrouble.roads[11] = road;
        trafficTrouble.roads[13] = road;
        trafficTrouble.roads[14] = road;

        // Prevent null crash
        var mewindow = Resources.FindObjectsOfTypeAll<Material>().Last(x => x.name == "WindowTexture");
        trafficTrouble.roads[15] = mewindow;
        //

        // Nightmares
        var insanity = new RandomEventBuilder<NightmaresEvent>(Info)
            .SetName("Event_Nightmares")
            .SetEnum("InsanityNightmare")
            .SetJingle(assets.Get<SoundObject>("EventJingles/Nightmares"))
            .SetSound(assets.Get<SoundObject>("EventIntros/Nightmares"))
            .SetMinMaxTime(165f, 255f)
            .SetMeta(RandomEventFlags.AffectsGenerator)
            .Build();
        insanity.dawn = assets.Get<SoundObject>("Nightmares/PhaseChange");

        var _collisionLayerMask = AccessTools.DeclaredField(typeof(Entity), "collisionLayerMask");
        NightmareEntity nightmare = new NPCBuilder<NightmareEntity>(Info)
            .SetName("Crawling Horror")
            .SetEnum(Character.Null)
            .SetAirborne()
            .AddLooker()
            .AddTrigger()
            .SetWanderEnterRooms()
            .SetForcedSubtitleColor(Color.gray - new Color(0f, 0f, 0f, 0.5f))
            .SetAudioTimescaleType(TimeScaleType.Null)
            .Build();
        nightmare.audMan = nightmare.gameObject.AddComponent<AudioManager>();
        nightmare.audMan.audioDevice = nightmare.GetComponent<AudioSource>();
        DestroyImmediate(nightmare.GetComponent<PropagatedAudioManager>());
        nightmare.audMan.volumeModifier = 0.65f;
        nightmare.gameObject.AddComponent<AudioReverbFilter>().reverbPreset = AudioReverbPreset.Psychotic;
        nightmare.gameObject.AddComponent<AudioEchoFilter>().delay = 0.1f;
        nightmare.spriteRenderer[0].sprite = NPCMetaStorage.Instance.Get(Character.Cumulo).value.spriteRenderer[0].sprite;
        nightmare.Navigator.SetSpeed(10f);
        nightmare.Navigator.maxSpeed = 10f;
        nightmare.spriteRenderer[0].color = new Color(0f, 0f, 0f, 0.55f);
        _collisionLayerMask.SetValue(nightmare.Navigator.Entity, (LayerMask)0);
        lookerLayerMask.SetValue(nightmare.looker, (LayerMask)LayerMask.NameToLayer("Player"));
        nightmare.spriteRenderer[0].gameObject.layer = LayerMask.NameToLayer("Overlay");

        insanity.nightmares.Add(nightmare);

        NightmareEntity terror = new NPCBuilder<NightmareEntity>(Info)
            .SetName("Terrorbeak")
            .SetEnum(Character.Null)
            .SetAirborne()
            .AddLooker()
            .AddTrigger()
            .SetWanderEnterRooms()
            .SetForcedSubtitleColor(Color.gray - new Color(0f, 0f, 0f, 0.5f))
            .SetAudioTimescaleType(TimeScaleType.Null)
            .Build();
        terror.audMan = terror.gameObject.AddComponent<AudioManager>();
        terror.audMan.audioDevice = terror.GetComponent<AudioSource>();
        terror.audMan.volumeModifier = 0.65f;
        DestroyImmediate(terror.GetComponent<PropagatedAudioManager>());
        terror.gameObject.AddComponent<AudioReverbFilter>().reverbPreset = AudioReverbPreset.Psychotic;
        terror.gameObject.AddComponent<AudioEchoFilter>().delay = 0.1f;
        terror.nightmareType = NightmareType.Terror;
        terror.spriteRenderer[0].sprite = NPCMetaStorage.Instance.Get(Character.Crafters).value.spriteRenderer[0].sprite;
        terror.Navigator.SetSpeed(35f);
        terror.Navigator.maxSpeed = 35f;
        terror.spriteRenderer[0].color = new Color(0f, 0f, 0f, 0.55f);
        _collisionLayerMask.SetValue(terror.Navigator.Entity, (LayerMask)0);
        lookerLayerMask.SetValue(terror.looker, (LayerMask)LayerMask.NameToLayer("Player"));
        terror.spriteRenderer[0].gameObject.layer = LayerMask.NameToLayer("Overlay");
        terror.spriteBase.transform.position = Vector3.up * 1.5f;

        insanity.nightmares.Add(terror);

        insanity.fissuresPre = Instantiate(trafficTrouble.roadPrefab, MTM101BaldiDevAPI.prefabTransform).AddComponent<NightmareFissures>();
        insanity.fissuresPre.gameObject.name = "NightmareFissures";
        insanity.fissuresPre.emerge = assets.Get<SoundObject>("Nightmares/SpawnerOpen");
        insanity.fissuresPre.emergeWarning = assets.Get<SoundObject>("Nightmares/SpawnerWarningOpen");
        insanity.fissuresPre.demerge = assets.Get<SoundObject>("Nightmares/SpawnerClose");
        insanity.fissuresPre.fx = assets.Get<SoundObject>("Nightmares/SpawnerFX");
        insanity.fissuresPre.audMan = insanity.fissuresPre.gameObject.AddComponent<PropagatedAudioManager>();
        insanity.fissuresPre.audMan.audioDevice = insanity.fissuresPre.gameObject.AddComponent<AudioSource>();
        insanity.fissuresPre.audMan.audioDevice.spatialBlend = 1;
        insanity.fissuresPre.audMan.audioDevice.rolloffMode = AudioRolloffMode.Linear;
        insanity.fissuresPre.audMan.audioDevice.maxDistance = 200;
        insanity.fissuresPre.audMan.audioDevice.dopplerLevel = 0;
        insanity.fissuresPre.audMan.audioDevice.spread = 0;
        var propfissures = insanity.fissuresPre.audMan as PropagatedAudioManager;
        var timescaleAud = AccessTools.DeclaredField(typeof(PropagatedAudioManager), "pitchTimeScaleType");
        timescaleAud.SetValue(propfissures, TimeScaleType.Null);
        propfissures.ReflectionSetVariable("minDistance", 0);
        propfissures.ReflectionSetVariable("maxDistance", 50);
        var fissureMat = Instantiate(roadPre);
        fissureMat.name = "NightmareFissureMat";
        fissureMat.SetMainTexture(assets.Get<Texture2D>("Nightmares/Fissure"));
        insanity.fissuresPre.transform.GetChild(0).GetComponent<MeshRenderer>().SetMaterial(fissureMat);
        insanity.fissuresPre.transform.GetChild(0).localPosition = Vector3.up * 0.01f;
        var fissureSprite = new GameObject("SpriteRender", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
        fissureSprite.transform.SetParent(insanity.fissuresPre.transform, false);
        fissureSprite.sprite = assets.Get<Sprite>("Nightmares/FissureGlow");
        fissureSprite.SetMaterial(Resources.FindObjectsOfTypeAll<Material>().Last(x => x.name == "SpriteStandard_Billboard"));
        fissureSprite.gameObject.layer = LayerMask.NameToLayer("Billboard");
        fissureSprite.transform.localPosition = Vector3.up * 5f;
        fissureSprite.transform.localScale = Vector3.one * 10f;
        insanity.fissuresPre.glowyThing = fissureSprite;
        var rendercontainer = insanity.fissuresPre.GetComponent<RendererContainer>();
        rendercontainer.renderers = rendercontainer.renderers.AddToArray(fissureSprite);

        insanity.audMan = Instantiate(Resources.FindObjectsOfTypeAll<CoreGameManager>().Last().musicMan, insanity.transform, false);

        // Missle Strike Shuffle
        MissleStrikeShuffleEvent missleShuffle = new RandomEventBuilder<MissleStrikeShuffleEvent>(Info)
            .SetName("Event_MissleStrikeShuffle")
            .SetEnum("MissleStrikeShuffle")
            .SetJingle(abwJingle)
            .SetSound(assets.Get<SoundObject>("EventIntros/MissleStrikeShuffle"))
            .SetMinMaxTime(140f, 175f)
            .SetMeta(RandomEventFlags.Permanent)
            .Build();
        missleShuffle.ufo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        missleShuffle.ufo.GetComponent<Renderer>().SetMaterial(Resources.FindObjectsOfTypeAll<Material>().Last(x => x.name == "Vent_Base"));
        missleShuffle.ufo.gameObject.name = "Unidentified Satellite";
        missleShuffle.ufo.transform.localScale = new Vector3(10f, 1f, 10f);
        DestroyImmediate(missleShuffle.ufo.GetComponent<Collider>());
        missleShuffle.ufo.ConvertToPrefab(true);

        MissleStrikeShuffleGuy missleguy = new NPCBuilder<MissleStrikeShuffleGuy>(Info)
            .SetName("Missle Strike Shuffle Guy")
            .SetEnum(Character.Null)
            .SetAirborne()
            .SetAudioTimescaleType(TimeScaleType.Npc)
            .DisableAutoRotation()
            .AddLooker()
            .Build();
        _collisionLayerMask.SetValue(missleguy.Navigator.Entity, (LayerMask)0); // Flying guy
        lookerLayerMask.SetValue(missleguy.looker, (LayerMask)LayerMask.NameToLayer("Player")); // X-Ray unlike being an imaginary monster
        missleguy.Navigator.SetSpeed(20f);
        missleguy.Navigator.maxSpeed = 20f;
        missleguy.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast B");
        var rotatormissle = missleguy.spriteBase.AddComponent<AnimatedSpriteRotator>();
        _renderer.SetValue(rotatormissle, missleguy.spriteRenderer[0]);
        var rotatormapagain = sheet.GetValue(NPCMetaStorage.Instance.Get(Character.DrReflex).value.spriteBase.GetComponentInChildren<AnimatedSpriteRotator>()) as SpriteRotationMap[]; // Secret Dr. Reflex Clone
        sheet.SetValue(rotatormissle, new SpriteRotationMap[] { rotatormapagain[0] });

        var strike = new GameObject("Missle Shuffle Strike", typeof(MissleStrikeImpact), typeof(CapsuleCollider)).GetComponent<MissleStrikeImpact>();
        strike.gameObject.ConvertToPrefab(true);
        strike.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast B");
        strike.audMan = strike.gameObject.AddComponent<AudioManager>();
        strike.audMan.audioDevice = strike.gameObject.AddComponent<AudioSource>();
        strike.audMan.audioDevice.spatialBlend = 1;
        strike.audMan.audioDevice.rolloffMode = AudioRolloffMode.Linear;
        strike.audMan.audioDevice.maxDistance = 200;
        strike.audMan.audioDevice.dopplerLevel = 0;
        strike.audMan.audioDevice.spread = 0;

        strike.indication = new GameObject("Indication", typeof(MeshFilter), typeof(MeshRenderer));
        strike.indication.transform.SetParent(strike.transform, false);
        strike.indication.layer = LayerMask.NameToLayer("Ignore Raycast B");
        var leftover = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        strike.indication.GetComponent<MeshFilter>().mesh = leftover.GetComponent<MeshFilter>().mesh;
        DestroyImmediate(leftover);
        strike.indication.transform.localPosition = Vector3.down * 5f;
        var indicMat = Instantiate(Resources.FindObjectsOfTypeAll<Material>().First(x => x.name == "Wind"));
        indicMat.SetMainTexture(assets.Get<Texture2D>("MissleShuffleStrike/Indication"));
        strike.indication.GetComponent<MeshRenderer>().SetMaterial(indicMat);
        strike.indication.transform.localScale = Vector3.zero;
        leftover = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        leftover.transform.SetParent(strike.transform, false);
        leftover.layer = LayerMask.NameToLayer("Ignore Raycast B");
        DestroyImmediate(leftover.GetComponent<Collider>());
        var flames = Instantiate(Resources.FindObjectsOfTypeAll<Material>().Last(x => x.name == "FloodWater_Transparent"));
        flames.SetMainTexture(assets.Get<Texture2D>("MissleShuffleStrike/Flames"));
        flames.SetVector("_Tiling", new Vector4(1f, 1f, 0f, 0f));
        leftover.GetComponent<MeshRenderer>().SetMaterial(flames);
        leftover.transform.localScale = new Vector3(1f, 0f, 1f);
        strike.impact = leftover;
        leftover.SetActive(false);

        var rocket = new GameObject("Missle Strike", typeof(SpriteRenderer));
        rocket.ConvertToPrefab(true);
        rocket.GetComponent<SpriteRenderer>().sprite = assets.Get<Sprite>("MissleShuffleStrike/Rocket");
        rocket.layer = LayerMask.NameToLayer("Billboard");

        missleguy.rocketPre = rocket;
        strike.rocket = Instantiate(rocket, strike.transform, false);
        strike.rocket.transform.localRotation = Quaternion.Euler(Vector3.forward * 180f);
        strike.rocket.SetActive(false);

        var collider = strike.GetComponent<CapsuleCollider>();
        collider.isTrigger = true;
        collider.height = 45f; // Explains why below the amount (which assigned is 30) will fuck up the capsule collider.
        collider.radius = 1.5f;
        collider.center = Vector3.zero;

        strike.incomingPre = assets.Get<SoundObject>("MissleStrikeShuffle/IncomingPre");
        strike.incoming = assets.Get<SoundObject>("MissleStrikeShuffle/Incoming");
        strike.explosion = assets.Get<SoundObject>("MissleStrikeShuffle/Exploded");

        missleguy.strikePre = strike;

        missleShuffle.guyPre = missleguy;

        yield return "Initializing Hyper Events";
        var hyperJingle = assets.Get<SoundObject>("EventJingles/Hyper");
        var abwhyperJingle = assets.Get<SoundObject>("EventJingles/HyperABW");
        // Gravity Chaos
        GravityEvent gravityHyperEvent = Instantiate(Resources.FindObjectsOfTypeAll<GravityEvent>().Last(), MTM101BaldiDevAPI.prefabTransform);
        gravityHyperEvent.gameObject.name = "Hyper " + gravityHyperEvent.gameObject.name.Replace("(Clone)","");
        gravityHyperEvent.ReflectionSetVariable("maxRespawnTime", 15);
        gravityHyperEvent.ReflectionSetVariable("minFlippers", 75);
        gravityHyperEvent.ReflectionSetVariable("maxFlippers", 95);
        gravityHyperEvent.ReflectionSetVariable("maxRespawnTime", 21);
        gravityHyperEvent.MarkAsCrazy();
        //RandomEventMetaStorage.Instance.Add(new RandomEventMetadata(Info, gravityHyperEvent, RandomEventFlags.Special));
        // Flood
        FloodEvent floodHyperEvent = Instantiate(Resources.FindObjectsOfTypeAll<FloodEvent>().Last(), MTM101BaldiDevAPI.prefabTransform);
        floodHyperEvent.gameObject.name = "Hyper " + floodHyperEvent.gameObject.name.Replace("(Clone)", "");
        floodHyperEvent.ReflectionSetVariable("minWhirlpools", 20);
        floodHyperEvent.ReflectionSetVariable("maxWhirlpools", 45);
        var _whirlpoolPre = AccessTools.Field(typeof(FloodEvent), "whirlpoolPre");
        var whirlpool = Instantiate((Whirlpool)_whirlpoolPre.GetValue(floodHyperEvent), MTM101BaldiDevAPI.prefabTransform);
        whirlpool.ReflectionSetVariable("maxForce", 55f);
        whirlpool.FormTime = 3f;
        whirlpool.ReflectionSetVariable("sinkSpeed", 1f);
        _whirlpoolPre.SetValue(floodHyperEvent, whirlpool);
        floodHyperEvent.MarkAsCrazy();
        //RandomEventMetaStorage.Instance.Add(new RandomEventMetadata(Info, floodHyperEvent, RandomEventFlags.Special));
        // Student Shuffle
        StudentEvent shuffleHyperEvent = Instantiate(Resources.FindObjectsOfTypeAll<StudentEvent>().Last(), MTM101BaldiDevAPI.prefabTransform);
        shuffleHyperEvent.gameObject.name = "Hyper " + shuffleHyperEvent.gameObject.name.Replace("(Clone)", "");
        shuffleHyperEvent.ReflectionSetVariable("minCrowdSize", 30);
        shuffleHyperEvent.ReflectionSetVariable("maxCrowdSize", 59);
        //shuffleHyperEvent.ReflectionSetVariable("spawnRate", 5f);
        shuffleHyperEvent.MarkAsCrazy();
        //RandomEventMetaStorage.Instance.Add(new RandomEventMetadata(Info, shuffleHyperEvent, RandomEventFlags.Special));
        // Balder Dash
        BalderEvent balderHyperEvent = Instantiate(Resources.FindObjectsOfTypeAll<BalderEvent>().Last(), MTM101BaldiDevAPI.prefabTransform);
        balderHyperEvent.gameObject.name = "Hyper " + balderHyperEvent.gameObject.name.Replace("(Clone)", "");
        balderHyperEvent.ReflectionSetVariable("spawnRate", 1f);
        var _balder = AccessTools.DeclaredField(typeof(BalderEvent), "balderPrefab");
        Balder_Entity balder = Instantiate(_balder.GetValue(balderHyperEvent) as Balder_Entity, MTM101BaldiDevAPI.prefabTransform);
        balder.gameObject.name = "Crazy Balder";
        balder.Navigator.maxSpeed = 70; // Regular speed is 35f
        _balder.SetValue(balderHyperEvent, balder);
        balderHyperEvent.MarkAsCrazy();
        //RandomEventMetaStorage.Instance.Add(new RandomEventMetadata(Info, balderHyperEvent, RandomEventFlags.Special));
        // Gnat Swarm
        GnatSwarm hyperGnats = Instantiate(gnatSwarm, MTM101BaldiDevAPI.prefabTransform);
        hyperGnats.gameObject.name = "Hyper " + hyperGnats.gameObject.name.Replace("(Clone)", "");
        hyperGnats.isHyper = true;
        hyperGnats.MarkAsCrazy();
        _eventJingleOverride.SetValue(hyperGnats, abwhyperJingle);
        _eventIntro.SetValue(hyperGnats, assets.Get<SoundObject>("EventIntros/HyperGnatSwarm"));

        // Traffic Trouble
        TrafficTroubleEvent hyperTraffic = Instantiate(trafficTrouble, MTM101BaldiDevAPI.prefabTransform);
        hyperTraffic.gameObject.name = "Hyper " + hyperTraffic.gameObject.name.Replace("(Clone)", "");
        hyperTraffic.trafficCarPrefab = Instantiate(hyperTraffic.trafficCarPrefab, MTM101BaldiDevAPI.prefabTransform);
        hyperTraffic.trafficCarPrefab.name = "Traffic Trouble Insane Car";
        //hyperTraffic.trafficCarPrefab.Navigator.passableObstacles.Remove(trafficPath);
        hyperTraffic.trafficCarPrefab.speed = 75f;
        hyperTraffic.trafficCarPrefab.Navigator.maxSpeed = 75f;
        hyperTraffic.trafficCarPrefab.Navigator.accel = 75f;
        hyperTraffic.timerInitial = 1f;
        hyperTraffic.minRoads = 10;
        hyperTraffic.maxRoads = 25;
        hyperTraffic.MarkAsCrazy();
        _eventJingleOverride.SetValue(hyperTraffic, abwhyperJingle);
        _eventIntro.SetValue(hyperTraffic, assets.Get<SoundObject>("EventIntros/HyperTrafficTrouble"));

        // Missle Strike Chaos
        MissleStrikeShuffleChaos chaos = new RandomEventBuilder<MissleStrikeShuffleChaos>(Info)
            .SetName("Missle Strike Chaos")
            .SetEnum("MissleStrikeShuffleGamemode")
            .SetMinMaxTime(0f, 0f)
            .SetMeta(RandomEventFlags.Permanent | RandomEventFlags.Special)
            .Build();
        chaos.ufo = missleShuffle.ufo;
        chaos.guyPre = missleguy;

        var gamemanager = new BaseGameManagerBuilder<MissleStrikeShuffleGameManager>()
            .SetObjectName("Missle Strike Chaos Game Manager")
            .SetNameKey("Mode_MissleStrikeShuffle")
            .SetNPCSpawnMode(GameManagerNPCAutomaticSpawn.Never)
            .SetLevelNumber(3)
            .Build();

        gamemanager.eventPre = chaos;
        gamemanager.pitstop = Resources.FindObjectsOfTypeAll<SceneObject>().Last(x => x.levelTitle == "PIT");

        var modescene = ScriptableObject.CreateInstance<SceneObject>();
        modescene.name = "MissleStrikeShuffleLevel";
        modescene.manager = gamemanager;
        modescene.skybox = Resources.FindObjectsOfTypeAll<Cubemap>().Last(x => x.name == "Cubemap_Void");
        modescene.skyboxColor = new Color(0.12f, 0.12f, 0.12f);
        modescene.levelTitle = "F5";
        modescene.levelNo = 4;
        modescene.nameKey = "Level_Main5";
        modescene.additionalNPCs = 0;
        modescene.potentialNPCs = [.. Resources.FindObjectsOfTypeAll<SceneObject>().Last(x => x.levelTitle == "F5").potentialNPCs]; // I can't... Recommended Characters mod kept on crashing because this list was empty, not it being null.
        modescene.totalShopItems = 9;
        modescene.previousLevels = [.. Resources.FindObjectsOfTypeAll<SceneObject>().Last(x => x.levelTitle == "F5").previousLevels];
        modescene.mapPrice = Resources.FindObjectsOfTypeAll<SceneObject>().Last(x => x.levelTitle == "F5").mapPrice;
        modescene.nextLevel = Resources.FindObjectsOfTypeAll<SceneObject>().Last(x => x.levelTitle == "F5").nextLevel;
        modescene.shopItems = (WeightedItemObject[])Resources.FindObjectsOfTypeAll<SceneObject>().Last(x => x.levelTitle == "F5").shopItems.Clone();
        // Assigning base because why the fuck is Criminal Pack being executed first?
        var level3 = Resources.FindObjectsOfTypeAll<SceneObject>().Last(x => x.levelTitle == "F3");
        var level5 = Resources.FindObjectsOfTypeAll<SceneObject>().Last(x => x.levelTitle == "F5");
        List<WeightedLevelObject> levels = new List<WeightedLevelObject>();
        levels.Add(new WeightedLevelObject()
        {
            selection = ((CustomLevelObject)level3.levelObject).MakeClone(),
            weight = 100
        });
        foreach (var _level in level5.randomizedLevelObject.Where(x => x.selection is CustomLevelObject))
            levels.Add(new WeightedLevelObject()
            {
                selection = ((CustomLevelObject)_level.selection).MakeClone(),
                weight = _level.weight
            });
        foreach (var level in levels)
            level.selection.name = level.selection.name.Replace("(Clone)", "") + "_Shuffle_Chaos";
        modescene.randomizedLevelObject = levels.ToArray();
        //
        modescene.MarkAsNeverUnload();
        modescene.AddMeta(this, ["missleshuffle"]);
        GeneratorManagement.EnqueueGeneratorChanges(modescene);

        var timeoutShuffle = new RandomEventBuilder<TimeOut_Shuffle>(Info)
            .SetName("TimeOut_Shuffle")
            .SetEnum("Null")
            .SetMinMaxTime(0f, 0f)
            .SetJingle(Resources.FindObjectsOfTypeAll<SoundObject>().Last(x => x.name == "TimeLimitBell"))
            .SetSound(Resources.FindObjectsOfTypeAll<SoundObject>().Last(x => x.name == "run"))
            .Build();
        RandomEventMetaStorage.Instance.Remove(timeoutShuffle.Type); // API does not account for custom time out events, I hate it and I made a custom time out event for B1.
        timeoutShuffle.ReflectionSetVariable("eventType", RandomEventType.TimeOut);

        GeneratorPatches.missleShuffleObject = modescene;

        yield return "Initializing Bonus Events";
        var bonusJingle = assets.Get<SoundObject>("EventJingles/Bonus");
        // Mystery??
        MysteryEvent mysteryEvent = new RandomEventBuilder<MysteryEvent>(Info)
            .SetName("Mystery Random Event")
            .SetEnum("MysteryRandomEvent")
            .SetMeta(RandomEventFlags.Special)
            .SetMinMaxTime(70, 120)
            .SetJingle(bonusJingle)
            .SetSound(assets.Get<SoundObject>("EventIntros/BonusRandomEvent"))
            .Build();

        // Token Outrun
        TokenOutrunEvent tokenOutrunEvent = new RandomEventBuilder<TokenOutrunEvent>(Info)
            .SetName("Token Outrun")
            .SetEnum("TokenOutrun")
            .SetMeta(RandomEventFlags.Special)
            .SetMinMaxTime(100, 110)
            .SetJingle(bonusJingle)
            .SetSound(assets.Get<SoundObject>("EventIntros/TokenOutrun"))
            .Build();
        TokenOutrunGuy guy = new NPCBuilder<TokenOutrunGuy>(Info)
            .SetAudioTimescaleType(TimeScaleType.Npc)
            .SetEnum(Character.Null)
            .SetName("Token Outrun Guy")
            .AddLooker()
            .AddHeatmap()
            .SetWanderEnterRooms()
            .SetMinMaxAudioDistance(0, 25)
            .DisableNavigationPrecision()
            .IgnorePlayerOnSpawn()
            .EnableAcceleration()
            .SetMaxSightDistance(1000f)
            .Build();
        var theifRot = guy.gameObject.AddComponent<AnimatedSpriteRotator>();
        _renderer.SetValue(theifRot, guy.spriteRenderer[0]);
        SpriteRotationMap theifRotationMap = new SpriteRotationMap()
        {
            angleCount = 2,
        };
        var front = assets.Get<Sprite[]>("TokenOutrun/ThiefFrontfacing"); // Front facing, used for the animator.
        var back = assets.Get<Sprite[]>("TokenOutrun/ThiefBackfacing");
        List<Sprite> theifSprites = new List<Sprite>();
        for (int i = 0; i < 6; i++)
        {
            theifSprites.AddRange([
                front[i], // Two sides??
                back[i]
                ]);
        }
        _spriteSheet.SetValue(theifRotationMap, theifSprites.ToArray());
        sheet.SetValue(theifRot, new SpriteRotationMap[] { theifRotationMap });
        guy.animator = guy.gameObject.AddComponent<CustomSpriteRotatorAnimator>();
        guy.animator.spriteRotator = theifRot;
        guy.animation = [.. front];
        guy.spriteRenderer[0].transform.localPosition = Vector3.down * 0.3f;
        tokenOutrunEvent.guyPrefab = guy;
        TokenOutrunToken tokenOutrunToken = new EntityBuilder()
            .SetName("Token Outrun Token")
            .AddTrigger(2f)
            .SetLayerCollisionMask(2113541)
            .SetLayer("CollidableEntities")
            .SetBaseRadius(2f)
            .AddRenderbaseFunction((entity) =>
            {
                Transform transform = new GameObject().transform;
                transform.name = "RenderBase";
                transform.transform.SetParent(entity.transform, false);
                transform.gameObject.layer = entity.gameObject.layer;
                var mesh = Instantiate(assets.Get<GameObject>("TokenModel").transform.GetChild(0).gameObject, transform, false);
                mesh.transform.localScale = Vector3.one;
                //mesh.transform.localRotation = Quaternion.Euler(Vector3.up * 90f); // Does not do anything??
                mesh.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast B");
                mesh.GetComponent<MeshRenderer>().SetMaterial(assets.Get<Material>("TokenGreen"));
                return transform;
            })
            .Build().gameObject.AddComponent<TokenOutrunToken>();
        tokenOutrunToken.entity = tokenOutrunToken.GetComponent<Entity>();
        tokenOutrunToken.audMan = tokenOutrunToken.gameObject.AddComponent<PropagatedAudioManager>();
        tokenOutrunToken.audMan.audioDevice = tokenOutrunToken.gameObject.AddComponent<AudioSource>();
        tokenOutrunToken.audMan.audioDevice.dopplerLevel = 0f;
        tokenOutrunToken.audMan.audioDevice.spread = 0f;
        tokenOutrunToken.audMan.audioDevice.spatialBlend = 1;
        tokenOutrunToken.audMan.audioDevice.rolloffMode = AudioRolloffMode.Linear;
        tokenOutrunToken.collected = Resources.FindObjectsOfTypeAll<SoundObject>().Last(x => x.name == "YTPPickup_0");
        tokenOutrunToken.render = tokenOutrunToken.transform.Find("RenderBase");

        guy.tokenPrefab = tokenOutrunToken;
        tokenOutrunToken.gameObject.SetActive(true);

        // UFO Smasher
        UFOSmasherEvent smasherEvent = new RandomEventBuilder<UFOSmasherEvent>(Info)
            .SetName("Event_UFOSmasher")
            .SetEnum("UFOSmasher")
            .SetMinMaxTime(135f, 179f)
            .SetMeta(RandomEventFlags.Special)
            .SetJingle(bonusJingle)
            .SetSound(assets.Get<SoundObject>("EventIntros/UFOSmasher"))
            .Build();
        UFOEntity ufoGuy = new NPCBuilder<UFOEntity>(Info)
            .SetName("UFO Entity")
            .SetEnum(Character.Null)
            .SetAirborne()
            .EnableAcceleration()
            .AddTrigger()
            .DisableNavigationPrecision()
            .SetAudioTimescaleType(TimeScaleType.Environment)
            .DisableAutoRotation()
            .Build();
        ufoGuy.Navigator.accel = 99f;
        ufoGuy.explodeThing = Resources.FindObjectsOfTypeAll<QuickExplosion>().Last(x => x.name == "QuickExplosion");
        ufoGuy.spawnSnd = assets.Get<SoundObject>("UFOSmasher/Spawn");
        ufoGuy.despawnSnd = assets.Get<SoundObject>("UFOSmasher/Despawn");
        ufoGuy.dieSnd = assets.Get<SoundObject>("UFOSmasher/Die");
        var prop = Instantiate(assets.Get<GameObject>("UFOSmasher/UFOModel").transform.GetChild(0), ufoGuy.spriteBase.transform, false);
        prop.transform.localPosition = Vector3.zero;
        prop.transform.localScale = Vector3.one * 3f;
        ufoGuy.spriteRenderer[0].transform.localPosition = Vector3.up * 0.5f;

        ufoGuy.audMan = ufoGuy.GetComponent<PropagatedAudioManager>();
        _soundOnStart.SetValue(ufoGuy.audMan, new SoundObject[]
            {
                assets.Get<SoundObject>("UFOSmasher/Idling")
            });
        _loopOnStart.SetValue(ufoGuy.audMan, true);
        ufoGuy.audMan.audioDevice.dopplerLevel = 1f;

        smasherEvent.ufoPrefab = ufoGuy;

        ITM_SpikedBall ballPrefab = Instantiate(ItemMetaStorage.Instance.FindByEnum(Items.Bsoda).value.item, MTM101BaldiDevAPI.prefabTransform).gameObject.AddComponent<ITM_SpikedBall>();
        DestroyImmediate(ballPrefab.GetComponent<ITM_BSODA>());
        ballPrefab.entity = ballPrefab.GetComponent<Entity>();
        ballPrefab.throwSnd = Resources.FindObjectsOfTypeAll<SoundObject>().Last(x => x.name == "CampfireToss");
        ballPrefab.impactSnd = assets.Get<SoundObject>("UFOSmasher/BallImpact");
        ballPrefab.damageSnd = assets.Get<SoundObject>("TrafficTrouble/CarImpact");
        ballPrefab.rollingSnd = assets.Get<SoundObject>("UFOSmasher/BallRolling");
        PropagatedAudioManager ballAudMan = ballPrefab.gameObject.AddComponent<PropagatedAudioManager>();
        ballAudMan.audioDevice = trafficCar.gameObject.AddComponent<AudioSource>();
        ballAudMan.audioDevice.spatialBlend = 1;
        ballAudMan.audioDevice.rolloffMode = AudioRolloffMode.Custom;
        ballAudMan.audioDevice.maxDistance = 150;
        ballAudMan.audioDevice.dopplerLevel = 0;
        ballAudMan.audioDevice.spread = 0;
        ballPrefab.audMan = ballAudMan;
        ballPrefab.transform.GetChild(0).transform.localScale = Vector3.one / 2f;
        ballPrefab.entity.GetComponent<CapsuleCollider>().height = 4f;
        ballPrefab.entity.GetComponent<SphereCollider>().radius = 2f;
        ballPrefab.gameObject.name = "ITM_SpikedBall";
        AnimatedSpriteRotator ballRot = ballPrefab.gameObject.AddComponent<AnimatedSpriteRotator>();
        _renderer.SetValue(ballRot, ballPrefab.transform.GetChild(0).GetComponentInChildren<SpriteRenderer>());
        SpriteRotationMap rollerRotationMap = new SpriteRotationMap()
        {
            angleCount = 8,
        };
        var dir1 = assets.Get<Sprite[]>("UFOSmasher/Dir1");
        var dir2 = assets.Get<Sprite[]>("UFOSmasher/Dir2");
        var dir3 = assets.Get<Sprite[]>("UFOSmasher/Dir3");
        var dir4 = assets.Get<Sprite[]>("UFOSmasher/Dir4");
        var dir5 = assets.Get<Sprite[]>("UFOSmasher/Dir5"); // FOR CODE VIEWERS: These are the front facing sprites that we are using for the custom animator.
        var dir6 = assets.Get<Sprite[]>("UFOSmasher/Dir6");
        var dir7 = assets.Get<Sprite[]>("UFOSmasher/Dir7");
        var dir8 = assets.Get<Sprite[]>("UFOSmasher/Dir8");
        List<Sprite> rollersprites = new List<Sprite>();
        for (int i = 0; i < 6; i++)
        {
            rollersprites.AddRange([
                dir5[i],
                dir6[i],
                dir7[i],
                dir8[i],
                dir1[i],
                dir2[i],
                dir3[i],
                dir4[i]
                ]);
        }
        _spriteSheet.SetValue(rollerRotationMap, rollersprites.ToArray());
        sheet.SetValue(ballRot, new SpriteRotationMap[] { rollerRotationMap });
        ballPrefab.animator = ballPrefab.gameObject.AddComponent<CustomSpriteRotatorAnimator>();
        ballPrefab.animator.spriteRotator = ballRot;
        /*ballPrefab.animator.LoadAnimations(new Dictionary<string, SpriteAnimation>()
        {
            { "rolling", new SpriteAnimation(24, dir5.ToArray()) }
        });
        ballPrefab.animator.timeScale = TimeScaleType.Environment;*/
        ballPrefab.animation = [.. dir5]; // The custom animation classes are not serialized tho...

        _collisionLayerMask.SetValue(ballPrefab.entity, (LayerMask)LayerMask.GetMask("Default", "Ignore Raycast", "Ignore Raycast B", "Windows"));

        SpikeBallFlinger flinger = new EntityBuilder()
            .SetName("SpikeBall Flinger")
            .SetBaseRadius(0.5f)
            .SetHeight(8f)
            .AddTrigger(2f) // It shouldn't be set to zero...
            .SetLayerCollisionMask(2113541) // There's no default in the EntityBuilder...
            .AddRenderbaseFunction((entity) =>
            {
                var thing = new GameObject("Nothing");
                thing.transform.SetParent(entity.transform, false);
                return thing.transform;
            })
            .SetLayer("StandardEntities")
            .Build().gameObject.AddComponent<SpikeBallFlinger>();
        flinger.self = flinger.GetComponent<Entity>();
        ballPrefab.flinger = flinger;

        UFOEntity.ytp = ItemMetaStorage.Instance.GetPointsObject(100, true);

        var largeSprite = assets.Get<Sprite>("UFOSmasher/SpikedBall_Large");
        var smallSprite = assets.Get<Sprite>("UFOSmasher/SpikedBall_Small");
        ItemObject ball = new ItemBuilder(Info)
            .SetNameAndDescription("Itm_SpikedBallBonus", "Desc_SpikedBallBonus")
            .SetEnum("SpikedBall")
            .SetSprites(smallSprite, largeSprite)
            .SetItemComponent(ballPrefab)
            .SetShopPrice(50)
            .SetMeta(ItemFlags.MultipleUse | ItemFlags.Persists | ItemFlags.CreatesEntity, ["abw_eventsoverload_isnotufosmasherloot"])
            .Build();

        for (int i = 0; i < ITM_SpikedBall.stacksItems.Length; i++)
        {
            var ballPrefabAgain = Instantiate(ballPrefab, MTM101BaldiDevAPI.prefabTransform);
            ItemObject ballAgain = new ItemBuilder(Info)
                .SetNameAndDescription("Itm_SpikedBallBonus", "Desc_SpikedBallBonus")
                .SetEnum("SpikedBall")
                .SetSprites(smallSprite, largeSprite)
                .SetItemComponent(ballPrefabAgain)
                .SetMeta(ball.GetMeta())
                .Build();
            ballPrefabAgain.stacks = i;
            ballPrefabAgain.gameObject.name = $"ITM_SpikedBall_{i}";
            ITM_SpikedBall.stacksItems[i] = ballAgain;
            if (i == 0)
                smasherEvent.spikedBall = ballAgain;
        }

        // Token Collector
        TokenCollectorEvent collectorEvent = new RandomEventBuilder<TokenCollectorEvent>(Info)
            .SetName("Event_TokenCollector")
            .SetEnum("TokenCollector")
            .SetMinMaxTime(120f, 141f)
            .SetJingle(bonusJingle)
            .SetSound(assets.Get<SoundObject>("EventIntros/TokenCollector"))
            .SetMeta(RandomEventFlags.Special)
            .Build();
        leftover = new GameObject("Token Collector Token");
        leftover.ConvertToPrefab(true);
        TokenCollectorToken tokenCollectorToken = leftover.AddComponent<TokenCollectorToken>();
        tokenCollectorToken.audMan = tokenCollectorToken.gameObject.AddComponent<PropagatedAudioManager>();
        tokenCollectorToken.audMan.audioDevice = tokenOutrunToken.gameObject.AddComponent<AudioSource>();
        tokenCollectorToken.audMan.audioDevice.dopplerLevel = 0f;
        tokenCollectorToken.audMan.audioDevice.spread = 0f;
        tokenCollectorToken.audMan.audioDevice.spatialBlend = 1;
        tokenCollectorToken.audMan.audioDevice.rolloffMode = AudioRolloffMode.Linear;
        var collectorRender = new GameObject("Base");
        collectorRender.transform.SetParent(tokenCollectorToken.transform, false);
        tokenCollectorToken.render = Instantiate(tokenOutrunToken.render.GetChild(0), collectorRender.transform, false).GetComponent<Renderer>();
        tokenCollectorToken.transform.localScale = Vector3.one * 3f;
        var tokenCollider = tokenCollectorToken.gameObject.AddComponent<SphereCollider>();
        tokenCollider.center = Vector3.zero;
        tokenCollider.radius = 0.85f;
        tokenCollider.isTrigger = true;
        collectorEvent.tokenPre = tokenCollectorToken;
        TokenCollectorEvent.tokenMaterialSets.AddRange(new()
        {
            { ItemMetaStorage.Instance.GetPointsObject(25, true), assets.Get<Material>("TokenGreen") },
            { ItemMetaStorage.Instance.GetPointsObject(50, true), assets.Get<Material>("TokenSilver") },
            { ItemMetaStorage.Instance.GetPointsObject(100, true), assets.Get<Material>("TokenGold") } 
        });
        collectorEvent.weightedYTPs.AddRange([
            new WeightedItemObject()
            {
                selection = ItemMetaStorage.Instance.GetPointsObject(25, true),
                weight = 150
            },
            new WeightedItemObject()
            {
                selection = ItemMetaStorage.Instance.GetPointsObject(50, true),
                weight = 45
            },
            new WeightedItemObject()
            {
                selection = ItemMetaStorage.Instance.GetPointsObject(100, true),
                weight = 10
            }
            ]);

        GeneratorManagement.Register(this, GenerationModType.Preparation, (title, num, scene) =>
        {
            var meta = scene.GetMeta();
            if (meta?.tags.Contains("missleshuffle") == true)
            {
                var level5 = Resources.FindObjectsOfTypeAll<SceneObject>().Last(x => x.levelTitle == "F5");
                List<WeightedLevelObject> levels = new List<WeightedLevelObject>();
                var leveltyped = scene.GetMeta().GetSupportedLevelTypes();
                foreach (var _level in level5.randomizedLevelObject.Where(x => x.selection is CustomLevelObject && !leveltyped.Contains(x.selection.type)))
                    levels.Add(new WeightedLevelObject()
                    {
                        selection = ((CustomLevelObject)_level.selection).MakeClone(),
                        weight = _level.weight
                    });
                foreach (var level in levels)
                    level.selection.name = level.selection.name.Replace("(Clone)", "") + "_Shuffle_Chaos";
                scene.randomizedLevelObject = scene.randomizedLevelObject.AddRangeToArray([.. levels]);
                scene.MarkAsNeverUnload();
            }    
        });
        GeneratorManagement.Register(this, GenerationModType.Base, (title, num, scene) =>
        {
            foreach (var level in scene.GetCustomLevelObjects())
            {
                level.SetCustomModValue(Info, "hyper_events", new List<HyperEventSelection>());
                level.SetCustomModValue(Info, "hyper_event_chance", 0.055f);
                level.SetCustomModValue(Info, "bonus_events", new List<WeightedRandomEvent>());
                level.MarkAsModifiedByMod(Info);
            }
        });
        GeneratorManagement.Register(this, GenerationModType.Addend, (title, num, scene) =>
        {
            foreach (var level in scene.GetCustomLevelObjects())
            {
                if ((title == "F2" && num == 1) || (title == "F4" && num == 3)
                || (title == "F3" && num == 2) || (title == "F5" && num == 4))
                    level.randomEvents.AddRange([
                        new()
                        {
                            selection = gnatSwarm,
                            weight = youtuberMode.Value ? 9999 : 100
                        },
                        new()
                        {
                            selection = insanity,
                            weight = youtuberMode.Value ? 9999 : 99
                        }
                        ]);
                if (((title == "F3" && num == 2) || (title == "F5" && num == 4)) && (level.type == LevelType.Schoolhouse || level.type == LevelType.Maintenance)) // Roads can go into special rooms, which will ruin wormhole room and conveyor room.
                    level.randomEvents.AddRange([
                    new()
                    {
                        selection = trafficTrouble,
                        weight = youtuberMode.Value ? 9999 : 120
                    }
                        ]);
                if (((title == "F3" && num == 2) || (title == "F5" && num == 4)) && level.type != LevelType.Maintenance) // Hiding too much I see?
                    level.randomEvents.AddRange([
                    new()
                    {
                        selection = missleShuffle,
                        weight = youtuberMode.Value ? 9999 : 100
                    }
                        ]);
                if (title == "F1" && num == 0)
                    level.SetCustomModValue(Info, "hyper_event_chance", 0f);
                else if (title == "F2" && num == 1)
                    level.SetCustomModValue(Info, "hyper_event_chance", 0.05f);
                else if (title == "F3" && num == 2)
                    level.SetCustomModValue(Info, "hyper_event_chance", 0.2f);
                else if (title == "F4" && num == 3)
                    level.SetCustomModValue(Info, "hyper_event_chance", 0.15f);
                else if (title == "F5" && num == 4)
                    level.SetCustomModValue(Info, "hyper_event_chance", 0.25f);
                else if (title == "END")
                    level.SetCustomModValue(Info, "hyper_event_chance", 0.1f);
                else if (title == "INF")
                    level.SetCustomModValue(Info, "hyper_event_chance", 0.205f);
                level.MarkAsModifiedByMod(Info);
            }
        });
        GeneratorManagement.Register(this, GenerationModType.Finalizer, (title, num, scene) =>
        {
            foreach (var level in scene.GetCustomLevelObjects())
            {
                var hypers = level.GetCustomModValue(Info, "hyper_events") as List<HyperEventSelection>;
                if (hypers != null)
                {
                    foreach (var hyper in hyperEvents)
                        if (level.randomEvents.Exists(x => x?.selection?.Type == hyper.Type))
                            hypers.Add(new HyperEventSelection() { replacingExistingEvent = hyper.Type, hyperEvent = hyper });
                }
                if ((level.minEvents > 1 || level.maxEvents > 2) && level.timeOutEvent != null)
                {
                    var bonuses = level.GetCustomModValue(Info, "bonus_events") as List<WeightedRandomEvent>;
                    if (bonuses != null)
                    {
                        bonuses.AddRange([
                    new()
                    {
                        selection = mysteryEvent,
                        weight = 99
                    },
                    new()
                    {
                        selection = tokenOutrunEvent,
                        weight = 100
                    },
                    new()
                    {
                        selection = smasherEvent,
                        weight = 85
                    },
                    new()
                    {
                        selection = collectorEvent,
                        weight = 100
                    }
                        ]);
                    }
                    
                }
                if (instantHyper.Value)
                    level.SetCustomModValue(Info, "hyper_event_chance", 1f);

                level.MarkAsModifiedByMod(Info);
            }
            var meta = scene.GetMeta();
            if (meta?.tags.Contains("missleshuffle") == true)
            {
                foreach (var level in scene.GetCustomLevelObjects())
                {
                    level.timeOutEvent = timeoutShuffle;
                    level.finalLevel = scene.levelTitle == "F5" && scene.levelNo == 4;
                    level.MarkAsModifiedByMod(Info);
                }
            }
        });

        // For Level Loader and Level Studio
        assets.AddRange<RandomEvent>([
            gnatSwarm,
            trafficTrouble,
            insanity,
            missleShuffle,

            gravityHyperEvent,
            floodHyperEvent,
            shuffleHyperEvent,
            balderHyperEvent,
            hyperGnats,
            hyperTraffic,

            mysteryEvent,
            tokenOutrunEvent,
            smasherEvent,
            collectorEvent,

            timeoutShuffle
            ],
            [
                "GnatSwarm",
                "TrafficTrouble",
                "Nightmares",
                "MissleShuffleStrike",
                
                "CrazyGravityChaos",
                "CrazyFlood",
                "CrazyStudentShuffle",
                "CrazyBalderDash",
                "CrazyGnatSwarm",
                "CrazyTrafficTrouble",

                "BonusMysteryEvent",
                "TokenOutrun",
                "UFOSmasher",
                "TokenCollector",

                "TimeOver/MissleShuffleChaos"
                ]);
        assets.Add("MissleShuffleChaos", gamemanager);
    }
}

[Serializable]
public class HyperEventSelection
{
    public RandomEventType replacingExistingEvent;
    public RandomEvent hyperEvent;
}

public static class ABWEventExtensions
{
    private static FieldInfo _eventJingleOverride = AccessTools.Field(typeof(RandomEvent), "eventJingleOverride");
    private static bool MarkAsHyper(this RandomEvent hyperevent)
    {
        if (ABWEventsPlugin.hyperEvents.Contains(hyperevent))
            return false;
        ABWEventsPlugin.hyperEvents.Add(hyperevent);
        _eventJingleOverride.SetValue(hyperevent, ABWEventsPlugin._hyperJingle);
        return true;
    }
    public static RandomEvent MarkAsCrazy(this RandomEvent crazyevent)
    {
        if (!MarkAsHyper(crazyevent))
            ABWEventsPlugin.Logger.LogWarning($"{crazyevent.name} is already marked as CRAZY");
        return crazyevent;
    }
}

internal class ABWEventsOverloadSaveIO : ModdedSaveGameIOBinary // Copied from MTM101's Content Packs as base.
{
    public override PluginInfo pluginInfo => Chainloader.PluginInfos[ABWEventsPlugin.PLUGIN_GUID];

    public override void Load(BinaryReader reader)
    {
        reader.ReadByte();
    }

    public override void Reset()
    {

    }

    public override void Save(BinaryWriter writer)
    {
        writer.Write((byte)0);
    }

    public override string[] GenerateTags()
    {
        List<string> generatedTags = new List<string>();
        if (ABWEventsPlugin.youtuberMode.Value)
            generatedTags.Add("YoutuberMode");
        if (ABWEventsPlugin.instantHyper.Value)
            generatedTags.Add("CrazyEventsAlways");
        return generatedTags.ToArray();
    }

    public override string DisplayTags(string[] tags)
    {
        string baseMode = tags.Contains("YoutuberMode") ? "Youtuber Mode" : "Standard Mode";
        if (tags.Contains("CrazyEventsAlways"))
            baseMode += "\nCrazy Events Always Encounterable";
        return baseMode;
    }
}

internal interface IEventSpawnPlacement
{
    public abstract Cell GetCellPos(EnvironmentController ec);

    public abstract void MarkAsFound(RandomEvent _event);
}

// Figured that this is easier than doing the same thing from Siege Cannon Cart.
// Also the component exists in the Baldi Dev API but that does hacky things.
/*public class CustomSpriteRotatorAnimator : CustomAnimator<SpriteAnimation, SpriteFrame, Sprite>
{
    public AnimatedSpriteRotator spriteRotator;
    public override void ApplyFrame(Sprite frame) => spriteRotator.targetSprite = frame;
}*/
public class CustomSpriteRotatorAnimator : CustomAnimatorMono<AnimatedSpriteRotator, CustomAnimation<Sprite>, Sprite>
{
    public AnimatedSpriteRotator spriteRotator;

    public override AnimatedSpriteRotator affectedObject { get => spriteRotator; set => spriteRotator = value; }

    protected override void UpdateFrame() => spriteRotator.targetSprite = currentFrame.value;
}