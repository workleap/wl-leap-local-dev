namespace Leap.Cli.Model;

internal sealed class IngressHost(string value)
{
    public static readonly IngressHost Localhost = new IngressHost("127.0.0.1");

    private readonly string _value = value;

    public bool IsLocalhost { get; } = "localhost".Equals(value, StringComparison.OrdinalIgnoreCase) || "127.0.0.1" == value;

    public static implicit operator IngressHost(string value)
    {
        return new IngressHost(value);
    }

    public static implicit operator string(IngressHost host)
    {
        return host.ToString();
    }

    public override string ToString()
    {
        return this._value;
    }

    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj) || obj switch
        {
            IngressHost other => string.Equals(this._value, other._value, StringComparison.OrdinalIgnoreCase),
            string value => string.Equals(this._value, value, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    public override int GetHashCode()
    {
        return this._value.GetHashCode();
    }
}