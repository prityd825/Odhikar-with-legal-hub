using Odhikar.Domain.Models;

namespace Odhikar.Application.Settings;

/// <summary>
/// Configuration settings for question categories loaded from the "QuestionCategories" section.
/// Allows operators to add custom categories or override built-in question definitions at runtime.
/// </summary>
public sealed class QuestionCategorySettings
{
    public const string SectionName = "QuestionCategories";

    /// <summary>
    /// If true, the backend may use the LLM's tailored Bangla question wording for the selected category
    /// as long as it passes safety, relevance, and anti-duplicate validation.
    /// If false, the backend strictly uses DefaultQuestionBangla.
    /// </summary>
    public bool AllowLlmQuestionWording { get; set; } = true;

    /// <summary>
    /// Custom or overridden category definitions.
    /// </summary>
    public List<QuestionCategoryDefinition> Categories { get; set; } = [];
}
