using MTM101BaldAPI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ABWEvents.Events;

public class UFOSmasherEvent : BonusEventBase
{
    [SerializeField] internal UFOEntity ufoPrefab;
    [SerializeField] internal ItemObject spikedBall;
    private List<UFOEntity> ufos = new List<UFOEntity>();
    private List<Pickup> balls = new List<Pickup>();
    private float timerUntilItem = 2f;

    public override void Begin()
    {
        base.Begin();
        var hallcells = ec.mainHall.cells;
        var size = Mathf.RoundToInt((float)(ec.levelSize.x + ec.levelSize.z) / 5f);
        for (int i = 0; i < size; i++)
            ufos.Add((UFOEntity)ec.SpawnNPC(ufoPrefab, hallcells[Mathf.RoundToInt(UnityEngine.Random.Range(0f, hallcells.Count - 1f))].position));
    }

    public override void End()
    {
        base.End();
        foreach (var ufo in ufos)
            if (ufo != null)
            {
                if (!ufo.Despawning)
                    ufo.Despawn();
            }
        foreach (var ball in balls)
        {
            if (ball.item.itemType == spikedBall.itemType)
            {
                ec.items.Remove(ball);
                Destroy(ball.gameObject);
            }
        }
        ufos.Clear();
        balls.Clear();
    }

    private void Update()
    {
        if (active)
        {
            if (timerUntilItem > 0f)
                timerUntilItem -= Time.deltaTime * ec.EnvironmentTimeScale;
            else
            {
                var size = Mathf.RoundToInt((float)(ec.levelSize.x + ec.levelSize.z) / 5f);
                for (int times = 0;  times < size; times++)
                {
                    var pos = ec.mainHall.cells[Mathf.RoundToInt(UnityEngine.Random.Range(0f, ec.mainHall.cells.Count - 1f))].CenterWorldPosition;
                    balls.Add(ec.CreateItem(ec.mainHall, spikedBall, new Vector2(pos.x, pos.z)));
                }
                timerUntilItem = 3.2f;
            }
        }
    }
}

public class UFOEntity : NPC
{
    private EntityOverrider overrider;
    [SerializeField] internal AudioManager audMan;
    [SerializeField] internal SoundObject spawnSnd, despawnSnd, dieSnd;
    private int hits;
    public const int totalHits = 7; // Are 9 total hits that impossible??
    private IEnumerator despawnEnum;
    public bool Despawning => despawnEnum != null;

    public static List<ItemObject> regularItem = new List<ItemObject>(), goodItem = new List<ItemObject>();
    public static ItemObject ytp;

    private System.Random rng = new System.Random(UnityEngine.Random.RandomRangeInt(int.MinValue, int.MaxValue)); // No parameters randomizes on the specified current tick.
    [SerializeField] internal ItemObject itemSelected;
    [SerializeField] internal QuickExplosion explodeThing;

    public override void Initialize()
    {
        base.Initialize();
        behaviorStateMachine.ChangeState(new UFOEntity_StateBase(this));
        overrider = new EntityOverrider();
        navigator.Entity.Override(overrider);
        overrider.SetHeight(55f);
        //overrider.SetAudioPositionOverride(spriteBase.transform);
        if (rng.NextDouble() < (double)0.1f)
            itemSelected = goodItem[Mathf.RoundToInt(UnityEngine.Random.Range(0f, goodItem.Count - 1))];
        else if (rng.NextDouble() < (double)0.5f)
            itemSelected = regularItem[Mathf.RoundToInt(UnityEngine.Random.Range(0f, regularItem.Count - 1))];
        else
            itemSelected = ytp;
        spriteRenderer[0].sprite = itemSelected.itemSpriteLarge;
        StartCoroutine(SpawnAnim()); // Cool
        audMan.PlaySingle(spawnSnd);
    }

    private IEnumerator SpawnAnim()
    {
        float timer = 0f;
        float random = UnityEngine.Random.Range(2f, 3f);
        while (timer <= random)
        {
            timer += Time.deltaTime * ec.NpcTimeScale;
            float actualDuration = timer / random;
            overrider.SetHeight(Mathf.LerpUnclamped(55f, 5f, 1 + (1.70158f + 1) * Mathf.Pow(actualDuration - 1, 3) + 1.70158f * Mathf.Pow(actualDuration - 1, 2)));
            yield return null;
        }
        overrider.SetHeight(5f);
    }

