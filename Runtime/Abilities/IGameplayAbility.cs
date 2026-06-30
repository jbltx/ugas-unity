using Jbltx.Ugas.Schema;

namespace Jbltx.Ugas.Abilities
{
    /// <summary>
    /// A runtime gameplay ability and its lifecycle (SPEC §8). Implementations drive the
    /// <see cref="AbilityState"/> machine: validate activation requirements, commit cost/cooldown,
    /// execute tasks, and end or cancel.
    /// </summary>
    /// <remarks>
    /// <see cref="GameplayAbility"/> provides a minimal default implementation of the state machine
    /// and the activation checks (granted, not-already-active, required/blocked tags). Cost,
    /// cooldown, and ability-task execution are scaffolded as virtual hooks pending full
    /// implementation.
    /// </remarks>
    public interface IGameplayAbility
    {
        /// <summary>The static ability definition.</summary>
        GameplayAbilityDefinition Definition { get; }

        /// <summary>Current lifecycle state.</summary>
        AbilityState State { get; }

        /// <summary>Ability level for magnitude/curve scaling.</summary>
        int Level { get; }

        /// <summary>
        /// Attempts to activate the ability against the given controller. Runs the §8 validation
        /// sequence; on success transitions Granted → Activating → Active (Commit) and returns true.
        /// On failure returns to Granted and returns false.
        /// </summary>
        bool TryActivate(IGameplayController controller);

        /// <summary>Ends the ability normally (Active → Ending → Granted).</summary>
        void EndAbility();

        /// <summary>Cancels the ability (Active → Ending → Granted), e.g. due to a cancel tag or death.</summary>
        void CancelAbility();
    }
}
