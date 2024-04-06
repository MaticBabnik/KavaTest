using System.Text;
using System.CommandLine;
using System.Diagnostics;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;

namespace KavaTest;

class Program
{
    static async Task ShowInBrowser(string text, string name = "")
    {
        var fullName = $"{name}${DateTime.Now:yyyyMMdd_HHmmss}";
        var path = Path.Combine(Path.GetTempPath(), $"{fullName}.html");

        // Write the text content to a temporary HTML file
        byte[] byteArray = Encoding.UTF8.GetBytes(text);
        using (FileStream fs = new(path, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await fs.WriteAsync(byteArray);
        }

        // Open the temporary HTML file in the default browser
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }

    static async Task List()
    {
        Lambda s = new();

        Console.WriteLine("Specs on remote server:");
        foreach (Lambda.TestSpec spec in await s.ListSpecs())
        {
            Console.WriteLine($"\t{spec.Name} ({spec.Path})");
        }
    }

    static async Task Dump(string specName, DirectoryInfo rootDir)
    {
        Lambda s = new();

        var html = await s.TestJavaBySpecName(specName);

        var ks = KavaSpec.ParseFromHtmlDump(specName, html);
        Console.WriteLine(ks);
        ks.WriteToDirectory(rootDir);
    }

    static async Task TestOnline(FileInfo f)
    {
        if (!f.Exists) throw new Exception("File not found");
        if (!f.Name.EndsWith(".java")) throw new Exception("Supply a Java file");


        string specName = Path.GetFileNameWithoutExtension(f.Name),
            sourceCode = File.ReadAllText(f.FullName);

        Lambda s = new();
        var res = await s.TestJavaBySpecName(specName, source: sourceCode);

        await ShowInBrowser(res, $"TestResult-{specName}-");
    }

    static async Task Test(FileInfo f, string? testList, DirectoryInfo d, bool noDiff, bool failFatal)
    {
        if (!f.Exists) throw new Exception("File not found");
        if (!f.Name.EndsWith(".java")) throw new Exception("Supply a Java file");

        string specName = Path.GetFileNameWithoutExtension(f.Name);
        var spec = KavaSpec.Restore(d, specName);

        var program = await JavaProgram.Compile(f, d);

        var testsToRun = testList == null ? spec.Tests : spec.GetTestSubset(testList);

        Dictionary<int, string> testResults = new();

        await Task.WhenAll(
            testsToRun.Select(async test =>
            {
                var stdout = await program.MainS(test.args);
                lock (testResults)
                {
                    testResults.TryAdd(test.id, stdout);
                }
            }).ToArray());


        ConsoleColor ogForeground = Console.ForegroundColor,
                     ogBackground = Console.BackgroundColor;

        int n_passed = 0;
        foreach (var t in testsToRun)
        {
            var diff = InlineDiffBuilder.Diff(t.expectedStdout, testResults.GetValueOrDefault(t.id, ""));

            if (!diff.HasDifferences) n_passed++;

            Console.Write($"Test {t.id,3}: ");

            Console.ForegroundColor = diff.HasDifferences ? ConsoleColor.Red : ConsoleColor.Green;
            Console.WriteLine(diff.HasDifferences ? "FAIL" : "PASS");
            Console.ForegroundColor = ogForeground;

            if (diff.HasDifferences && !noDiff)
            {

                foreach (var line in diff.Lines)
                {
                    switch (line.Type)
                    {
                        case ChangeType.Inserted:
                            Console.ForegroundColor = ConsoleColor.White;
                            Console.BackgroundColor = ConsoleColor.Green;
                            Console.Write("+ ");
                            break;
                        case ChangeType.Deleted:
                            Console.ForegroundColor = ConsoleColor.White;
                            Console.BackgroundColor = ConsoleColor.Red;
                            Console.Write("- ");
                            break;
                        default:
                            Console.BackgroundColor = ConsoleColor.Gray;
                            Console.Write($"  ");
                            break;
                    }

                    Console.Write(line.Text);
                    Console.BackgroundColor = ogBackground;
                    Console.ForegroundColor = ogForeground;
                    Console.WriteLine();
                }

            }

            if (diff.HasDifferences && failFatal)
            {
                Console.WriteLine($"Exiting due to failure ({t.id})");
                return;
            }
        }

        Console.WriteLine($"{n_passed}/{testsToRun.Length}");
    }

    static async Task<int> Main(string[] args)
    {
        Option<DirectoryInfo> rootDir = new("--rootDir", () => new DirectoryInfo(Directory.GetCurrentDirectory()), "Where to store specs");
        rootDir.AddAlias("-d");
        Option<string?> tests = new("--tests", "Override which tests to run");
        tests.AddAlias("-t");

        Option<bool> noDiffs = new("--nodiff", "Never display diffs");
        noDiffs.AddAlias("-b");

        Option<bool> failFatal = new("--failfatal", "Exit on first test failure");
        failFatal.AddAlias("-f");

        Argument<FileInfo> sourceFile = new("sourceFile", "File to test");
        Argument<string> specName = new("specName", "Name of the spec");

        Command list = new("list", "Lists specs from the remote server");
        list.SetHandler(List);

        Command dump = new("dump", "Dumps a spec from the remote server");
        dump.AddArgument(specName);
        dump.SetHandler(Dump, specName, rootDir);

        Command online = new("online", "Tests a file on the remote server");
        online.AddArgument(sourceFile);
        online.SetHandler(TestOnline, sourceFile);

        Command test = new("test", "Tests a file locally");
        test.AddArgument(sourceFile);
        test.AddOption(noDiffs);
        test.AddOption(failFatal);
        test.AddOption(tests);
        test.SetHandler(Test, sourceFile, tests, rootDir, noDiffs, failFatal);

        RootCommand root = new();
        root.AddCommand(list);
        root.AddCommand(dump);
        root.AddCommand(online);
        root.AddCommand(test);
        root.AddGlobalOption(rootDir);

        return await root.InvokeAsync(args);
    }


}
