using System;
using System.Runtime.CompilerServices;

namespace PollingOutboxPublisher.Database.Repositories.Postgres.Modules
{
    public static class DatabaseModuleInitializer
    {
        [ModuleInitializer]
        public static void Initialize()
        {
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        }
    }
}