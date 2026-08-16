// SPDX-FileCopyrightText: 2026 Sprinkle <40203084+lnn0q@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Goobstation.Common.Cloning;
using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Shared.Access.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.IdentityManagement.Components;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Timing;

namespace Content.Server._BRatbite.PermaBrig;

public sealed class AutomaticPermaEligibilitySystem : EntitySystem
{
    private const int RoundsPerValidatedKill = 2;

    private static readonly HashSet<string> ExemptDepartments = new()
    {
        "Security",
        "CentralCommand",
        "Command"
    };

    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly PermaBrigManager _permaBrigManager = default!;
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedRoleSystem _roles = default!;
    [Dependency] private readonly StationAiVisionSystem _stationAiVision = default!;

    private readonly HashSet<(NetUserId Attacker, NetUserId Victim)> _harmLedger = new();
    private readonly Dictionary<EntityUid, DeathAttribution> _deathAttribution = new();
    private readonly Dictionary<Guid, PermaEligibilityReport> _reports = new();
    private readonly Dictionary<NetUserId, HashSet<Guid>> _reportsByKiller = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<DamageableComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<MindContainerComponent, MindAddedMessage>(OnMindAdded);
        SubscribeLocalEvent<TransferredToCloneEvent>(OnTransferredToClone);
        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    public IReadOnlyCollection<PermaEligibilityReport> Reports => _reports.Values;

    private void OnDamageChanged(EntityUid uid, DamageableComponent component, DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.Origin == null)
            return;

        if (!TryGetPlayerSnapshot(args.Origin.Value, out var attacker) ||
            !TryGetPlayerSnapshot(uid, out var victim) ||
            attacker.UserId == victim.UserId)
            return;

        _harmLedger.Add((attacker.UserId, victim.UserId));

        if (TryComp<MobStateComponent>(uid, out var mobState) && mobState.CurrentState == MobState.Critical)
            _deathAttribution[uid] = new DeathAttribution(args.Origin.Value, attacker);
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
        {
            TryCreateReport(args.Target, args.Origin);
            _deathAttribution.Remove(args.Target);
            return;
        }

        if (args.NewMobState == MobState.Critical && TryGetDeathAttribution(args.Origin, out var attribution))
            _deathAttribution[args.Target] = attribution;

