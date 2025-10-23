using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static UnityEngine.ParticleSystem.PlaybackState;

namespace ABWEvents.Events;

public class MissleStrikeShuffleEvent : RandomEvent
{
    public static MissleStrikeShuffleEvent Instance { get; private set; }
    public bool isMode => this is MissleStrikeShuffleChaos;

    [SerializeField] internal MissleStrikeShuffleGuy guyPre;
    protected MissleStrikeShuffleGuy guy;
    [SerializeField] internal GameObject ufo;
    private Vector3 spawnPoint;

    public override void Initialize(EnvironmentController controller, System.Random rng)
    {
        base.Initialize(controller, rng);
        if (Instance == null) // Would Chaos and Shuffle exist at the same time??
            Instance = this;
        spawnPoint = ec.CellFromPosition(new IntVector2(Mathf.RoundToInt(ec.levelSize.x / 2f), Mathf.RoundToInt(ec.levelSize.z / 2f))).FloorWorldPosition + Vector3.up * 50f;
        var newUFO = Instantiate(ufo, ec.transform, false);
        newUFO.transform.position = spawnPoint;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public override void Begin()
    {
        base.Begin();
        guy = ec.SpawnNPC(guyPre, IntVector2.GetGridPosition(spawnPoint)) as MissleStrikeShuffleGuy;
        //guy.transform.position += Vector3.up * 55f;
    }

    public override void End()
    {
        base.End();
        guy.Despawn();
    }

    protected float raidusTime = 0f;
    internal virtual void InRadius() => raidusTime = 0f;
}

public class MissleStrikeShuffleGuy : NPC
{
    [SerializeField] internal MissleStrikeImpact strikePre;
    private EntityOverrider overrider;
    [SerializeField] internal GameObject rocketPre;
    private Vector3 home;
    internal int times = 1;

    public override void Initialize()
    {
        base.Initialize();
        ClearSoundLocations();
        overrider = new EntityOverrider();
        navigator.Entity.Override(overrider);
        overrider.SetHeight(55f);
        overrider.SetInBounds(false);
        home = transform.position;
        behaviorStateMachine.ChangeState(new MissleStrikeShuffleGuy_StateBase(this));
        behaviorStateMachine.ChangeNavigationState(new NavigationState_TargetPosition(this, 9, new Vector3(UnityEngine.Random.Range(home.x - 20f, home.x + 20f), transform.position.y, UnityEngine.Random.Range(home.z - 20f, home.z + 20f))));
    }

    public void FireInTheHole(params Vector3[] targets)
    {
        foreach (var target in targets)
        {
            var strike = Instantiate(strikePre);
            strike.transform.position = target;
            strike.Initialize(ec);
        }
    }

