using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ABWEvents.Events;

public class NightmaresEvent : RandomEvent
{
    [SerializeField] internal SoundObject dawn;
    [SerializeField] internal NightmareFissures fissuresPre;
    [SerializeField] internal List<NightmareEntity> nightmares = new List<NightmareEntity>();
    [SerializeField] internal AudioManager audMan;
    internal AudioManager sndMan { get; private set; }
    internal NightmareEventPhase phase { get; private set; }
    private List<NightmareFissures> fissures = new List<NightmareFissures>();
    private List<NPC> spawnedNightmares = new List<NPC>();

    public static NightmaresEvent nightmareInstance { get; private set; }

    public override void Initialize(EnvironmentController controller, System.Random rng)
    {
        base.Initialize(controller, rng);
        nightmareInstance = this;
        sndMan = Instantiate(audMan, transform, false);
        sndMan.volumeModifier = 0.5f;
        sndMan.audioDevice.gameObject.AddComponent<AudioReverbFilter>().reverbPreset = AudioReverbPreset.Psychotic;
        sndMan.audioDevice.gameObject.AddComponent<AudioEchoFilter>().delay = 0.1f;
    }
    private void OnDestroy()
    {
        if (nightmareInstance == this)
            nightmareInstance = null;
    }

    public override void AfterUpdateSetup(System.Random rng)
    {
        base.AfterUpdateSetup(rng);
        var hall = ec.mainHall.GetNewTileList();
        hall.RemoveAll(cell =>
        !cell.HardCoverageFits(CellCoverage.Down) || !(cell.shape == TileShapeMask.Open || cell.shape == TileShapeMask.Corner) || ec.TrapCheck(cell) || cell.hasLight);
        var size = Mathf.RoundToInt((float)(ec.levelSize.x + ec.levelSize.z) / 5f);
        hall.ControlledShuffle(rng);
        for (int i = 0; i < Mathf.Min(size, hall.Count); i++)
        {
            if (i > hall.Count - 1) break;
            if (!GetDistance(hall[i]))
            {
                hall.RemoveAt(i);
                i--;
                continue;
            }
            else
                CreateFissure(hall[i]);
        }
    }

    private bool GetDistance(Cell cell)
    {
        foreach (var fissure in fissures)
        {
            if (ec.GetDistance(cell, fissure.cell) < 25)
                return false;
        }
        return true;
    }

    private void CreateFissure(Cell cell)
    {
        var fissure = Instantiate(fissuresPre, cell.room.objectObject.transform, false);
        fissure.transform.position = cell.FloorWorldPosition;
        fissure.Initialize(this, ec);
        cell.HardCover(CellCoverage.Down); // Gnat Swarm and Nightmares hard covers the floor.
        cell.renderers.AddRange(fissure.GetComponent<RendererContainer>().renderers);
        fissures.Add(fissure);
    }

    public override void Begin()
    {
        base.Begin();
        phase = NightmareEventPhase.Nightmare;
        foreach (var fissure in fissures)
            fissure.StartCoroutine(DelayFissure(fissure, 0.5f, 2f));
    }

    public override void End()
    {
        base.End();
        phase = NightmareEventPhase.Calm;
        foreach (var fissure in fissures)
            fissure.StartCoroutine(DelayFissure(fissure, 0.1f, 1f));
        foreach (var nightmare in spawnedNightmares)
        {
            if (nightmare == null) continue;
            nightmare.Despawn();
        }
        spawnedNightmares.Clear();
        audMan.pitchModifier = 0.5f;
        audMan.QueueAudio(dawn, true);
    }

    private IEnumerator DelayFissure(NightmareFissures fissure, float min, float max)
    {
        float time = UnityEngine.Random.Range(min, max);
        while (time > 0f)
        {
            time -= Time.deltaTime * ec.EnvironmentTimeScale;
            yield return null;
        }
        if (phase == NightmareEventPhase.Warning || phase == NightmareEventPhase.Nightmare)
            fissure.Emerge();
        else if (phase == NightmareEventPhase.Dawn || phase == NightmareEventPhase.Calm)
            fissure.Demerge();
        if (phase == NightmareEventPhase.Nightmare)
            spawnedNightmares.Add(ec.SpawnNPC(nightmares[crng.Next(0, nightmares.Count)], ec.CellFromPosition(fissure.transform.position).position));
        yield break;
    }

    private void Update()
    {
        if (active)
        {
            if (phase != NightmareEventPhase.Dawn && remainingTime < 45f)
                Dawn();
        }
    }

    internal void Warning()
    {
        if (phase < NightmareEventPhase.Warning)
        {
            phase = NightmareEventPhase.Warning;
            foreach (var fissure in fissures)
                fissure.StartCoroutine(DelayFissure(fissure, 1f, 9f));
        }
    }

    internal void Dawn()
    {
        if (phase < NightmareEventPhase.Dawn)
        {
            phase = NightmareEventPhase.Dawn;
            audMan.pitchModifier = 1f;
            audMan.QueueAudio(dawn, true);
            foreach (var fissure in fissures)
                fissure.StartCoroutine(DelayFissure(fissure, 1f, 9f));
        }
    }

