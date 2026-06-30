using System.Collections.Generic;
using Jbltx.Ugas.Schema.Yaml;

namespace Jbltx.Ugas.Schema
{
    /// <summary>
    /// Maps parsed <see cref="YamlNode"/> trees onto the typed engine-agnostic model. One method per
    /// core schema. All mappers tolerate missing/unknown fields so genre packs that carry extra keys
    /// (or the <c>Duration: -1</c> shorthand) still load.
    /// </summary>
    internal static class SchemaMapper
    {
        public static AttributeDefinition MapAttribute(YamlMapping m)
        {
            var a = new AttributeDefinition
            {
                Name = YamlParser.AsString(m.Get("Name")),
                DefaultBaseValue = YamlParser.AsFloat(m.Get("DefaultBaseValue")),
                Category = SchemaEnums.ParseCategory(YamlParser.AsString(m.Get("Category"))),
                ReplicationMode = SchemaEnums.ParseReplicationMode(YamlParser.AsString(m.Get("ReplicationMode")))
            };

            if (m.Get("Clamping") is YamlMapping clamp)
            {
                a.Clamping = new AttributeClamping
                {
                    Min = MapBound(clamp.Get("Min")),
                    Max = MapBound(clamp.Get("Max"))
                };
            }

            if (m.Get("Metadata") is YamlMapping meta)
            {
                a.Metadata = new AttributeMetadata
                {
                    DisplayName = YamlParser.AsString(meta.Get("DisplayName")),
                    Description = YamlParser.AsString(meta.Get("Description")),
                    UICategory = YamlParser.AsString(meta.Get("UICategory")),
                    Icon = YamlParser.AsString(meta.Get("Icon"))
                };
            }

            return a;
        }

        private static AttributeBound MapBound(YamlNode node)
        {
            if (!(node is YamlScalar s)) return null;
            // A numeric literal vs an attribute reference (oneOf: number | string).
            if (!s.WasQuoted &&
                float.TryParse(s.Raw, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var v))
            {
                return AttributeBound.FromLiteral(v);
            }
            var str = YamlParser.AsString(s);
            return str == null ? null : AttributeBound.FromReference(str);
        }

        public static AttributeSetDefinition MapAttributeSet(YamlMapping m)
        {
            var set = new AttributeSetDefinition
            {
                Name = YamlParser.AsString(m.Get("Name")),
                Dependencies = YamlParser.AsStringList(m.Get("Dependencies"))
            };

            if (m.Get("Attributes") is YamlSequence attrs)
            {
                foreach (var item in attrs.Items)
                {
                    if (item is YamlMapping am) set.Attributes.Add(MapAttribute(am));
                }
            }

            if (m.Get("Metadata") is YamlMapping meta)
            {
                set.Metadata = new AttributeSetMetadata
                {
                    DisplayName = YamlParser.AsString(meta.Get("DisplayName")),
                    Description = YamlParser.AsString(meta.Get("Description"))
                };
            }

            return set;
        }

        public static GameplayTagRegistry MapTagRegistry(YamlMapping m)
        {
            var reg = new GameplayTagRegistry();
            if (m.Get("Tags") is YamlSequence tags)
            {
                foreach (var item in tags.Items)
                {
                    if (item is YamlMapping tm)
                    {
                        reg.Tags.Add(new GameplayTagDefinition
                        {
                            Tag = YamlParser.AsString(tm.Get("Tag")),
                            Description = YamlParser.AsString(tm.Get("Description")),
                            AllowMultiple = YamlParser.AsBool(tm.Get("AllowMultiple")),
                            DevComment = YamlParser.AsString(tm.Get("DevComment"))
                        });
                    }
                }
            }
            return reg;
        }

