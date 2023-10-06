using System.Text;
using NJsonSchema;
using NJsonSchema.CodeGeneration.CSharp;

namespace Leap.Cli.Config;

public static class CodeGenerator
{
    public static async Task Generate(string file, string outputType)
    {
        var schema = await JsonSchema.FromFileAsync(file);
        var generator = new CSharpGenerator(schema);
        generator.Settings.Namespace = typeof(CodeGenerator).Namespace;

        var fileContent = generator.GenerateFile(outputType);
        var sb = new StringBuilder(fileContent.Length);
        sb.Append(fileContent);
        await using var fileStream = File.CreateText(@$"../../../Config/{outputType}Spec.gen.cs");
        await fileStream.WriteAsync(sb);
    }
}