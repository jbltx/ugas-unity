using System.Collections.Generic;
using System.Globalization;
using Jbltx.Ugas.Definitions;
using Jbltx.Ugas.Editor.Yaml;
using Jbltx.Ugas.Kernel;
using UnityEngine;

namespace Jbltx.Ugas.Editor
{
    /// <summary>
    /// Editor-only conversion of parsed UGAS spec YAML into the runtime ScriptableObject definitions.
    /// This is the re-homed deserialization logic (formerly a runtime layer): the runtime never parses
    /// YAML; it consumes the <c>.asset</c> definitions this mapper produces at import time.
    /// </summary>
    /// <remarks>Public so editor tooling and tests can convert spec text to SOs without the AssetDatabase.</remarks>
    public static class SpecEntityMapper
    {
        /// <summary>Detects the entity kind from the parsed root mapping's keys.</summary>
        public static SpecEntityKind Detect(YamlMapping root)
        {
            if (root == null) return SpecEntityKind.Unknown;
            if (root.Has("DurationPolicy")) return SpecEntityKind.GameplayEffect;
            if (root.Has("OwnerActor") || root.Has("AttributeSets")) return SpecEntityKind.GameplayController;
            if (root.Has("Attributes")) return SpecEntityKind.AttributeSet;
            if (root.Has("Tags") && root.Get("Tags") is YamlSequence) return SpecEntityKind.GameplayTagRegistry;
            if (root.Has("Name") && (root.Has("Cost") || root.Has("Cooldown") || root.Has("Tasks") ||
                                     root.Get("Tags") is YamlMapping))
                return SpecEntityKind.GameplayAbility;
            return SpecEntityKind.Unknown;
        }

        // ---- Attribute set ----

        public static void PopulateAttributeSet(AttributeSetDefinition so, YamlMapping m)
        {
            var attrs = new List<AttributeDefinition>();
            if (m.Get("Attributes") is YamlSequence seq)
            {
                foreach (var item in seq.Items)
                    if (item is YamlMapping am) attrs.Add(MapAttribute(am));
            }

            string display = null, desc = null;
            if (m.Get("Metadata") is YamlMapping meta)
            {
                display = Str(meta.Get("DisplayName"));
                desc = Str(meta.Get("Description"));
            }

            so.Populate(Str(m.Get("Name")), StrList(m.Get("Dependencies")), attrs, display, desc);
        }

        private static AttributeDefinition MapAttribute(YamlMapping m)
        {
            var a = new AttributeDefinition
            {
                Name = Str(m.Get("Name")),
                DefaultBaseValue = F(m.Get("DefaultBaseValue")),
                Category = ParseCategory(Str(m.Get("Category"))),
                Replication = ParseReplication(Str(m.Get("ReplicationMode"))),
                Min = AttributeBound.Off,
                Max = AttributeBound.Off
            };

            if (m.Get("Clamping") is YamlMapping clamp)
            {
                a.Min = MapBound(clamp.Get("Min"));
                a.Max = MapBound(clamp.Get("Max"));
            }

            if (m.Get("Metadata") is YamlMapping meta)
            {
                a.DisplayName = Str(meta.Get("DisplayName"));
                a.Description = Str(meta.Get("Description"));
                a.UICategory = Str(meta.Get("UICategory"));
            }
            return a;
        }

