namespace Jbltx.Ugas.Abilities
{
    /// <summary>
    /// A latent task that waits a fixed number of seconds, then completes (SPEC §10) — the canonical
    /// cast-time / wind-up primitive. Built from a <c>WaitDelay</c> task entry with a <c>Seconds</c>
    /// parameter.
    /// </summary>
    public sealed class WaitDelayTask : AbilityTaskBase
    {
        public override string Type => "WaitDelay";

        private readonly float _seconds;
        private float _elapsed;

        public WaitDelayTask(float seconds, float tickInterval = 0f, int priority = 0)
            : base(tickInterval, priority)
        {
            _seconds = seconds;
        }

        protected override void OnTick(float step)
        {
            _elapsed += step;
            if (_elapsed >= _seconds) Complete();
        }
    }
}
