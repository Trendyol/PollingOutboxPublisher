namespace PollingOutboxPublisher.Helper.Enums
{
    public record DbProviders(string ProviderName)
    {
        public static DbProviders Mssql { get; } = new(nameof(Mssql));
        public static DbProviders Postgres { get; } = new(nameof(Postgres));

        public static bool IsValid(string providerName) =>
            providerName == Mssql || providerName == Postgres;

        public static implicit operator string(DbProviders dbProvider) => dbProvider.ProviderName;
    }
}