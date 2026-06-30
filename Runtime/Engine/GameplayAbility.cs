using System.Collections.Generic;
using Jbltx.Ugas.Abilities;
using Jbltx.Ugas.Definitions;
using Jbltx.Ugas.Kernel;
using Jbltx.Ugas.Tags;

namespace Jbltx.Ugas.Runtime
{
    /// <summary>
    /// A live gameplay ability and its §8 lifecycle state machine, re-homed onto the Unity-native
    /// runtime. Drives NotGranted → Granted → Activating → Active → Ending → Granted, with
    /// Activating → Granted on validation failure. Activation-tag checks use interned handles
    /// resolved once at grant time. Cost and cooldown are applied as GameplayEffects; ability-task
    /// execution (§10) remains a virtual stub.
    /// </summary>
    public class GameplayAbility
    {
        public GameplayAbilityDefinition Definition { get; }
        public AbilityState State { get; private set; } = AbilityState.NotGranted;
        public int Level { get; private set; } = 1;

        protected IUgasRuntime Runtime { get; private set; }

        // Activation-tag handles, resolved once when granted.
        private readonly List<GameplayTag> _activationRequired = new List<GameplayTag>();
        private readonly List<GameplayTag> _activationBlocked = new List<GameplayTag>();
        private readonly List<GameplayTag> _blockedBy = new List<GameplayTag>();
        private readonly List<GameplayTag> _activationOwned = new List<GameplayTag>();
        // Tags the cooldown effect grants for its duration; their presence means "on cooldown".
        private readonly List<GameplayTag> _cooldownTags = new List<GameplayTag>();

        public GameplayAbility(GameplayAbilityDefinition definition, int level = 1)
        {
            Definition = definition;
            Level = level;
        }

        /// <summary>NotGranted → Granted. Resolves activation-tag handles against the runtime registry.</summary>
        public void Grant(IUgasRuntime runtime)
        {
            if (State != AbilityState.NotGranted) return;
            var registry = runtime.OwnedTags.Registry;
            Resolve(registry, Definition.Tags.ActivationRequiredTags, _activationRequired);
            Resolve(registry, Definition.Tags.ActivationBlockedTags, _activationBlocked);
            Resolve(registry, Definition.Tags.BlockedByTags, _blockedBy);
            Resolve(registry, Definition.Tags.ActivationOwnedTags, _activationOwned);
            _cooldownTags.Clear();
            if (Definition.Cooldown != null) Resolve(registry, Definition.Cooldown.GrantedTags, _cooldownTags);
            State = AbilityState.Granted;
        }

        /// <summary>True if currently Granted and all §8 requirements are met.</summary>
        public bool CanActivate(IUgasRuntime runtime)
        {
            if (runtime == null || State != AbilityState.Granted) return false;
            return MeetsActivationRequirements(runtime);
        }

        /// <summary>State-independent §8 requirement checks (tags, cost, cooldown).</summary>
        protected bool MeetsActivationRequirements(IUgasRuntime runtime)
        {
            var tags = runtime.OwnedTags;
            if (_activationRequired.Count > 0 && !tags.HasAll(_activationRequired)) return false;
            if (_activationBlocked.Count > 0 && tags.HasAny(_activationBlocked)) return false;
            if (_blockedBy.Count > 0 && tags.HasAny(_blockedBy)) return false;
            if (!CheckCost(runtime)) return false;
            if (!CheckCooldown(runtime)) return false;
            return true;
        }

        /// <summary>Granted → Activating → Active on success; back to Granted on failure.</summary>
        public bool TryActivate(IUgasRuntime runtime)
        {
            if (runtime == null || State != AbilityState.Granted) return false;

            State = AbilityState.Activating;
            if (!MeetsActivationRequirements(runtime))
            {
                State = AbilityState.Granted;
                return false;
            }

            Runtime = runtime;
            Commit(runtime);
            State = AbilityState.Active;
            OnActivate(runtime);
            return true;
        }

        /// <summary>Active → Ending → Granted.</summary>
        public void EndAbility()
        {
            if (State != AbilityState.Active) return;
            State = AbilityState.Ending;
            OnEnd(false);
            FinishEnding();
        }

        /// <summary>Active → Ending → Granted (cancellation).</summary>
        public void CancelAbility()
        {
            if (State != AbilityState.Active) return;
            State = AbilityState.Ending;
            OnEnd(true);
            FinishEnding();
        }

        private void FinishEnding()
        {
            if (Runtime != null)
            {
                for (int i = 0; i < _activationOwned.Count; i++) Runtime.OwnedTags.RemoveTag(_activationOwned[i]);
            }
            Runtime = null;
            State = AbilityState.Granted;
        }

        // ---- Hooks (overridable; default behaviour intentionally minimal) ----

        /// <summary>Validates the cost can be paid: each flat resource spend stays >= 0 (§8.5).</summary>
        protected virtual bool CheckCost(IUgasRuntime runtime)
        {
            var cost = Definition.Cost;
            if (cost == null) return true;
            var mods = cost.Modifiers;
            for (int i = 0; i < mods.Count; i++)
            {
                var m = mods[i];
                if (m.Operation != ModifierOp.Add) continue; // costs are flat resource deltas
                float delta = runtime.ResolveMagnitude(m.Magnitude, Level);
                if (delta < 0f && runtime.GetCurrentValue(m.Attribute) + delta < 0f) return false;
            }
            return true;
        }

        /// <summary>Validates the ability is not on cooldown (no cooldown-granted tag present, §8.5).</summary>
        protected virtual bool CheckCooldown(IUgasRuntime runtime)
            => _cooldownTags.Count == 0 || !runtime.OwnedTags.HasAny(_cooldownTags);

        /// <summary>Commit phase (§8): apply Cost (Instant) + Cooldown (durational) effects, grant owned tags.</summary>
        protected virtual void Commit(IUgasRuntime runtime)
        {
            if (Definition.Cost != null) runtime.ApplyEffect(Definition.Cost, Level);
            if (Definition.Cooldown != null) runtime.ApplyEffect(Definition.Cooldown, Level);
            for (int i = 0; i < _activationOwned.Count; i++) runtime.OwnedTags.AddTag(_activationOwned[i]);
        }

        /// <summary>Called on entering Active. Stub: ability tasks are not yet run (SPEC §10).</summary>
        protected virtual void OnActivate(IUgasRuntime runtime)
        {
            // TODO(tasks): run Definition.Tasks through an ability-task scheduler.
        }

        /// <summary>Called when ending. <paramref name="cancelled"/> distinguishes cancel from normal end.</summary>
        protected virtual void OnEnd(bool cancelled)
        {
            // TODO(tasks): cancel in-flight ability tasks.
        }

        private static void Resolve(GameplayTagRegistryRuntime registry, IReadOnlyList<string> names, List<GameplayTag> into)
        {
            into.Clear();
            if (names == null) return;
            for (int i = 0; i < names.Count; i++)
            {
                var tag = registry.Resolve(names[i]);
                if (tag.IsValid) into.Add(tag);
            }
        }
    }
}
