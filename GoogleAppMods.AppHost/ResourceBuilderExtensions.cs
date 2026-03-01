using Microsoft.Extensions.Configuration;

namespace GoogleAppMods.AppHost;

public static class ResourceBuilderExtensions
{
    public static IResourceBuilder<T> WithGoogleProjectConfig<T>(
        this IResourceBuilder<T> builder,
        IConfigurationSection section) where T : IResourceWithEnvironment
    {
        return builder.WithConfigSection("GoogleProject", section);
    }

    public static IResourceBuilder<T> WithGmailSweeperConfig<T>(
        this IResourceBuilder<T> builder,
        IConfigurationSection section) where T : IResourceWithEnvironment
    {
        return builder.WithConfigSection("GmailSweeper", section);
    }

    private static IResourceBuilder<T> WithConfigSection<T>(
        this IResourceBuilder<T> builder,
        string prefix,
        IConfigurationSection section) where T : IResourceWithEnvironment
    {
        foreach (var (key, value) in FlattenSection(prefix, section))
        {
            builder = builder.WithEnvironment(key, value);
        }

        return builder;
    }

    private static IEnumerable<(string Key, string? Value)> FlattenSection(string prefix, IConfigurationSection section)
    {
        foreach (var child in section.GetChildren())
        {
            var key = $"{prefix}__{child.Key}";

            if (child.Value is not null)
            {
                yield return (key, child.Value);
            }

            // Recurse for nested sections (e.g., arrays like Queries:0, Queries:1)
            foreach (var nested in FlattenSection(key, child))
            {
                yield return nested;
            }
        }
    }
}