    public override void PremadeSetup()
    {
        base.PremadeSetup();
        foreach (var points in FindObjectsOfType<FissurePlacement>(false))
            points.MarkAsFound(this);
    }

    internal class FissurePlacement : TileBasedObject, IEventSpawnPlacement
    {
        public Cell GetCellPos(EnvironmentController ec) => ec.CellFromPosition(transform.position);

        public void MarkAsFound(RandomEvent _event)
        {
            var nightmares = (NightmaresEvent)_event;
            nightmares.CreateFissure(GetCellPos(ec));
            Destroy(gameObject);
        }
    }
}

internal enum NightmareEventPhase
{
    Calm,
    Warning,
    Nightmare,
    Dawn
}

public class NightmareFissures : MonoBehaviour
{
    private NightmaresEvent instance;
    private Cell light;
    public Cell cell => light;
    private EnvironmentController ec;
    [SerializeField] internal SoundObject emergeWarning, emerge, demerge, fx;
    [SerializeField] internal AudioManager audMan;
    [SerializeField] internal SpriteRenderer glowyThing;

    public void Initialize(NightmaresEvent _event, EnvironmentController ec)
    {
        instance = _event;
        this.ec = ec;
        light = ec.CellFromPosition(transform.position);
        light.permanentLight = true; // In studio, stupid lightbulb testing room gets this cell.
        ec.GenerateLight(light, new Color(0.8980392157f, 0.2235294118f, 0.2666666667f), 15);
        light.SetLight(false);
        light.SetPower(true);
        transform.localScale = Vector3.zero;
        glowyThing.color = light.lightColor;
    }
    private IEnumerator ObjectAnimation(GameObject gobj, float time, Vector3 scale)
    {
        var ogscale = gobj.transform.localScale;
        float leftovers = 0f;
        while (leftovers <= time)
        {
            leftovers += Time.deltaTime * ec.EnvironmentTimeScale;
            gobj.transform.localScale = Vector3.Lerp(ogscale, scale, leftovers / time);
            yield return null;
        }
        yield break;
    }

    public void Emerge()
    {
        if (instance.phase == NightmareEventPhase.Warning)
        {
            StartCoroutine(ObjectAnimation(gameObject, 0.3f, Vector3.one * 0.65f));
            light.SetLight(true);
            light.lightStrength = 5;
            ec.UpdateLightingAtCell(light);
            audMan.PlaySingle(emergeWarning);
        }
        else if (instance.phase == NightmareEventPhase.Nightmare)
        {
            StartCoroutine(ObjectAnimation(gameObject, 0.3f, Vector3.one));
            light.lightStrength = 15;
            ec.UpdateLightingAtCell(light);
            audMan.PlaySingle(emerge);
        }
        audMan.QueueAudio(fx, !audMan.QueuedAudioIsPlaying);
        audMan.SetLoop(true);
    }

    public void Demerge()
    {
        if (instance.phase == NightmareEventPhase.Dawn)
        {
            StartCoroutine(ObjectAnimation(gameObject, 0.3f, Vector3.one * 0.65f));
            light.lightStrength = 5;
            ec.UpdateLightingAtCell(light);
        }
        else if (instance.phase == NightmareEventPhase.Calm)
        {
            StartCoroutine(ObjectAnimation(gameObject, 0.3f, Vector3.zero));
            light.SetLight(false);
            audMan.FlushQueue(true);
        }
        audMan.PlaySingle(demerge);
    }
}

public enum NightmareType
{
    Crawling,
    Terror
}

public class NightmareEntity : NPC
{
    [SerializeField] internal NightmareType nightmareType = NightmareType.Crawling;
    internal static SoundObject[] snds;
    [SerializeField] internal AudioManager audMan;
    public override void Initialize()
    {
        base.Initialize();
        if (audMan == null) audMan = GetComponent<AudioManager>();
        switch (nightmareType)
        {
            case NightmareType.Crawling:
                behaviorStateMachine.ChangeState(new CrawlingHorror_StateBase(this));
                break;
            case NightmareType.Terror:
                behaviorStateMachine.ChangeState(new Terrorbeak_StateBase(this));
                break;
        }
    }

    public void Snds()
    {
        audMan.pitchModifier = UnityEngine.Random.Range(0.5f, 0.85f);
        audMan.PlayRandomAudio(snds);
    }

    internal Cell GetRandomNearest() // ec.AllCells returns initialized cells, which is a bummer for these wall bypassers.
    {
        List<Cell> list = new List<Cell>();
        for (int i = 0; i < ec.levelSize.x; i++)
        {
            for (int j = 0; j < ec.levelSize.z; j++)
                list.Add(ec.cells[i, j]);
        }
        var cells = list.Where(cell => ec.GetDistance(cell, ec.CellFromPosition(transform.position)) < 55 && cell != ec.CellFromPosition(transform.position)).ToList();
        return cells[Mathf.RoundToInt(UnityEngine.Random.Range(0f, (float)cells.Count - 1f))];
    }
}

