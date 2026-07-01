using Jbltx.Ugas.Runtime;

namespace Jbltx.Ugas.Cues
{
    /// <summary>How a gameplay cue was triggered (SPEC §12.3/§12.4).</summary>
    public enum CueNotifyType
    {
        /// <summary>Burst / fire-and-forget: an Instant effect executed or a periodic tick fired.</summary>
        Execute = 0,

        /// <summary>Looping: a durational effect was applied (start the looping VFX/SFX).</summary>
        Add = 1,

        /// <summary>Looping ended: the durational effect was removed (stop the loop).</summary>
        Remove = 2,
    }

    /// <summary>
    /// A gameplay cue notification (SPEC §12): a <c>GameplayCue.*</c> tag on an effect, communicated to
    /// presentation when the effect executes / is added / is removed. Carries only mechanical context
    /// (tag, kind, target, effect handle) — never visual/audio resources — so a headless server can
    /// raise cues without loading any presentation assets (§12.5).
    /// </summary>
    public readonly struct GameplayCueEvent
    {
        public readonly string CueTag;
        public readonly CueNotifyType Type;

        /// <summary>The controller the cue plays on (the effect's owner).</summary>
        public readonly UgasController Target;

        /// <summary>The active-effect handle for Add/Remove looping cues; -1 for Execute bursts.</summary>
        public readonly int EffectHandle;

        public GameplayCueEvent(string cueTag, CueNotifyType type, UgasController target, int effectHandle)
        {
            CueTag = cueTag;
            Type = type;
            Target = target;
            EffectHandle = effectHandle;
        }
    }
}
