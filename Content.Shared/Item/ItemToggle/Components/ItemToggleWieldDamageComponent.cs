// SPDX-FileCopyrightText: 2026 Sprinkle <40203084+lnn0q@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Damage;
using Robust.Shared.GameStates;

namespace Content.Shared.Item.ItemToggle.Components;

/// <summary>
/// Handles changes to wield bonus damage when the item is toggled.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ItemToggleWieldDamageComponent : Component
{
    /// <summary>
    /// Bonus damage applied by <see cref="Wieldable.Components.IncreaseDamageOnWieldComponent"/> when activated.
    /// </summary>
    [DataField, AutoNetworkedField]
    public DamageSpecifier? ActivatedDamage;

    /// <summary>
    /// Bonus damage applied by <see cref="Wieldable.Components.IncreaseDamageOnWieldComponent"/> when deactivated.
    /// </summary>
    [DataField, AutoNetworkedField]
    public DamageSpecifier? DeactivatedDamage;
}
