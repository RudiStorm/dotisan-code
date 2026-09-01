using Dotisan.Core;

namespace Dotisan.Generators;

public sealed record CrudScaffoldingResult(bool Success, string? ErrorMessage, IReadOnlyList<string> CreatedFiles)
{
    public static CrudScaffoldingResult Succeeded(IReadOnlyList<string> files) => new(true, null, files);
    public static CrudScaffoldingResult Failed(string message) => new(false, message, []);
}

public static class CrudScaffolder
{
    public static async Task<CrudScaffoldingResult> ScaffoldAsync(string projectDirectory, string resourceName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(resourceName) || !IsIdentifier(resourceName))
            return CrudScaffoldingResult.Failed("Resource names must be valid C# identifiers, for example 'Customer'.");

        var root = Path.GetFullPath(projectDirectory);
        var frontend = Directory.Exists(Path.Combine(root, "src"))
            ? Directory.EnumerateDirectories(Path.Combine(root, "src"), "*.Web", SearchOption.AllDirectories).FirstOrDefault(directory => File.Exists(Path.Combine(directory, "package.json")))
            : null;
        if (frontend is null)
            return CrudScaffoldingResult.Failed("Could not find the generated Vue frontend. Run this command from a generated Dotisan project.");

        var plural = Pluralize(resourceName);
        var featureDirectory = Path.Combine(frontend, "src", "features", plural);
        var routeDirectory = Path.Combine(frontend, "src", "routes");
        var files = new Dictionary<string, string>
        {
            [Path.Combine(featureDirectory, resourceName + "List.vue")] = ListPage(resourceName, plural),
            [Path.Combine(featureDirectory, resourceName + "Detail.vue")] = DetailPage(resourceName, plural),
            [Path.Combine(featureDirectory, resourceName + "Form.vue")] = FormPage(resourceName, plural),
            [Path.Combine(routeDirectory, ToCamelCase(plural) + ".ts")] = Route(resourceName, plural),
        };
        var routeRegistry = Path.Combine(routeDirectory, "index.ts");
        if (!File.Exists(routeRegistry))
            return CrudScaffoldingResult.Failed("The Vue route registry is missing. Regenerate the project before creating CRUD UI.");
        if (files.Keys.Any(File.Exists))
            return CrudScaffoldingResult.Failed($"Frontend CRUD files for '{resourceName}' already exist.");

