

using System.Diagnostics;
using System.Text;

namespace KavaTest;


class JavaProgram
{
    protected string dir, classname;
    protected JavaProgram(string dir, string classname)
    {
        this.dir = dir;
        this.classname = classname;
    }

    public async Task<string> Main(string[] args)
    {
        ProcessStartInfo javaStart = new("java", [classname, .. args])
        {
            WorkingDirectory = dir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
        };

        StringBuilder stdout = new();
        using Process java = new() { StartInfo = javaStart };

        java.OutputDataReceived += (sender, e) =>
        {
            stdout.Append(e.Data);
            stdout.Append('\n');
        };


        java.Start();
        java.BeginOutputReadLine();
        await java.WaitForExitAsync();
        return stdout.ToString();
    }

    public async Task<string> MainS(string[] args)
    {
        ProcessStartInfo javaStart = new("java", [classname, .. args])
        {
            WorkingDirectory = dir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
        };

        using Process java = new Process { StartInfo = javaStart };

        java.Start();

        return await java.StandardOutput.ReadToEndAsync();
    }

    public static async Task<JavaProgram> Compile(FileInfo f, DirectoryInfo d)
    {
        ProcessStartInfo javacStart = new("javac", ["-d", d.FullName, f.FullName])
        {
            WorkingDirectory = d.FullName,
            UseShellExecute = false,
        };

        using Process javac = new() { StartInfo = javacStart };
        javac.Start();

        await javac.WaitForExitAsync();
        if (javac.ExitCode != 0) throw new Exception("javac failed");

        var expectedClassFile = Path.Join(d.FullName, f.Name.Replace(".java", ".class"));
        if (!File.Exists(expectedClassFile)) throw new Exception("Can't find the class file");

        return new JavaProgram(d.FullName, Path.GetFileNameWithoutExtension(f.Name));
    }
}