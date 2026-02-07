namespace TreeTopic.Common.Helpers;

public static class CorsOriginHelper
{
    public static string[] ResolveCorsOrigins(IConfiguration configuration, string configuredSectionKey)
    {
        static string[] NormalizeOrigins(IEnumerable<string> candidates)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var candidate in candidates)
            {
                AddOrigin(set, candidate);
            }

            return set.ToArray();
        }

        static void AddOrigin(HashSet<string> set, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            {
                return;
            }

            var host = uri.Host;
            var isWildcardHost =
                host == "*" ||
                host == "+" ||
                host == "0.0.0.0" ||
                host == "::" ||
                host == "[::]";

            if (isWildcardHost)
            {
                // Kestrel wildcard host cannot be used as CORS origin.
                set.Add($"{uri.Scheme}://localhost:{uri.Port}");
                set.Add($"{uri.Scheme}://127.0.0.1:{uri.Port}");
                return;
            }

            set.Add(uri.GetLeftPart(UriPartial.Authority));
        }

        static IEnumerable<string> SplitCandidates(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return Array.Empty<string>();
            }

            return value
                .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        // Prefer explicit appsettings/environment section values when provided.
        var explicitOrigins = NormalizeOrigins(
            configuration.GetSection(configuredSectionKey).Get<string[]>() ?? Array.Empty<string>()
        );
        if (explicitOrigins.Length > 0)
        {
            return explicitOrigins;
        }

        var origins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Fallback to runtime URL related settings.
        foreach (var candidate in SplitCandidates(configuration["Urls"]))
        {
            AddOrigin(origins, candidate);
        }

        foreach (var candidate in SplitCandidates(configuration["ASPNETCORE_URLS"]))
        {
            AddOrigin(origins, candidate);
        }

        foreach (var endpoint in configuration.GetSection("Kestrel:Endpoints").GetChildren())
        {
            foreach (var candidate in SplitCandidates(endpoint["Url"]))
            {
                AddOrigin(origins, candidate);
            }
        }

        AddOrigin(origins, configuration["Authentication:PublicBaseUrl"]);

        if (origins.Count == 0)
        {
            AddOrigin(origins, "http://localhost:5000");
            AddOrigin(origins, "https://localhost:5001");
        }

        return origins.ToArray();
    }
}
