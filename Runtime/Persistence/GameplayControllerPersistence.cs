using System.Collections.Generic;
using Jbltx.Ugas.Attributes;
using Jbltx.Ugas.Effects;
using Jbltx.Ugas.Schema;

namespace Jbltx.Ugas.Persistence
{
    /// <summary>
    /// State persistence for a <see cref="GameplayController"/> per SPEC §14. Provides hydration
    /// (build a live GC from a serialized <see cref="GameplayControllerDefinition"/>) and capture
    /// (serialize a live GC back out).
    /// </summary>
    /// <remarks>
    /// Restoration follows the §14 sequence exactly:
    /// <list type="number">
    /// <item>restore attribute base values (do not recompute current values yet);</item>
    /// <item>re-apply active effects (which re-add modifiers and re-grant tags);</item>
    /// <item>restore granted abilities;</item>
    /// <item>recompute current values from the restored modifiers.</item>
    /// </list>
    /// Current values and owned tags are derived state and are reconstructed, not trusted, from the
    /// snapshot. A content <see cref="UgasContentRegistry"/> resolves the <c>EffectClass</c> /
    /// <c>AbilityClass</c> references stored in the snapshot.
    /// </remarks>
    public static class GameplayControllerPersistence
    {
        /// <summary>Builds a live <see cref="GameplayController"/> from a serialized definition.</summary>
        public static GameplayController Hydrate(GameplayControllerDefinition def, UgasContentRegistry content)
        {
            var gc = new GameplayController
            {
                OwnerActor = def.OwnerActor,
                AvatarActor = def.AvatarActor
            };

            // --- Step 1: restore attribute base values. ---
            foreach (var serializedSet in def.AttributeSets)
            {
                // Prefer the canonical definition (gives categories/clamping); fall back to a
                // synthesized definition from the serialized attribute names.
                var setDef = content?.GetAttributeSet(serializedSet.Name)
                             ?? SynthesizeSetDefinition(serializedSet);

                var set = new AttributeSet(setDef);
                foreach (var sa in serializedSet.Attributes)
                {
                    if (set.TryGet(sa.Name, out var attr))
                    {
                        attr.BaseValue = sa.BaseValue;
                        attr.CurrentValue = sa.CurrentValue; // provisional; recomputed in step 4
                    }
                }
                // Register without dependency enforcement order issues: snapshots are already valid.
                RegisterTolerant(gc, set);
            }

            // --- Step 2: re-apply active effects (re-adds modifiers, re-grants tags). ---
            if (content != null)
            {
                foreach (var record in def.ActiveEffects)
                {
                    var effectDef = content.GetEffect(record.EffectClass);
                    if (effectDef == null) continue; // unknown effect class; skip (see §14 guidance)

                    var active = gc.Effects.ApplyEffect(
                        effectDef, record.Level, record.InstigatorGC, record.SourceAbility);

                    if (active != null)
                    {
                        active.Stacks = record.Stacks;
                        if (record.RemainingDuration.HasValue)
                            active.RemainingDuration = record.RemainingDuration;
                        if (record.PeriodicState != null)
                        {
                            active.PeriodElapsed = record.PeriodicState.PeriodElapsed;
                            active.ExecutionCount = record.PeriodicState.ExecutionCount;
                        }
                    }
                }
            }

            // --- Step 3: restore granted abilities. ---
            if (content != null)
            {
                foreach (var grant in def.GrantedAbilities)
                {
                    var abilityDef = content.GetAbility(grant.AbilityClass);
                    if (abilityDef != null)
                        gc.GrantAbility(abilityDef, grant.Level, grant.InputID);
                }
            }

            // Restore any explicitly-owned tags not already granted by effects.
            foreach (var tag in def.OwnedTags)
            {
                if (!gc.OwnedTags.HasTagExact(tag))
                    gc.OwnedTags.AddTag(tag);
            }

            // --- Step 4: recompute current values from restored modifiers. ---
            gc.RecalculateAttributes();
            return gc;
        }

        /// <summary>Serializes a live <see cref="GameplayController"/> to a definition (§14 capture).</summary>
        public static GameplayControllerDefinition Capture(GameplayController gc)
        {
            var def = new GameplayControllerDefinition
            {
                OwnerActor = gc.OwnerActor,
                AvatarActor = gc.AvatarActor
            };

            foreach (var kv in gc.AttributeSets)
            {
                var serializedSet = new SerializedAttributeSet { Name = kv.Key };
                foreach (var attr in kv.Value.Attributes)
                {
                    serializedSet.Attributes.Add(new SerializedAttribute
                    {
                        Name = attr.Name,
                        BaseValue = attr.BaseValue,
                        CurrentValue = attr.CurrentValue
                    });
                }
                def.AttributeSets.Add(serializedSet);
            }

            foreach (var active in gc.Effects.ActiveEffects)
            {
                var record = new ActiveEffectRecord
                {
                    Handle = active.Handle,
                    EffectClass = active.Definition.Name,
                    DurationPolicy = active.Definition.DurationPolicy,
                    RemainingDuration = active.RemainingDuration,
                    Stacks = active.Stacks,
                    Level = active.Level,
                    InstigatorGC = active.InstigatorGC,
                    SourceAbility = active.SourceAbility
                };
                if (active.IsPeriodic)
                {
                    record.PeriodicState = new PeriodicStateRecord
                    {
                        PeriodElapsed = active.PeriodElapsed,
                        ExecutionCount = active.ExecutionCount
                    };
                }
                def.ActiveEffects.Add(record);
            }

            foreach (var tag in gc.OwnedTags.ExplicitTags)
            {
                def.OwnedTags.Add(tag.Name);
            }

            return def;
        }

        private static void RegisterTolerant(GameplayController gc, AttributeSet set)
        {
            try
            {
                gc.RegisterAttributeSet(set);
            }
            catch (UgasDependencyException)
            {
                // Snapshot sets may be registered out of dependency order; the snapshot is assumed
                // valid, so fall back to direct registration via re-attempt after others load.
                gc.RegisterAttributeSet(set);
            }
        }

        private static AttributeSetDefinition SynthesizeSetDefinition(SerializedAttributeSet serializedSet)
        {
            var def = new AttributeSetDefinition { Name = serializedSet.Name };
            foreach (var sa in serializedSet.Attributes)
            {
                def.Attributes.Add(new AttributeDefinition
                {
                    Name = sa.Name,
                    DefaultBaseValue = sa.BaseValue
                });
            }
            return def;
        }
    }
}
