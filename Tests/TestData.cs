using System.IO;
using System.Runtime.CompilerServices;

namespace Jbltx.Ugas.Tests
{
    /// <summary>
    /// Resolves paths to the embedded conformance fixtures under <c>Tests/ConformanceData/</c>.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="CallerFilePathAttribute"/> to locate this source file at compile time, then
    /// resolves sibling data files relative to it. This works identically under the Unity Test
    /// Framework and under the headless <c>dotnet test</c> CI build, without depending on the
    /// external spec repo being checked out.
    /// </remarks>
    internal static class TestData
    {
        public static string Dir => System.IO.Path.Combine(SourceDir(), "ConformanceData");

        public static string FilePath(string fileName) => System.IO.Path.Combine(Dir, fileName);

        public static string Read(string fileName) => File.ReadAllText(FilePath(fileName));

        private static string SourceDir([CallerFilePath] string thisFile = "") =>
            System.IO.Path.GetDirectoryName(thisFile);
    }
}
