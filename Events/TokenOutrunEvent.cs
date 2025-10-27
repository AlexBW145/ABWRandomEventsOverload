using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace ABWEvents.Events;

public class TokenOutrunEvent : BonusEventBase
{
    [SerializeField] internal TokenOutrunGuy guyPrefab;
    private TokenOutrunGuy guy;

    public override void Begin()
    {
        base.Begin();
        guy = ec.SpawnNPC(guyPrefab, ec.CellFromPosition(ec.elevators[0].transform.position).position) as TokenOutrunGuy;
    }

    public override void End()
    {
        base.End();
        guy.behaviorStateMachine.ChangeState(new TokenOutrunGuy_Leaving(guy));
    }
}

public class TokenOutrunToken : MonoBehaviour, IEntityTrigger
{
    [SerializeField] internal Entity entity;
    [SerializeField] internal int ytps = 15;
    [SerializeField] internal SoundObject collected;
    [SerializeField] internal AudioManager audMan;
    [SerializeField] internal float startHeight = 6f, endHeight = 0.5f, gravity = 25f;
    private float height, existingTime = 1.1f;
    private EnvironmentController ec;
    private Vector3 direction;
    private bool touchedGround, gotTouched;
    [SerializeField] internal Transform render;
    private IEnumerator bouncer;

    internal void Spawn(EnvironmentController _ec, Vector3 forward)
    {
        ec = _ec;
        transform.forward = forward;
        height = startHeight;
        direction = transform.forward;
        entity.Initialize(ec, transform.position);
        var throwSpeed = UnityEngine.Random.Range(7.15f, 8.15f);
        entity.AddForce(new Force(direction, throwSpeed, 0f - throwSpeed));
        entity.OnEntityMoveInitialCollision += OnEntityMoveCollision;
    }

    private void OnEntityMoveCollision(RaycastHit hit) => transform.forward = Vector3.Reflect(transform.forward, hit.normal);

    private IEnumerator Bounce()
    {
        float time = 0f;
        while (time <= 1f)
        {
            time += Time.deltaTime * ec.EnvironmentTimeScale;
            entity.SetHeight(Mathf.Lerp(endHeight, endHeight + 2f, Mathf.Sin((time * Mathf.PI) / 2f)));
            yield return null;
        }
        time = 0f;
        while (time <= 1f)
        {
            time += Time.deltaTime * ec.EnvironmentTimeScale;
            entity.SetHeight(Mathf.Lerp(endHeight + 2f, -1.5f, 1f - Mathf.Cos((time * Mathf.PI) / 2f)));
            yield return null;
        }
        Destroy(gameObject);
        yield break;
    }

    private void Update()
    {
        if (!gotTouched)
        {
            render.GetChild(0).Rotate((Vector3.one * 15f) * (Time.deltaTime * ec.EnvironmentTimeScale) * (float)Math.PI * gravity, Space.Self);
            if (!touchedGround)
            {
                height -= gravity * (Time.deltaTime * ec.EnvironmentTimeScale);
                entity.UpdateInternalMovement(Vector3.zero);
                if (height <= endHeight)
                {
                    height = endHeight;
                    touchedGround = true;
                    bouncer = Bounce();
                    StartCoroutine(bouncer);
                }

                entity.SetHeight(height);
            }
            else
            {
                entity.UpdateInternalMovement(direction * 5f * ec.EnvironmentTimeScale);
                /*if (existingTime > 0f)
                    existingTime -= Time.deltaTime * ec.EnvironmentTimeScale;
                else
                    Destroy(gameObject);*/
            }
        }
        else
        {
            if (existingTime > 0f)
            {
                existingTime -= Time.deltaTime * ec.EnvironmentTimeScale;
                render.GetChild(0).localScale = Vector3.Lerp(Vector3.zero, Vector3.one, existingTime);
            }
            else
                Destroy(gameObject);
        }
    }