    private IEnumerator DespawnAnim()
    {
        float timer = 0f;
        audMan.pitchModifier = 1f;
        if (hits >= totalHits)
        {
            audMan.FlushQueue(true);
            audMan.PlaySingle(dieSnd);
            navigator.SetSpeed(5f);
            navigator.maxSpeed = 5f;
            navigator.accel = 0f;
            while (timer <= 0.8f)
            {
                timer += Time.deltaTime * ec.NpcTimeScale;
                float actualDuration = timer / 0.8f;
                overrider.SetHeight(Mathf.LerpUnclamped(5f, 0f, actualDuration * actualDuration * actualDuration));
                yield return null;
            }
            overrider.SetHeight(0f);
            Instantiate(explodeThing, transform.position, default);
            ec.CreateItem(navigator.Entity.CurrentRoom, itemSelected, new Vector2(transform.position.x, transform.position.z));
        }
        else
        {
            audMan.PlaySingle(despawnSnd);
            float random = UnityEngine.Random.Range(1f, 1.5f);
            while (timer <= random)
            {
                timer += Time.deltaTime * ec.NpcTimeScale;
                float actualDuration = timer / random;
                overrider.SetHeight(Mathf.LerpUnclamped(5f, 55f, (1.70158f + 1) * actualDuration * actualDuration * actualDuration - 1.70158f * actualDuration * actualDuration));
                yield return null;
            }
            overrider.SetHeight(55f);
        }
        base.Despawn();
    }

    public override void Despawn()
    {
        despawnEnum = DespawnAnim();
        StartCoroutine(despawnEnum);
    }

    public void Damage()
    {
        hits++;
        audMan.pitchModifier += 3f / totalHits;
        navigator.speed += 15f;
        navigator.maxSpeed += 15f;
        if (hits >= totalHits)
            Despawn();
    }
}

public class UFOEntity_StateBase : NpcState
{
    public UFOEntity_StateBase(UFOEntity ufo) : base(ufo) { }

    public override void Enter()
    {
        base.Enter();
        ChangeNavigationState(new NavigationState_WanderRandom(npc, 99));
    }
}

public class ITM_SpikedBall : Item, IEntityTrigger
{
    internal static ItemObject[] stacksItems = new ItemObject[9];
    [SerializeField] internal int stacks = 8;
    [SerializeField] internal float speed = 30f;
    [SerializeField] internal Entity entity;
    [SerializeField] internal SpikeBallFlinger flinger;
    [SerializeField] internal SoundObject throwSnd, impactSnd, damageSnd, rollingSnd;
    [SerializeField] internal AudioManager audMan;
    private EnvironmentController ec;
    private float time = 9f;
    private bool outOfPlayerCol = false;

    public void EntityTriggerEnter(Collider other, bool validCollision)
    {
        if (((other.CompareTag("Player") && outOfPlayerCol) || other.CompareTag("NPC")) && validCollision)
        {
            var entity = other.GetComponent<Entity>();
            if (entity != null && !SpikeBallFlinger.Flingza.Contains(entity))
            {
                var component = Instantiate(flinger, other.transform.position, default);
                component.Initialize(this, entity);
                audMan.PlaySingle(damageSnd);
                component.StartCoroutine(Bawomp(other.GetComponent<Entity>()));
                time--;
                var ufo = other.GetComponent<UFOEntity>();
                if (ufo != null)
                    ufo.Damage();
            }
        }
    }

    private IEnumerator Bawomp(Entity otherentity)
    {
        otherentity?.SetTrigger(false);
        otherentity?.SetFrozen(true);
        entity?.SetTrigger(false);
        entity?.SetFrozen(true);
        yield return new WaitForSecondsEnvironmentTimescale(ec, 0.5f);
        otherentity?.SetFrozen(false);
        otherentity?.SetTrigger(true);
        entity?.SetFrozen(false);
        entity?.SetTrigger(true);
    }

    public void EntityTriggerExit(Collider other, bool validCollision)
    {
        if (other.CompareTag("Player"))
            outOfPlayerCol = true;
    }

    public void EntityTriggerStay(Collider other, bool validCollision)
    {
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Window") && speed >= 45)
            other.GetComponent<Window>()?.Break(true);
    }

    public override bool Use(PlayerManager pm)
    {
        ec = pm.ec;
        transform.position = pm.transform.position;
        transform.forward = CoreGameManager.Instance.GetCamera(pm.playerNumber).transform.forward;
        entity.Initialize(ec, transform.position);
        entity.CopyStatusEffects(pm.plm.Entity);
        entity.OnEntityMoveInitialCollision += OnEntityMoveCollision;
        speed += pm.plm.RealVelocity;
        CoreGameManager.Instance.audMan.PlaySingle(throwSnd);
        StartCoroutine(ThrowAnim());
        if (stacks > 0)
        {
            pm.itm.SetItem(stacksItems[stacks - 1], pm.itm.selectedItem);
            return false;
        }
        return true;
    }

