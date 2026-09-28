using MongoDB.Bson.Serialization.Attributes;

namespace Odhikar.Domain.Models;

/// <summary>
/// A single extracted fact. <see cref="IsInferred"/> is true when the value was
/// derived (e.g. by arithmetic) or guessed by the LLM rather than stated
/// explicitly by the client — the paralegal UI surfaces this as an [Inferred] badge.
/// </summary>
[BsonIgnoreExtraElements]
public sealed class ExtractedField
{
    [BsonElement("value")]
    public string? Value { get; set; }

    [BsonElement("isInferred")]
    public bool IsInferred { get; set; }

    /// <summary>Optional short note explaining how an inferred value was derived.</summary>
    [BsonElement("inferenceNote")]
    [BsonIgnoreIfNull]
    public string? InferenceNote { get; set; }

    public static ExtractedField Stated(string? value) => new() { Value = value, IsInferred = false };

    public static ExtractedField Inferred(string? value, string? note = null) =>
        new() { Value = value, IsInferred = true, InferenceNote = note };
}
