using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Odhikar.Domain.Models;
using Odhikar.Application.Settings;

namespace Odhikar.Infrastructure.Persistence;

/// <summary>Thin wrapper exposing typed collections from a single shared <see cref="IMongoDatabase"/>.</summary>
public sealed class MongoDbContext(IOptions<MongoDbSettings> options)
{
    private readonly IMongoDatabase _database = new MongoClient(options.Value.ConnectionString)
        .GetDatabase(options.Value.DatabaseName);

    public IMongoDatabase Database => _database;
    public IMongoCollection<CaseModel> Cases => _database.GetCollection<CaseModel>("cases");
    public IMongoCollection<ClinicModel> Clinics => _database.GetCollection<ClinicModel>("clinics");
    public IMongoCollection<IncidentTypeModel> IncidentTypes => _database.GetCollection<IncidentTypeModel>("incident_types");
    public IMongoCollection<ConversationModel> Conversations => _database.GetCollection<ConversationModel>("conversations");
    public IMongoCollection<Conversation> AiConversations => _database.GetCollection<Conversation>("conversations");
    public IMongoCollection<ConversationMessage> ConversationMessages => _database.GetCollection<ConversationMessage>("conversation_messages");
    public IMongoCollection<ConversationState> ConversationStates => _database.GetCollection<ConversationState>("conversation_states");
    public IMongoCollection<AIAnalysis> AiAnalyses => _database.GetCollection<AIAnalysis>("ai_analyses");
    public IMongoCollection<QuestionBankItem> QuestionBank => _database.GetCollection<QuestionBankItem>("question_bank");
    public IMongoCollection<FinalConversationReport> ConversationReports => _database.GetCollection<FinalConversationReport>("conversation_reports");
    public IMongoCollection<FinalReport> FinalReports => _database.GetCollection<FinalReport>("final_reports");
    public IMongoCollection<ParentIncidentCategoryModel> ParentIncidentCategories => _database.GetCollection<ParentIncidentCategoryModel>("parent_incident_categories");
    public IMongoCollection<CaseSummaryTemplateModel> CaseSummaryTemplates => _database.GetCollection<CaseSummaryTemplateModel>("case_summary_templates");
    public IMongoCollection<ConversationAnswerModel> ConversationAnswers => _database.GetCollection<ConversationAnswerModel>("conversation_answers");
    public IMongoCollection<GeneratedCaseSummaryModel> GeneratedCaseSummaries => _database.GetCollection<GeneratedCaseSummaryModel>("generated_case_summaries");
    public IMongoCollection<ApplicationTemplateModel> ApplicationTemplates => _database.GetCollection<ApplicationTemplateModel>("application_templates");
    public IMongoCollection<GeneratedApplicationModel> GeneratedApplications => _database.GetCollection<GeneratedApplicationModel>("generated_applications");
}
