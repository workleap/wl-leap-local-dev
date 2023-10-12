namespace Leap.Cli.Configuration.Yaml;

internal class DynamicObjectYaml : Dictionary<string, object?>
{
    public DynamicObjectYaml()
        : base(StringComparer.Ordinal)
    {
    }

    public string? GetScalar(string key)
    {
        return this.TryGetValue(key, out var obj) && obj is string str ? str : null;
    }

    public IEnumerable<object?>? GetSequence(string key)
    {
        return this.TryGetValue(key, out var obj) && obj is IEnumerable<object?> items ? items : null;
    }

    public IEnumerable<KeyValuePair<object, object?>>? GetMapping(string key)
    {
        return this.TryGetValue(key, out var obj) && obj is IEnumerable<KeyValuePair<object, object?>> items ? items : null;
    }
}