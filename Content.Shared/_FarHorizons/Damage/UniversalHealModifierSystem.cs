using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
namespace Content.Shared._FarHorizons.Damage;

public sealed class UniversalHealModifierSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<UniversalHealModifierComponent, DamageModifyEvent>(OnDamageModify);
    }

    private void OnDamageModify(Entity<UniversalHealModifierComponent> ent, ref DamageModifyEvent args)
    {
        DamageSpecifier damage = new();
        foreach (var (key, value) in args.Damage.DamageDict)
            if (value < 0)
                damage.DamageDict[key] = value * ent.Comp.Modifier;
            else
                damage.DamageDict[key] = value;

        args.Damage = damage;
    }
}
