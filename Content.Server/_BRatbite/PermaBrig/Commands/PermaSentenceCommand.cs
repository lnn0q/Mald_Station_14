// SPDX-FileCopyrightText: 2026 Sprinkle <40203084+lnn0q@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Administration;
using Content.Server.Administration.Commands;
using Content.Server.Administration.Managers;
using Content.Server.Chat.Managers;
using Content.Server.Players.PlayTimeTracking;
using Content.Shared.Administration;
using Content.Shared.Chat;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Player;

namespace Content.Server._BRatbite.PermaBrig.Commands
{
    [AnyCommand]
    public sealed class PermaSentenceCommand : IConsoleCommand
    {
        [Dependency] private readonly PermaBrigManager _permaBrigManager = default!;
        [Dependency] private readonly IChatManager _chatManager = default!;
        [Dependency] private readonly IAdminManager _adminManager = default!;
        [Dependency] private readonly PlayTimeTrackingManager _tracking = default!;
        public string Command => "perma:sentence";
        public string Description => "check your/another players Brig Sentence";

        public string Help => "Usage: perma:sentence <optional: player>"
                              + "\n    player: (optional) who to view brigsentence of.";

        public void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            string balance;
            var commonSession = shell.Player;
            if (commonSession != null)
            {
                _tracking.QueueRefreshTrackers(commonSession);
            }

            switch (args.Length)
            {
                case 0:
                    if (commonSession is not { } player)
                    {
                        shell.WriteError(Loc.GetString("shell-cannot-run-command-from-server"));
                        break;
                    }

                    balance = Loc.GetString("perma-your-current-sentence",
                        ("sentence", _permaBrigManager.GetBrigRoundsLabel(commonSession.UserId)));

                    _chatManager.ChatMessageToOne(ChatChannel.Local,
                        balance,
                        balance,
                        EntityUid.Invalid,
                        false,
                        commonSession.Channel);
                    shell.WriteLine(balance);

                    break;
                case 1:
                    if (commonSession is { } player2)
                    {
                        var plyMgrm = IoCManager.Resolve<IPlayerManager>();
                        if (!plyMgrm.TryGetUserId(args[0], out var targetPlayerm))
                        {
                            shell.WriteError(Loc.GetString("perma-command-invalid-player"));
                            break;
                        }

                        if ((targetPlayerm != commonSession.UserId)
                            && !_adminManager.HasAdminFlag(commonSession, AdminFlags.ViewNotes, false))
                        {
                            Loc.GetString("perma-other-current-sentence-deny");
                            break;
                        }

                        balance = Loc.GetString("perma-other-current-sentence",
                            ("player", targetPlayerm.UserId),
                            ("sentence", _permaBrigManager.GetBrigRoundsLabel(targetPlayerm)));

                        _chatManager.ChatMessageToOne(ChatChannel.Local,
                            balance,
                            balance,
                            EntityUid.Invalid,
                            false,
                            commonSession.Channel);

                        shell.WriteLine(balance);

                        break;
                    }

                    var plyMgr = IoCManager.Resolve<IPlayerManager>();
                    if (!plyMgr.TryGetUserId(args[0], out var targetPlayer))
                    {
                        shell.WriteError(Loc.GetString("perma-command-invalid-player"));
                        break;
                    }

                    balance = Loc.GetString("perma-other-current-sentence",
                        ("player", targetPlayer.UserId),
                        ("sentence", _permaBrigManager.GetBrigRoundsLabel(targetPlayer)));

                    shell.WriteLine(balance);

                    break;
            }
        }

