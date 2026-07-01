using System;
using System.Collections.Generic;
using Jbltx.Ugas.Runtime;

namespace Jbltx.Ugas.Cues
{
    /// <summary>
    /// The client-side cue dispatcher (SPEC §12.4): matches <c>GameplayCue.*</c> tags to registered
    /// handlers and routes Execute / Add / Remove notifications to them. Attach it to a controller's
    /// <see cref="UgasController.OnGameplayCue"/> stream; a headless server (§12.5) simply never
    /// creates one, so no presentation assets are touched while the cue tags still flow for replication.
    /// </summary>
    /// <remarks>
    /// This reference manager owns only the tag→handler mapping and dispatch. Concrete cue behaviours
    /// (spawning particles, playing sound, managing looping components keyed by effect handle) live in
    /// the handlers the presentation layer registers — the runtime never references a VFX/SFX type.
    /// </remarks>
    public sealed class UgasCueManager
    {
        private readonly Dictionary<string, Action<GameplayCueEvent>> _handlers = new Dictionary<string, Action<GameplayCueEvent>>();

        /// <summary>Registers the handler invoked for a cue tag (§12.4 CueRegistry).</summary>
        public void Register(string cueTag, Action<GameplayCueEvent> handler)
        {
            if (!string.IsNullOrEmpty(cueTag) && handler != null) _handlers[cueTag] = handler;
        }

        /// <summary>Removes a cue tag's handler.</summary>
        public void Unregister(string cueTag)
        {
            if (cueTag != null) _handlers.Remove(cueTag);
        }

        /// <summary>Dispatches a notification to the tag's handler, if one is registered (§12.4 HandleCueNotify).</summary>
        public void Handle(GameplayCueEvent evt)
        {
            if (_handlers.TryGetValue(evt.CueTag, out var handler)) handler(evt);
        }

        /// <summary>Subscribes this manager to a controller's cue stream.</summary>
        public void Attach(UgasController controller)
        {
            if (controller != null) controller.OnGameplayCue += Handle;
        }

        /// <summary>Unsubscribes from a controller's cue stream.</summary>
        public void Detach(UgasController controller)
        {
            if (controller != null) controller.OnGameplayCue -= Handle;
        }
    }
}
