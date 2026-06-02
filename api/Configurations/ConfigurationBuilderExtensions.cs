namespace api.Configurations
{
    public static class ConfigurationBuilderExtensions
    {
        /// <summary>
        /// Creates if don't already exist/sets all the configuration variables present on the .env file for the
        /// <see href="https://docs.microsoft.com/en-us/dotnet/api/azure.identity.environmentcredential?view=azure-dotnet">Environment Credentials</see>
        /// used by the application when dockerized.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public static void AddDotEnvironmentVariables(
            this WebApplicationBuilder builder,
            string filePath
        )
        {
            if (!File.Exists(filePath))
                return;

            foreach (string line in File.ReadAllLines(filePath))
            {
                string[] parts = line.Split('=', StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length == 0 || parts[0].StartsWith('#'))
                    continue;

                Environment.SetEnvironmentVariable(parts[0], parts[1]);
            }

            builder.Configuration.AddEnvironmentVariables();
        }

        /// <summary>
        /// Configures the logger used by the application
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static void ConfigureLogger(this WebApplicationBuilder builder)
        {
            builder.Logging.AddSimpleConsole(options =>
            {
                options.IncludeScopes = false;
                options.TimestampFormat = "yyyy-MM-dd HH:mm:ss - ";
                options.ColorBehavior = Microsoft
                    .Extensions
                    .Logging
                    .Console
                    .LoggerColorBehavior
                    .Enabled;
            });
        }
    }
}