        public static MagnitudeDefinition MapMagnitude(YamlMapping m)
        {
            if (m == null) return null;
            return new MagnitudeDefinition
            {
                Type = SchemaEnums.ParseMagnitudeType(YamlParser.AsString(m.Get("Type"))),
                Value = YamlParser.AsFloat(m.Get("Value")),
                Curve = YamlParser.AsString(m.Get("Curve")),
                CurveInput = YamlParser.AsString(m.Get("CurveInput")),
                BackingAttribute = YamlParser.AsString(m.Get("BackingAttribute")),
                Source = SchemaEnums.ParseMagnitudeSource(YamlParser.AsString(m.Get("Source"))),
                Coefficient = YamlParser.AsFloat(m.Get("Coefficient"), 1f),
                PreMultiplyAdditive = YamlParser.AsFloat(m.Get("PreMultiplyAdditive")),
                PostMultiplyAdditive = YamlParser.AsFloat(m.Get("PostMultiplyAdditive")),
                CalculatorClass = YamlParser.AsString(m.Get("CalculatorClass")),
                DataTag = YamlParser.AsString(m.Get("DataTag"))
            };
        }

        public static GameplayEffectDefinition MapEffect(YamlMapping m)
        {
            var e = new GameplayEffectDefinition
            {
                Name = YamlParser.AsString(m.Get("Name")),
                DurationPolicy = SchemaEnums.ParseDurationPolicy(YamlParser.AsString(m.Get("DurationPolicy"))),
                ExecutionPolicy = SchemaEnums.ParseExecutionPolicy(YamlParser.AsString(m.Get("ExecutionPolicy"))),
                Priority = YamlParser.AsInt(m.Get("Priority")),
                Duration = MapMagnitude(m.Get("Duration") as YamlMapping),
                GrantedTags = YamlParser.AsStringList(m.Get("GrantedTags")),
                ApplicationRequiredTags = YamlParser.AsStringList(m.Get("ApplicationRequiredTags")),
                GameplayCues = YamlParser.AsStringList(m.Get("GameplayCues"))
            };

            if (m.Get("Period") is YamlMapping period)
            {
                e.Period = new PeriodDefinition
                {
                    Period = YamlParser.AsFloat(period.Get("Period")),
                    ExecuteOnApplication = YamlParser.AsBool(period.Get("ExecuteOnApplication"))
                };
            }

            if (m.Get("Modifiers") is YamlSequence mods)
            {
                foreach (var item in mods.Items)
                {
                    if (item is YamlMapping mm)
                    {
                        e.Modifiers.Add(new ModifierDefinition
                        {
                            Attribute = YamlParser.AsString(mm.Get("Attribute")),
                            Operation = SchemaEnums.ParseModifierOperation(YamlParser.AsString(mm.Get("Operation"))),
                            Magnitude = MapMagnitude(mm.Get("Magnitude") as YamlMapping),
                            Channel = YamlParser.AsString(mm.Get("Channel"))
                        });
                    }
                }
            }

            if (m.Get("Executions") is YamlSequence execs)
            {
                foreach (var item in execs.Items)
                {
                    if (item is YamlMapping xm)
                    {
                        e.Executions.Add(new ExecutionDefinition
                        {
                            CalculatorClass = YamlParser.AsString(xm.Get("CalculatorClass"))
                        });
                    }
                }
            }

            if (m.Get("GrantedAbilities") is YamlSequence grants)
            {
                foreach (var item in grants.Items)
                {
                    if (item is YamlMapping gm)
                    {
                        e.GrantedAbilities.Add(new GrantedAbilityDefinition
                        {
                            AbilityClass = YamlParser.AsString(gm.Get("AbilityClass")),
                            Level = YamlParser.AsInt(gm.Get("Level"), 1),
                            InputID = YamlParser.AsString(gm.Get("InputID")),
                            RemoveOnEffectRemoval = YamlParser.AsBool(gm.Get("RemoveOnEffectRemoval"), true)
                        });
                    }
                }
            }

            return e;
        }

