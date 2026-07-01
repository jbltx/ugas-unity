using System.Collections.Generic;
using System.Globalization;
using Jbltx.Ugas.Definitions;

namespace Jbltx.Ugas.Abilities
{
    /// <summary>
    /// Builds <see cref="IAbilityTask"/> instances from authored <see cref="AbilityTaskDefinition"/>
    /// data (SPEC §10). Unknown task types fall back to a <see cref="NoOpTask"/> that completes
    /// immediately, so an ability never hangs on a type this reference implementation doesn't model yet.
    /// </summary>
    public static class AbilityTaskFactory
    {
        public static IAbilityTask Create(in AbilityTaskDefinition def)
        {
            switch (def.Type)
            {
                case "WaitDelay":
                    return new WaitDelayTask(GetFloat(def.Params, "Seconds", 0f), def.TickInterval, def.Priority);
                default:
                    return new NoOpTask(def.Type, def.TickInterval, def.Priority);
            }
        }

        private static float GetFloat(List<TaskParam> p, string key, float fallback)
        {
            if (p != null)
            {
                for (int i = 0; i < p.Count; i++)
                    if (p[i].Key == key &&
                        float.TryParse(p[i].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                        return v;
            }
            return fallback;
        }
    }

    /// <summary>Completes on its first tick — fallback for task types not yet modeled (SPEC §10).</summary>
    public sealed class NoOpTask : AbilityTaskBase
    {
        public override string Type { get; }

        public NoOpTask(string type, float tickInterval = 0f, int priority = 0) : base(tickInterval, priority)
        {
            Type = type;
        }

        protected override void OnTick(float step) => Complete();
    }
}
