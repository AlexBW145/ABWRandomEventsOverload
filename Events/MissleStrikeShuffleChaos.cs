using HarmonyLib;
using MidiPlayerTK;
using MTM101BaldAPI;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ABWEvents.Events;

public class MissleStrikeShuffleChaos : MissleStrikeShuffleEvent
{
    public override void End()
    {
    }

    internal void Enrage() => guy.times++;

    private void Update()
    {
        if (raidusTime >= 5f)
            MissleStrikeShuffleGameManager.shuffleInstance.EndGame();
        if (raidusTime > 0f)
            raidusTime -= 0.1f * (Time.deltaTime * ec.PlayerTimeScale);
    }

    internal override void InRadius() => raidusTime += (Time.deltaTime * ec.PlayerTimeScale);
}

public class MissleStrikeShuffleGameManager : BaseGameManager
{
    [SerializeField] internal MissleStrikeShuffleChaos eventPre;
    [SerializeField] internal SceneObject pitstop;
    private MissleStrikeShuffleChaos shuffleEvent;
    public static MissleStrikeShuffleGameManager shuffleInstance { get; private set; }

    protected override void AwakeFunction() => shuffleInstance = this;

    private void OnDestroy()
    {
        if (shuffleInstance == this)
            shuffleInstance = null;
    }

    public override void PrepareLevelGenerationData()
    {
        base.PrepareLevelGenerationData();
        levelObject.additionalNPCs = 0;
        levelObject.forcedNpcs = [];
        levelObject.potentialNPCs = [];
        levelObject.minEvents = 0;
        levelObject.maxEvents = 0;
        levelObject.randomEvents = [];
        levelObject.potentialBaldis = [];
        if (levelObject is CustomLevelGenerationParameters)
        {
            var ld = levelObject as CustomLevelGenerationParameters;
            ld.SetCustomModValue(ABWEventsPlugin.PLUGIN_GUID, "hyper_events", null);
            ld.SetCustomModValue(ABWEventsPlugin.PLUGIN_GUID, "hyper_event_chance", 0f);
            ld.SetCustomModValue(ABWEventsPlugin.PLUGIN_GUID, "bonus_events", null);
        }
    }

    private static MethodInfo _EndSequence = AccessTools.Method(typeof(CoreGameManager), "EndSequence");
    public void EndGame()
    {
        var player = ec.Players[0];
        Time.timeScale = 0f;
        MusicManager.Instance.StopMidi();
        CoreGameManager.Instance.disablePause = true;
        CoreGameManager.Instance.GetCamera(0).UpdateTargets(player.transform, 0);
        CoreGameManager.Instance.GetCamera(0).offestPos = Vector3.up;
        CoreGameManager.Instance.GetCamera(0).SetControllable(false);
        CoreGameManager.Instance.GetCamera(0).matchTargetRotation = false;
        CoreGameManager.Instance.audMan.volumeModifier = 0.6f;
        StartCoroutine((IEnumerator)_EndSequence.Invoke(CoreGameManager.Instance, []));
        Singleton<InputManager>.Instance.Rumble(1f, 2f);
        Singleton<HighlightManager>.Instance.Highlight("steam_x", LocalizationManager.Instance.GetLocalizedText("Steam_Highlight_Lose"), string.Format(LocalizationManager.Instance.GetLocalizedText("Steam_Highlight_Lose_Desc"), LocalizationManager.Instance.GetLocalizedText(managerNameKey), LocalizationManager.Instance.GetLocalizedText(CoreGameManager.Instance.sceneObject.nameKey)), 2u, 0f, 0f, TimelineEventClipPriority.Standard);
    }

    public override void Initialize()
    {
        if (CoreGameManager.Instance.sceneObject.levelNo > CoreGameManager.Instance.lastLevelNumber)
        {
            CoreGameManager.Instance.SetLives(defaultLives, false);
            CoreGameManager.Instance.lastLevelNumber = CoreGameManager.Instance.sceneObject.levelNo;
        }
        base.Initialize();
        shuffleEvent = Instantiate(eventPre, ec.transform);
        shuffleEvent.Initialize(ec, new System.Random(CoreGameManager.Instance.Seed() + CoreGameManager.Instance.sceneObject.levelNo));
        var crng = new System.Random(CoreGameManager.Instance.Seed() + CoreGameManager.Instance.sceneObject.levelNo);
        foreach (var cell in ec.lights)
            if (crng.NextDouble() < (float)0.5f)
                cell.SetLight(false);
    }