        public static GameplayAbilityDefinition MapAbility(YamlMapping m)
        {
            var a = new GameplayAbilityDefinition
            {
                Name = YamlParser.AsString(m.Get("Name")),
                Cost = YamlParser.AsString(m.Get("Cost")),
                Cooldown = YamlParser.AsString(m.Get("Cooldown"))
            };

            if (m.Get("Tags") is YamlMapping t)
            {
                a.Tags = new AbilityTagSet
                {
                    AbilityTags = YamlParser.AsStringList(t.Get("AbilityTags")),
                    BlockedByTags = YamlParser.AsStringList(t.Get("BlockedByTags")),
                    BlockAbilitiesWithTags = YamlParser.AsStringList(t.Get("BlockAbilitiesWithTags")),
                    CancelAbilitiesWithTags = YamlParser.AsStringList(t.Get("CancelAbilitiesWithTags")),
                    ActivationRequiredTags = YamlParser.AsStringList(t.Get("ActivationRequiredTags")),
                    ActivationBlockedTags = YamlParser.AsStringList(t.Get("ActivationBlockedTags")),
                    ActivationOwnedTags = YamlParser.AsStringList(t.Get("ActivationOwnedTags"))
                };
            }

            if (m.Get("Tasks") is YamlSequence tasks)
            {
                foreach (var item in tasks.Items)
                {
                    if (item is YamlMapping tm)
                    {
                        var task = new AbilityTaskDefinition
                        {
                            Type = YamlParser.AsString(tm.Get("Type")),
                            TickInterval = YamlParser.AsFloat(tm.Get("TickInterval")),
                            Priority = YamlParser.AsInt(tm.Get("Priority"))
                        };
                        if (tm.Get("Params") is YamlMapping pm)
                        {
                            task.Params = FlattenParams(pm);
                        }
                        a.Tasks.Add(task);
                    }
                }
            }

            if (m.Get("Metadata") is YamlMapping meta)
            {
                a.Metadata = new GameplayAbilityMetadata
                {
                    DisplayName = YamlParser.AsString(meta.Get("DisplayName")),
                    Description = YamlParser.AsString(meta.Get("Description")),
                    Icon = YamlParser.AsString(meta.Get("Icon"))
                };
            }

            return a;
        }

        // Task params are schema-free; keep scalars as strings and nested structures as YamlNodes.
        private static Dictionary<string, object> FlattenParams(YamlMapping pm)
        {
            var result = new Dictionary<string, object>();
            foreach (var key in pm.Keys)
            {
                var v = pm.Children[key];
                result[key] = v is YamlScalar sc ? (object)sc.Raw : v;
            }
            return result;
        }

        public static GameplayControllerDefinition MapController(YamlMapping m)
        {
            var gc = new GameplayControllerDefinition
            {
                OwnedTags = YamlParser.AsStringList(m.Get("OwnedTags")),
                ActiveActionSets = YamlParser.AsStringList(m.Get("ActiveActionSets")),
                ReplicationMode = SchemaEnums.ParseGCReplicationMode(YamlParser.AsString(m.Get("ReplicationMode"))),
                IsActive = YamlParser.AsBool(m.Get("bIsActive"), true)
            };

            gc.OwnerActor = MapActor(m.Get("OwnerActor") as YamlMapping);
            gc.AvatarActor = MapActor(m.Get("AvatarActor") as YamlMapping);

            if (m.Get("AttributeSets") is YamlSequence sets)
            {
                foreach (var item in sets.Items)
                {
                    if (item is YamlMapping sm)
                    {
                        var set = new SerializedAttributeSet { Name = YamlParser.AsString(sm.Get("Name")) };
                        if (sm.Get("Attributes") is YamlSequence attrs)
                        {
                            foreach (var a in attrs.Items)
                            {
                                if (a is YamlMapping am)
                                {
                                    set.Attributes.Add(new SerializedAttribute
                                    {
                                        Name = YamlParser.AsString(am.Get("Name")),
                                        BaseValue = YamlParser.AsFloat(am.Get("BaseValue")),
                                        CurrentValue = YamlParser.AsFloat(am.Get("CurrentValue"))
                                    });
                                }
                            }
                        }
                        gc.AttributeSets.Add(set);
                    }
                }
            }

            if (m.Get("GrantedAbilities") is YamlSequence grants)
            {
                foreach (var item in grants.Items)
                {
                    if (item is YamlMapping gm)
                    {
                        gc.GrantedAbilities.Add(new GrantedAbilityRecord
                        {
                            AbilityClass = YamlParser.AsString(gm.Get("AbilityClass")),
                            Level = YamlParser.AsInt(gm.Get("Level"), 1),
                            InputID = YamlParser.AsString(gm.Get("InputID")),
                            Handle = YamlParser.AsString(gm.Get("Handle")),
                            IsActive = YamlParser.AsBool(gm.Get("bIsActive"))
                        });
                    }
                }
            }

            if (m.Get("ActiveEffects") is YamlSequence effects)
            {
                foreach (var item in effects.Items)
                {
                    if (item is YamlMapping em)
                    {
                        gc.ActiveEffects.Add(MapActiveEffect(em));
                    }
                }
            }

            if (m.Get("Metadata") is YamlMapping meta)
            {
                gc.Metadata = new GameplayControllerMetadata
                {
                    DisplayName = YamlParser.AsString(meta.Get("DisplayName")),
                    Description = YamlParser.AsString(meta.Get("Description")),
                    Tags = YamlParser.AsStringList(meta.Get("Tags")),
                    DebugCategory = YamlParser.AsString(meta.Get("DebugCategory"))
                };
            }

            return gc;
        }

