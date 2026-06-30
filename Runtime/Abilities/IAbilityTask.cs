namespace Jbltx.Ugas.Abilities
{
    /// <summary>Ability-task lifecycle states (SPEC §10).</summary>
    public enum AbilityTaskState
    {
        /// <summary>Created but not yet active.</summary>
        Inactive,

        /// <summary>Configured, awaiting activation.</summary>
        Ready,

        /// <summary>Listening / executing.</summary>
        Active,

        /// <summary>Trigger condition met; ability execution resumes.</summary>
        Completed,

        /// <summary>Aborted (ability cancelled, owner died).</summary>
        Cancelled
    }

    /// <summary>
    /// A latent ability task (SPEC §10): an asynchronous node that pauses ability execution until a
    /// trigger condition (timer, gameplay event, input, tag change, overlap, animation notify) is
    /// met, then resumes the owning ability.
    /// </summary>
    /// <remarks>
    /// This is an interface stub. A full implementation provides concrete task types (WaitDelay,
    /// WaitGameplayEvent, PlayMontage, ...) and a scheduler honoring <see cref="TickInterval"/> and
    /// per-frame tick budgeting. See the <c>// TODO(tasks)</c> markers in
    /// <see cref="GameplayAbility"/>.
    /// </remarks>
    public interface IAbilityTask
    {
        /// <summary>Task type identifier (matches the schema <c>Type</c>).</summary>
        string Type { get; }

        /// <summary>Current lifecycle state.</summary>
        AbilityTaskState State { get; }

        /// <summary>Seconds between <see cref="Tick"/> evaluations; 0 means every frame.</summary>
        float TickInterval { get; }

        /// <summary>Tick scheduling priority when the per-frame budget is exhausted; higher ticks first.</summary>
        int Priority { get; }

        /// <summary>Begins listening / executing (Ready → Active).</summary>
        void Activate();

        /// <summary>Advances the task. Implementations transition to Completed when their trigger fires.</summary>
        void Tick(float deltaSeconds);

        /// <summary>Aborts the task (→ Cancelled), releasing subscriptions and resources.</summary>
        void Cancel();
    }
}
