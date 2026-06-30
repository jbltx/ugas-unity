namespace Jbltx.Ugas.Abilities
{
    /// <summary>
    /// The gameplay-ability lifecycle states (SPEC §8). Legal transitions:
    /// <list type="number">
    /// <item><see cref="NotGranted"/> → <see cref="Granted"/> (GrantAbility)</item>
    /// <item><see cref="Granted"/> → <see cref="Activating"/> (TryActivate)</item>
    /// <item><see cref="Activating"/> → <see cref="Granted"/> (activation fails validation)</item>
    /// <item><see cref="Activating"/> → <see cref="Active"/> (Commit)</item>
    /// <item><see cref="Active"/> → <see cref="Ending"/> (End or Cancel)</item>
    /// <item><see cref="Ending"/> → <see cref="Granted"/> (ability ends; re-activatable)</item>
    /// </list>
    /// </summary>
    public enum AbilityState
    {
        /// <summary>Not granted to any controller.</summary>
        NotGranted,

        /// <summary>Granted and inactive; eligible for activation.</summary>
        Granted,

        /// <summary>Activation requested; validating requirements (point before commit).</summary>
        Activating,

        /// <summary>Committed and executing.</summary>
        Active,

        /// <summary>Ending or being cancelled; cleaning up before returning to Granted.</summary>
        Ending
    }
}
