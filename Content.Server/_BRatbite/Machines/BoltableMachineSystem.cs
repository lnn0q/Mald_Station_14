using Content.Server.Power.EntitySystems;
using Content.Server.Wires;
using Content.Shared._BRatbite.Machines;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Wires;
using Robust.Shared.Audio.Systems;

namespace Content.Server._BRatbite.Machines;

public sealed class BoltableMachineSystem : SharedMachineBoltableSystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly PowerReceiverSystem _power = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BoltableMachineComponent, SignalReceivedEvent>(OnSignalReceived);
    }

    public bool BoltWireCut(EntityUid user, Wire wire, BoltableMachineComponent comp)
    {
        if (!_power.IsPowered(comp.Owner))
            return true;

        SetBolted((comp.Owner, comp), true);

        return true;
    }

    public bool BoltWireMend(EntityUid user, Wire wire, BoltableMachineComponent comp)
    {
        // Mending bolt wire does nothing
        return true;
    }

    public void BoltWirePulse(EntityUid user, Wire wire, BoltableMachineComponent comp)
    {
        if (!_power.IsPowered(comp.Owner))
            return;

        SetBolted((comp.Owner, comp), !comp.Bolted);
    }

    private void OnSignalReceived(Entity<BoltableMachineComponent> ent, ref SignalReceivedEvent args)
    {
        if (!_power.IsPowered(ent))
            return;

        if (args.Port == ent.Comp.OnPort)
            SetBolted(ent, true);
        else if (args.Port == ent.Comp.OffPort)
            SetBolted(ent, false);
        else if (args.Port == ent.Comp.TogglePort)
            SetBolted(ent, !ent.Comp.Bolted);
    }

    private void SetBolted(Entity<BoltableMachineComponent> ent, bool bolted)
    {
        if (ent.Comp.Bolted == bolted)
            return;

        ent.Comp.Bolted = bolted;
        Dirty(ent);

        _audio.PlayPvs(bolted ? ent.Comp.BoltSound : ent.Comp.UnboltSound, ent);
    }
}
