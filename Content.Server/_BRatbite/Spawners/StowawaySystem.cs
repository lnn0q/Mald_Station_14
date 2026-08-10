using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
using Content.Server.Storage.Components;
using Content.Shared.Inventory;
using Robust.Server.Containers;
using Robust.Shared.Containers;
using Robust.Shared.Random;

namespace Content.Server._BRatbite.Spawners;

public sealed partial class StowawaySystem : EntitySystem
{
    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly StationSpawningSystem _stationSpawning = default!;
    [Dependency] private readonly ILogManager _logManager = default!;
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = _logManager.GetSawmill("stowaway");

        SubscribeLocalEvent<PlayerSpawningEvent>(OnPlayerSpawn, before: new[] { typeof(ArrivalsSystem) });
        SubscribeLocalEvent<StowawayComponent, MapInitEvent>(OnStowawayInit);
    }

    private void OnPlayerSpawn(PlayerSpawningEvent args)
    {
        // Skip station AIs
        if (args.Job == "StationAi") return;
        if (!args.HumanoidCharacterProfile?.TraitPreferences?.Contains("Stowaway") ?? false)
            return;
        if (args.SpawnResult != null)
        {
            _sawmill.Warning($"Entity {args.SpawnResult} has StowawayComponent but was already spawned");
            return;
        }
        var query = EntityQueryEnumerator<StowawaySpawnPointComponent, ContainerManagerComponent, TransformComponent>();
        var closets = new List<Entity<StowawaySpawnPointComponent, ContainerManagerComponent, TransformComponent>>();
        while (query.MoveNext(out var uid, out var spawnPoint, out var container, out var transform))
        {
            if (args.Station != null && _station.GetOwningStation(uid, transform) != args.Station)
                continue;
            // Maybe in the future allow some jobs to be spawned in secure lockers
            if (spawnPoint.Secure) continue;
            closets.Add((uid, spawnPoint, container, transform));
        }
        if (closets.Count == 0)
            return;
        var spawnedEntity = _stationSpawning.SpawnPlayerMob(closets[0].Comp3.Coordinates, args.Job, args.HumanoidCharacterProfile, args.Station);
        args.SpawnResult = spawnedEntity;
        while (closets.Count != 0)
        {
            var (closetUid, spawnPoint, manager, xform) = _random.PickAndTake(closets);
            if (!_container.TryGetContainer(closetUid, spawnPoint.ContainerId, out var container, manager))
                continue;
            if (TryComp<EntityStorageComponent>(closetUid, out var entityStorage) && entityStorage.Open)
                continue;

            if (!_container.Insert(args.SpawnResult.Value, container, containerXform: xform))
                continue;

            return;
        }
        Del(args.SpawnResult);
        args.SpawnResult = null;
    }

    private void OnStowawayInit(Entity<StowawayComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<InventoryComponent>(ent, out var inventoryComp))
            return;
        foreach (var slot in inventoryComp.Slots)
        {
            if (!_random.Prob(ent.Comp.SlotDeletionChance)) continue;
            if (!_inventorySystem.TryUnequip(ent, slot.Name, out var removedItem, force: true, silent: true)) continue;
            Del(removedItem);
        }

    }
}
