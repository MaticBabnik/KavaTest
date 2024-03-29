using System.Text;
using HtmlAgilityPack;

namespace KavaTest;

class Lambda
{

    public record TestSpec
    {
        public required string Name;
        public required string Path;
    }

    protected HttpClient remote;

    public Lambda(string baseUrl = "http://lambda.fri.uni-lj.si")
    {
        remote = new HttpClient()
        {
            BaseAddress = new Uri(baseUrl)
        };
    }

    public async Task<TestSpec[]> ListSpecs()
    {
        var docStream = await remote.GetStreamAsync("/cgi-bin/index.pl");
        var document = new HtmlDocument();
        document.Load(docStream);


        var selectElement = document.GetElementbyId("dir")
        ?? throw new Exception("Missing select element");

        return selectElement
            .ChildNodes
            .Where(x => x.Name == "option")
            .Select(x => new TestSpec()
            {
                Name = x.InnerText,
                Path = x.GetAttributeValue<string>("value", "")
            })
            .Where(x => x.Name != "Izberi nalogo..." && x.Path != "/tmp")
            .ToArray();
    }


    protected static string buildDummyJavaFile(string mainClass)
    {
        return @$"public class {mainClass} {{
                    public static void main(String[] args) {{
                        System.out.println(""KavaTest dumping {mainClass}"");
                    }}
                }}";
    }

    protected async Task<string> GetSpecDir(string name)
    {
        var specs = await ListSpecs();
        return specs.First(x => x.Name == name)?.Path
        ?? throw new Exception("Spec does not exist on the remote server.");
    }

    public async Task<string> TestJava(string dir, string sourceCode)
    {

        var content = new MultipartFormDataContent();

        var dirContent = new StringContent(dir);
        dirContent.Headers.Add("Content-Disposition", "form-data; name=\"dir\"");
        content.Add(dirContent, "dir");

        var javafileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(sourceCode));
        javafileContent.Headers.Add("Content-Type", "text/x-java");
        javafileContent.Headers.Add("Content-Disposition", $"form-data; name=\"javafile\"; filename=\"{dir}.java\"");
        content.Add(javafileContent, "javafile", $"{dir}.java");

        var res = await remote.PostAsync("/cgi-bin/modules/testJavaDN.pl", content);
        var text = await res.Content.ReadAsStringAsync();

        return text;
    }

    public async Task<string> TestJavaBySpecName(string name, string? source = null)
    {
        var dir = await GetSpecDir(name);
        return await TestJava(dir, source ?? buildDummyJavaFile(name));
    }
}