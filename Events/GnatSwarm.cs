using HarmonyLib;
using MTM101BaldAPI;
using MTM101BaldAPI.Components;
using MTM101BaldAPI.PlusExtensions;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ABWEvents.Events;

public class GnatSwarm : RandomEvent
{
    [SerializeField] internal GameObject housePrefab;
    private List<IntVector2> spawnPoints = new List<IntVector2>();

    [SerializeField] internal GnatEntity gnatPrefab;
    private List<GnatEntity> gnats = new List<GnatEntity>();

    [SerializeField] internal bool isHyper = false;
    private float coolInit = 15f;
    private float cool = 15f;

    public override void AfterUpdateSetup(System.Random rng)
    {
        base.AfterUpdateSetup(rng);
        var hall = ec.mainHall.GetNewTileList();
        hall.RemoveAll(cell =>
        !cell.HardCoverageFits(CellCoverage.Down) || cell.shape == TileShapeMask.Straight || cell.shape == TileShapeMask.Closed || ec.TrapCheck(cell));
        var size = Mathf.RoundToInt((float)(ec.levelSize.x + ec.levelSize.z) / 10f);
        hall.ControlledShuffle(rng);
        for (int i = 0; i < Mathf.Min(size, hall.Count); i++)
            CreateHousing(hall[i]);
    }
    
    private void CreateHousing(Cell cell)
    {
        var house = Instantiate(housePrefab, cell.room.objectObject.transform, false);
        house.transform.position = cell.FloorWorldPosition;
        cell.AddRenderer(house.GetComponent<Renderer>());
        cell.HardCover(CellCoverage.Down);
        spawnPoints.Add(cell.position);
    }

    public override void Begin()
    {
        base.Begin();
        foreach (var spawn in spawnPoints)
            gnats.Add((GnatEntity)ec.SpawnNPC(gnatPrefab, spawn));
    }

    private void Update()
    {
        if (active && isHyper)
        {
            if (cool > 0f)
                cool -= Time.deltaTime * ec.NpcTimeScale;
            else
            {
                foreach (var spawn in spawnPoints)
                    gnats.Add((GnatEntity)ec.SpawnNPC(gnatPrefab, spawn));
                cool = coolInit;
            }
        }
    }

    public override void End()
    {
        base.End();
        int index = 0;
        foreach (var gnat in gnats)
        {
            if (gnat == null) continue;
            gnat.behaviorStateMachine.ChangeState(new Gnat_Returning(gnat, spawnPoints[index]));
            index++;
            if (index > spawnPoints.Count - 1)
                index = 0;
        }
        gnats.Clear();
    }

    public override void PremadeSetup()
    {
        base.PremadeSetup();
        foreach (var points in FindObjectsOfType<HousingPlacement>(false)) // How do I even find the tile based object from rooms??
            points.MarkAsFound(this);
    }

    internal class HousingPlacement : TileBasedObject, IEventSpawnPlacement
    {
        public Cell GetCellPos(EnvironmentController ec) => ec.CellFromPosition(transform.position);
        public void MarkAsFound(RandomEvent _event)
        {
            var gnatswarm = (GnatSwarm)_event;
            gnatswarm.CreateHousing(GetCellPos(ec));
            Destroy(gameObject);
        }
    }
}

public class GnatEntity : NPC
{
    [SerializeField] internal AudioManager audMan;
    [SerializeField] internal SoundObject attack;
    [SerializeField] internal ParticleSystem clouds;
    private Entity bugging;
    private Navigator buggingNav;
    private PlayerMovement buggingPlm;
    private MovementModifier moveMod = new MovementModifier(Vector3.zero, 0f);
    private ValueModifier staminaStat = new ValueModifier(1f, -5f);
    private ValueModifier staminaDropStat = new ValueModifier(1f, 5f);

    private void Start()
    {
        foreach (SpriteRenderer _spriteRenderer in spriteRenderer)
            _spriteRenderer.color = Color.clear;
    }

    public override void Initialize()
    {
        base.Initialize();
        navigator.Entity.SetHeight(6.5f);
        moveMod.forceTrigger = true;
        behaviorStateMachine.ChangeState(new Gnat_StateBase(this));
    }

