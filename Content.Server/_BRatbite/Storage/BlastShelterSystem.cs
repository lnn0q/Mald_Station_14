using Content.Shared._BRatbite.Storage;
using Content.Shared.Explosion;

namespace Content.Server._BRatbite.Storage;

public sealed class BlastShelterSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BlastShelterComponent, GetExplosionResistanceEvent>(OnGetExplosionResistance);
        SubscribeLocalEvent<BlastShelterComponent, BeforeExplodeEvent>(OnBeforeExplode);
    }

    private void OnGetExplosionResistance(Entity<BlastShelterComponent> ent, ref GetExplosionResistanceEvent args)
    {
        args.DamageCoefficient *= ent.Comp.DamageCoefficient;
    }

    private void OnBeforeExplode(Entity<BlastShelterComponent> ent, ref BeforeExplodeEvent args)
    {
        args.DamageCoefficient *= ent.Comp.DamageCoefficient;
    }
}
