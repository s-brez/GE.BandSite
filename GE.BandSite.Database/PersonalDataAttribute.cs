namespace GE.BandSite.Database;

/// <summary>
/// Marks a property as containing personal data (PII) for compliance workflows.
/// <para>
/// Properties annotated with this attribute are discoverable by data protection
/// routines (e.g., export and erasure) via reflection-driven catalogs.
/// </para>
/// </summary>
public sealed class PersonalDataAttribute : Attribute
{
    /// <summary>
    /// Indicates that the personal data is sensitive (e.g., tokens, secrets) and
    /// may require stricter handling during exports and redactions.
    /// </summary>
    public bool Sensitive { get; init; } = false;
}

