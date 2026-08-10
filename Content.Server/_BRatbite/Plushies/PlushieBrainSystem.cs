// SPDX-FileCopyrightText: 2026 Sprinkle <40203084+lnn0q@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Actions;
using Content.Server.Body.Components;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Mind;
using Content.Shared._BRatbite.Plushies;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Destructible;
using Content.Shared.Examine;
using Content.Shared.Mind.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Speech.Muting;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Tools.Components;
using Content.Shared.Tools.EntitySystems;
using Robust.Shared.Containers;

namespace Content.Server._BRatbite.Plushies;

public sealed class PlushieBrainSystem : EntitySystem
{
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly MindSystem _mind = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlushieBrainComponent, ToolOpenableDoAfterEventToggleOpen>(OnToolOpenableToggled, after: [typeof(ToolOpenableSystem)]);
        SubscribeLocalEvent<PlushieBrainComponent, EntRemovedFromContainerMessage>(OnRemoved);
        SubscribeLocalEvent<PlushieBrainComponent, DestructionEventArgs>(OnDestroyed, before: [typeof(SecretStashSystem)]);
        SubscribeLocalEvent<PlushieBrainComponent, PlushieSqueakActionEvent>(OnSqueak);
    }

    private void OnToolOpenableToggled(Entity<PlushieBrainComponent> ent, ref ToolOpenableDoAfterEventToggleOpen args)
    {
        if (!TryComp<ToolOpenableComponent>(ent, out var openable))
            return;

        if (openable.IsOpen)
            Deactivate(ent);
        else
            TryActivate(ent);
    }

    private void OnRemoved(Entity<PlushieBrainComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (TryComp<SecretStashComponent>(ent, out var stash) && args.Container == stash.ItemContainer)
            Deactivate(ent, args.Entity);
    }

    private void OnDestroyed(Entity<PlushieBrainComponent> ent, ref DestructionEventArgs args)
    {
        Deactivate(ent);
    }

    private void OnSqueak(Entity<PlushieBrainComponent> ent, ref PlushieSqueakActionEvent args)
    {
        if (args.Handled)
            return;

        var trigger = new TriggerEvent(ent.Owner, ent.Owner);
        RaiseLocalEvent(ent.Owner, trigger);
        args.Handled = trigger.Handled;
    }

    private void TryActivate(Entity<PlushieBrainComponent> ent)
    {
        if (!TryGetBrain(ent, out var brain))
            return;

        if (!_mind.TryGetMind(brain, out var mindId, out var mind))
            return;

        EnsureComp<MindContainerComponent>(ent);
        EnsureComp<InputMoverComponent>(ent);
        EnsureComp<MobMoverComponent>(ent);
        EnsureComp<MovementSpeedModifierComponent>(ent);
        EnsureComp<ExaminerComponent>(ent);
        EnsureComp<ActionsComponent>(ent);

        ent.Comp.AddedMuted = !HasComp<MutedComponent>(ent);
        EnsureComp<MutedComponent>(ent);

        _actions.AddAction(ent.Owner, ref ent.Comp.ActionEntity, ent.Comp.Action, ent.Owner);
        _mind.TransferTo(mindId, ent.Owner, true, mind: mind);
    }

    private void Deactivate(Entity<PlushieBrainComponent> ent, EntityUid? brain = null)
    {
        brain ??= TryGetBrain(ent, out var contained) ? contained : null;

        if (brain != null && _mind.TryGetMind(ent, out var mindId, out var mind))
            _mind.TransferTo(mindId, brain, true, mind: mind);

        if (ent.Comp.ActionEntity != null)
            _actions.RemoveAction(ent.Owner, ent.Comp.ActionEntity);

        RemComp<ActionsComponent>(ent);
        RemComp<ExaminerComponent>(ent);
        RemComp<MovementSpeedModifierComponent>(ent);
        RemComp<MobMoverComponent>(ent);
        RemComp<InputMoverComponent>(ent);
        RemComp<MindContainerComponent>(ent);

        if (ent.Comp.AddedMuted)
            RemComp<MutedComponent>(ent);

        ent.Comp.AddedMuted = false;
        ent.Comp.ActionEntity = null;
    }

    private bool TryGetBrain(EntityUid uid, out EntityUid brain)
    {
        brain = EntityUid.Invalid;

        if (!TryComp<SecretStashComponent>(uid, out var stash))
            return false;

        var contained = stash.ItemContainer.ContainedEntity;
        if (contained == null || !HasComp<BrainComponent>(contained.Value))
            return false;

        brain = contained.Value;
        return true;
    }
}