        private static ActorReference MapActor(YamlMapping m)
        {
            if (m == null) return null;
            return new ActorReference
            {
                ActorID = YamlParser.AsString(m.Get("ActorID")),
                ActorType = YamlParser.AsString(m.Get("ActorType"))
            };
        }

        private static ActiveEffectRecord MapActiveEffect(YamlMapping em)
        {
            var rec = new ActiveEffectRecord
            {
                Handle = YamlParser.AsString(em.Get("Handle")),
                EffectClass = YamlParser.AsString(em.Get("EffectClass")),
                Stacks = YamlParser.AsInt(em.Get("Stacks"), 1),
                StartTime = YamlParser.AsFloat(em.Get("StartTime")),
                Level = YamlParser.AsInt(em.Get("Level"), 1),
                InstigatorGC = YamlParser.AsString(em.Get("InstigatorGC")),
                SourceAbility = YamlParser.AsString(em.Get("SourceAbility"))
            };

            var policy = YamlParser.AsString(em.Get("DurationPolicy"));
            if (policy != null) rec.DurationPolicy = SchemaEnums.ParseDurationPolicy(policy);

            // Prefer explicit RemainingDuration; otherwise accept the `Duration: -1` shorthand
            // some genre packs use to denote an Infinite active effect.
            if (em.Has("RemainingDuration"))
            {
                rec.RemainingDuration = YamlParser.AsFloat(em.Get("RemainingDuration"));
            }
            else if (em.Has("Duration"))
            {
                float d = YamlParser.AsFloat(em.Get("Duration"), -1f);
                if (d < 0)
                {
                    rec.DurationPolicy ??= Schema.DurationPolicy.Infinite;
                }
                else
                {
                    rec.RemainingDuration = d;
                    rec.DurationPolicy ??= Schema.DurationPolicy.HasDuration;
                }
            }

            if (em.Get("PeriodicState") is YamlMapping ps)
            {
                rec.PeriodicState = new PeriodicStateRecord
                {
                    PeriodElapsed = YamlParser.AsFloat(ps.Get("PeriodElapsed")),
                    ExecutionCount = YamlParser.AsInt(ps.Get("ExecutionCount"))
                };
            }

            ReadFloatMap(em.Get("CapturedAttributes") as YamlMapping, rec.CapturedAttributes);
            ReadFloatMap(em.Get("SetByCallerMagnitudes") as YamlMapping, rec.SetByCallerMagnitudes);
            return rec;
        }

        private static void ReadFloatMap(YamlMapping src, Dictionary<string, float> dest)
        {
            if (src == null) return;
            foreach (var key in src.Keys)
            {
                dest[key] = YamlParser.AsFloat(src.Children[key]);
            }
        }
    }
}
