// SPDX-FileCopyrightText: 2023 deltanedas <deltanedas@laptop>
// SPDX-FileCopyrightText: 2023 deltanedas <user@zenith>
// SPDX-FileCopyrightText: 2023 keronshb <54602815+keronshb@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 deltanedas <39013340+deltanedas@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 deltanedas <@deltanedas:kde.org>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Clothing.Components;
using Content.Shared.Ninja.Components;
using Content.Shared.Ninja.Systems;
using Content.Shared.Toggleable;
using Robust.Client.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Client.Ninja.Systems;

public sealed class NinjaGlovesSystem : SharedNinjaGlovesSystem
{
    private const string NinjaGlovesPrototype = "ClothingHandsGlovesSpaceNinja";
    private const string InactiveState = "icon";
    private const string ActiveState = "icon-green";

    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaGlovesComponent, AppearanceChangeEvent>(OnAppearanceChange);
        SubscribeLocalEvent<NinjaGlovesComponent, AfterAutoHandleStateEvent>(OnAutoHandleState);
    }

    private void OnAppearanceChange(Entity<NinjaGlovesComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        UpdateSprite(ent.Owner, args.Sprite);
    }

    private void OnAutoHandleState(Entity<NinjaGlovesComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (TryComp<SpriteComponent>(ent.Owner, out var sprite))
            UpdateSprite(ent.Owner, sprite);
    }

    private void UpdateSprite(EntityUid uid, SpriteComponent sprite, ChameleonClothingComponent? chameleon = null)
    {
        if (!Resolve(uid, ref chameleon, false) ||
            chameleon.Default != NinjaGlovesPrototype)
        {
            return;
        }

        if (!_sprite.LayerMapTryGet((uid, sprite), ToggleableVisuals.Layer, out var layer, false))
            return;

        var enabled = false;
        if (TryComp<AppearanceComponent>(uid, out var appearance))
            _appearance.TryGetData(uid, ToggleableVisuals.Enabled, out enabled, appearance);

        _sprite.LayerSetRsiState((uid, sprite), layer, enabled ? ActiveState : InactiveState);
    }
}