    public void EntityTriggerEnter(Collider other, bool validCollision)
    {
        if (!gotTouched && other.isTrigger)
        {
            if (other.CompareTag("Player"))
            {
                var player = other.gameObject.GetComponent<PlayerManager>();
                if (player != null)
                {
                    CoreGameManager.Instance.AddPoints(ytps, player.playerNumber, true);
                    CoreGameManager.Instance.audMan.PlaySingle(collected);
                    Destroy();
                }
            }
            else if (other.CompareTag("NPC") && other.GetComponent<TokenOutrunGuy>() == null)  
                Destroy();
        }
    }

    private void Destroy()
    {
        if (bouncer != null)
            StopCoroutine(bouncer);
        existingTime = 1f;
        gotTouched = true;
        audMan.PlaySingle(collected);
    }

    public void EntityTriggerStay(Collider other, bool validCollision)
    {
    }

    public void EntityTriggerExit(Collider other, bool validCollision)
    {
    }
}

public class TokenOutrunGuy : NPC
{
    [SerializeField] internal TokenOutrunToken tokenPrefab;
    private float timer = 0.15f;

    public override void Initialize()
    {
        base.Initialize();
        navigator.SetSpeed(35f);
        navigator.maxSpeed = 50f;
        behaviorStateMachine.ChangeState(new TokenOutrunGuy_Active(this));
    }

    protected override void VirtualUpdate()
    {
        base.VirtualUpdate();
        if (timer > 0f)
            timer -= Time.deltaTime * ec.NpcTimeScale;
        else
        {
            var token = Instantiate(tokenPrefab, transform.position, default);
            token.Spawn(ec, transform.forward * -1f);
            timer = UnityEngine.Random.Range(0.15f, 0.55f);
        }
    }
}

public class TokenOutrunGuy_StateBase : NpcState
{
    protected TokenOutrunGuy guy;
    public TokenOutrunGuy_StateBase(TokenOutrunGuy guy) : base(guy) { this.guy = guy; }

    public override void PlayerSighted(PlayerManager player)
    {
        base.PlayerSighted(player);
        ChangeNavigationState(new NavigationState_WanderFlee(guy, 9, player.DijkstraMap));
    }

    public override void DestinationEmpty()
    {
        base.DestinationEmpty();
        guy.Navigator.SetSpeed(15f);
        guy.Navigator.maxSpeed = 50f;
    }

    public override void Update()
    {
        if (!npc.Navigator.HasDestination && currentNavigationState is NavigationState_WanderFlee)
        {
            bool flag = false;
            for (int i = 0; i < CoreGameManager.Instance.setPlayers; i++)
            {
                if (npc.ec.Players[i] != null && Vector3.Distance(guy.transform.position, npc.ec.Players[i].transform.position) < 55)
                {
                    ChangeNavigationState(new NavigationState_WanderFlee(guy, 9, npc.ec.Players[i].DijkstraMap));
                    break;
                }
                flag = true;
            }
            if (flag)
                ChangeNavigationState(new NavigationState_WanderRandom(guy, 9, true));
        }
    }
}

public class TokenOutrunGuy_Active : TokenOutrunGuy_StateBase
{
    public TokenOutrunGuy_Active(TokenOutrunGuy guy) : base(guy) { }

    public override void Enter()
    {
        base.Enter();
        ChangeNavigationState(new NavigationState_WanderRandom(guy, 9, true));
    }
}

public class TokenOutrunGuy_Leaving : TokenOutrunGuy_StateBase
{
    public TokenOutrunGuy_Leaving(TokenOutrunGuy guy) : base(guy) { }

    public override void Enter()
    {
        base.Enter();
        ChangeNavigationState(new NavigationState_TargetPosition(guy, 99, guy.ec.CellFromPosition(guy.ec.elevators.Last().transform.position).CenterWorldPosition));
    }

    public override void DestinationEmpty()
    {
        if (guy.ec.CellFromPosition(guy.transform.position) == guy.ec.CellFromPosition(guy.ec.elevators.Last().transform.position))
            guy.Despawn();
        else
            ChangeNavigationState(new NavigationState_TargetPosition(guy, 99, guy.ec.CellFromPosition(guy.ec.elevators.Last().transform.position).CenterWorldPosition));
    }

    public override void PlayerSighted(PlayerManager player)
    {
    }

    public override void Update()
    {
    }
}