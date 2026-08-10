using Content.Shared.Weapons.Ranged.Events;
using Content.Shared._BRatbite.Weapons.Ranged;
using Content.Shared.NukeOps;

namespace Content.Server._BRatbite.Weapons.Ranged;

public sealed partial class CombatTrainedSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CombatTrainedComponent, GunRefreshModifiersEvent>(OnGunRefreshModifiers);
        SubscribeLocalEvent<NukeOperativeComponent, ComponentStartup>(OnInitializeNukeOps);
    }

    private void OnInitializeNukeOps(Entity<NukeOperativeComponent> ent, ref ComponentStartup args)
    {
        // Add it like this to nukeops because there are a bunch of nuclear operative prototypes
        // And I don't want to add them manually
        AddComp<CombatTrainedComponent>(ent.Owner);
    }

    private void OnGunRefreshModifiers(Entity<CombatTrainedComponent> ent, ref GunRefreshModifiersEvent args)
    {
        args.MinAngle *= ent.Comp.AccuracyMultiplier;
        args.MaxAngle *= ent.Comp.AccuracyMultiplier;
        args.AngleIncrease *= ent.Comp.AccuracyMultiplier;
    }
}
