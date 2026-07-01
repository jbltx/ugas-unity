using System.Collections.Generic;
using System.Globalization;
using Jbltx.Ugas.Definitions;
using Jbltx.Ugas.Runtime;
using Jbltx.Ugas.Spatial;

namespace Jbltx.Ugas.Abilities
{
    /// <summary>
    /// The execution context a task needs beyond its authored definition (SPEC §10): the instigating
    /// controller, the spatial provider it queries (§17), and the ability level. Supplied by
    /// <c>GameplayAbility.OnActivate</c>; <c>default</c> for context-free tasks.
    /// </summary>
    public readonly struct AbilityTaskContext
    {
        public readonly UgasController Instigator;
        public readonly ISpatialQueryProvider Provider;
        public readonly int Level;

        public AbilityTaskContext(UgasController instigator, ISpatialQueryProvider provider, int level)
        {
            Instigator = instigator;
            Provider = provider;
            Level = level;
        }
    }

    /// <summary>
    /// Builds <see cref="IAbilityTask"/> instances from authored <see cref="AbilityTaskDefinition"/>
    /// data (SPEC §10). Unknown task types fall back to a <see cref="NoOpTask"/> that completes
    /// immediately, so an ability never hangs on a type this reference implementation doesn't model yet.
    /// </summary>
    public static class AbilityTaskFactory
    {
        /// <summary>Context-free convenience: builds tasks that don't need an instigator/provider.</summary>
        public static IAbilityTask Create(in AbilityTaskDefinition def) => Create(def, default);

        public static IAbilityTask Create(in AbilityTaskDefinition def, in AbilityTaskContext ctx)
        {
            switch (def.Type)
            {
                case "WaitDelay":
                    // Genre packs author the wait as `Duration`; older data uses `Seconds`. Accept both.
                    return new WaitDelayTask(GetFloat(def.Params, "Duration", GetFloat(def.Params, "Seconds", 0f)), def.TickInterval, def.Priority);
                case "ApplyEffectToOwner":
                    return new ApplyEffectToOwnerTask(
                        ctx.Instigator,
                        ctx.Instigator != null ? ctx.Instigator.ResolveEffect(GetString(def.Params, "EffectClass", null)) : null,
                        ctx.Level,
                        def.TickInterval,
                        def.Priority);
                case "RemoveEffectFromOwner":
                    return new RemoveEffectFromOwnerTask(ctx.Instigator, GetString(def.Params, "EffectClass", null), def.TickInterval, def.Priority);
                case "ApplyEffectToActorsInRadius":
                    return new ApplyEffectInRadiusTask(
                        ctx.Instigator,
                        ctx.Provider,
                        ctx.Instigator != null ? ctx.Instigator.ResolveEffect(GetString(def.Params, "EffectClass", null)) : null,
                        GetFloat(def.Params, "Radius", 0f),
                        GetString(def.Params, "IgnoreTargetsWithTag", null),
                        ctx.Level,
                        def.TickInterval,
                        def.Priority);
                default:
                    return new NoOpTask(def.Type, def.TickInterval, def.Priority);
            }
        }

        private static string GetString(List<TaskParam> p, string key, string fallback)
        {
            if (p != null)
            {
                for (int i = 0; i < p.Count; i++)
                    if (p[i].Key == key) return p[i].Value;
            }
            return fallback;
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