        private static AttributeBound MapBound(YamlNode node)
        {
            if (!(node is YamlScalar s)) return AttributeBound.Off;
            if (!s.WasQuoted && float.TryParse(s.Raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
                return AttributeBound.FromLiteral(v);
            var str = Str(s);
            return str == null ? AttributeBound.Off : AttributeBound.FromReference(str);
        }

        // ---- Gameplay effect ----

        public static void PopulateEffect(GameplayEffectDefinition so, YamlMapping m)
        {
            var duration = m.Get("Duration") is YamlMapping dm ? MapMagnitude(dm) : default;

            var period = default(PeriodDefinition);
            if (m.Get("Period") is YamlMapping pm)
            {
                period.Period = F(pm.Get("Period"));
                period.ExecuteOnApplication = B(pm.Get("ExecuteOnApplication"));
            }

            var mods = new List<ModifierDefinition>();
            if (m.Get("Modifiers") is YamlSequence ms)
            {
                foreach (var item in ms.Items)
                {
                    if (item is YamlMapping mm)
                    {
                        mods.Add(new ModifierDefinition
                        {
                            Attribute = Str(mm.Get("Attribute")),
                            Operation = ParseOp(Str(mm.Get("Operation"))),
                            Magnitude = mm.Get("Magnitude") is YamlMapping gm ? MapMagnitude(gm) : default,
                            Channel = Str(mm.Get("Channel"))
                        });
                    }
                }
            }

            so.Populate(
                Str(m.Get("Name")),
                ParseDuration(Str(m.Get("DurationPolicy"))),
                duration, period,
                ParseExecution(Str(m.Get("ExecutionPolicy"))),
                I(m.Get("Priority")),
                mods,
                StrList(m.Get("GrantedTags")),
                StrList(m.Get("ApplicationRequiredTags")),
                StrList(m.Get("GameplayCues")));

            // §9.6 custom execution: the schema's `Executions` is a list of { CalculatorClass }. The
            // reference effect runs a single ExecCalc, so map the first CalculatorClass. Previously dropped,
            // which left HasExecution=false — imported execution-driven combat effects silently did nothing.
            if (m.Get("Executions") is YamlSequence execs)
            {
                foreach (var item in execs.Items)
                {
                    if (item is YamlMapping em)
                    {
                        var cls = Str(em.Get("CalculatorClass"));
                        if (!string.IsNullOrEmpty(cls)) { so.SetExecutionClass(cls); break; }
                    }
                }
            }
        }

        private static MagnitudeDefinition MapMagnitude(YamlMapping m)
        {
            return new MagnitudeDefinition
            {
                Type = ParseMagnitudeType(Str(m.Get("Type"))),
                Value = F(m.Get("Value")),
                BackingAttribute = Str(m.Get("BackingAttribute")),
                Source = ParseSource(Str(m.Get("Source"))),
                Coefficient = F(m.Get("Coefficient"), 1f),
                PreMultiplyAdditive = F(m.Get("PreMultiplyAdditive")),
                PostMultiplyAdditive = F(m.Get("PostMultiplyAdditive")),
                CalculatorClass = Str(m.Get("CalculatorClass")),
                DataTag = Str(m.Get("DataTag"))
            };
        }

        // ---- Gameplay ability ----

        public static void PopulateAbility(GameplayAbilityDefinition so, YamlMapping m)
        {
            var tagSet = new AbilityTagSet
            {
                AbilityTags = new List<string>(),
                BlockedByTags = new List<string>(),
                BlockAbilitiesWithTags = new List<string>(),
                CancelAbilitiesWithTags = new List<string>(),
                ActivationRequiredTags = new List<string>(),
                ActivationBlockedTags = new List<string>(),
                ActivationOwnedTags = new List<string>()
            };
            if (m.Get("Tags") is YamlMapping t)
            {
                tagSet.AbilityTags = StrList(t.Get("AbilityTags"));
                tagSet.BlockedByTags = StrList(t.Get("BlockedByTags"));
                tagSet.BlockAbilitiesWithTags = StrList(t.Get("BlockAbilitiesWithTags"));
                tagSet.CancelAbilitiesWithTags = StrList(t.Get("CancelAbilitiesWithTags"));
                tagSet.ActivationRequiredTags = StrList(t.Get("ActivationRequiredTags"));
                tagSet.ActivationBlockedTags = StrList(t.Get("ActivationBlockedTags"));
                tagSet.ActivationOwnedTags = StrList(t.Get("ActivationOwnedTags"));
            }

            var tasks = new List<AbilityTaskDefinition>();
            if (m.Get("Tasks") is YamlSequence ts)
            {
                foreach (var item in ts.Items)
                {
                    if (item is YamlMapping tm)
                    {
                        var task = new AbilityTaskDefinition
                        {
                            Type = Str(tm.Get("Type")),
                            TickInterval = F(tm.Get("TickInterval")),
                            Priority = I(tm.Get("Priority")),
                            Params = new List<TaskParam>()
                        };
                        if (tm.Get("Params") is YamlMapping pm)
                        {
                            foreach (var key in pm.Keys)
                            {
                                var v = pm.Children[key];
                                task.Params.Add(new TaskParam { Key = key, Value = v is YamlScalar sc ? sc.Raw : v?.ToString() });
                            }
                        }
                        tasks.Add(task);
                    }
                }
            }

            string display = null, desc = null;
            if (m.Get("Metadata") is YamlMapping meta)
            {
                display = Str(meta.Get("DisplayName"));
                desc = Str(meta.Get("Description"));
            }

            so.Populate(Str(m.Get("Name")), tagSet, tasks, Str(m.Get("Cost")), Str(m.Get("Cooldown")), display, desc);
        }

        // ---- Gameplay tag registry ----

        public static void PopulateTagRegistry(GameplayTagRegistry so, YamlMapping m)
        {
            var entries = new List<GameplayTagRegistry.Entry>();
            if (m.Get("Tags") is YamlSequence seq)
            {
                foreach (var item in seq.Items)
                {
                    if (item is YamlMapping tm)
                    {
                        entries.Add(new GameplayTagRegistry.Entry
                        {
                            Tag = Str(tm.Get("Tag")),
                            Description = Str(tm.Get("Description")),
                            AllowMultiple = B(tm.Get("AllowMultiple"))
                        });
                    }
                }
            }
            so.SetEntries(entries);
        }

        // ---- Gameplay controller config ----

        public static void PopulateController(GameplayControllerConfig so, YamlMapping m)
        {
            var startingValues = new List<GameplayControllerConfig.StartingAttribute>();
            if (m.Get("AttributeSets") is YamlSequence sets)
            {
                foreach (var item in sets.Items)
                {
                    if (item is YamlMapping sm)
                    {
                        string setName = Str(sm.Get("Name"));
                        if (sm.Get("Attributes") is YamlSequence attrs)
                        {
                            foreach (var a in attrs.Items)
                            {
                                if (a is YamlMapping am)
                                {
                                    startingValues.Add(new GameplayControllerConfig.StartingAttribute
                                    {
                                        SetName = setName,
                                        AttributeName = Str(am.Get("Name")),
                                        BaseValue = F(am.Get("BaseValue"))
                                    });
                                }
                            }
                        }
                    }
                }
            }

            string display = null;
            if (m.Get("Metadata") is YamlMapping meta) display = Str(meta.Get("DisplayName"));

            so.Populate(StrList(m.Get("OwnedTags")), startingValues,
                ParseControllerReplication(Str(m.Get("ReplicationMode"))), display);
        }

        // ---- Scalar helpers ----

        private static string Str(YamlNode n) => YamlParser.AsString(n);
        private static float F(YamlNode n, float fb = 0f) => YamlParser.AsFloat(n, fb);
        private static int I(YamlNode n, int fb = 0) => YamlParser.AsInt(n, fb);
        private static bool B(YamlNode n, bool fb = false) => YamlParser.AsBool(n, fb);
        private static List<string> StrList(YamlNode n) => YamlParser.AsStringList(n);

        // ---- Enum parsers (verbatim spec strings) ----

        private static AttributeCategory ParseCategory(string s) => s switch
        {
            "Resource" => AttributeCategory.Resource,
            "Meta" => AttributeCategory.Meta,
            _ => AttributeCategory.Statistic
        };

        private static AttributeReplication ParseReplication(string s) => s switch
        {
            "None" => AttributeReplication.None,
            "OwnerOnly" => AttributeReplication.OwnerOnly,
            _ => AttributeReplication.All
        };

        private static DurationPolicy ParseDuration(string s) => s switch
        {
            "Instant" => DurationPolicy.Instant,
            "Infinite" => DurationPolicy.Infinite,
            _ => DurationPolicy.HasDuration
        };

        private static ExecutionPolicy ParseExecution(string s) => s switch
        {
            "RunInSequence" => ExecutionPolicy.RunInSequence,
            "RunInMerge" => ExecutionPolicy.RunInMerge,
            _ => ExecutionPolicy.RunInParallel
        };

        private static MagnitudeType ParseMagnitudeType(string s) => s switch
        {
            "AttributeBased" => MagnitudeType.AttributeBased,
            "CustomCalculation" => MagnitudeType.CustomCalculation,
            "SetByCaller" => MagnitudeType.SetByCaller,
            _ => MagnitudeType.ScalableFloat
        };

        private static MagnitudeSource ParseSource(string s) => s == "Target" ? MagnitudeSource.Target : MagnitudeSource.Source;

        private static ModifierOp ParseOp(string s) => s switch
        {
            "AddPost" => ModifierOp.AddPost,
            "Multiply" => ModifierOp.Multiply,
            "Override" => ModifierOp.Override,
            _ => ModifierOp.Add
        };

        private static ControllerReplication ParseControllerReplication(string s) => s switch
        {
            "Minimal" => ControllerReplication.Minimal,
            "Full" => ControllerReplication.Full,
            "None" => ControllerReplication.None,
            _ => ControllerReplication.Mixed
        };
    }
}