        if (args.OldMobState == MobState.Dead)
        {
            _deathAttribution.Remove(args.Target);
            ClearReportsForVictim(args.Target, "victim revived");
        }
    }

    private void OnMindAdded(EntityUid uid, MindContainerComponent component, MindAddedMessage args)
    {
        if (!args.Mind.Comp.UserId.HasValue || HasComp<GhostComponent>(uid))
            return;

        if (TryComp<MobStateComponent>(uid, out var mobState) && mobState.CurrentState == MobState.Dead)
            return;

        ClearReportsForVictim(args.Mind.Owner, args.Mind.Comp.UserId.Value, "victim mind transferred to a living body or container");
    }

    private void OnTransferredToClone(ref TransferredToCloneEvent args)
    {
        ClearReportsForVictim(args.Cloned, "victim cloned");
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent ev)
    {
        if (ev.New == GameRunLevel.PostRound)
            ApplyPendingReports();
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _harmLedger.Clear();
        _deathAttribution.Clear();
        _reports.Clear();
        _reportsByKiller.Clear();
    }

    private void TryCreateReport(EntityUid victimEnt, EntityUid? origin)
    {
        if (!TryGetDeathAttribution(origin, out var attribution) &&
            !_deathAttribution.TryGetValue(victimEnt, out attribution))
            return;

        var killer = attribution.Killer;
        if (!TryGetPlayerSnapshot(victimEnt, out var victim) ||
            killer.UserId == victim.UserId)
            return;

        if (IsReportExempt(killer, victim, attribution.KillerEntity, out var reason))
        {
            _adminLogger.Add(LogType.Perma, LogImpact.Low,
                $"Automatic perma report skipped for {killer.Name} killing {victim.Name}: {reason}.");
            return;
        }

        if (!TryFindWitness(attribution.KillerEntity, victimEnt, out var witness))
        {
            _adminLogger.Add(LogType.Perma, LogImpact.Low,
                $"Automatic perma report skipped for {killer.Name} killing {victim.Name}: kill was not in station AI camera view.");
            return;
        }

        foreach (var report in _reports.Values)
        {
            if (report.Status == PermaEligibilityReportStatus.Pending &&
                report.Killer.UserId == killer.UserId &&
                report.Victim.UserId == victim.UserId &&
                report.Victim.MindId == victim.MindId)
                return;
        }

        var id = Guid.NewGuid();
        var newReport = new PermaEligibilityReport(
            id,
            _ticker.RoundId,
            _timing.RealTime,
            killer,
            victim,
            witness,
            PermaEligibilityReportStatus.Pending,
            null);

        _reports[id] = newReport;
        if (!_reportsByKiller.TryGetValue(killer.UserId, out var reportsByKiller))
        {
            reportsByKiller = new HashSet<Guid>();
            _reportsByKiller[killer.UserId] = reportsByKiller;
        }

        reportsByKiller.Add(id);

        _adminLogger.Add(LogType.Perma, LogImpact.High,
            $"Automatic perma report created for {killer.Name} killing {victim.Name}; witness: {witness}.");
    }

    private bool TryGetDeathAttribution(EntityUid? origin, out DeathAttribution attribution)
    {
        attribution = default;
        if (origin == null || !TryGetPlayerSnapshot(origin.Value, out var killer))
            return false;

        attribution = new DeathAttribution(origin.Value, killer);
        return true;
    }

    private bool IsReportExempt(PlayerSnapshot killer, PlayerSnapshot victim, EntityUid killerEnt, out string reason)
    {
        if (killer.IsAntagonist || killer.IsFreeAgent)
        {
            reason = "killer was antagonist/free agent";
            return true;
        }

        if (victim.IsAntagonist || victim.IsFreeAgent)
        {
            reason = "victim was antagonist/free agent";
            return true;
        }

        if (killer.HasExemptDepartment)
        {
            reason = $"killer department was {string.Join(", ", killer.Departments)}";
            return true;
        }

        if (victim.HasExemptDepartment)
        {
            reason = $"victim department was {string.Join(", ", victim.Departments)}";
            return true;
        }

        if (IsConcealingIdentity(killerEnt))
        {
            reason = "killer had no ID and fully concealed identity";
            return true;
        }

        if (_harmLedger.Contains((victim.UserId, killer.UserId)))
        {
            reason = "victim attacked killer earlier this round";
            return true;
        }

        reason = string.Empty;
        return false;
    }

    private bool TryFindWitness(EntityUid killer, EntityUid victim, out string witness)
    {
        if (IsInStationAiView(killer) || IsInStationAiView(victim))
        {
            witness = "station AI camera network";
            return true;
        }

        witness = string.Empty;
        return false;
    }

    private bool IsInStationAiView(EntityUid target)
    {
        if (Deleted(target))
            return false;

        var xform = Transform(target);
        if (xform.GridUid is not { } gridUid ||
            !TryComp<BroadphaseComponent>(gridUid, out var broadphase) ||
            !TryComp<MapGridComponent>(gridUid, out var grid))
            return false;

        var targetTile = _map.LocalToTile(gridUid, grid, xform.Coordinates);

        lock (_stationAiVision)
        {
            return _stationAiVision.IsAccessible((gridUid, broadphase, grid), targetTile);
        }
    }

    private bool IsConcealingIdentity(EntityUid uid)
    {
        if (_idCard.TryFindIdCard(uid, out _))
            return false;

        var ev = new SeeIdentityAttemptEvent();
        RaiseLocalEvent(uid, ev);
        return ev.Cancelled;
    }

    private void ClearReportsForVictim(EntityUid victimEnt, string reason)
    {
        if (!TryGetPlayerSnapshot(victimEnt, out var victim))
            return;

        ClearReportsForVictim(victim.MindId, victim.UserId, reason);
    }

    private void ClearReportsForVictim(EntityUid victimMindId, NetUserId victimUserId, string reason)
    {
        foreach (var (id, report) in _reports.ToArray())
        {
            if (report.Status != PermaEligibilityReportStatus.Pending ||
                (report.Victim.MindId != victimMindId && report.Victim.UserId != victimUserId))
                continue;

            _reports[id] = report with
            {
                Status = PermaEligibilityReportStatus.Cleared,
                Resolution = reason
            };

            _adminLogger.Add(LogType.Perma, LogImpact.Low,
                $"Automatic perma report cleared for {report.Killer.Name} killing {report.Victim.Name}: {reason}.");
        }
    }

    private async void ApplyPendingReports()
    {
        foreach (var (id, report) in _reports.ToArray())
        {
            if (report.Status != PermaEligibilityReportStatus.Pending)
                continue;

            if (VictimReturnedToLife(report.Victim))
            {
                _reports[id] = report with
                {
                    Status = PermaEligibilityReportStatus.Cleared,
                    Resolution = "victim was alive or transferred before round end"
                };
                continue;
            }

            _permaBrigManager.AddBrigRounds(report.Killer.UserId, RoundsPerValidatedKill);

            var playtime = TimeSpan.Zero;
            var playtimes = await _db.GetPlayTimes(report.Killer.UserId.UserId);
            foreach (var tracker in playtimes)
                playtime += tracker.TimeSpent;

            await _db.AddAdminNote(
                report.RoundId == 0 ? null : report.RoundId,
                report.Killer.UserId.UserId,
                playtime,
                BuildAdminRemark(report),
                NoteSeverity.High,
                true,
                null,
                DateTimeOffset.UtcNow,
                null);

            _reports[id] = report with
            {
                Status = PermaEligibilityReportStatus.Applied,
                Resolution = $"+{RoundsPerValidatedKill} perma rounds applied"
            };

            _adminLogger.Add(LogType.Perma, LogImpact.High,
                $"Automatic perma report applied to {report.Killer.Name}: +{RoundsPerValidatedKill} rounds for killing {report.Victim.Name}.");
        }
    }

    private bool VictimReturnedToLife(PlayerSnapshot victim)
    {
        if (!_mind.TryGetMind(victim.UserId, out var mindId, out var mind) ||
            mindId != victim.MindId ||
            mind.OwnedEntity == null ||
            Deleted(mind.OwnedEntity.Value) ||
            HasComp<GhostComponent>(mind.OwnedEntity.Value))
            return false;

        if (!TryComp<MobStateComponent>(mind.OwnedEntity.Value, out var mobState))
            return true;

        return mobState.CurrentState != MobState.Dead;
    }

    private string BuildAdminRemark(PermaEligibilityReport report)
    {
        return $"Automatic perma eligibility applied: +{RoundsPerValidatedKill} rounds. " +
               $"Round {report.RoundId}; kill observed by {report.WitnessName}. " +
               $"Killer snapshot: {report.Killer.Describe()}. " +
               $"Victim snapshot: {report.Victim.Describe()}. " +
               $"Timestamp: {DateTimeOffset.UtcNow:O}.";
    }

    private bool TryGetPlayerSnapshot(EntityUid uid, out PlayerSnapshot snapshot)
    {
        snapshot = null!;

        if (!_mind.TryGetMind(uid, out var mindId, out var mind) || !mind.UserId.HasValue)
            return false;

        string? jobId = _jobs.MindTryGetJobId(mindId, out var job) ? job?.ToString() : null;
        var departments = new List<string>();
        if (jobId != null && _jobs.TryGetAllDepartments(jobId, out var departmentPrototypes))
        {
            foreach (var department in departmentPrototypes)
                departments.Add(department.ID);
        }

        var freeAgent = mind.RoleType.Id == "FreeAgent";
        var antag = _roles.MindIsAntagonist(mindId);
        var hasExemptDepartment = departments.Any(ExemptDepartments.Contains);

        snapshot = new PlayerSnapshot(
            mind.UserId.Value,
            mindId,
            Name(uid),
            jobId,
            departments.ToArray(),
            antag,
            freeAgent,
            hasExemptDepartment);

        return true;
    }
}

public sealed record PermaEligibilityReport(
    Guid Id,
    int RoundId,
    TimeSpan CreatedAt,
    PlayerSnapshot Killer,
    PlayerSnapshot Victim,
    string WitnessName,
    PermaEligibilityReportStatus Status,
    string? Resolution);

public readonly record struct DeathAttribution(EntityUid KillerEntity, PlayerSnapshot Killer);

public sealed record PlayerSnapshot(
    NetUserId UserId,
    EntityUid MindId,
    string Name,
    string? JobId,
    string[] Departments,
    bool IsAntagonist,
    bool IsFreeAgent,
    bool HasExemptDepartment)
{
    public string Describe()
    {
        var departments = Departments.Length == 0 ? "none" : string.Join(", ", Departments);
        return $"{Name} ({UserId.UserId}), job={JobId ?? "none"}, departments={departments}, antag={IsAntagonist}, freeAgent={IsFreeAgent}";
    }
}

public enum PermaEligibilityReportStatus
{
    Pending,
    Cleared,
    Applied
}
