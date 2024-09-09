using Leap.Cli.Model;

namespace Leap.Cli.Dependencies;

internal sealed class SqlServerDependencyYamlHandler : IDependencyYamlHandler<SqlServerDependencyYaml>
{
    public SqlServerDependencyYaml Merge(SqlServerDependencyYaml leftYaml, SqlServerDependencyYaml rightYaml)
    {
        return leftYaml;
    }

    public Dependency ToDependencyModel(SqlServerDependencyYaml yaml)
    {
        return new SqlServerDependency();
    }
}