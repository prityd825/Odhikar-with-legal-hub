using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Odhikar.Domain.Models;

/// <summary>
/// Represents a formal legal application draft generated from conversation and case summary data.
/// Persisted in the <c>generated_applications</c> collection.
/// </summary>
[BsonIgnoreExtraElements]
public sealed class GeneratedApplicationModel
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; } = Guid.NewGuid();

    [BsonElement("conversationId")]
    [BsonRepresentation(BsonType.String)]
    public Guid ConversationId { get; set; }

    [BsonElement("caseSummaryId")]
    [BsonRepresentation(BsonType.String)]
    [BsonIgnoreIfNull]
    public Guid? CaseSummaryId { get; set; }

    [BsonElement("incidentCategoryCode")]
    public string IncidentCategoryCode { get; set; } = string.Empty;

    [BsonElement("incidentTypeCode")]
    [BsonIgnoreIfNull]
    public string? IncidentTypeCode { get; set; }

    [BsonElement("templateCode")]
    public string TemplateCode { get; set; } = string.Empty;

    [BsonElement("subject")]
    public string Subject { get; set; } = string.Empty;

    [BsonElement("applicationText")]
    public string ApplicationText { get; set; } = string.Empty;

    /// <summary>
    /// Lifecycle status. Initial value: "DRAFT".
    /// </summary>
    [BsonElement("status")]
    public string Status { get; set; } = "DRAFT";

    /// <summary>
    /// Verification and review state. Initial value: "PENDING_REVIEW".
    /// </summary>
    [BsonElement("reviewStatus")]
    public string ReviewStatus { get; set; } = "PENDING_REVIEW";

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // --- Additional Rich Metadata & Traceability ---

    [BsonElement("caseId")]
    [BsonRepresentation(BsonType.String)]
    [BsonIgnoreIfNull]
    public Guid? CaseId { get; set; }

    [BsonElement("templateId")]
    [BsonRepresentation(BsonType.String)]
    public Guid TemplateId { get; set; }

    [BsonElement("templateTitleBn")]
    public string TemplateTitleBn { get; set; } = string.Empty;

    [BsonElement("recipientAuthorityBn")]
    public string RecipientAuthorityBn { get; set; } = string.Empty;

    [BsonElement("disclaimerNotice")]
    public string DisclaimerNotice { get; set; } = string.Empty;

    [BsonElement("placeholdersUsed")]
    public Dictionary<string, string> PlaceholdersUsed { get; set; } = [];

    [BsonElement("missingPlaceholders")]
    public List<string> MissingPlaceholders { get; set; } = [];

    [BsonElement("reviewedBy")]
    [BsonIgnoreIfNull]
    public string? ReviewedBy { get; set; }

    [BsonElement("reviewNotes")]
    [BsonIgnoreIfNull]
    public string? ReviewNotes { get; set; }

    [BsonElement("reviewedAt")]
    [BsonIgnoreIfNull]
    public DateTime? ReviewedAt { get; set; }
}
