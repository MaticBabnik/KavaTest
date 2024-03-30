using HtmlAgilityPack;
using Fizzler.Systems.HtmlAgilityPack;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KavaTest;

record KFile
{
    [JsonInclude]
    public required string filename;

    [JsonInclude]
    public required string content;

    public static KFile FromHtml(HtmlNode header, HtmlNode pre1) => new()
    {
        filename = header.InnerText.Trim(),
        content = pre1.InnerHtml
    };
}

partial record Test
{
    [JsonInclude]
    public required int id;

    [JsonInclude]
    public required string[] args;

    [JsonInclude]
    public required string expectedStdout;

    [GeneratedRegex("\\d+")]
    private static partial Regex TestNumberPattern();
    public static Test FromHtml(HtmlNode header, HtmlNode div1)
    {
        int id = int.Parse(TestNumberPattern().Match(header.InnerText).Value);
        // index 0 is args, 1 is expected out
        var preContents = div1.QuerySelectorAll("pre").Take(2).Select(x => x.InnerHtml).ToArray();
        return new Test()
        {
            id = id,
            args = preContents[0].Trim().Split(' '),
            expectedStdout = preContents[1].EndsWith('\n') ? preContents[1] : preContents[1] + "\n"
        };
    }
}


[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(KavaSpec))]
[JsonSerializable(typeof(KFile))]
[JsonSerializable(typeof(Test))]
internal partial class SourceGenerationContext : JsonSerializerContext
{
}

partial class KavaSpec
{
    static readonly JsonSerializerOptions serializeOptions = new()
    {
        TypeInfoResolver = SourceGenerationContext.Default
    };

    public string Name
    {
        get;
        internal set;
    }

    [JsonInclude]
    public KFile[] Files
    {
        get;
        internal set;
    }

    [JsonInclude]
    public Test[] Tests
    {
        get;
        internal set;
    }

    [JsonConstructor]
    internal KavaSpec()
    {
        Name = "";
        Tests = [];
        Files = [];
    }

    protected KavaSpec(string name, KFile[] f, Test[] t)
    {
        Name = name;
        Files = f;
        Tests = t;
    }

    protected static IEnumerable<Test> ParseTests(HtmlNode testRow)
    {
        var testNodes = testRow.QuerySelector("#rezultat").ChildNodes.Where(x => x.Name == "h4" || x.Name == "div").ToArray();

        for (int i = 0; i < testNodes.Length; i += 3)
        {
            yield return Test.FromHtml(testNodes[i], testNodes[i + 1]);
        }
    }

    protected static IEnumerable<KFile> ParseFiles(HtmlNode filesRow)
    {
        var testNodes = filesRow.QuerySelector("#rezultat").ChildNodes.Where(x => x.Name == "h4" || x.Name == "pre").ToArray();

        for (int i = 0; i < testNodes.Length; i += 3)
        {
            yield return KFile.FromHtml(testNodes[i], testNodes[i + 1]);
        }
    }

    public static KavaSpec ParseFromHtmlDump(string name, string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var rows = doc.DocumentNode.QuerySelectorAll(".container>div.row").ToArray();
        if (rows.Length != 5) throw new Exception("Unexpected document layout");

        return new KavaSpec(name, [.. ParseFiles(rows[4])], [.. ParseTests(rows[3])]);
    }

    public override string ToString()
    {
        return "KavaSpec\n\tTests:\n" +
            string.Join('\n', Tests.Select(x => $"\t\tTest {x.id} [ {string.Join(' ', x.args)} ]")) +
            "\n\tFiles:\n" + string.Join('\n', Files.Select(x => $"\t\tFile {x.filename}"));
    }

    /// <summary>
    /// This blocks. womp womp
    /// </summary>
    public void WriteToDirectory(DirectoryInfo d)
    {
        // write spec file
        var specFilePath = Path.Join(d.FullName, $"{Name}.kavaspec.json");
        var specString = JsonSerializer.Serialize(this, typeof(KavaSpec), serializeOptions);
        File.WriteAllText(specFilePath, specString);

        // write other files
        var viriPath = Path.Join(d.FullName, "viri");
        if (!Directory.Exists(viriPath))
        {
            Directory.CreateDirectory(viriPath);
        }

        foreach (var file in Files)
        {
            var filePath = Path.Join(viriPath, file.filename);
            File.WriteAllText(filePath, file.content);
        }
    }


    [GeneratedRegex("^(\\d{1,3})(?:-(\\d{1,3}))?$")]
    private static partial Regex TestSublistPattern();

    public Test[] GetTestSubset(string testList)
    {
        HashSet<int> which = new();

        foreach (var testSubList in testList.Split(','))
        {
            var match = TestSublistPattern().Match(testSubList)
                ?? throw new Exception($"'{testSubList}' is not a test number or a test range.");

            var testNumbers = match.Groups.Values.Skip(1)
                .Select(x => uint.TryParse(x.ValueSpan, out uint val) ? (int)val : -1).ToArray();

            if (testNumbers.Length == 1)
                which.Add(testNumbers[0]);
            else
                for (int i = testNumbers[0]; i <= testNumbers[1]; i++)
                    which.Add(i);

        }

        return Tests.Where(x => which.Contains(x.id)).ToArray();
    }

    public static KavaSpec Restore(DirectoryInfo d, string specName)
    {
        var specFilePath = Path.Join(d.FullName, $"{specName}.kavaspec.json");
        return JsonSerializer.Deserialize(File.ReadAllText(specFilePath), typeof(KavaSpec), serializeOptions) as KavaSpec ?? throw new Exception("Failed to restore");
    }

}