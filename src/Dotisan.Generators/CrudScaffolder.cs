using Dotisan.Core;

namespace Dotisan.Generators;

public sealed record CrudScaffoldingResult(bool Success, string? ErrorMessage, IReadOnlyList<string> CreatedFiles)
{
    public static CrudScaffoldingResult Succeeded(IReadOnlyList<string> files) => new(true, null, files);

    public static CrudScaffoldingResult Failed(string message) => new(false, message, []);
}

public static class CrudScaffolder
{
    public static async Task<CrudScaffoldingResult> ScaffoldAsync(
        string projectDirectory,
        string resourceName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(resourceName) || !IsIdentifier(resourceName))
            return CrudScaffoldingResult.Failed("Resource names must be valid C# identifiers, for example 'Customer'.");

        var root = Path.GetFullPath(projectDirectory);
        var frontend = Directory.Exists(Path.Combine(root, "src"))
            ? Directory.EnumerateDirectories(Path.Combine(root, "src"), "*.Web", SearchOption.AllDirectories)
                .FirstOrDefault(directory => File.Exists(Path.Combine(directory, "package.json")))
            : null;
        if (frontend is null)
            return CrudScaffoldingResult.Failed("Could not find the generated Vue frontend. Run this command from a generated Dotisan project.");

        var plural = Pluralize(resourceName);
        var featureDirectory = Path.Combine(frontend, "src", "features", plural);
        var listPath = Path.Combine(featureDirectory, resourceName + "List.vue");
        var routePath = Path.Combine(frontend, "src", "routes", ToCamelCase(plural) + ".ts");
        if (File.Exists(listPath) || File.Exists(routePath))
            return CrudScaffoldingResult.Failed($"Frontend CRUD files for '{resourceName}' already exist.");

        Directory.CreateDirectory(featureDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(routePath)!);
        await File.WriteAllTextAsync(listPath, ListPage(resourceName, plural), cancellationToken);
        await File.WriteAllTextAsync(routePath, Route(resourceName, plural), cancellationToken);
        return CrudScaffoldingResult.Succeeded([listPath, routePath]);
    }

    private static bool IsIdentifier(string value) => char.IsLetter(value[0]) && value.All(character => char.IsLetterOrDigit(character) || character == '_');

    private static string Pluralize(string value) => value.EndsWith('y') && value.Length > 1
        ? value[..^1] + "ies"
        : value.EndsWith('s') ? value : value + "s";

    private static string ToCamelCase(string value) => char.ToLowerInvariant(value[0]) + value[1..];

    private static string ListPage(string resourceName, string plural) => $$"""
    <script setup lang="ts">
    import { onMounted, ref } from 'vue';

    type {{resourceName}} = { id: string; name: string; createdAtUtc: string };
    const items = ref<{{resourceName}}[]>([]);
    const loading = ref(true);
    const error = ref('');

    async function load() {
      loading.value = true;
      error.value = '';
      try {
        const response = await fetch('/api/{{plural.ToLowerInvariant()}}', { credentials: 'include' });
        if (!response.ok) throw new Error('Could not load {{plural.ToLowerInvariant()}}.');
        items.value = await response.json() as {{resourceName}}[];
      } catch (exception) {
        error.value = exception instanceof Error ? exception.message : 'Could not load {{plural.ToLowerInvariant()}}.';
      } finally {
        loading.value = false;
      }
    }

    onMounted(load);
    </script>

    <template>
      <section>
        <h1>{{resourceName}}s</h1>
        <p v-if="loading">Loading...</p>
        <p v-else-if="error" role="alert" v-text="error"></p>
        <p v-else-if="items.length === 0">No {{plural.ToLowerInvariant()}} yet.</p>
        <ul v-else>
          <li v-for="item in items" :key="item.id" v-text="item.name"></li>
        </ul>
      </section>
    </template>
    """;

    private static string Route(string resourceName, string plural) => $$"""
    import {{resourceName}}List from '../features/{{plural}}/{{resourceName}}List.vue';

    export default {
      path: '/{{plural.ToLowerInvariant()}}',
      component: {{resourceName}}List
    };
    """;
}
