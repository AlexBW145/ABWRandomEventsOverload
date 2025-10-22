using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ABWEvents.Events;

public class TrafficTroubleEvent : RandomEvent
{
    [SerializeField] internal Material[] roads = new Material[16];
    [SerializeField] internal GameObject roadPrefab;
    [SerializeField] internal TrafficTroubleTunnel tunnel;
    //private DijkstraMap _roadPlacementMap;
    private Dictionary<Cell, Tuple<MeshRenderer, int>> createdRoads = new Dictionary<Cell, Tuple<MeshRenderer, int>>();
    [SerializeField, Range(4, 64)] internal int minRoads = 4, maxRoads = 6;
    [SerializeField] internal TrafficTroubleCar trafficCarPrefab;
    private List<IntVector2> spawnPoints = new List<IntVector2>();
    [SerializeField] internal float timerInitial = 4f;
    private float timerUntilNextCar;
    private int nextElevatorPos = 0;

    internal static TrafficTroubleEvent Instance { get; private set; }

    public override void Initialize(EnvironmentController controller, System.Random rng)
    {
        base.Initialize(controller, rng);
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public override void Begin()
    {
        timerUntilNextCar = timerInitial;
        spawnPoints.Shuffle();
        base.Begin();
    }

    public void TempOpen()
    {
        ec.FreezeNavigationUpdates(true);
        BlockSurroundingCells(true);
        ec.FreezeNavigationUpdates(false);
    }

    public void TempClose()
    {
        ec.FreezeNavigationUpdates(true);
        BlockSurroundingCells(false);
        ec.FreezeNavigationUpdates(false);
    }

    private void BlockSurroundingCells(bool block)
    {
        foreach (var cell in createdRoads.Keys)
        {
            for (int i = 0; i < 4; i++)
            {
                if (!ec.ContainsCoordinates(cell.position + ((Direction)i).ToIntVector2())) continue;
                var othercell = ec.CellFromPosition(cell.position + ((Direction)i).ToIntVector2());
                if (!spawnPoints.Contains(othercell.position) && !othercell.Null && cell.ConstNavigable((Direction)i) && !createdRoads.ContainsKey(othercell))
                {
                    othercell.Block(((Direction)i).GetOpposite(), block);
                    cell.Block((Direction)i, block);
                }
            }
        }
    }

    private void Update()
    {
        if (active)
        {
            if (timerUntilNextCar > 0f)
                timerUntilNextCar -= Time.deltaTime * ec.EnvironmentTimeScale;
            else
            {
                var newcar = ec.SpawnNPC(trafficCarPrefab, spawnPoints[nextElevatorPos++]) as TrafficTroubleCar;
                if (nextElevatorPos > spawnPoints.Count - 1)
                {
                    nextElevatorPos = 0;
                    spawnPoints.Shuffle();
                }
                newcar.Initialize(ec.CellFromPosition(spawnPoints[nextElevatorPos]).CenterWorldPosition);
                timerUntilNextCar = timerInitial;
            }
        }
    }

    private List<Cell> premadeElevators;
    public override void PremadeSetup()
    {
        base.PremadeSetup();
        premadeElevators = new List<Cell>();
        LevelLoader loader = FindObjectOfType<LevelLoader>(false);
        foreach (var points in FindObjectsOfType<TunnelPlacement>(false))
        {
            points.ll = loader;
            points.MarkAsFound(this);
        }
        var possibleCombinations = new List<Tuple<Cell, Cell>>();
        for (int elv = 0; elv < premadeElevators.Count; elv++)
        {
            var otherelvs = premadeElevators.Where(x => x != premadeElevators[elv]).ToList();
            for (int other = 0; other < otherelvs.Count; other++)
                possibleCombinations.Add(new Tuple<Cell, Cell>(premadeElevators[elv], otherelvs[other]));
        }
        premadeElevators.Clear();
        for (int elv = 0; elv < possibleCombinations.Count; elv++)
        {
            if (!GenerateRoad(possibleCombinations[elv].Item1, possibleCombinations[elv].Item2))
                ABWEventsPlugin.Logger.LogWarning("One of the traffic trouble building thingies have failed or got an error.");
            else
            {
                if (!spawnPoints.Contains(possibleCombinations[elv].Item1.position))
                    spawnPoints.Add(possibleCombinations[elv].Item1.position);
                if (!spawnPoints.Contains(possibleCombinations[elv].Item2.position))
                    spawnPoints.Add(possibleCombinations[elv].Item2.position);
            }
        }
        var list = createdRoads.ToList();
        for (int i = 0; i < list.Count; i++)
        {
            for (int dir = 0; dir < 4; dir++)
            {
                if (!ec.ContainsCoordinates(list[i].Key.position + ((Direction)dir).ToIntVector2())) continue;
                var othercell = ec.CellFromPosition(list[i].Key.position + ((Direction)dir).ToIntVector2());
                if ((createdRoads.ContainsKey(othercell) || spawnPoints.Contains(othercell.position))
                    && list[i].Value.Item2.ContainsDirection((Direction)dir)
                    && !list[i].Key.ConstBin.ContainsDirection((Direction)dir))
                {
                    int tileshape = createdRoads[list[i].Key].Item2 - (1 << dir);
                    list[i].Value.Item1.transform.parent.rotation = BinToRotation(tileshape);
                    createdRoads[list[i].Key] = new Tuple<MeshRenderer, int>(list[i].Value.Item1, tileshape);
                }
            }
            list[i].Value.Item1.SetMaterial(roads[createdRoads[list[i].Key].Item2]);
        }
        //TempOpen();
    }

    internal class TunnelPlacement : TileBasedObject, IEventSpawnPlacement
    {
        internal LevelLoader ll;

        public Cell GetCellPos(EnvironmentController ec) => ec.CellFromPosition(ec.CellFromPosition(transform.position).position + direction.ToIntVector2());
        public void MarkAsFound(RandomEvent _event)
        {
            var traffictrouble = (TrafficTroubleEvent)_event;
            var pos = GetCellPos(traffictrouble.ec).position;
            var oppositeDirection = direction.GetOpposite();
            if (!GetCellPos(traffictrouble.ec).Null) // Studio users try not to leave a placement with a initalized cell behind it.
            {
                Destroy(gameObject);
                return;
            }
            var elevatorRoom = traffictrouble.CreateTunnelRoom(ll, pos, oppositeDirection);
            elevatorRoom.ConnectRooms(traffictrouble.ec.mainHall);

            traffictrouble.ec.CreateCell(15, elevatorRoom.transform, pos, elevatorRoom);
            traffictrouble.ec.ConnectCells(pos, oppositeDirection);
            var outCell = traffictrouble.ec.CellFromPosition(pos + oppositeDirection.ToIntVector2());
            TrafficTroubleTunnel traffittunnel = Instantiate(traffictrouble.tunnel);
            traffictrouble.ec.SetupDoor(traffittunnel, outCell, oppositeDirection.GetOpposite());
            traffittunnel.Initialize();

            var cell = traffictrouble.ec.CellFromPosition(pos);
            cell.HardCoverEntirely();
            cell.offLimits = true;
            cell.hideFromMap = true;
            traffictrouble.premadeElevators.Add(cell);
            Destroy(gameObject);
        }
    }

    public override void AfterUpdateSetup(System.Random rng)
    {
        LevelGenerator lg = FindObjectOfType<LevelGenerator>(false);
        base.AfterUpdateSetup(rng);
        List<Cell> hallcells = ec.mainHall.GetNewTileList();

        // I am no smart person, this code is from the NPC Elevator.
        List<IntVector2> finalSpots = [];
        List<Direction> finalDirections = [];

        for (int i = 0; i < hallcells.Count; i++)
        {
            if (hallcells[i].HasAnyAllCoverage)
            {
                var dir = hallcells[i].RandomUncoveredDirection(rng);
                var nextCell = ec.CellFromPosition(hallcells[i].position + dir.ToIntVector2());
                if (nextCell.Null)
                {
                    finalSpots.Add(nextCell.position);
                    finalDirections.Add(dir.GetOpposite());
                }
            }
        }
        // That's all.

        if (finalSpots.Count == 0)
        {
            ABWEventsPlugin.Logger.LogWarning("Traffic Event Trouble has not found anywhere to place a single tunnel.");
            return;
        }

        int total = rng.Next(minRoads, maxRoads + 1);
        var elevators = new List<Cell>();
        for (int i = 0; i < total; i++)
        {
            if (finalSpots.Count == 0)
                break;

            int idx = rng.Next(finalSpots.Count);

            var elevatorRoom = CreateTunnelRoom(lg, finalSpots[idx], finalDirections[idx]);
            elevatorRoom.ConnectRooms(ec.mainHall);

            ec.CreateCell(15, elevatorRoom.transform, finalSpots[idx], elevatorRoom);
            ec.ConnectCells(finalSpots[idx], finalDirections[idx]);
            var outCell = ec.CellFromPosition(finalSpots[idx] + finalDirections[idx].ToIntVector2());
            //outCell.Block(finalDirections[idx].GetOpposite(), true);
            TrafficTroubleTunnel traffittunnel = Instantiate(tunnel);
            ec.SetupDoor(traffittunnel, outCell, finalDirections[idx].GetOpposite());
            traffittunnel.Initialize();

            var cell = ec.CellFromPosition(finalSpots[idx]);
            cell.HardCoverEntirely();
            cell.offLimits = true;
            cell.hideFromMap = true;

            finalSpots.RemoveAt(idx);
            finalDirections.RemoveAt(idx);

            elevators.Add(cell);
        }
        //_roadPlacementMap = new DijkstraMap(ec, PathType.Nav, int.MaxValue);

        var possibleCombinations = new List<Tuple<Cell, Cell>>();
        for (int elv = 0; elv < elevators.Count; elv++)
        {
            var otherelvs = elevators.Where(x => x != elevators[elv]).ToList();
            for (int other = 0; other < otherelvs.Count; other++)
                possibleCombinations.Add(new Tuple<Cell, Cell>(elevators[elv], otherelvs[other]));
        }
        for (int elv = 0; elv < possibleCombinations.Count; elv++)
        {
            if (!GenerateRoad(possibleCombinations[elv].Item1, possibleCombinations[elv].Item2))
                ABWEventsPlugin.Logger.LogWarning("One of the traffic trouble building thingies have failed or got an error.");
            else
            {
                if (!spawnPoints.Contains(possibleCombinations[elv].Item1.position))
                    spawnPoints.Add(possibleCombinations[elv].Item1.position);
                if (!spawnPoints.Contains(possibleCombinations[elv].Item2.position))
                    spawnPoints.Add(possibleCombinations[elv].Item2.position);
            }
        }
        var list = createdRoads.ToList();
        for (int i = 0; i < list.Count; i++)
        {
            for (int dir = 0; dir < 4; dir++)
            {
                if (!ec.ContainsCoordinates(list[i].Key.position + ((Direction)dir).ToIntVector2())) continue;
                var othercell = ec.CellFromPosition(list[i].Key.position + ((Direction)dir).ToIntVector2());
                if ((createdRoads.ContainsKey(othercell) || spawnPoints.Contains(othercell.position))
                    && list[i].Value.Item2.ContainsDirection((Direction)dir)
                    && !list[i].Key.ConstBin.ContainsDirection((Direction)dir))
                {
                    int tileshape = createdRoads[list[i].Key].Item2 - (1 << dir);
                    list[i].Value.Item1.transform.parent.rotation = BinToRotation(tileshape);
                    createdRoads[list[i].Key] = new Tuple<MeshRenderer, int>(list[i].Value.Item1, tileshape);
                }
            }
            list[i].Value.Item1.SetMaterial(roads[createdRoads[list[i].Key].Item2]);
        }
        //TempOpen();
    }

    int roomId = 0;
    private static Texture2D dark = Resources.FindObjectsOfTypeAll<Texture2D>().Last(x => x.name == "BlackTexture" && x.isReadable);
    RoomController CreateTunnelRoom(LevelBuilder lg, IntVector2 position, Direction dir)
    {
        var elevatorRoom = Instantiate(lg.roomControllerPre, ec.transform);
        elevatorRoom.name = "TrafficTroubleTunnel_" + (++roomId);
        elevatorRoom.ec = ec;
        elevatorRoom.color = Color.gray;
        elevatorRoom.transform.localPosition = Vector3.zero;
        elevatorRoom.type = RoomType.Room;
        elevatorRoom.category = RoomCategory.Null;
        elevatorRoom.offLimits = true;
        elevatorRoom.acceptsExits = false;
        elevatorRoom.acceptsPosters = false;

        elevatorRoom.wallTex = dark;
        elevatorRoom.ceilTex = dark;
        elevatorRoom.florTex = dark;
        elevatorRoom.GenerateTextureAtlas();

        elevatorRoom.position = position;
        elevatorRoom.size = new(1, 1);
        elevatorRoom.maxSize = new(1, 1);
        elevatorRoom.dir = dir;

        ec.rooms.Add(elevatorRoom); // Without this, culling manager crashes because it'll never assign a chunk to the cells of this room
        return elevatorRoom;
    }

    private bool GenerateRoad(Cell elevator, Cell connectTo)
    {
        if (ec.FindPath(elevator.position, connectTo.position, PathType.Nav, out var path))
        {
            try // I had issues and that one thing was hindering my brain.
            {
                BuildRoad(path);
            }
            catch (Exception ex)
            {
                ABWEventsPlugin.Logger.LogError(ex);
                throw ex;
            }
            return true;
        }
        return false;
    }

    private void BuildRoad(List<IntVector2> path)
    {
        for (int i = 1; i < path.Count; i++)
        {
            Cell cell = ec.CellFromPosition(path[i - 1]);
            Cell nextCell = ec.CellFromPosition(path[i]);
            if (cell?.room?.category == RoomCategory.Null)
                continue;
            Direction direction = Directions.FromPointAToB(cell.position, nextCell.position);
            if (createdRoads.ContainsKey(cell))
            {
                /*var thatroad = createdRoads[cell];
                var tileshape = thatroad.Item2;
                if (tileshape.ContainsDirection(direction)) // This hinderance...
                    tileshape = thatroad.Item2 - (1 << (int)direction);
                else
                    continue;
                thatroad.Item1.transform.parent.rotation = BinToRotation(tileshape);
                createdRoads[cell] = new Tuple<MeshRenderer, int>(thatroad.Item1, tileshape);*/
            }
            else
            {
                GameObject road = Instantiate(roadPrefab, cell.TileTransform);
                road.transform.position = cell.FloorWorldPosition;
                cell.renderers.AddRange(road.GetComponent<RendererContainer>().renderers);
                /*var tileshape = 15 - (1 << (int)direction);
                road.transform.rotation = BinToRotation(tileshape);*/
                cell.SoftCover(CellCoverage.Down);
                createdRoads.Add(cell, new Tuple<MeshRenderer, int>(road.GetComponentInChildren<MeshRenderer>(), 15));
            }
        }
    }

    private Quaternion BinToRotation(int bin)
    {
        switch (bin)
        {
            default:
                return default;
            case 5 or 8 or 9:
                return Quaternion.Euler(0f, 90f, 0f);
            case 1 or 3:
                return Quaternion.Euler(0f, 180f, 0f);
            case 2 or 6:
                return Quaternion.Euler(0f, 270f, 0f);
        }
    }
}

public class TrafficTroubleTunnel : Door
{
    private Texture2D[] bg = new Texture2D[2];

