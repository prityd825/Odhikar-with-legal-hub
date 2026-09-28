namespace Odhikar.Application.Settings;

public sealed class MongoDbSettings
{
    public const string SectionName = "MongoDb";

    public string ConnectionString { get; init; } = "mongodb://localhost:27017";
    public string DatabaseName { get; init; } = "odhikar";
}
