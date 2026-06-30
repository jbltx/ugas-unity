namespace Jbltx.Ugas.Editor
{
    /// <summary>The UGAS entity kind a spec YAML file describes, detected from its root keys.</summary>
    public enum SpecEntityKind
    {
        Unknown,
        AttributeSet,
        GameplayEffect,
        GameplayAbility,
        GameplayTagRegistry,
        GameplayController
    }
}
