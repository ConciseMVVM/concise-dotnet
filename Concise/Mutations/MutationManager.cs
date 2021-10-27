using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Concise.Mutations
{
    public static class MutationManager
    {
        private static List<Mutation> _mutations = new();

        public static void Initialize()
        {
            // to do - load persisted mutations
        }

        private static void SaveState()
        {
            // todo
        }

        internal static void Log(string message)
        {
            Debug.WriteLine(message);
        }

        private static async Task PerformMutations()
        {
            // perform mutations runswhile there are mutations in our list to add...

            while (_mutations.Count > 0)
            {
                // First, we need to rollback any failed mutations...

                var failed = _mutations.Find((m) => m.Failed && m.State != MutationState.RolledBack);

                if (failed != null)
                {
                    await failed.PerformRollbackAsync();
                    // Now, this mutation is in the RolledBack state and will be
                    // removed later in the loop

                    continue; // ok, loop back and see what needs to be done next.
                }

                // Now, we need to start any tasks that are in the initial state...

                var ready = _mutations.Find((m) => m.State == MutationState.Ready);

                if (ready != null)
                {
                    await ready.PerformStartAsync();
                    // it's now either in Started state or Failed = true
                    continue;
                }

                // Ok, so we have cleaned up any failed mutations and
                // any Ready mutations have have completed Starting...
                // Any tasks in Started state need to be started...

                var started = _mutations.Find((m) => m.State == MutationState.Started);

                if (started != null)
                {
                    await started.PerformCommitAsync();

                    // Now, we are either in Committed or Failed == true

                    continue;
                }

                // At this point tasks should be either in Committed
                // or RolledBack. In either case, it's time to remove them...

                var completed = _mutations.Find((m) => m.State == MutationState.Committed || m.State == MutationState.RolledBack);

                if (completed != null)
                {
                    completed.Log("Remove completed mutation.");
                    completed.SetCompleted();
                    _mutations.Remove(completed);
                    continue;
                }

                // Ok, If we got here a mutation is in an unexpected state...
                // Show an error and remove it...

                var invalid = (_mutations.Count > 0) ? _mutations[0] : null;

                if (invalid != null)
                {
                    invalid.Log($"Mutation in invalid state {invalid.State}, MutationManager is aborting Mutation");
                    invalid.SetCompleted();
                    _mutations.RemoveAt(0);
                    continue;
                }

                // If we got here the list is empty, let the loop exit.
            }
        }

        static bool _performingMutations = false;

        private static async void StartPerformMutations()
        {
            if (_performingMutations)
                return;

            try
            {
                _performingMutations = true;
                await PerformMutations();
            }
            catch(Exception ex)
            {
                // should not happen
                Log($"MutationManager: PerformMutations failed - {ex.GetType().Name} - {ex.Message}");
            }
            finally
            {
                _performingMutations = false;
            }
        }

        public static void Start(Mutation mutation)
        {
            mutation.SetReady();

            _mutations.Add(mutation);
            StartPerformMutations();
        }

        public static Task StartAsync(Mutation mutation)
        {
            Start(mutation);
            return mutation.Started;
        }
    }
}
