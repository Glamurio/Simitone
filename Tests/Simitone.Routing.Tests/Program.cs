using System;
using System.Linq;

namespace Simitone.Routing.Tests
{
    /// <summary>
    /// Dependency-free test runner (no NuGet test packages): exits with the number of failed tests.
    /// Run with: dotnet run --project Tests/Simitone.Routing.Tests -c Release
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            int failed = 0, total = 0;
            var tests = ShimmyPlannerTests.All()
                .Concat(DiagnosticsTests.All())
                .Concat(SelectionTests.All())
                .Concat(AutonomyTests.All());
            foreach (var (name, test) in tests)
            {
                total++;
                try
                {
                    test();
                    Console.WriteLine("PASS  " + name);
                }
                catch (Exception e)
                {
                    failed++;
                    Console.WriteLine("FAIL  " + name + ": " + e.Message);
                    //GitHub Actions annotation, readable without signing in
                    if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true")
                        Console.WriteLine("::error title=Routing test failed::" + name + ": " + e.Message.Replace("\n", " "));
                }
            }
            Console.WriteLine($"{total - failed}/{total} passed");
            return failed;
        }
    }
}
