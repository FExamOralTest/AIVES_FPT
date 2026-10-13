using Npgsql;

namespace Aives.Infrastructure.Persistence;

/// <summary>
/// Translates C# enum member names to uppercase PostgreSQL enum labels.
/// e.g.  UserRole.TEACHER  →  "TEACHER"  (not "teacher" which is Npgsql's default)
/// </summary>
public sealed class NpgsqlUpperCaseTranslator : INpgsqlNameTranslator
{
    /// <inheritdoc/>
    public string TranslateMemberName(string clrName) => clrName.ToUpperInvariant();

    /// <inheritdoc/>
    public string TranslateTypeName(string clrName) => clrName.ToUpperInvariant();
}
