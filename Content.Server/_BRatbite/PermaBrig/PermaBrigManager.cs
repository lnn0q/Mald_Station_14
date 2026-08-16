// SPDX-FileCopyrightText: 2026 Sprinkle <40203084+lnn0q@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Threading;
using System.Threading.Tasks;
using Content.Server.Administration.Logs;
using Content.Server.Database;
using Content.Shared.Database;
using Robust.Shared.Asynchronous;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server._BRatbite.PermaBrig
{
    /// <summary>
    /// Handles getting and setting values in database for perma sentences
    /// Modified version of GoobStations ServerCurrencyManager
    /// </summary>
    public sealed class PermaBrigManager
    {
        [Dependency] private readonly IServerDbManager _db = default!;
        [Dependency] private readonly ITaskManager _task = default!;
        [Dependency] private readonly IAdminLogManager _adminLogger = default!;

        private readonly List<Task> _pendingSaveTasks = new();

        public void Shutdown()
        {
            _task.BlockWaitOnTask(Task.WhenAll(_pendingSaveTasks));
        }

        private ISawmill _sawmill = default!;

        public void Initialize()
        {
            _sawmill = Logger.GetSawmill("server_permabrig");
        }

        public int AddBrigRounds(NetUserId userId, int rounds)
        {
            var newTotal = ModifyBrigRounds(userId, rounds);
            _sawmill.Info($"Added {rounds} rounds to {userId} sentence. Current sentence: {newTotal}");
            return newTotal;
        }

        public bool ShouldPlayerBeBrigged(ICommonSession session)
        {
            return GetBrigRounds(session.UserId) > 0;
        }

        public void UpdateTimeServed(TimeSpan time, ICommonSession session)
        {
            // Perma is round-based. Time tracking still flushes this tracker for UI/playtime consistency,
            // but serving time no longer reduces the sentence.
        }

        public string GetRoundsLabel(int rounds)
        {
            if (rounds <= 0)
                return " served";

            return $"{rounds} round(s)";
        }

        public async void UpdatePlayerOnJoin(NetUserId userId, string name)
        {
            var record = await _db.GetPlayerRecordByUserId(userId, CancellationToken.None);
            if (record is not null)
            {
                _adminLogger.Add(LogType.Perma,
                    LogImpact.Low,
                    $"{name} joined with {GetBrigRounds(userId)} perma rounds left.");
            }
        }

        public int RemoveBrigRounds(NetUserId userId, int rounds)
        {
            var newTotal = ModifyBrigRounds(userId, -rounds);
            _sawmill.Info($"Removed {rounds} rounds from {userId} sentence. Current sentence: {newTotal}");
            return newTotal;
        }

        public int SetBrigRounds(NetUserId userId, int rounds)
        {
            var oldSentence = Task.Run(() => SetBrigRoundsAsync(userId, Math.Max(0, rounds))).GetAwaiter().GetResult();
            _sawmill.Info($"Setting {userId} sentence to {rounds} rounds from {oldSentence}");
            return oldSentence;
        }

        public int GetBrigRounds(NetUserId userId)
        {
            return Task.Run(() => GetBrigRoundsAsync(userId)).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Check wether a prisoner is a inpatient
        /// </summary>
        /// <param name="userId">The player's NetUserId</param>
        /// <returns>The players current time in minutes.</returns>
        public bool GetBrigInpatient(NetUserId userId)
        {
            return Task.Run(() => GetBrigInpatientAsync(userId)).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Check wether a prisoner is a inpatient
        /// </summary>
        /// <param name="userId">The player's NetUserId</param>
        /// <returns>The players current time in minutes.</returns>
        public void SetBrigInpatient(NetUserId userId, bool status)
        {
            Task.Run(() => SetBrigInpatientAsync(userId, status)).GetAwaiter().GetResult();
        }


        public string GetBrigRoundsLabel(NetUserId userId)
        {
            return GetRoundsLabel(GetBrigRounds(userId));
        }

        public async void UpdateTimeLastSeen(ICommonSession session)
        {
            var record = await _db.GetPlayerRecordByUserId(session.UserId, CancellationToken.None);
            if (record is not null)
            {
                await _db.UpdatePlayerRecordAsync(record.UserId,
                    record.LastSeenUserName,
                    record.LastSeenAddress,
                    record.HWId);
            }
        }

        public int AddBrigTime(NetUserId userId, int minutes) => AddBrigRounds(userId, minutes);
        public int RemoveBrigTime(NetUserId userId, int minutes) => RemoveBrigRounds(userId, minutes);
        public int SetBrigTime(NetUserId userId, int minutes) => SetBrigRounds(userId, minutes);
        public int GetBrigTime(NetUserId userId) => GetBrigRounds(userId);
        public string GetBrigTimeLabel(NetUserId userId) => GetBrigRoundsLabel(userId);
        public string GetTimeLabel(int minutes) => GetRoundsLabel(minutes);
        public int AddBrigSentence(NetUserId userId, int rounds) => AddBrigRounds(userId, rounds);
        public int RemoveBrigSentence(NetUserId userId, int rounds) => RemoveBrigRounds(userId, rounds);
        public int SetBrigSentence(NetUserId userId, int rounds) => SetBrigRounds(userId, rounds);
        public int GetBrigSentence(NetUserId userId) => GetBrigRounds(userId);

        /// <summary>
        /// Adds PPpoints to a player.
        /// </summary>
        /// <param name="userId">The player's NetUserId</param>
        /// <param name="amount">The amount of PPpoints to add.</param>
        /// <returns>An integer containing the new amount of PPpoints attributed to the player.</returns>
        public int AddPPpoints(NetUserId userId, int amount)
        {
            var newAmount = ModifyPPpoints(userId, amount);
            _sawmill.Info($"Added {amount} PPpoints to {userId} account. Current PPpoint total: {newAmount}");
            return newAmount;
        }

        /// <summary>
        /// Removes PPpoints from a player.
        /// </summary>
        /// <param name="userId">The player's NetUserId</param>
        /// <param name="amount">The amount of PPpoints to remove.</param>
        /// <returns>An integer containing the old amount of PPpoints attributed to the player.</returns>
        public int RemovePPpoints(NetUserId userId, int amount)
        {
            var oldAmount = ModifyPPpoints(userId, -amount);
            _sawmill.Info($"Removed {amount} PPpoints from {userId} account. Previous PPpoint total: {oldAmount}");
            return oldAmount;
        }

        /// <summary>
        /// Sets a player's PPpoint total.
        /// </summary>
        /// <param name="userId">The player's NetUserId</param>
        /// <param name="amount">The amount of PPpoints that will be set.</param>
        /// <returns>An integer containing the old amount of PPpoints attributed to the player.</returns>
        /// <remarks>Use the return value instead of calling <see cref="GetPPpoints(NetUserId)"/> prior to this.</remarks>
        public int SetPPpoints(NetUserId userId, int amount)
        {
            var oldAmount = Task.Run(() => SetPPpointsAsync(userId, amount)).GetAwaiter().GetResult();
            _sawmill.Info($"Setting {userId} PPpoint total to {amount} from {oldAmount}");
            return oldAmount;
        }

        /// <summary>
        /// Gets a player's PPpoint total.
        /// </summary>
        /// <param name="userId">The player's NetUserId</param>
        /// <returns>The players PPpoint total.</returns>
        public int GetPPpoints(NetUserId userId)
        {
            return Task.Run(() => GetPPpoints(userId)).GetAwaiter().GetResult();
        }

        #region INTERNAL/ASYNC

        private int ModifyBrigRounds(NetUserId userId, int amountDelta)
        {
            var result = Task.Run(() => ModifyBrigRoundsAsync(userId, amountDelta)).GetAwaiter().GetResult();
            if (result < 0)
            {
                SetBrigRounds(userId, 0);
                result = 0;
            }

            return result;
        }

        private async Task SetBrigRoundsAsyncInternal(NetUserId userId, int amount)
        {
            var task = Task.Run(() => _db.SetPermaRoundsLeft(userId, amount));
            TrackPending(task);
            await task;
        }

        private async Task<int> SetBrigRoundsAsync(NetUserId userId, int amount)
        {
            var oldAmount = GetBrigRounds(userId);
            await SetBrigRoundsAsyncInternal(userId, amount);
            return oldAmount;
        }

        private async Task<int> GetBrigRoundsAsync(NetUserId userId) => await _db.GetPermaRoundsLeft(userId);

        /// <summary>
        /// Gets the number of rounds a player needs to serve in perma.
        /// </summary>
        /// <param name="userId">The player's NetUserId</param>
        /// <returns>An integer containing the rounds left to serve.</returns>
        private async Task<bool> GetBrigInpatientAsync(NetUserId userId) => await _db.GetPermaInpatient(userId);

        /// <summary>
        /// Gets the number of rounds a player needs to serve in perma.
        /// </summary>
        /// <param name="userId">The player's NetUserId</param>
        /// <returns>An integer containing the rounds left to serve.</returns>
        private async Task SetBrigInpatientAsync(NetUserId userId, bool status) => await _db.SetPermaInpatient(userId, status);

        private async Task<int> ModifyBrigRoundsAsync(NetUserId userId, int amountDelta)
        {
            var task = Task.Run(() => _db.ModifyPermaRoundsLeft(userId, amountDelta));
            TrackPending(task);
            return await task;
        }

        /// <summary>
        /// Modifies a player's PPpoints total.
        /// </summary>
        /// <param name="userId">The player's NetUserId</param>
        /// <param name="amountDelta">The PPpoints to add.</param>
        /// <returns>An integer containing the new PPpoints.</returns>
        /// <remarks>Use the return value instead of calling <see cref="GetPPpoints(NetUserId)"/> after to this.</remarks>
        private int ModifyPPpoints(NetUserId userId, int amountDelta)
        {
            var result = Task.Run(() => ModifyPPpointsAsync(userId, amountDelta)).GetAwaiter().GetResult();
            return result;
        }

        /// <summary>
        /// Sets a player's PPpoints total.
        /// </summary>
        /// <param name="userId">The player's NetUserId</param>
        /// <param name="amount">The number of PPpoints to set.</param>
        /// <param name="oldAmount">The number of PPpoints originally set.</param>
        /// <remarks>This and its classes will block server shutdown until execution finishes.</remarks>
        private async Task SetPPpointsAsyncInternal(NetUserId userId, int amount, int oldAmount)
        {
            var task = Task.Run(() => _db.SetPPpoints(userId, amount));
            TrackPending(task);
            await task;
        }

        /// <summary>
        /// Sets a player's PPpoints total.
        /// </summary>
        /// <param name="userId">The player's NetUserId</param>
        /// <param name="amount">he number of PPpoints set.</param>
        /// <returns>The number of PPpoints originally set.</returns>
        /// <remarks>Use the return value instead of calling <see cref="GetPPpoints(NetUserId)"/> prior to this.</remarks>
        private async Task<int> SetPPpointsAsync(NetUserId userId, int amount)
        {
            // We need to block it first to ensure we don't read our own amount, hence sync function
            var oldAmount = GetPPpoints(userId);
            await SetPPpointsAsyncInternal(userId, amount, oldAmount);
            return oldAmount;
        }

        /// <summary>
        /// Gets the number of PPpoints a player has.
        /// </summary>
        /// <param name="userId">The player's NetUserId</param>
        /// <returns>An integer containing the PPpoints total.</returns>
        private async Task<int> GetPPpointsAsync(NetUserId userId) => await _db.GetPPpoints(userId);

        /// <summary>
        /// Modifies a player's PPpoints total.
        /// </summary>
        /// <param name="userId">The player's NetUserId</param>
        /// <param name="amountDelta">The amount of PPpoints that will be given or taken.</param>
        /// <returns>An integer containing the new amount of PPpoints attributed to the player.</returns>
        /// <remarks>This and its classes will block server shutdown until execution finishes.</remarks>
        private async Task<int> ModifyPPpointsAsync(NetUserId userId, int amountDelta)
        {
            var task = Task.Run(() => _db.ModifyPPpoints(userId, amountDelta));
            TrackPending(task);
            return await task;
        }

        /// <summary>
        /// Track a database save task to make sure we block server shutdown on it.
        /// </summary>
        private async void TrackPending(Task task)
        {
            _pendingSaveTasks.Add(task);

            try
            {
                await task;
            }
            finally
            {
                _pendingSaveTasks.Remove(task);
            }
        }

        #endregion
    }
}