        public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
        {
            return args.Length switch
            {
                1 => CompletionResult.FromHintOptions(CompletionHelper.SessionNames(), "<player> (optional)"),
                _ => CompletionResult.Empty
            };
        }
    }

    [AdminCommand(AdminFlags.Ban)]
    public sealed class PermaSentenceAddCommand : IConsoleCommand
    {
        [Dependency] private readonly PermaBrigManager _permaBrigManager = default!;
        [Dependency] private readonly IChatManager _chatManager = default!;
        public string Command => "perma:brig";
        public string Description => "Add rounds to player's brig sentence";

        public string Help => "Usage: perma:brig <player> <rounds>"
                              + "\n    player: who to add rounds to."
                              + "\n    rounds: whole number of rounds to add to sentence.";

        public void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            if (args.Length != 2)
            {
                return;
            }

            var plyMgr = IoCManager.Resolve<IPlayerManager>();
            if (!plyMgr.TryGetUserId(args[0], out var targetPlayer))
            {
                shell.WriteError(Loc.GetString("perma-command-invalid-player"));
                return;
            }

            if (!TryParseRounds(args[1], shell, out var rounds))
                return;

            _permaBrigManager.AddBrigRounds(targetPlayer, rounds);

            var message = Loc.GetString("perma-add-time-to-player",
                ("rounds", rounds),
                ("player", targetPlayer.UserId));

            shell.WriteLine(message);

            if (shell.Player is { } player)
            {
                _chatManager.ChatMessageToOne(ChatChannel.Local,
                    message,
                    message,
                    EntityUid.Invalid,
                    false,
                    shell.Player.Channel);
            }
        }

        public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
        {
            return args.Length switch
            {
                1 => CompletionResult.FromHintOptions(CompletionHelper.SessionNames(), "<Player>"),
                2 => CompletionResult.FromHint("<Rounds>"),
                _ => CompletionResult.Empty
            };
        }

        private static bool TryParseRounds(string input, IConsoleShell shell, out int rounds)
        {
            if (!int.TryParse(input, out rounds) || rounds < 0)
            {
                shell.WriteError(Loc.GetString("perma-command-invalid-time"));
                return false;
            }

            return true;
        }
    }

    [AdminCommand(AdminFlags.Ban)]
    public sealed class PermaSentenceRemoveCommand : IConsoleCommand
    {
        [Dependency] private readonly PermaBrigManager _permaBrigManager = default!;
        [Dependency] private readonly IChatManager _chatManager = default!;
        public string Command => "perma:pardon";
        public string Description => "Remove rounds from player's brig sentence";

        public string Help => "Usage: perma:pardon <player> <rounds>"
                              + "\n    player: who to remove rounds from."
                              + "\n    rounds: whole number of rounds to remove from sentence.";

        public void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            if (args.Length != 2)
            {
                return;
            }

            var plyMgr = IoCManager.Resolve<IPlayerManager>();
            if (!plyMgr.TryGetUserId(args[0], out var targetPlayer))
            {
                shell.WriteError(Loc.GetString("perma-command-invalid-player"));
                return;
            }

            if (!TryParseRounds(args[1], shell, out var rounds))
                return;

            _permaBrigManager.RemoveBrigRounds(targetPlayer, rounds);

            var message = Loc.GetString("perma-rem-time-to-player",
                ("rounds", rounds),
                ("player", targetPlayer.UserId));

            shell.WriteLine(message);

            if (shell.Player is { } player)
            {
                _chatManager.ChatMessageToOne(ChatChannel.Local,
                    message,
                    message,
                    EntityUid.Invalid,
                    false,
                    shell.Player.Channel);
            }
        }

        public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
        {
            return args.Length switch
            {
                1 => CompletionResult.FromHintOptions(CompletionHelper.SessionNames(), "<Player>"),
                2 => CompletionResult.FromHint("<Rounds>"),
                _ => CompletionResult.Empty
            };
        }

        private static bool TryParseRounds(string input, IConsoleShell shell, out int rounds)
        {
            if (!int.TryParse(input, out rounds) || rounds < 0)
            {
                shell.WriteError(Loc.GetString("perma-command-invalid-time"));
                return false;
            }

            return true;
        }
    }

    [AdminCommand(AdminFlags.Ban)]
    public sealed class PermaSentenceSetCommand : IConsoleCommand
    {
        [Dependency] private readonly PermaBrigManager _permaBrigManager = default!;
        [Dependency] private readonly IChatManager _chatManager = default!;
        public string Command => "perma:set";
        public string Description => "Set the rounds player is serving in brig";

        public string Help => "Usage: permaset <player> <rounds>"
                              + "\n    player: who to set rounds for."
                              + "\n    rounds: whole number of rounds to set the sentence to.";

        public void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            if (args.Length != 2)
            {
                return;
            }

            var plyMgr = IoCManager.Resolve<IPlayerManager>();
            if (!plyMgr.TryGetUserId(args[0], out var targetPlayer))
            {
                shell.WriteError(Loc.GetString("perma-command-invalid-player"));
                return;
            }

            if (!TryParseRounds(args[1], shell, out var rounds))
                return;

            _permaBrigManager.SetBrigRounds(targetPlayer, rounds);

            var message = Loc.GetString("perma-set-time-to-player",
                ("rounds", rounds),
                ("player", targetPlayer.UserId));

            shell.WriteLine(message);

            if (shell.Player is { } player)
            {
                _chatManager.ChatMessageToOne(ChatChannel.Local,
                    message,
                    message,
                    EntityUid.Invalid,
                    false,
                    shell.Player.Channel);
            }
        }

        public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
        {
            return args.Length switch
            {
                1 => CompletionResult.FromHintOptions(CompletionHelper.SessionNames(), "<Player>"),
                2 => CompletionResult.FromHint("<Rounds>"),
                _ => CompletionResult.Empty
            };
        }

        private static bool TryParseRounds(string input, IConsoleShell shell, out int rounds)
        {
            if (!int.TryParse(input, out rounds) || rounds < 0)
            {
                shell.WriteError(Loc.GetString("perma-command-invalid-time"));
                return false;
            }

            return true;
        }
    }

