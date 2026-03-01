using Microsoft.Extensions.Configuration;

namespace GoogleAppMods.AppHost;

public static class ResourceBuilderExtensions
{
    public static IResourceBuilder<T> WithGoogleProjectConfig<T>(
        this IResourceBuilder<T> builder,
        IConfigurationSection section) where T : IResourceWithEnvironment
    {
        foreach (var child in section.GetChildren())
        {
            builder = builder.WithEnvironment($"GoogleProject__{child.Key}", child.Value);
        }

        return builder;
    }
}