    public override void BeginPlay()
    {
        base.BeginPlay();
        MusicManager.Instance.PlayMidi("TimeOut_MMP_Corrected", true);
    }

    // This method is from Level Studio although its for stealthy shit.
    protected override void VirtualUpdate()
    {
        base.VirtualUpdate();
        if (MusicManager.Instance.MidiPlaying)
        {
            MidiFilePlayer midiPlayer = MusicManager.Instance.MidiPlayer;
            midiPlayer.MPTK_ChannelEnableSet(7, spoopBegan ? !ec.Players[0].Invisible : false); // Pan flute disables
        }    
    }

    protected override void ExitedSpawn()
    {
        base.ExitedSpawn();
        BeginSpoopMode();
    }

    private bool spoopBegan;
    public override void BeginSpoopMode()
    {
        base.BeginSpoopMode();
        ec.StartEventTimers();
        shuffleEvent.Begin();
        spoopBegan = true;
    }

    public override void LoadNextLevel()
    {
        HighlightManager.Instance.Highlight("steam_completed", LocalizationManager.Instance.GetLocalizedText("Steam_Highlight_Win"), string.Format(LocalizationManager.Instance.GetLocalizedText("Steam_Highlight_Win_Desc"), CurrentLevel + 1), 2u, 0f, 0f, TimelineEventClipPriority.Standard);
        CoreGameManager.Instance.saveMapAvailable = false;
        for (int i = 0; i < 2 - CoreGameManager.Instance.Attempts; i++)
        {
            CoreGameManager.Instance.AddPoints(CoreGameManager.Instance.GetPointsThisLevel(0), 0, false, false);
        }

        PrepareToLoad();
        elevatorScreen = Object.Instantiate(elevatorScreenPre);
        elevatorScreen.OnLoadReady += base.LoadNextLevel;
        elevatorScreen.Initialize();
        int num = 0;
        if (time <= levelObject.timeBonusLimit)
        {
            num = levelObject.timeBonusVal;
        }

        if (problems > 0)
        {
            CoreGameManager.Instance.GradeVal += -Mathf.RoundToInt(gradeValue * ((float)correctProblems / (float)problems * 2f - 1f));
        }

        CoreGameManager.Instance.AddPoints(num, 0, playAnimation: false, includeInLevelTotal: false);
        CoreGameManager.Instance.AwardGradeBonus();
        elevatorScreen.ShowResults(time, num);
    }

    protected override void LoadSceneObject(SceneObject sceneObject, bool restarting)
    {
        CoreGameManager.Instance.nextLevel = sceneObject;
        if (!levelObject.finalLevel || restarting)
            base.LoadSceneObject(pitstop, restarting);
        else
            base.LoadSceneObject(sceneObject, restarting);
    }

    public override void RestartLevel()
    {
        CoreGameManager.Instance.saveMapAvailable = true;
        PrepareToLoad();
        elevatorScreen = Object.Instantiate(elevatorScreenPre);
        elevatorScreen.OnLoadReady += base.RestartLevel;
        elevatorScreen.Initialize();
        CoreGameManager.Instance.GetPoints(0);
    }

    protected override void AllNotebooks()
    {
        base.AllNotebooks();
        foreach (Activity activity in ec.activities)
        {
            if (activity != lastActivity)
            {
                activity.Corrupt(false);
                activity.SetBonusMode(true);
            }
        }
        shuffleEvent.Enrage();
    }

    internal void Enrage() => shuffleEvent.Enrage();

    private int incorrect = 0;
    public override void ActivityCompleted(bool correct, Activity activity)
    {
        base.ActivityCompleted(correct, activity);
        if (!correct)
        {
            incorrect++;
            if (incorrect % 2 == 0) shuffleEvent.Enrage();
        }
    }
}

public class TimeOut_Shuffle : TimeOut
{
    [SerializeField] private float rate = 10f;
    private float timeToNextAnger = 5f;

    private void Update()
    {
        if (!active) return;
        timeToNextAnger -= Time.deltaTime * ec.NpcTimeScale;
        if (timeToNextAnger <= 0f)
        {
            timeToNextAnger = rate + timeToNextAnger;
            MissleStrikeShuffleGameManager.shuffleInstance.Enrage();
        }
    }
}