    public MeshRenderer[] walls;

    public Material[] mask;

    public Material[] overlayShut;

    public Material[] overlayOpen;

    [SerializeField]
    internal Sprite mapSprite;

    private MapTile aMapTile;

    private MapTile bMapTile;

    public override void Initialize()
    {
        base.Initialize();
        ec.tempOpenBully += TempOpen;
        ec.tempCloseBully += TempClose;
    }

    private void TempOpen()
    {
        ec.FreezeNavigationUpdates(true);
        Block(false);
        ec.FreezeNavigationUpdates(false);
    }

    private void TempClose()
    {
        ec.FreezeNavigationUpdates(true);
        Block(true);
        ec.FreezeNavigationUpdates(false);
    }

    private void Start()
    {
        bg[0] = aTile.room.wallTex;
        bg[1] = bTile.room.wallTex;
        UpdateTextures();
        aMapTile = ec.map.AddExtraTile(aTile.position);
        aMapTile.SpriteRenderer.sprite = mapSprite;
        aMapTile.SpriteRenderer.color = aTile.room.color;
        aMapTile.transform.rotation = direction.ToUiRotation();
        bMapTile = ec.map.AddExtraTile(bTile.position);
        bMapTile.SpriteRenderer.sprite = mapSprite;
        bMapTile.transform.rotation = direction.GetOpposite().ToUiRotation();
        bMapTile.SpriteRenderer.color = bTile.room.color;
    }