    private IEnumerator Spin()
    {
        isSpin = true;
        bool playWasInvisible = ec.Players[0].Invisible;
        behaviorStateMachine.ChangeNavigationState(new NavigationState_DoNothing(this, 9, true));
        transform.rotation = Quaternion.LookRotation((ec.Players[0].transform.position - transform.position).normalized, Vector3.up);
        Vector3 vector = default;
        float spintime = 0f;
        while (spintime < 1.2f)
        {
            spintime += Time.deltaTime * ec.NpcTimeScale;
            transform.rotation = Quaternion.Euler(transform.rotation.eulerAngles + ((Vector3.up * 30f) * (Time.deltaTime * ec.NpcTimeScale) * (5f - (spintime * 5f)) * (float)Math.PI * 3f));
            overrider.SetHeight(55f + spintime);
            yield return null;
        }
        spintime = 1f;
        var rocket = Instantiate(rocketPre);
        rocket.transform.position = transform.position + (Vector3.up * 55f);
        float leftovers = 0f; // Why is lerp always be clamped at the range of 0 to 1?
        while (spintime > 0f)
        {
            spintime -= Time.deltaTime * ec.NpcTimeScale;
            leftovers += (Time.deltaTime * ec.NpcTimeScale);
            overrider.SetHeight(55f + spintime);
            vector = Vector3.RotateTowards(transform.forward, (ec.Players[0].transform.position - transform.position).normalized, (Time.deltaTime * ec.NpcTimeScale) * 2f * (float)Math.PI * 3f, 0f);
            if (vector != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(vector, Vector3.up);
            rocket.transform.position = Vector3.Lerp(transform.position + (Vector3.up * 55f), transform.position + Vector3.up * 199f, leftovers / 1f);
            yield return null;
        }
        var cells = ec.Players[0].plm.Entity.CurrentRoom?.cells.Where(x => !x.Null && (x.CenterWorldPosition - ec.Players[0].transform.position).magnitude < (10f * 5f) * Mathf.Max(1f, Mathf.RoundToInt(times / 6f))).ToList();
        if (!playWasInvisible && cells != null && cells.Count > 0)
        {
            List<Vector3> targets = new List<Vector3>(sounds.Where(x => x != Vector3.zero));
            for (int i = targets.Count - 1; i >= 0; i--)
                if (i >= times)
                    targets.RemoveAt(i);
            if (ec.Players[0].Invisible)
                cells.Clear();
            for (int i = targets.Count; i < Mathf.Min(times, cells.Count); i++)
                targets.Add(cells[Mathf.RoundToInt(UnityEngine.Random.Range(0f, cells.Count - 1))].FloorWorldPosition);
            FireInTheHole([.. targets]);
        }
        else if (sounds.Any(x => x != Vector3.zero))
        {
            List<Vector3> targets = new List<Vector3>(sounds.Where(x => x != Vector3.zero));
            for (int i = targets.Count - 1; i >= 0; i--)
                if (i >= times)
                    targets.RemoveAt(i);
            FireInTheHole([.. targets]);
        }
        ClearSoundLocations();
        overrider.SetHeight(55f);
        Destroy(rocket);
        spintime = 4f;
        while (spintime > 0f)
        {
            spintime -= Time.deltaTime * ec.NpcTimeScale;
            yield return null;
        }
        isSpin = false;
        behaviorStateMachine.ChangeNavigationState(new NavigationState_TargetPosition(this, 9, new Vector3(UnityEngine.Random.Range(home.x - 20f, home.x + 20f), transform.position.y, UnityEngine.Random.Range(home.z - 20f, home.z + 20f))));
        yield break;
    }

    private bool isSpin;
    public void Spinny()
    {
        if (ec.Players[0].Invisible && !sounds.Any(x => x != Vector3.zero))
            behaviorStateMachine.ChangeNavigationState(new NavigationState_TargetPosition(this, 9, new Vector3(UnityEngine.Random.Range(home.x - 20f, home.x + 20f), transform.position.y, UnityEngine.Random.Range(home.z - 20f, home.z + 20f))));
        else
            StartCoroutine(Spin());
    }

    private Vector3[] sounds = new Vector3[128]; // Stealth only challenge?? Nah, don't make a sound challenge.
    public override void Hear(GameObject source, Vector3 position, int value)
    {
        base.Hear(source, position, value);
        if (value > 10 && ec.Players[0].Invisible) // Cannot be above door's default noise value
            sounds[value] = position + (Vector3.down * position.y);

    }

    public void ClearSoundLocations()
    {
        for (int i = 0; i < sounds.Length; i++)
            sounds[i] = Vector3.zero;
    }

    protected override void VirtualUpdate()
    {
        base.VirtualUpdate();
        if (navigator.HasDestination && (navigator.NextPoint - (navigator.CurrentDestination == Vector3.zero ? transform.position : navigator.CurrentDestination)).magnitude > 50f)
            navigator.SkipCurrentDestinationPoint();
        if (navigator.HasDestination && !isSpin)
        {
            Vector3 vector = default;
            vector = Vector3.RotateTowards(transform.forward, (navigator.NextPoint - transform.position).normalized, Time.deltaTime * 2f * (float)Math.PI * 3f, 0f);
            if (vector != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(vector, Vector3.up);
        }
    }
}

public class MissleStrikeShuffleGuy_StateBase : NpcState
{
    protected MissleStrikeShuffleGuy guy;
    
    public MissleStrikeShuffleGuy_StateBase(MissleStrikeShuffleGuy guy) : base(guy) { this.guy = guy; }

    public override void DestinationEmpty()
    {
        base.DestinationEmpty();
        guy.Spinny();
    }

    public override void PlayerInSight(PlayerManager player)
    {
        base.PlayerInSight(player);
        guy.ClearSoundLocations();
    }

    public override void DoorHit(StandardDoor door)
    {
    }
}

public class MissleStrikeImpact : MonoBehaviour
{
    [SerializeField] internal AudioManager audMan;
    [SerializeField] internal SoundObject incomingPre, incoming, explosion;
    [SerializeField] internal GameObject indication, impact, rocket;
    private EnvironmentController ec;
    private bool Active => ec != null;

    private Fog fog = new Fog()
    {
        color = new Color(1f, 0.2f, 0f),
        priority = 99,
        startDist = 10,
        maxDist = 25,
        strength = 15
    };

    public void Initialize(EnvironmentController ec)
    {
        this.ec = ec;
        audMan.PlaySingle(incomingPre);
        StartCoroutine(Incoming());
    }

    private void Update()
    {
        if (Active)
        {
            if (indication.activeSelf)
                indication.transform.localRotation = Quaternion.Euler(indication.transform.localRotation.eulerAngles + ((Vector3.down * 210f) * (Time.deltaTime * ec.EnvironmentTimeScale)));
            else if (impact.activeSelf)
                impact.transform.localRotation = Quaternion.Euler(impact.transform.localRotation.eulerAngles + ((Vector3.up * 90f) * (Time.deltaTime * ec.EnvironmentTimeScale)));
        }
    }

    private IEnumerator Incoming()
    {
        var trigger = GetComponent<Collider>();
        trigger.enabled = false;
        float time = 0.9f;
        while (time > 0f)
        {
            time -= Time.deltaTime * ec.EnvironmentTimeScale;
            yield return null;
        }
        audMan.PlaySingle(incoming);
        StartCoroutine(ObjectAnimation(indication, 0.05f, Vector3.one * 10f));
        time = 1.75f - 0.5f;
        while (time > 0f)
        {
            time -= Time.deltaTime * ec.EnvironmentTimeScale;
            yield return null;
        }
        rocket.SetActive(true);
        StartCoroutine(ObjectAnimation(indication, 0.05f, Vector3.zero));
        time = 0.5f;
        float leftovers = 0f;
        while (time > 0f)
        {
            time -= Time.deltaTime * ec.EnvironmentTimeScale;
            leftovers += Time.deltaTime * ec.EnvironmentTimeScale;
            rocket.transform.localPosition = Vector3.Lerp(Vector3.up * 199f, Vector3.down, leftovers / 0.5f);
            yield return null;
        }
        indication.SetActive(false);
        rocket.SetActive(false);
        impact.SetActive(true);
        trigger.enabled = true;
        StartCoroutine(ObjectAnimation(impact, 1f, new Vector3(3f, 10f, 3f)));
        StartCoroutine(ObjectAnimation(gameObject, 2.5f, Vector3.one * 30f)); // It has to be all the same number or else the capsule collider will be fucked up.
        audMan.PlaySingle(explosion);
        time = 2f;
        while (time > 0f)
        {
            time -= Time.deltaTime * ec.EnvironmentTimeScale;
            yield return null;
        }
        StartCoroutine(ObjectAnimation(impact, 0.5f, new Vector3(3f, 0f, 3f)));
        time = 0.5f;
        while (time > 0f)
        {
            time -= Time.deltaTime * ec.EnvironmentTimeScale;
            yield return null;
        }
        trigger.enabled = false;
        Destroy(gameObject);
        yield break;
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

    private static LayerMask triggerLayerMask = 5191680;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Window")) // Ignore Raycast B does not collide with windows, which can piss me off and confuse me sometimes.
            other.GetComponent<Window>()?.Break(true);
        if (((1 << other.gameObject.layer) & (int)triggerLayerMask) == 0)
            return;
        if (other.CompareTag("Player"))
            ec.AddFog(fog);
    }

    private void OnTriggerExit(Collider other)
    {
        if (((1 << other.gameObject.layer) & (int)triggerLayerMask) == 0)
            return;
        if (other.CompareTag("Player"))
            ec.RemoveFog(fog);
    }

    private void OnDestroy() => ec.RemoveFog(fog);

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("StandardDoor")) // Ignore Raycast B does not collide with doors, which can piss me off and confuse me sometimes.
            other.GetComponent<StandardDoor>()?.OpenTimed(5f, false);
        if (((1 << other.gameObject.layer) & (int)triggerLayerMask) == 0)
            return;
        if (MissleStrikeShuffleEvent.Instance.isMode && other.CompareTag("Player"))
            MissleStrikeShuffleEvent.Instance.InRadius();
        else if (other.GetComponent<Entity>() != null)
            other.GetComponent<Entity>().AddForce(new Force((other.transform.position - transform.position).normalized, 5f, -20f));
    }
}