using System;

namespace Jbltx.Ugas
{
    /// <summary>Base type for exceptions raised by the UGAS runtime.</summary>
    public class UgasException : Exception
    {
        public UgasException(string message) : base(message) { }
    }

    /// <summary>Thrown when an attribute set's declared dependencies are not satisfied (SPEC §6).</summary>
    public sealed class UgasDependencyException : UgasException
    {
        public UgasDependencyException(string message) : base(message) { }
    }
}
