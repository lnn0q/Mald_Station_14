using Content.Server._FarHorizons.Silicons.Glitching;
using Content.Server.Body.Systems;
using Content.Server.Emp;
using Content.Server.Hands.Systems;
using Content.Server.Stunnable;
using Content.Goobstation.Common.Standing;
using Content.Shared._FarHorizons.Silicons.HumanoidEMP;
using Content.Shared.Damage;
using Content.Shared.StatusEffect;
using Robust.Shared.Timing;

namespace Content.Server._FarHorizons.Silicons.HumanoidEMP;

public sealed partial class HumanoidEMPSystem : EntitySystem
{
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly StunSystem _stunSystem = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;
    [Dependency] private readonly HandsSystem _hands = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly GlitchingSystem _glitching = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HumanoidEMPComponent, EmpPulseEvent>(OnHumanoidEMP);
    }

    private void OnHumanoidEMP(Entity<HumanoidEMPComponent> ent, ref EmpPulseEvent args)
    {
        if (args.Disabled || _timing.CurTime < ent.Comp.NextEffect)
            return;

        ent.Comp.NextEffect = _timing.CurTime + ent.Comp.EffectCooldown;

        var effect = ent.Comp.Effect;
        if (TryComp(ent, out HumanoidEMPCompositeComponent? composite))
            effect = CompositeEffect((ent, composite));

        ApplyEffect(ent, effect);
    }

    public HumanoidEMPEffect CompositeEffect(Entity<HumanoidEMPCompositeComponent> ent)
    {
        HumanoidEMPEffect composite = new();

        if (TryComp<HumanoidEMPComponent>(ent, out var empComp))
            composite = empComp.Effect;

        foreach (var part in _body.GetBodyChildren(ent))
        {
            if (TryComp<HumanoidEMPCompositeElementComponent>(part.Id, out var compositeElement))
                composite += compositeElement.Effect;
        }

        foreach (var organ in _body.GetBodyOrgans(ent))
        {
            if (TryComp<HumanoidEMPCompositeElementComponent>(organ.Id, out var compositeElement))
                composite += compositeElement.Effect;
        }

        return composite;
    }

    public void ApplyEffect(EntityUid ent, HumanoidEMPEffect effect)
    {
        _stunSystem.TryKnockdown(ent, effect.KnockdownAmount, false, DropHeldItemsBehavior.AlwaysDrop);
        _stunSystem.TryStun(ent, effect.StunAmount, false);
        _damageable.TryChangeDamage(ent, effect.DamageAmount);

        foreach (var statusEffect in effect.AdditionalEffects)
            _status.TryAddStatusEffect(ent, statusEffect.Key, statusEffect.Value, true);

        if (effect.SlowdownAmount > TimeSpan.Zero)
            _stunSystem.TrySlowdown(ent, effect.SlowdownAmount, true, effect.WalkSpeedModifier, effect.SprintSpeedModifier);

        foreach (var hand in effect.DropItemsFrom)
            _hands.DoDrop(ent, hand);

        if (effect.GlitchDuration <= TimeSpan.Zero)
            return;

        var rampTime = effect.GlitchDuration / 4;
        _glitching.ApplyGlitch(ent, effect.GlitchDuration, rampTime);
    }
}
