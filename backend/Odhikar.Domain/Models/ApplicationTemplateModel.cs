using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Odhikar.Domain.Models;

/// <summary>
/// Configuration-driven legal application template for drafting petitions and complaints.
/// Persisted in the <c>application_templates</c> collection.
/// </summary>
[BsonIgnoreExtraElements]
public sealed class ApplicationTemplateModel
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Parent category code: DOWER_MAINTENANCE, DOWRY_DOMESTIC_VIOLENCE, LABOUR_WAGE,
    /// LAND_PROPERTY, WOMEN_CHILD_PROTECTION, OTHER
    /// </summary>
    [BsonElement("categoryCode")]
    public string CategoryCode { get; set; } = string.Empty;

    [BsonElement("titleBn")]
    public string TitleBn { get; set; } = string.Empty;

    [BsonElement("titleEn")]
    public string TitleEn { get; set; } = string.Empty;

    [BsonElement("recipientAuthorityBn")]
    public string RecipientAuthorityBn { get; set; } = string.Empty;

    [BsonElement("subjectBn")]
    public string SubjectBn { get; set; } = string.Empty;

    /// <summary>
    /// Legal draft text containing placeholders such as {{APPLICANT_NAME}}, {{RESPONDENT_NAME}}, etc.
    /// </summary>
    [BsonElement("bodyTemplateBn")]
    public string BodyTemplateBn { get; set; } = string.Empty;

    /// <summary>
    /// Supported placeholder descriptors with Bangla fallbacks and descriptions.
    /// </summary>
    [BsonElement("supportedPlaceholders")]
    public List<ApplicationPlaceholderDefinition> SupportedPlaceholders { get; set; } = [];

    [BsonElement("disclaimerNoticeBn")]
    public string DisclaimerNoticeBn { get; set; } =
        "সতর্কীকরণ ও আইনি বিজ্ঞপ্তি (DRAFT - PENDING REVIEW): এই আবেদনপত্রটি আবেদনকারীর প্রদত্ত প্রাথমিক ও একতরফা মৌখিক/লিখিত বিবরণের ভিত্তিতে অধিকার (Odhikar) ডিজিটাল সহায়িকা কর্তৃক প্রস্তুতকৃত একটি খসড়া রূপরেখা মাত্র। এতে বর্ণিত অভিযোগ ও তথ্যাদি কোনো আদালত বা তদন্তকারী কর্তৃপক্ষ কর্তৃক চূড়ান্তভাবে যাচাই বা প্রমাণিত নয়। বিজ্ঞ আইনজীবী বা সংশ্লিষ্ট আইনি কর্মকর্তার দ্বারা তথ্যসমূহ যাচাই ও স্বাক্ষর ব্যতীত এটি চূড়ান্ত আইনগত নথি হিসেবে গণ্য হবে না।";

    [BsonElement("version")]
    public int Version { get; set; } = 1;

    [BsonElement("isActive")]
    public bool IsActive { get; set; } = true;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Descriptor for individual placeholders supported in application templates.
/// </summary>
[BsonIgnoreExtraElements]
public sealed class ApplicationPlaceholderDefinition
{
    [BsonElement("placeholder")]
    public string Placeholder { get; set; } = string.Empty; // e.g. "{{APPLICANT_NAME}}"

    [BsonElement("key")]
    public string Key { get; set; } = string.Empty; // e.g. "APPLICANT_NAME"

    [BsonElement("labelBn")]
    public string LabelBn { get; set; } = string.Empty;

    [BsonElement("fallbackBn")]
    public string FallbackBn { get; set; } = "[তথ্য পাওয়া যায়নি / আবেদনকারী কর্তৃক পূরণীয়]";

    [BsonElement("isRequired")]
    public bool IsRequired { get; set; }

    [BsonElement("aliases")]
    public List<string> Aliases { get; set; } = [];
}