    public void UpdateTextures()
    {
        for (int i = 0; i < walls.Length; i++)
        {
            MaterialModifier.ChangeHole(walls[i], mask[i], overlayShut[i]);
            MaterialModifier.SetBase(walls[i], bg[i]);
        }
    }

    public override void Open(bool cancelTimer, bool makeNoise)
    {
    }

    public override void Shut()
    {
    }

    public override void OpenTimed(float time, bool makeNoise)
    {
    }

    public override void Lock(bool cancelTimer)
    {
    }

    public override void LockTimed(float time)
    {
    }

    public override void Unlock()
    {
    }
}

public class TrafficTroubleCar : NPC
{
    [SerializeField] internal AudioManager motorAudMan, audMan;
    [SerializeField] internal QuickExplosion deathSignal;
    [SerializeField] internal SoundObject push;
    [SerializeField] internal SoundObject[] horn;
    [SerializeField] internal float speed = 35f;
    private float sightedCooldown;
    [SerializeField] internal List<SpriteRotationMap> sheetSelection = new List<SpriteRotationMap>();
    [SerializeField] internal AnimatedSpriteRotator rotatorMan;

    public override void Initialize()
    {
        base.Initialize();
        behaviorStateMachine.ChangeState(new NpcState(this));
        behaviorStateMachine.ChangeNavigationState(new NavigationState_DoNothing(this, 0));
    }

