// SPDX-FileCopyrightText: 2026 Sprinkle <40203084+lnn0q@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._BRatbite.PermaBrig.Commands;

[AdminCommand(AdminFlags.ViewNotes)]
public sealed class PermaReportsCommand : IConsoleCommand
{
    [Dependency] private readonly IEntitySystemManager _systems = default!;

    public string Command => "perma:reports";
    public string Description => "Lists automatic perma eligibility reports for the current round.";
    public string Help => "Usage: perma:reports [player]";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var reports = _systems.GetEntitySystem<AutomaticPermaEligibilitySystem>().Reports;
        var filter = args.Length > 0 ? args[0] : null;

        foreach (var report in reports)
        {
            if (filter != null &&
                !report.Killer.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) &&
                !report.Killer.UserId.UserId.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase))
                continue;

            shell.WriteLine(
                $"{report.Id}: {report.Status} | {report.Killer.Name} -> {report.Victim.Name} | " +
                $"round={report.RoundId} witness={report.WitnessName} resolution={report.Resolution ?? "none"}");
        }
    }
}