    public void BugEm(Entity them)
    {
        behaviorStateMachine.ChangeState(new Gnat_DoNothing(this));
        bugging = them;
        bugging.SetBlinded(true);
        buggingNav = them.GetComponent<Navigator>();
        buggingPlm = them.GetComponent<PlayerMovement>();
        navigator.Entity.ExternalActivity.moveMods.Add(moveMod);
        navigator.Entity.Teleport(them.transform.position);
        navigator.Entity.SetTrigger(false);
        PlayAnim();
        var stat = them.GetComponent<PlayerMovementStatModifier>();
        if (stat != null)
        {
            stat.AddModifier("staminaRise", staminaStat);
            stat.AddModifier("staminaDrop", staminaDropStat);
        }
        StartCoroutine(Timer(5));
    }

    private void PlayAnim()
    {
        IEnumerator Delay()
        {
            var main = clouds.main;
            var emission = clouds.emission;
            main.startSpeed = -50f;
            emission.rateOverTime = 30f;
            yield return new WaitForSecondsNPCTimescale(this, 0.5f);
            main.startSpeed = -10f;
            emission.rateOverTime = 15f;
        }
        audMan.PlaySingle(attack);
        StartCoroutine(Delay());
    }

    private IEnumerator Timer(int times)
    {
        void DoneFor()
        {
            if (behaviorStateMachine.currentState is not Gnat_Returning)
                behaviorStateMachine.ChangeState(new Gnat_Cooldown(this));
            navigator.Entity.ExternalActivity.moveMods.Remove(moveMod);
            var stat = bugging?.GetComponent<PlayerMovementStatModifier>();
            if (stat != null)
            {
                stat.RemoveModifier(staminaStat);
                stat.RemoveModifier(staminaDropStat);
            }
            navigator.Entity.SetTrigger(true);
            bugging?.SetBlinded(false);
            bugging = null;
            buggingNav = null;
            buggingPlm = null;
        }
        while (times > 0)
        {
            float timer = 6f;
            while (timer > 0f)
            {
                if (behaviorStateMachine.currentState is Gnat_Returning)
                {
                    DoneFor();
                    yield break;
                }
                timer -= Time.deltaTime * ec.NpcTimeScale;
                yield return null;
            }
            times--;
            PlayAnim();
            yield return null;
        }
        DoneFor();
        yield break;
    }

    private static FieldInfo _running = AccessTools.DeclaredField(typeof(PlayerMovement), "running");
    protected override void VirtualUpdate()
    {
        base.VirtualUpdate();
        if (buggingNav != null)
            moveMod.movementAddend = buggingNav.Velocity.normalized * buggingNav.speed * buggingNav.Am.Multiplier;
        else if (buggingPlm != null)
            moveMod.movementAddend = buggingPlm.Entity.Velocity.normalized * ((bool)_running.GetValue(buggingPlm) ? buggingPlm.runSpeed : buggingPlm.walkSpeed) * buggingPlm.am.Multiplier;
        if (bugging != null && Vector3.Distance(bugging.transform.position, transform.position) > 0.5f)
            navigator.Entity.Teleport(bugging.transform.position);

    }
}

public class Gnat_StateBase : NpcState
{
    protected GnatEntity gnat;
    public Gnat_StateBase(GnatEntity gnat) : base(gnat) { this.gnat = gnat; }

    private Entity target;

    public override void DoorHit(StandardDoor door)
    {
    }

    public override void Enter()
    {
        base.Enter();
        gnat.Navigator.SetSpeed(20f);
        gnat.Navigator.maxSpeed = 25f;
        gnat.Navigator.accel = 20f;
        ChangeNavigationState(new NavigationState_WanderRandom(gnat, 9, true));
    }