    private static FieldInfo _sheet = AccessTools.DeclaredField(typeof(AnimatedSpriteRotator), "spriteMap");
    public void Initialize(Vector3 endPos)
    {
        _sheet.SetValue(rotatorMan, new SpriteRotationMap[] { sheetSelection[Mathf.RoundToInt(UnityEngine.Random.Range(0f, sheetSelection.Count - 1f))] });
        behaviorStateMachine.ChangeState(new TrafficTroubleCar_Wandering(this, endPos));
    }

    protected override void VirtualUpdate()
    {
        motorAudMan.pitchModifier = 1f + navigator.speed / 50f;
        if (sightedCooldown > 0f)
            sightedCooldown -= Time.deltaTime * ec.NpcTimeScale;
    }

    public void Honk()
    {
        if (sightedCooldown <= 0f)
        {
            audMan.PlayRandomAudio(horn);
            sightedCooldown = 1.5f;
        }
    }

    public void Push(Entity victim)
    {
        audMan.PlaySingle(push);
        victim.AddForce(new Force(transform.forward * navigator.Speed, 5f, -5f));
    }

    public void Explode()
    {
        Instantiate(deathSignal, transform.position, default);
        Despawn();
    }
}

public class TrafficTroubleCar_Wandering : NpcState
{
    protected TrafficTroubleCar car;
    private Vector3 destination;
    private float _rotation, timeSinceIdling;
    public TrafficTroubleCar_Wandering(TrafficTroubleCar car, Vector3 endPos) : base(car) { this.car = car; destination = endPos; }

