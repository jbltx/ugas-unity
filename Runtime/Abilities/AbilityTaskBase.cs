namespace Jbltx.Ugas.Abilities
{
    /// <summary>
    /// Base class for latent ability tasks (SPEC §10). Handles the lifecycle state and
    /// <see cref="IAbilityTask.TickInterval"/> accumulation so concrete tasks only implement
    /// <see cref="OnTick"/> and call <see cref="Complete"/> when their trigger fires.
    /// </summary>
    public abstract class AbilityTaskBase : IAbilityTask
    {
        public abstract string Type { get; }
        public AbilityTaskState State { get; protected set; } = AbilityTaskState.Inactive;
        public float TickInterval { get; protected set; }
        public int Priority { get; protected set; }

        private float _sinceTick;

        protected AbilityTaskBase(float tickInterval = 0f, int priority = 0)
        {
            TickInterval = tickInterval;
            Priority = priority;
            State = AbilityTaskState.Ready;
        }

        public virtual void Activate()
        {
            if (State == AbilityTaskState.Ready) State = AbilityTaskState.Active;
        }

        public void Tick(float deltaSeconds)
        {
            if (State != AbilityTaskState.Active) return;

            if (TickInterval <= 0f)
            {
                OnTick(deltaSeconds); // every-frame task
                return;
            }

            _sinceTick += deltaSeconds;
            while (_sinceTick >= TickInterval && State == AbilityTaskState.Active)
            {
                _sinceTick -= TickInterval;
                OnTick(TickInterval);
            }
        }

        public virtual void Cancel()
        {
            if (State == AbilityTaskState.Active || State == AbilityTaskState.Ready)
                State = AbilityTaskState.Cancelled;
        }

        /// <summary>Advances the task by one (interval-quantized) step; call <see cref="Complete"/> when done.</summary>
        protected abstract void OnTick(float step);

        protected void Complete() => State = AbilityTaskState.Completed;
    }
}
