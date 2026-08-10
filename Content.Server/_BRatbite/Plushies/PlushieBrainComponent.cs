// SPDX-FileCopyrightText: 2026 Sprinkle <40203084+lnn0q@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;

namespace Content.Server._BRatbite.Plushies;

[RegisterComponent]
public sealed partial class PlushieBrainComponent : Component
{
    [DataField]
    public EntProtoId Action = "ActionPlushieSqueak";

    [ViewVariables]
    public EntityUid? ActionEntity;

    [ViewVariables]
    public bool AddedMuted;
}
