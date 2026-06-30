using Jbltx.Ugas.Schema;

namespace Jbltx.Ugas.Abilities
{
    /// <summary>
    /// Minimal default <see cref="IGameplayAbility"/> implementing the SPEC §8 lifecycle state
    /// machine and activation-validation sequence. Cost/cooldown application and ability-task
    /// execution are virtual hooks left as stubs for a full implementation.
    /// </summary>
    public class GameplayAbility : IGameplayAbility
    {
        public GameplayAbilityDefinition Definition { get; }
        public AbilityState State { get; private set; } = AbilityState.NotGranted;
        public int Level { get; private set; } = 1;

        /// <summary>The controller this ability is currently active on (null when inactive).</summary>
        protected IGameplayController Controller { get; private set; }

        public GameplayAbility(GameplayAbilityDefinition definition, int level = 1)
        {
            Definition = definition;
            Level = level;
        }

        /// <summary>Transitions NotGranted → Granted. Idempotent if already granted.</summary>
        public void Grant()
        {
            if (State == AbilityState.NotGranted) State = AbilityState.Granted;
        }

        /// <summary>
        /// Runs the §8 activation checks against the controller without changing state. True only
        /// when the ability is currently <see cref="AbilityState.Granted"/> and all requirements
        /// (tags, cost, cooldown) are satisfied.
        /// </summary>
        public bool CanActivate(IGameplayController controller)
        {
            if (controller == null) return false;
            // 1. Must be granted. 2. Must not already be active.
            if (State != AbilityState.Granted) return false;
            return MeetsActivationRequirements(controller);
        }

        /// <summary>
        /// The state-independent §8 requirement checks (tags, cost, cooldown). Separated from
        /// <see cref="CanActivate"/> so it can be re-run from the <see cref="AbilityState.Activating"/>
        /// validation phase without the Granted-state gate rejecting it.
        /// </summary>
        protected bool MeetsActivationRequirements(IGameplayController controller)
        {
            var tags = Definition.Tags;
            // 3. Owner must have ALL ActivationRequiredTags.
            if (tags.ActivationRequiredTags.Count > 0 && !controller.OwnedTags.HasAll(tags.ActivationRequiredTags))
                return false;
            // 4. Owner must have NONE of ActivationBlockedTags.
            if (tags.ActivationBlockedTags.Count > 0 && controller.OwnedTags.HasAny(tags.ActivationBlockedTags))
                return false;
            // 5. BlockedByTags currently present also prevent activation.
            if (tags.BlockedByTags.Count > 0 && controller.OwnedTags.HasAny(tags.BlockedByTags))
                return false;

            // 5b. Cost affordability and 6. cooldown availability.
            if (!CheckCost(controller)) return false;
            if (!CheckCooldown(controller)) return false;

            return true;
        }

        public bool TryActivate(IGameplayController controller)
        {
            if (controller == null) return false;
            if (State != AbilityState.Granted) return false;

            // Granted -> Activating (validation phase).
            State = AbilityState.Activating;

            if (!MeetsActivationRequirements(controller))
            {
                // Activating -> Granted on validation failure.
                State = AbilityState.Granted;
                return false;
            }

            Controller = controller;

            // Commit: consume cost, begin cooldown, grant ActivationOwnedTags. Point of no return.
            Commit(controller);

            // Activating -> Active.
            State = AbilityState.Active;
            OnActivate(controller);
            return true;
        }

        public void EndAbility()
        {
            if (State != AbilityState.Active) return;
            State = AbilityState.Ending;
            OnEnd(false);
            FinishEnding();
        }

        public void CancelAbility()
        {
            if (State != AbilityState.Active) return;
            State = AbilityState.Ending;
            OnEnd(true);
            FinishEnding();
        }

        private void FinishEnding()
        {
            // Ending -> Granted (re-activatable). Remove ActivationOwnedTags granted at Commit.
            var owned = Definition.Tags.ActivationOwnedTags;
            if (Controller != null && owned.Count > 0)
            {
                foreach (var tag in owned) Controller.OwnedTags.RemoveTag(tag);
            }
            Controller = null;
            State = AbilityState.Granted;
        }

        // ---- Hooks (overridable; default behaviour is intentionally minimal) ----

        /// <summary>Validates the cost effect can be paid. Stub: always affordable.</summary>
        protected virtual bool CheckCost(IGameplayController controller)
        {
            // TODO: evaluate Definition.Cost (a GameplayEffect reference) against current resources.
            return true;
        }

        /// <summary>Validates the cooldown tag is absent. Stub: always available.</summary>
        protected virtual bool CheckCooldown(IGameplayController controller)
        {
            // TODO: check the cooldown tag granted by Definition.Cooldown via OwnedTags.HasTag.
            return true;
        }

        /// <summary>
        /// Commit phase (SPEC §8): apply cost &amp; cooldown effects and grant ActivationOwnedTags via
        /// an auto-generated Infinite effect. This default grants the owned tags directly; full cost
        /// and cooldown application is a TODO.
        /// </summary>
        protected virtual void Commit(IGameplayController controller)
        {
            // TODO: apply Definition.Cost and Definition.Cooldown as GameplayEffects.
            // Per §8, ActivationOwnedTags should be applied as an auto-generated Infinite effect;
            // here they are granted directly and removed on end/cancel.
            foreach (var tag in Definition.Tags.ActivationOwnedTags)
            {
                controller.OwnedTags.AddTag(tag);
            }
        }

        /// <summary>Called when the ability enters Active. Stub: ability tasks are not yet run.</summary>
        protected virtual void OnActivate(IGameplayController controller)
        {
            // TODO(tasks): instantiate and run Definition.Tasks (PlayMontage, WaitGameplayEvent, ...)
            // through an ability-task scheduler (SPEC §10). For now activation is synchronous and the
            // caller is expected to drive EndAbility().
        }

        /// <summary>Called when the ability begins ending. <paramref name="cancelled"/> distinguishes cancel from normal end.</summary>
        protected virtual void OnEnd(bool cancelled)
        {
            // TODO(tasks): cancel any in-flight ability tasks here.
        }
    }
}
