using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ABWEvents.Events;

public class TokenCollectorEvent : BonusEventBase // It's just like the gravity event...
{
    [SerializeField] internal TokenCollectorToken tokenPre;
    public List<WeightedItemObject> weightedYTPs = new List<WeightedItemObject>(); // Adding your own custom YTPs and still have its own ability like the Times YTP and Divided YTP!
    public static Dictionary<ItemObject, Material> tokenMaterialSets { get; private set; } = new Dictionary<ItemObject, Material>();
    private List<TokenCollectorToken> tokens = new List<TokenCollectorToken>();
    [SerializeField] internal int minTokens = 20, maxTokens = 40, minRespawnTime = 15, maxRespawnTime = 20;
    [SerializeField] internal float initialSpawnDelay = 0.4f, minDistanceNotNeeded = 15f;
    private List<Cell> tiles = new List<Cell>();

    public override void Begin()
    {
        base.Begin();
        tiles.AddRange(ec.mainHall.AllTilesNoGarbage(false, true));
        StartCoroutine(SpawnTokens());
    }

    private IEnumerator RespawnTimer()
    {
        float time = UnityEngine.Random.Range(minRespawnTime, maxRespawnTime);
        while (time > 0f)
        {
            time -= Time.deltaTime * ec.EnvironmentTimeScale;
            yield return null;
        }

        StartCoroutine(SpawnTokens());
        yield break;
    }

    private IEnumerator SpawnTokens()
    {
        if (Active)
        {
            List<Cell> tilesThatAreNotNear = new List<Cell>();
            foreach (var cell in tiles)
            {
                var transforms = new List<Transform>();
                transforms.AddRange(ec.Npcs.Select(x => x?.transform));
                transforms.AddRange(ec.Players.Select(x => x?.transform));
                bool nearby = false;
                foreach (var _transform in transforms)
                {
                    if (_transform == null) continue;
                    if (ec.GetDistance(cell, ec.CellFromPosition(_transform.position)) <= minDistanceNotNeeded)
                    {
                        nearby = true;
                        break;
                    }
                }
                if (nearby) continue;
                tilesThatAreNotNear.Add(cell);
            }
            int num = crng.Next(minTokens, maxTokens);
            float timer = initialSpawnDelay;
            for (int i = 0; i < num; i++)
            {
                if (tilesThatAreNotNear.Count <= 0)
                    break;

                while (timer > 0f)
                {
                    timer -= Time.deltaTime * ec.EnvironmentTimeScale;
                    yield return null;
                }
                int index = crng.Next(0, tilesThatAreNotNear.Count);
                SpawnToken(tilesThatAreNotNear[index]);
                tilesThatAreNotNear.RemoveAt(index);
                timer = initialSpawnDelay;
            }

            StartCoroutine(RespawnTimer());
        }
        yield break;
    }

    private void SpawnToken(Cell tile)
    {
        TokenCollectorToken token = Instantiate(tokenPre, tile.TileTransform);
        token.transform.localPosition += Vector3.up * 5f;
        var itemobject = WeightedItemObject.ControlledRandomSelectionList(WeightedItemObject.Convert(weightedYTPs), crng);
        token.Initialize(this, ec, itemobject, tokenMaterialSets[itemobject]);
        tokens.Add(token);
    }

    public void DestroyToken(TokenCollectorToken token)
    {
        tokens.Remove(token);
        token.Destroy();
    }

    public override void End()
    {
        base.End();
        for (int i = tokens.Count - 1; i >= 0; i--)
            DestroyToken(tokens[i]);
    }
}

public class TokenCollectorToken : MonoBehaviour
{
    //[SerializeField] internal int ytps = 15;
    private ItemObject itemToInst;
    //[SerializeField] internal SoundObject collected;
    [SerializeField] internal AudioManager audMan;
    [SerializeField] internal Renderer render;
    private TokenCollectorEvent _event;
    private EnvironmentController ec;
    private IEnumerator despawnAnim;
    public bool IsDespawning => despawnAnim != null;

    public void Initialize(TokenCollectorEvent activeEvent, EnvironmentController ec, ItemObject itemObject, Material tokenMat)
    {
        _event = activeEvent;
        this.ec = ec;
        render.SetMaterial(tokenMat);
        itemToInst = itemObject;
        IEnumerator SpawnAnim()
        {
            float timer = 0f;
            while (timer < 1f)
            {
                timer += 1f * (Time.deltaTime * ec.EnvironmentTimeScale);
                transform.localScale = Vector3.Lerp(Vector3.zero, Vector3.one * 3f, timer);
                transform.Rotate(Vector3.down * (Time.deltaTime * ec.EnvironmentTimeScale) * (float)Math.PI * 25f);
                yield return null;
            }
            despawnAnim = null;
            yield break;
        }
        despawnAnim = SpawnAnim();
        StartCoroutine(despawnAnim);
    }

    private void Update()
    {
        transform.Rotate((Vector3.down * 25f) * Time.deltaTime * (float)Math.PI * 2f, Space.World);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_event != null && !IsDespawning && other.isTrigger)
        {
            if (other.CompareTag("Player") || other.CompareTag("NPC"))
            {
                _event.DestroyToken(this);
                if (other.CompareTag("Player"))
                {
                    var component = other.GetComponent<PlayerManager>();
                    if (component != null)
                    {
                        CoreGameManager.Instance.audMan.PlaySingle(itemToInst.audPickupOverride);
                        Instantiate(itemToInst.item).Use(component);
                    }
                        //CoreGameManager.Instance.AddPoints(ytps, component.playerNumber, true, ytps > 0);
                }
            }
        }
    }

    internal void Destroy()
    {
        IEnumerator DespawnAnim()
        {
            float timer = 0f;
            while (timer < 1f)
            {
                timer += 1.5f * (Time.deltaTime * ec.EnvironmentTimeScale);
                transform.localScale = Vector3.Lerp(Vector3.one * 3f, Vector3.zero, timer);
                transform.Rotate(Vector3.one * (Time.deltaTime * ec.EnvironmentTimeScale) * (float)Math.PI * 45f);
                yield return null;
            }
            Destroy(gameObject);
            yield break;
        }
        audMan.PlaySingle(itemToInst.audPickupOverride);
        despawnAnim = DespawnAnim();
        StartCoroutine(despawnAnim);
    }
}