[AdminCommand(AdminFlags.Ban)]
public sealed class PermaSentenceInpatientCommand : IConsoleCommand
{
    [Dependency] private readonly PermaBrigManager _permaBrigManager = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;

    public string Command => "perma:inpatient";
    public string Description => "Set whether a player spawns as an inpatient prisoner (cuffed high security)";

    public string Help => "Usage: perma:inpatient <player> <bool>"
                          + "\n    player: who to set flag for."
                          + "\n    status: true or false (also accepts yes/no or 1/0)";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            return;
        }

        var plyMgr = IoCManager.Resolve<IPlayerManager>();
        if (!plyMgr.TryGetUserId(args[0], out var targetPlayer))
        {
            shell.WriteError(Loc.GetString("perma-command-invalid-player"));
            return;
        }

        if (!TryParseBool(args[1], out var status))
        {
            shell.WriteError("Invalid status. Use true/false, yes/no, or 1/0.");
            return;
        }

        _permaBrigManager.SetBrigInpatient(targetPlayer, status);

        var message = Loc.GetString("perma-set-inpatient-status",
            ("status", status),
            ("player", targetPlayer.UserId));

        shell.WriteLine(message);

        if (shell.Player is { } player)
        {
            _chatManager.ChatMessageToOne(ChatChannel.Local,
                message,
                message,
                EntityUid.Invalid,
                false,
                shell.Player.Channel);
        }
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(CompletionHelper.SessionNames(), "<Player>"),
            2 => CompletionResult.FromHintOptions(
                new[] { "true", "false"},
                "<Bool>"),
            _ => CompletionResult.Empty
        };
    }

    private static bool TryParseBool(string input, out bool value)
    {
        switch (input.Trim().ToLowerInvariant())
        {
            case "true":
            case "yes":
            case "1":
                value = true;
                return true;

            case "false":
            case "no":
            case "0":
                value = false;
                return true;

            default:
                value = false;
                return false;
        }
    }
}

}