    public override void Enter()
    {
        base.Enter();
        ChangeNavigationState(new NavigationState_TargetPosition(npc, 1, destination));
        npc.Navigator.SetSpeed(car.speed);
    }

    private HashSet<NPC> sightedNPCs = new HashSet<NPC>();

    public override void Update()
    {
        base.Update();
        foreach (var npc in car.ec.Npcs)
        {
            if (npc != car && npc.Character != Character.Null && npc.Navigator.enabled) // Bully's navigator component is disabled
            {
                car.looker.Raycast(npc.transform, Mathf.Min((car.transform.position - npc.transform.position).magnitude, car.looker.distance, car.ec.MaxRaycast), out bool sighted);
                if (sighted && !sightedNPCs.Contains(npc))
                {
                    sightedNPCs.Add(npc);
                    car.Honk();
                }
                else if (!sighted)
                    sightedNPCs.Remove(npc);
            }
        }
        sightedNPCs.RemoveWhere(x => x == null);

        _rotation = Mathf.DeltaAngle(npc.transform.eulerAngles.y, Mathf.Atan2(npc.Navigator.NextPoint.x - npc.transform.position.x, npc.Navigator.NextPoint.z - npc.transform.position.z) * 57.29578f);
        if (Mathf.Abs(_rotation) > 5f)
            npc.Navigator.SetSpeed(10f);

        if (!npc.Navigator.HasDestination && !(npc.Navigator.Velocity.magnitude > 0f))
        {
            if (timeSinceIdling < 5f)
                timeSinceIdling += Time.deltaTime * npc.ec.NpcTimeScale;
            else
                car.Explode();
        }
        else
            timeSinceIdling = 0f;
    }

    public override void DestinationEmpty()
    {
        base.DestinationEmpty();
        if (npc.transform.position == destination)
            npc.Despawn();
        else
            currentNavigationState.UpdatePosition(destination);
    }

    public override void PlayerSighted(PlayerManager player)
    {
        base.PlayerSighted(player);
        car.Honk();
    }

    public override void OnStateTriggerEnter(Collider other, bool validCollision)
    {
        if ((other.CompareTag("Player") || other.CompareTag("NPC")) && validCollision)
        {
            var entity = other.gameObject.GetComponent<Entity>();
            if (entity != null)
            {
                if (other.GetComponent<Balder_Entity>() != null // Considered to be evil.
                    || other.GetComponent<TrafficTroubleCar>() != null // Reasonable despawning
                    || entity.Frozen) // Consider the laughing stock
                    car.Explode();
                else
                    car.Push(entity);
            }
        }
    }
}