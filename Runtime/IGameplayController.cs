using System.Collections.Generic;
using Jbltx.Ugas.Abilities;
using Jbltx.Ugas.Attributes;
using Jbltx.Ugas.Effects;
using Jbltx.Ugas.Schema;
using Jbltx.Ugas.Tags;

namespace Jbltx.Ugas
{
    /// <summary>
    /// The Gameplay Controller (SPEC §4): the authoritative container that wires the four pillars
    /// together. It owns the attribute sets, the owned-tag container, the granted abilities, and the
    /// active effects, and brokers activation/application requests between them.
    /// </summary>
    public interface IGameplayController
    {
        /// <summary>Logical owner (lifecycle, authority, persistence).</summary>
        ActorReference OwnerActor { get; }

        /// <summary>World spatial representation (may equal the owner).</summary>
        ActorReference AvatarActor { get; }

        /// <summary>The owned-tag container (current semantic state).</summary>
        GameplayTagContainer OwnedTags { get; }

        /// <summary>The effects system managing active effects on this GC.</summary>
        IGameplayEffectsSystem Effects { get; }

        /// <summary>Registered attribute sets, keyed by name.</summary>
        IReadOnlyDictionary<string, AttributeSet> AttributeSets { get; }

        /// <summary>Registers an attribute set (SPEC §6). Validates declared dependencies are present.</summary>
        void RegisterAttributeSet(AttributeSet set);

        /// <summary>Looks up an attribute across all registered sets.</summary>
        Attribute FindAttribute(string attributeName);

        /// <summary>Grants an ability to this controller (NotGranted → Granted).</summary>
        IGameplayAbility GrantAbility(GameplayAbilityDefinition ability, int level = 1, string inputId = null);

        /// <summary>Attempts to activate a previously granted ability by name.</summary>
        bool TryActivateAbility(string abilityName);

        /// <summary>Applies a gameplay effect to this controller.</summary>
        ActiveGameplayEffect ApplyEffect(GameplayEffectDefinition effect, int level = 1, string sourceAbility = null);

        /// <summary>Advances time-based systems (active effects, ability tasks) by one step.</summary>
        void Tick(float deltaSeconds);
    }
}
