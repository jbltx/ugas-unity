#if UGAS_DOTS
using System.Collections.Generic;
using Jbltx.Ugas.Kernel;
using Unity.Entities;

namespace Jbltx.Ugas.Dots
{
    /// <summary>
    /// Materializes UGAS attribute state into ECS entities for the DOTS-accelerated backend — the
    /// "baking" seed. It converts a base value plus already-resolved <see cref="ModifierSample"/>s
    /// (the same values the managed runtime feeds the shared <see cref="AttributeKernel"/>) into an
    /// <see cref="AttributeValue"/> component and an <see cref="AttributeModifierElement"/> buffer, so
    /// <see cref="AttributeAggregationSystem"/> recomputes <see cref="AttributeValue.CurrentValue"/>
    /// identically to the managed path. Compiled only when <c>com.unity.entities</c> is installed.
    /// </summary>
    public static class UgasDotsConversion
    {
        /// <summary>
        /// Creates an attribute entity: an <see cref="AttributeValue"/> (Current seeded to Base) plus a
        /// modifier buffer populated from <paramref name="modifiers"/>. This performs a structural
        /// change, so it MUST run on the main thread (not inside a job).
        /// </summary>
        public static Entity CreateAttributeEntity(
            EntityManager entityManager,
            int attributeId,
            float baseValue,
            IReadOnlyList<ModifierSample> modifiers = null,
            bool hasMin = false, float min = 0f,
            bool hasMax = false, float max = 0f)
        {
            var entity = entityManager.CreateEntity();
            entityManager.AddComponentData(entity, new AttributeValue
            {
                AttributeId = attributeId,
                BaseValue = baseValue,
                CurrentValue = baseValue,
                HasMin = hasMin, Min = min,
                HasMax = hasMax, Max = max,
            });

            var buffer = entityManager.AddBuffer<AttributeModifierElement>(entity);
            if (modifiers != null)
            {
                for (int i = 0; i < modifiers.Count; i++)
                    buffer.Add(new AttributeModifierElement { Sample = modifiers[i] });
            }

            return entity;
        }
    }
}
#endif
