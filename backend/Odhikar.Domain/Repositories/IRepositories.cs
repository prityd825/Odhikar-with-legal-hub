namespace Odhikar.Domain.Repositories;

/// <summary>
/// Persistence contract for multi-turn conversations and message history.
/// </summary>
public interface IConversationRepository
{
    Task<ConversationModel?> GetByIdAsync(Guid conversationId, CancellationToken ct = default);
    Task CreateAsync(ConversationModel conversation, CancellationToken ct = default);
    Task AddMessageAsync(Guid conversationId, ConversationMessage message, CancellationToken ct = default);
    Task UpdateConversationAsync(ConversationModel conversation, CancellationToken ct = default);
    Task SaveAnalysisAsync(Guid conversationId, AIAnalysis analysis, CancellationToken ct = default);
    Task SaveStateAsync(Guid conversationId, ConversationState state, CancellationToken ct = default);
    Task<IReadOnlyList<ConversationMessage>> GetRecentMessagesAsync(Guid conversationId, int limit = 20, CancellationToken ct = default);
    Task SaveReportAsync(FinalConversationReport report, CancellationToken ct = default);
    Task<FinalConversationReport?> GetReportByConversationIdAsync(Guid conversationId, CancellationToken ct = default);
    Task SaveFinalReportAsync(FinalReport report, CancellationToken ct = default);
    Task<FinalReport?> GetFinalReportByConversationIdAsync(Guid conversationId, CancellationToken ct = default);
    Task<FinalReport?> GetFinalReportByIdAsync(Guid reportId, CancellationToken ct = default);
}

/// <summary>
/// Persistence contract for legal aid cases.
/// </summary>
public interface ICaseRepository
{
    Task<CaseModel?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<CaseModel?> GetByCaseNumberAsync(string caseNumber, CancellationToken ct = default);
    Task<IReadOnlyList<CaseModel>> GetPrioritizedCasesAsync(CancellationToken ct = default);
    Task InsertAsync(CaseModel caseModel, CancellationToken ct = default);
    Task UpdateAsync(CaseModel caseModel, CancellationToken ct = default);
    Task<bool> DeleteAsync(string id, CancellationToken ct = default);
}

/// <summary>
/// Persistence contract for Question Bank items.
/// </summary>
public interface IQuestionBankRepository
{
    Task<IReadOnlyList<QuestionBankItem>> GetAllAsync(string? category = null, string? incidentType = null, string? parentCategory = null, CancellationToken ct = default);
    Task<IReadOnlyList<QuestionBankItem>> GetByParentCategoryCodeAsync(string parentCategoryCode, CancellationToken ct = default);
    Task<QuestionBankItem?> GetByIdAsync(string id, CancellationToken ct = default);
    Task InsertAsync(QuestionBankItem item, CancellationToken ct = default);
    Task InsertManyAsync(IEnumerable<QuestionBankItem> items, CancellationToken ct = default);
}

/// <summary>
/// Persistence contract for legal incident types and categories.
/// </summary>
public interface IIncidentTypeRepository
{
    Task<IReadOnlyList<IncidentTypeModel>> GetIncidentTypesAsync(bool activeOnly = true, string? parentCategoryCode = null, CancellationToken ct = default);
    Task<IncidentTypeModel?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ParentIncidentCategoryModel>> GetParentCategoriesAsync(bool activeOnly = true, CancellationToken ct = default);
    Task<ParentIncidentCategoryModel?> GetParentCategoryByCodeAsync(string code, CancellationToken ct = default);
}

/// <summary>
/// Persistence contract for legal clinics.
/// </summary>
public interface IClinicRepository
{
    Task<IReadOnlyList<ClinicModel>> GetClinicsAsync(string? district = null, CancellationToken ct = default);
    Task<ClinicModel?> GetByIdAsync(string id, CancellationToken ct = default);
    Task InsertManyAsync(IEnumerable<ClinicModel> clinics, CancellationToken ct = default);
}
