using System.Reflection;
using Serenity.Data;

namespace Idevs.Repositories;

/// <summary>
/// Base for services that need raw SQL access without being a typed-row repository.
/// Provides connection management, dialect lookup, SQL builder factories, and a
/// uniform <see cref="ExecuteAsync{T}"/> template.
/// </summary>
/// <remarks>
/// Does not inject ILogger, ITextLocalizer, or IServiceProvider — consumers inject
/// what they need into their own derived primary constructors.
/// </remarks>
public abstract class SqlServiceBase
{
    private readonly string _connectionKeyFromAttribute;

    protected ISqlConnections SqlConnections { get; }

    /// <summary>
    /// Connection key used when opening connections internally. Override per repo,
    /// or annotate the derived class with [ConnectionKey("MyDb")] for the same effect.
    /// Override of this property wins over the attribute.
    /// </summary>
    protected virtual string ConnectionKey => _connectionKeyFromAttribute;

    private readonly Lazy<ISqlDialect> _dialect;

    /// <summary>
    /// Cached SQL dialect for this base's <see cref="ConnectionKey"/>. Resolved on
    /// first access from the connection's metadata; cached for the lifetime of this
    /// instance. Resolution is thread-safe (single execution, published once).
    /// </summary>
    protected ISqlDialect Dialect => _dialect.Value;

    protected SqlServiceBase(ISqlConnections sqlConnections)
    {
        SqlConnections = sqlConnections ?? throw new ArgumentNullException(nameof(sqlConnections));

        var attr = GetType().GetCustomAttribute<ConnectionKeyAttribute>(inherit: true);
        _connectionKeyFromAttribute = attr?.Key ?? "Default";

        _dialect = new Lazy<ISqlDialect>(() =>
        {
            using var connection = SqlConnections.NewByKey(ConnectionKey);
            return connection.GetDialect();
        });
    }
}
