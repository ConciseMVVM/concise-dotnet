using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Concise.Mutations
{
    public enum MutationState
    {
        Initial,
        Ready,
        Starting,
        Started,
        Committing,
        Committed,
        RollingBack,
        RolledBack,
        Completed,
    }

    [Serializable]
    public abstract class Mutation
    {
        public string Id { get; }
        public DateTimeOffset? StartTime { get; private set; } // technically this is the "ReadyTime"
        public MutationState State { get; private set; }
        public bool Failed { get; private set; }
        public string? ErrorMessage { get; private set; }

        public Mutation()
        {
            Id = Guid.NewGuid().ToString().Replace("-", "");

            State = MutationState.Initial;
            Failed = false;
        }

        protected abstract Task StartAsync();
        protected abstract Task CommitAsync();
        protected abstract Task RollbackAsync();

        TaskCompletionSource<object?> _startCompletion = new();
        TaskCompletionSource<object?> _commitCompletion = new();
        TaskCompletionSource<object?> _rollbackCompletion = new();

        public Task Started => _startCompletion.Task;
        public Task Committed => _commitCompletion.Task;

        protected internal void Log(string message) =>
            MutationManager.Log($"{this.GetType().Name}[{Id}@{State}]: {message}");

        void SaveState()
        {
            // Save the current state of the Mutation. If it hasn't been saved before, create a record.

        }

        void RemoveState()
        {
            // remove our state record for thismutation.
        }

        internal void SetReady()
        {
            if (State != MutationState.Initial)
                throw new InvalidOperationException($"Mutation is unexpected state, expected {MutationState.Initial}");

            StartTime = DateTimeOffset.Now;
            State = MutationState.Ready;
            SaveState();
        }

        internal void SetCompleted()
        {
            State = MutationState.Completed;
            RemoveState();
        }

        private async Task PerformActionAsync(MutationState? entryState, MutationState startingState, MutationState completedState, Func<Task> action, TaskCompletionSource<object?> completion)
        {
            try
            {
                if (entryState != null && State != entryState)
                    throw new InvalidOperationException($"Mutation is unexpected state, expected {entryState}");

                State = startingState;
                Log("Performing Action...");
                await action();
                State = completedState;
                Log("Action Completed Successfully");

                completion.SetResult(null);
            }
            catch (Exception ex)
            {
                Log($"Action Failed - {ex.GetType().Name}: {ex.Message}");

                if (Failed)
                {
                    // if we have already failed, this is likely because we have
                    // failed rolling back. bummer.
                    // Don't record the result in the task and pretend it
                    // succeeded...

                    State = completedState;
                }
                else
                {
                    ErrorMessage = ex.Message;
                    Failed = true;
                }

                completion.SetException(ex);
            }
            finally
            {
                SaveState();
            }
        }

        internal Task PerformStartAsync() =>
            PerformActionAsync(MutationState.Ready, MutationState.Starting, MutationState.Started, StartAsync, _startCompletion);

        internal Task PerformCommitAsync() =>
            PerformActionAsync(MutationState.Started, MutationState.Committing, MutationState.Committed, CommitAsync, _commitCompletion);

        internal Task PerformRollbackAsync() =>
            PerformActionAsync(null, MutationState.RollingBack, MutationState.RolledBack, RollbackAsync, _rollbackCompletion);
    }
}