    private IEnumerator ThrowAnim()
    {
        float height = 5f;
        while (height > 0f)
        {
            height -= 25f * Time.deltaTime * ec.EnvironmentTimeScale;
            entity.SetHeight(height);
            yield return null;
        }
        entity.SetHeight(0f);
        entity.SetGrounded(true);
        audMan.PlaySingle(impactSnd);
        audMan.QueueAudio(rollingSnd, true);
        audMan.SetLoop(true);
        yield break;
    }

    private void OnEntityMoveCollision(RaycastHit hit) // This is better
    {
        transform.forward = Vector3.Reflect(transform.forward, hit.normal);
        audMan.PlaySingle(impactSnd);
    }

    /*private int collisionMask = LayerMask.GetMask("Default", "Ignore Raycast", "Ignore Raycast B");
    private bool bumped = false;

    private void FixedUpdate()
    {
        bumped = false;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (bumped) return;
        if (collision.gameObject.CompareTag("Wall") || collision.gameObject.CompareTag("Window") || collision.gameObject.CompareTag("Untagged"))
        {
            transform.forward = Vector3.Reflect(entity.Velocity, collision.GetContact(0).normal);
            audMan.PlaySingle(impactSnd);
            bumped = true;
        }
    }*/

    private void Update()
    {
        entity.UpdateInternalMovement(transform.forward * speed * ec.EnvironmentTimeScale);
        if (!entity.Frozen)
        {
            time -= Time.deltaTime * ec.EnvironmentTimeScale;
            if (time <= 0f)
                Destroy(gameObject);
        }
    }
}

public class SpikeBallFlinger : MonoBehaviour
{
    [SerializeField] internal Entity self;
    private Entity entity;
    private float flingSpeed = 30f, timeLeft = 1f;
    //private Force forceFling;
    private MovementModifier moveMod = new MovementModifier(default, 0f);
    private float TimeScale => entity.gameObject.CompareTag("Player") ? entity.Ec.PlayerTimeScale : entity.Ec.NpcTimeScale;
    internal static HashSet<Entity> Flingza = new HashSet<Entity>();

    public void Initialize(ITM_SpikedBall spikeBall, Entity other)
    {
        if (self.Ec != null) return;
        entity = other;
        Flingza.Add(other);
        self.Initialize(other.Ec, other.transform.position); // Well I forgot when redoing the system...
        flingSpeed += spikeBall.speed * 1.5f;
        transform.forward = spikeBall.transform.forward;
        //forceFling = new Force(spikeBall.transform.forward, flingSpeed, -flingSpeed);
        //self.AddForce(forceFling);
        entity.Teleport(transform.position);
        entity.OnTeleport += OnTeleport;
        moveMod.forceTrigger = true;
        moveMod.priority = 10;
        entity.ExternalActivity.moveMods.Add(moveMod);
        self.OnEntityMoveInitialCollision += OnEntityMoveCollision;
        if (entity.GetComponent<UFOEntity>() != null) // CANNOT BE STUNLOCK IMMUNE
        {
            timeLeft = 0f;
            flingSpeed = 30f;
        }
    }

    private void OnEntityMoveCollision(RaycastHit hit) => transform.forward = Vector3.Reflect(transform.forward, hit.normal); // This is better
    private void OnTeleport(Vector3 position)
    {
        if (position != transform.position)
            self.Teleport(position);
    }

    private void Update()
    {
        if (entity == null && self.Ec != null)
        {
            Destroy(gameObject);
            return;
        }
        if (entity?.Frozen == false)
        {
            if (flingSpeed > 0f)
                flingSpeed -= 30f * (Time.deltaTime * TimeScale);
            else if (flingSpeed < 0f)
                flingSpeed = 0f;
            self.UpdateInternalMovement(transform.forward * flingSpeed * TimeScale);
            moveMod.movementAddend = self.ExternalActivity.Addend + (transform.forward * flingSpeed * TimeScale);
        }
        if (entity?.Frozen == false && flingSpeed <= 0f)
        {
            moveMod.movementMultiplier = 1f;
            if (timeLeft > 0f)
                timeLeft -= Time.deltaTime * TimeScale;
            else
                Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        //forceFling.Kill();
        //self.RemoveForce(forceFling);
        Flingza.Remove(entity);
        if (entity != null)
            entity.OnTeleport -= OnTeleport;
        entity?.ExternalActivity.moveMods.Remove(moveMod);
    }

    private void OnTriggerEnter(Collider other) // This is barely called...
    {
        if (flingSpeed >= 10f && other.CompareTag("Window"))
            other.GetComponent<Window>()?.Break(gameObject.CompareTag("Player"));
    }
}