    public override void Update()
    {
        base.Update();
        foreach (var npc in gnat.ec.Npcs)
        {
            if (npc.Character == Character.Null || !npc.Navigator.enabled || !npc.looker.enabled) continue;
            gnat.looker.Raycast(npc.transform, Mathf.Min((gnat.transform.position - npc.transform.position).magnitude, gnat.looker.distance, gnat.ec.MaxRaycast), out bool sighted);
            if (sighted && target == null)
            {
                target = npc.Navigator.Entity;
                ChangeNavigationState(new NavigationState_TargetPosition(gnat, 9, target.transform.position));
            }
            else if (sighted && target == npc.Navigator.Entity)
                currentNavigationState.UpdatePosition(target.transform.position);
        }
        if (target != null && gnat.Navigator.maxSpeed < 65f)
            gnat.Navigator.maxSpeed += 1 * (Time.deltaTime * npc.TimeScale);
        else if (target == null && gnat.Navigator.maxSpeed != 25f)
            gnat.Navigator.maxSpeed = 25f;
    }

    public override void PlayerInSight(PlayerManager player)
    {
        base.PlayerInSight(player);
        if (target == null)
        {
            target = player.plm.Entity;
            ChangeNavigationState(new NavigationState_TargetPosition(gnat, 9, target.transform.position));
        }
        else if (target == player.plm.Entity)
            currentNavigationState.UpdatePosition(target.transform.position);
    }

    public override void OnStateTriggerEnter(Collider other, bool validCollision)
    {
        base.OnStateTriggerEnter(other, validCollision);
        if (other.CompareTag("Player") || other.CompareTag("NPC"))
        {
            bool flag = false;
            if (other.CompareTag("NPC"))
            {
                var npc = other.GetComponent<NPC>();
                if (npc != null) 
                    if (npc.Character == Character.Null || !npc.Navigator.enabled || !npc.looker.enabled) flag = true;
            }
            if (other?.GetComponent<Entity>() != null && !flag) // They still bug us while being squished.
                gnat.BugEm(other.GetComponent<Entity>());
        }
    }

    public override void DestinationEmpty()
    {
        base.DestinationEmpty();
        if (currentNavigationState is NavigationState_TargetPosition)
            target = null;
        ChangeNavigationState(new NavigationState_WanderRandom(gnat, 9, true));
    }
}

public class Gnat_DoNothing : Gnat_StateBase
{
    public Gnat_DoNothing(GnatEntity gnat) : base(gnat) { }

    public override void Enter()
    {
        base.Enter();
        ChangeNavigationState(new NavigationState_DoNothing(gnat, 9));
    }

    public override void Update()
    {
    }

    public override void PlayerInSight(PlayerManager player)
    {
    }

    public override void OnStateTriggerEnter(Collider other, bool validCollision)
    {
    }

    public override void DestinationEmpty()
    {
    }
}

public class Gnat_Cooldown : Gnat_StateBase
{
    private float coolDown = 9f;
    public Gnat_Cooldown(GnatEntity gnat) : base(gnat) { }

    public override void Initialize()
    {
        base.Initialize();
        coolDown = 30f;
    }

    public override void Enter()
    {
        base.Enter();
        gnat.Navigator.SetSpeed(20f);
        gnat.Navigator.maxSpeed = 25f;
        gnat.Navigator.accel = 20f;
    }

    public override void Update()
    {
        if (coolDown > 0f)
            coolDown -= Time.deltaTime * gnat.ec.NpcTimeScale;
        else
            gnat.behaviorStateMachine.ChangeState(new Gnat_StateBase(gnat));
    }

    public override void PlayerInSight(PlayerManager player)
    {
    }

    public override void OnStateTriggerEnter(Collider other, bool validCollision)
    {
    }
}

public class Gnat_Returning : Gnat_StateBase
{
    private IntVector2 home;
    public Gnat_Returning(GnatEntity gnat, IntVector2 home) : base(gnat) { this.home = home; }

    public override void Enter()
    {
        base.Enter();
        gnat.Navigator.maxSpeed = 65f;
        ChangeNavigationState(new NavigationState_TargetPosition(gnat, 99, gnat.ec.CellFromPosition(home).CenterWorldPosition));
    }

    public override void DestinationEmpty()
    {
        base.DestinationEmpty();
        gnat.Despawn();
    }

    public override void Update()
    {
    }

    public override void PlayerInSight(PlayerManager player)
    {
    }

    public override void OnStateTriggerEnter(Collider other, bool validCollision)
    {
    }
}