public class NightmareEntity_StateBase : NpcState
{
    protected NightmareEntity nightmare;
    private float walkDelay;
    public NightmareEntity_StateBase(NightmareEntity nightmare) : base(nightmare) { this.nightmare = nightmare; }

    public override void Enter()
    {
        base.Enter();
        ChangeNavigationState(new NavigationState_TargetPosition(npc, 9, nightmare.GetRandomNearest().CenterWorldPosition));
    }

    public override void Initialize()
    {
        base.Initialize();
        walkDelay = UnityEngine.Random.Range(2f, 3f);
    }

    public override void Update()
    {
        base.Update();
        if (!nightmare.Navigator.HasDestination)
        {
            if (walkDelay > 0f)
                walkDelay -= Time.deltaTime * npc.ec.PlayerTimeScale;
            else
            {
                walkDelay = UnityEngine.Random.Range(3f, 5f);
                if (Vector3.Distance(npc.transform.position, npc.ec.Players[0].transform.position) <= 55f)
                    Attack(npc.ec.Players[0]);
                else
                    ChangeNavigationState(new NavigationState_TargetPosition(npc, 9, nightmare.GetRandomNearest().CenterWorldPosition));
            }
        }
    }

    public override void DestinationEmpty()
    {
        base.DestinationEmpty();
        ChangeNavigationState(new NavigationState_DoNothing(npc, 9));
    }

    protected virtual void Attack(PlayerManager player)
    {
        nightmare.Snds();
        nightmare.behaviorStateMachine.ChangeState(new NightmareEntity_AttackBase(nightmare, player));
    }
}

public class NightmareEntity_AttackBase : NightmareEntity_StateBase
{
    protected PlayerManager target;
    public NightmareEntity_AttackBase(NightmareEntity nightmare, PlayerManager player) : base(nightmare) { target = player; }

    public override void Enter()
    {
        base.Enter();
        ChangeNavigationState(new NavigationState_TargetPlayer(npc, 9, target.transform.position));
    }

    public override void Update()
    {
        if (Vector3.Distance(nightmare.transform.position, target.transform.position) < 50f)
            currentNavigationState.UpdatePosition(target.transform.position);
    }

    public override void DestinationEmpty()
    {
        nightmare.Snds();
    }

    protected override void Attack(PlayerManager player)
    {
    }
}

#region CRAWLING
public class CrawlingHorror_StateBase : NightmareEntity_StateBase
{
    public CrawlingHorror_StateBase(NightmareEntity nightmare) : base(nightmare)
    {
    }

    protected override void Attack(PlayerManager player)
    {
        nightmare.Snds();
        nightmare.behaviorStateMachine.ChangeState(new CrawlingHorror_Attacking(nightmare, player));
    }
}

public class CrawlingHorror_Attacking : NightmareEntity_AttackBase
{
    public CrawlingHorror_Attacking(NightmareEntity nightmare, PlayerManager player) : base(nightmare, player)
    {
    }

    public override void DestinationEmpty()
    {
        base.DestinationEmpty();
        nightmare.behaviorStateMachine.ChangeState(new CrawlingHorror_StateBase(nightmare));
    }

    public override void OnStateTriggerEnter(Collider other, bool validCollision)
    {
        if (other.CompareTag("Player"))
        {
            var pm = other.GetComponent<PlayerManager>();
            if (pm != null)
            {
                pm.plm.AddStamina(-10f, true);
                NightmaresEvent.nightmareInstance.sndMan.pitchModifier = 0.75f;
                NightmaresEvent.nightmareInstance.sndMan.PlayRandomAudio(NightmareEntity.snds);
                nightmare.Despawn();
            }
        }
    }
}
#endregion

#region TERROR
public class Terrorbeak_StateBase : NightmareEntity_StateBase
{
    public Terrorbeak_StateBase(NightmareEntity nightmare) : base(nightmare)
    {
    }

    protected override void Attack(PlayerManager player)
    {
        nightmare.Snds();
        nightmare.behaviorStateMachine.ChangeState(new Terrorbeak_Attacking(nightmare, player));
    }
}

public class Terrorbeak_Attacking : NightmareEntity_AttackBase
{
    public Terrorbeak_Attacking(NightmareEntity nightmare, PlayerManager player) : base(nightmare, player)
    {
    }

    public override void DestinationEmpty()
    {
        base.DestinationEmpty();
        nightmare.behaviorStateMachine.ChangeState(new Terrorbeak_StateBase(nightmare));
    }

    public override void OnStateTriggerEnter(Collider other, bool validCollision)
    {
        if (other.CompareTag("Player"))
        {
            var pm = other.GetComponent<PlayerManager>();
            if (pm != null)
            {
                pm.ec.MakeNoise(pm.transform.position, 95);
                NightmaresEvent.nightmareInstance.sndMan.pitchModifier = 0.5f;
                NightmaresEvent.nightmareInstance.sndMan.PlayRandomAudio(NightmareEntity.snds);
                nightmare.Despawn();
            }
        }
    }
}
#endregion