        Directory.CreateDirectory(featureDirectory);
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await File.WriteAllTextAsync(file.Key, file.Value, cancellationToken);
        }
        var camelPlural = ToCamelCase(plural);
        var registry = await File.ReadAllTextAsync(routeRegistry, cancellationToken);
        registry = registry.Replace("// DOTISAN:ROUTES", $"// DOTISAN:ROUTES\nimport {camelPlural}Routes from './{camelPlural}';\nroutes.push(...{camelPlural}Routes);", StringComparison.Ordinal);
        await File.WriteAllTextAsync(routeRegistry, registry, cancellationToken);
        return CrudScaffoldingResult.Succeeded([.. files.Keys, routeRegistry]);
    }

    private static bool IsIdentifier(string value) => char.IsLetter(value[0]) && value.All(character => char.IsLetterOrDigit(character) || character == '_');
    private static string Pluralize(string value) => value.EndsWith('y') && value.Length > 1 ? value[..^1] + "ies" : value.EndsWith('s') ? value : value + "s";
    private static string ToCamelCase(string value) => char.ToLowerInvariant(value[0]) + value[1..];

    private static string ListPage(string resourceName, string plural) => $$"""
    <script setup lang="ts">
    import { onMounted, ref } from 'vue';
    import { RouterLink } from 'vue-router';
    type {{resourceName}} = { id: string; name: string; createdAtUtc: string };
    const items = ref<{{resourceName}}[]>([]); const loading = ref(true); const error = ref(''); const deleting = ref('');
    async function load() { loading.value = true; error.value = ''; try { const response = await fetch('/api/{{plural.ToLowerInvariant()}}', { credentials: 'include' }); if (!response.ok) throw new Error('Could not load {{plural.ToLowerInvariant()}}.'); items.value = await response.json() as {{resourceName}}[]; } catch (exception) { error.value = exception instanceof Error ? exception.message : 'Could not load {{plural.ToLowerInvariant()}}.'; } finally { loading.value = false; } }
    async function remove(item: {{resourceName}}) { if (!window.confirm(`Delete ${item.name}?`)) return; deleting.value = item.id; try { const response = await fetch(`/api/{{plural.ToLowerInvariant()}}/${item.id}`, { method: 'DELETE', credentials: 'include' }); if (!response.ok) throw new Error('Could not delete {{resourceName}}.'); await load(); } catch (exception) { error.value = exception instanceof Error ? exception.message : 'Could not delete {{resourceName}}.'; } finally { deleting.value = ''; } }
    onMounted(load);
    </script>
    <template><section aria-labelledby="{{plural.ToLowerInvariant()}}-heading"><header><h1 id="{{plural.ToLowerInvariant()}}-heading">{{resourceName}}s</h1><RouterLink to="/{{plural.ToLowerInvariant()}}/new">Create {{resourceName}}</RouterLink></header><p v-if="loading" aria-live="polite">Loading...</p><p v-else-if="error" role="alert" v-text="error"></p><p v-else-if="items.length === 0">No {{plural.ToLowerInvariant()}} yet.</p><ul v-else><li v-for="item in items" :key="item.id"><RouterLink :to="`/{{plural.ToLowerInvariant()}}/${item.id}`" v-text="item.name" /> <RouterLink :to="`/{{plural.ToLowerInvariant()}}/${item.id}/edit`">Edit</RouterLink> <button type="button" :disabled="deleting === item.id" @click="remove(item)" v-text="deleting === item.id ? 'Deleting...' : 'Delete'"></button></li></ul></section></template>
    """;

    private static string DetailPage(string resourceName, string plural) => $$"""
    <script setup lang="ts">
    import { onMounted, ref } from 'vue'; import { RouterLink, useRoute, useRouter } from 'vue-router';
    type {{resourceName}} = { id: string; name: string; createdAtUtc: string }; const route = useRoute(); const router = useRouter(); const item = ref<{{resourceName}}>(); const loading = ref(true); const error = ref('');
    async function load() { try { const response = await fetch(`/api/{{plural.ToLowerInvariant()}}/${route.params.id}`, { credentials: 'include' }); if (!response.ok) throw new Error('Could not load {{resourceName}}.'); item.value = await response.json() as {{resourceName}}; } catch (exception) { error.value = exception instanceof Error ? exception.message : 'Could not load {{resourceName}}.'; } finally { loading.value = false; } }
    async function remove() { if (!window.confirm('Delete this {{resourceName}}?')) return; const response = await fetch(`/api/{{plural.ToLowerInvariant()}}/${route.params.id}`, { method: 'DELETE', credentials: 'include' }); if (!response.ok) { error.value = 'Could not delete {{resourceName}}.'; return; } await router.push('/{{plural.ToLowerInvariant()}}'); } onMounted(load);
    </script>
    <template><section><p v-if="loading">Loading...</p><p v-else-if="error" role="alert" v-text="error"></p><div v-else-if="item"><h1 v-text="item.name"></h1><p>Created <span v-text="item.createdAtUtc"></span></p><RouterLink :to="`/{{plural.ToLowerInvariant()}}/${item.id}/edit`">Edit</RouterLink> <button type="button" @click="remove">Delete</button> <RouterLink to="/{{plural.ToLowerInvariant()}}">Back</RouterLink></div></section></template>
    """;

    private static string FormPage(string resourceName, string plural) => $$"""
    <script setup lang="ts">
    import { computed, ref } from 'vue'; import { useRoute, useRouter } from 'vue-router';
    const route = useRoute(); const router = useRouter(); const name = ref(''); const saving = ref(false); const error = ref(''); const editing = computed(() => Boolean(route.params.id));
    async function save() { if (!name.value.trim()) { error.value = 'Name is required.'; return; } saving.value = true; error.value = ''; try { const response = await fetch(editing.value ? `/api/{{plural.ToLowerInvariant()}}/${route.params.id}` : '/api/{{plural.ToLowerInvariant()}}', { method: editing.value ? 'PUT' : 'POST', headers: { 'Content-Type': 'application/json' }, credentials: 'include', body: JSON.stringify({ name: name.value }) }); if (!response.ok) throw new Error('Could not save {{resourceName}}.'); await router.push('/{{plural.ToLowerInvariant()}}'); } catch (exception) { error.value = exception instanceof Error ? exception.message : 'Could not save {{resourceName}}.'; } finally { saving.value = false; } }
    </script>
    <template><form @submit.prevent="save"><h1><span v-text="editing ? 'Edit' : 'Create'"></span> {{resourceName}}</h1><p v-if="error" role="alert" v-text="error"></p><label>Name <input v-model="name" required autocomplete="off" /></label><button type="submit" :disabled="saving" v-text="saving ? 'Saving...' : 'Save'"></button><button type="button" @click="router.push('/{{plural.ToLowerInvariant()}}')">Cancel</button></form></template>
    """;

    private static string Route(string resourceName, string plural) => $$"""
    import type { RouteRecordRaw } from 'vue-router'; import {{resourceName}}List from '../features/{{plural}}/{{resourceName}}List.vue'; import {{resourceName}}Detail from '../features/{{plural}}/{{resourceName}}Detail.vue'; import {{resourceName}}Form from '../features/{{plural}}/{{resourceName}}Form.vue';
    const {{ToCamelCase(plural)}}Routes: RouteRecordRaw[] = [{ path: '/{{plural.ToLowerInvariant()}}', component: {{resourceName}}List }, { path: '/{{plural.ToLowerInvariant()}}/new', component: {{resourceName}}Form }, { path: '/{{plural.ToLowerInvariant()}}/:id', component: {{resourceName}}Detail }, { path: '/{{plural.ToLowerInvariant()}}/:id/edit', component: {{resourceName}}Form }];
    export default {{ToCamelCase(plural)}}Routes;
    """;
}
