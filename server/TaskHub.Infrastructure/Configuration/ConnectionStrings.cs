using Npgsql;
using StackExchange.Redis;

namespace TaskHub.Infrastructure;

// Hosting companies hand out addresses in their own formats. These helpers turn them into
// the formats our libraries understand, so the same code runs on any host.
public static class ConnectionStrings
{
    // Accepts "redis://..." and "rediss://..." (encrypted) addresses, or the library's own
    // "host:port,password=..." format. The app starts even if Redis isn't reachable yet and keeps retrying.
    public static ConfigurationOptions ParseRedis(string value)
    {
        if (value.StartsWith("redis://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("rediss://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(value);

            var options = new ConfigurationOptions
            {
                Ssl = uri.Scheme.Equals("rediss", StringComparison.OrdinalIgnoreCase),
                AbortOnConnectFail = false
            };
            options.EndPoints.Add(uri.Host, uri.Port > 0 ? uri.Port : 6379);

            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                string[] parts = uri.UserInfo.Split(':', 2);

                if (parts.Length == 2)
                {
                    if (parts[0].Length > 0)
                        options.User = Uri.UnescapeDataString(parts[0]);

                    options.Password = Uri.UnescapeDataString(parts[1]);
                }
                else
                {
                    options.Password = Uri.UnescapeDataString(parts[0]);
                }
            }

            return options;
        }

        ConfigurationOptions parsed = ConfigurationOptions.Parse(value);
        parsed.AbortOnConnectFail = false;
        return parsed;
    }

    // Accepts a "postgresql://user:password@host/database" address (what Neon shows) or Npgsql's
    // own "Host=...;Database=..." format, which is passed through untouched.
    public static string NormalizePostgres(string value)
    {
        bool isUrl = value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
                     || value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase);

        if (!isUrl)
            return value;

        var uri = new Uri(value);
        string[] userInfo = uri.UserInfo.Split(':', 2);

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = uri.AbsolutePath.TrimStart('/'),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
            MaxPoolSize = 10,   // free databases allow only a limited number of connections
            Timeout = 30        // a sleeping database needs a moment to wake up
        };

        // Neon only accepts encrypted connections
        return builder.ConnectionString + ";SSL Mode=Require;Trust Server Certificate=true";
    }
}