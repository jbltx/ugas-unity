using System.IO;
using System.Runtime.CompilerServices;

namespace Jbltx.Ugas.Tests.Editor
{
    /// <summary>
    /// Loads the embedded spec-entity fixtures under <c>Tests/Editor/SpecData/</c> (real UGAS genre
    /// entities, stored as <c>*.yaml.txt</c>). Path resolution uses <see cref="CallerFilePathAttribute"/>
    /// so it works in the editor test runner without depending on the external spec repo.
    /// </summary>
    internal static class SpecData
    {
        public static string Read(string fileName) => File.ReadAllText(Path.Combine(Dir(), "SpecData", fileName));

        private static string Dir([CallerFilePath] string thisFile = "") => Path.GetDirectoryName(thisFile);
    }
}
