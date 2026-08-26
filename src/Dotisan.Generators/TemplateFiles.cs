using Dotisan.Core;

namespace Dotisan.Generators;

internal sealed record TemplateFile(string Path, string Content);

internal static class TemplateFiles
{
    public static IReadOnlyList<TemplateFile> Create(ProjectOptions options, string identifier)
    {
        var packageManager = options.PackageManager == PackageManager.Pnpm ? "pnpm@9.15.0" : "npm@10.9.0";
        var packageJson = $$"""
        {
          "name": "{{options.Name.ToLowerInvariant()}}-web",
          "private": true,
          "version": "0.1.0",
          "packageManager": "{{packageManager}}",
          "type": "module",
          "scripts": {
            "dev": "vite",
            "build": "vue-tsc -b && vite build",
            "test": "vitest run"
          },
          "dependencies": {
            "@tanstack/vue-query": "^5.59.0",
            "pinia": "^2.3.0",
            "vue": "^3.5.0",
            "vue-router": "^4.4.0",
            "zod": "^3.23.0"
          },
          "devDependencies": {
            "@types/node": "^22.0.0",
            "@vitejs/plugin-vue": "^5.2.0",
            "@vue/tsconfig": "^0.7.0",
            "typescript": "~5.6.0",
            "vite": "^6.0.0",
            "vitest": "^2.1.0",
            "vue-tsc": "^2.1.0"
          }
        }
        """;

        return
        [
            new(".gitignore", "bin/\nobj/\nwwwroot/\nnode_modules/\ndist/\n*.db\n"),
            new("global.json", "{\n  \"sdk\": {\n    \"version\": \"8.0.100\",\n    \"rollForward\": \"latestMajor\"\n  }\n}\n"),
            new(".node-version", "22\n"),
            new("Directory.Packages.props", PackageVersions()),
            new("README.md", ProjectReadme(options.Name)),
            new("dotisan.config", $$"""
            version: 1
            profile: quick
            database: {{options.Database.ToString().ToLowerInvariant()}}
            authentication: {{(options.AuthenticationEnabled ? "enabled" : "disabled")}}
            multi_tenancy: {{(options.MultiTenancyEnabled ? "enabled" : "disabled")}}
            package_manager: {{options.PackageManager.ToString().ToLowerInvariant()}}
            dev:
              services:
                api: true
                frontend: true
                database: true
                observability: false
                mail: false
                workers: false
            """),
            new("Dockerfile", Dockerfile(options.Name)),
            new($"src/{options.Name}.Api/{options.Name}.Api.csproj", ApiProject(options.Name)),
            new($"src/{options.Name}.Api/Program.cs", ApiProgram(identifier)),
            new($"src/{options.Name}.Api/Data/AppDbContext.cs", DbContext(identifier)),
            new($"src/{options.Name}.Api/Infrastructure/DotisanEndpointExtensions.cs", EndpointExtensions(identifier)),
            new($"src/{options.Name}.Api/appsettings.json", "{\n  \"ConnectionStrings\": {\n    \"DefaultConnection\": \"Data Source=app.db\"\n  },\n  \"Logging\": {\n    \"LogLevel\": {\n      \"Default\": \"Information\",\n      \"Microsoft.AspNetCore\": \"Warning\"\n    }\n  },\n  \"AllowedHosts\": \"*\"\n}\n"),
            new($"src/{options.Name}.Api/appsettings.Development.json", "{\n  \"Logging\": {\n    \"LogLevel\": {\n      \"Default\": \"Information\",\n      \"Microsoft.AspNetCore\": \"Information\"\n    }\n  }\n}\n"),
            new($"src/{options.Name}.Web/package.json", packageJson),
            new($"src/{options.Name}.Web/index.html", WebIndex(options.Name)),
            new($"src/{options.Name}.Web/tsconfig.json", "{\n  \"files\": [],\n  \"references\": [{ \"path\": \"./tsconfig.app.json\" }, { \"path\": \"./tsconfig.node.json\" }]\n}\n"),
            new($"src/{options.Name}.Web/tsconfig.app.json", "{\n  \"extends\": \"@vue/tsconfig/tsconfig.dom.json\",\n  \"include\": [\"src/**/*.ts\", \"src/**/*.tsx\", \"src/**/*.vue\"],\n  \"compilerOptions\": {\n    \"composite\": true,\n    \"tsBuildInfoFile\": \"./node_modules/.tmp/tsconfig.app.tsbuildinfo\",\n    \"strict\": true,\n    \"target\": \"ES2022\",\n    \"lib\": [\"ES2022\", \"DOM\", \"DOM.Iterable\"],\n    \"moduleResolution\": \"Bundler\"\n  }\n}\n"),
            new($"src/{options.Name}.Web/tsconfig.node.json", "{\n  \"compilerOptions\": {\n    \"composite\": true,\n    \"tsBuildInfoFile\": \"./node_modules/.tmp/tsconfig.node.tsbuildinfo\",\n    \"module\": \"ESNext\",\n    \"moduleResolution\": \"Bundler\",\n    \"allowSyntheticDefaultImports\": true,\n    \"target\": \"ES2022\",\n    \"types\": [\"node\"]\n  },\n  \"include\": [\"vite.config.ts\"]\n}\n"),
            new($"src/{options.Name}.Web/vite.config.ts", ViteConfig()),
            new($"src/{options.Name}.Web/src/env.d.ts", "/// <reference types=\"vite/client\" />\n"),
            new($"src/{options.Name}.Web/src/main.ts", MainTs()),
            new($"src/{options.Name}.Web/src/App.vue", AppVue(options.Name)),
            new($"src/{options.Name}.Web/src/style.css", StyleCss()),
            new($"src/{options.Name}.Web/src/generated/.gitkeep", string.Empty),
            new($"tests/{options.Name}.Api.Tests/{options.Name}.Api.Tests.csproj", ApiTestsProject(options.Name)),
            new($"tests/{options.Name}.Api.Tests/HealthEndpointTests.cs", ApiTests(identifier)),
            new($"tests/{options.Name}.Api.Tests/Usings.cs", "global using Xunit;\n"),
            new($"{options.Name}.sln", Solution(options.Name))
        ];
    }

    private static string ApiProject(string name) => $$"""
    <Project Sdk="Microsoft.NET.Sdk.Web">
      <PropertyGroup>
        <TargetFramework>net8.0</TargetFramework>
        <Nullable>enable</Nullable>
        <ImplicitUsings>enable</ImplicitUsings>
        <RootNamespace>{{name.Replace('-', '_')}}</RootNamespace>
      </PropertyGroup>
      <ItemGroup>
        <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" />
      </ItemGroup>
    </Project>
    """;

    private static string PackageVersions() => """
    <Project>
      <PropertyGroup>
        <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
      </PropertyGroup>
      <ItemGroup>
        <PackageVersion Include="Microsoft.EntityFrameworkCore.Sqlite" Version="8.0.11" />
        <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
        <PackageVersion Include="xunit" Version="2.9.2" />
        <PackageVersion Include="xunit.runner.visualstudio" Version="2.8.2" />
      </ItemGroup>
    </Project>
    """;

    private static string ProjectReadme(string name) => $$"""
    # {{name}}

    This project was generated by Dotisan. It is a standard ASP.NET Core + Vue/Vite application with EF Core SQLite defaults.

    ~~~powershell
    dotnet build {{name}}.sln
    dotnet run --project src\{{name}}.Api
    ~~~

    Frontend development:

    ~~~powershell
    cd src\{{name}}.Web
    npm install
    npm run dev
    ~~~

    dotisan.config controls orchestration preferences only. Normal appsettings.json, environment variables, EF Core, and Vite configuration remain the source of truth for their respective concerns.
    """;

    private static string ApiProgram(string identifier) => $$"""
    using Microsoft.EntityFrameworkCore;
    using {{identifier}}.Api.Data;
    using {{identifier}}.Api.Infrastructure;

    var builder = WebApplication.CreateBuilder(args);
    builder.Services.AddProblemDetails();
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

    var app = builder.Build();
    app.UseExceptionHandler();
    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.MapDotisanEndpoints();
    app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }))
        .WithName("Health")
        .WithTags("System");
    app.MapFallbackToFile("index.html");
    app.Run();

    public partial class Program { }
    """;

    private static string DbContext(string identifier) => $$"""
    using Microsoft.EntityFrameworkCore;

    namespace {{identifier}}.Api.Data;

    public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options);
    """;

    private static string EndpointExtensions(string identifier) => $$"""
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Routing;

    namespace {{identifier}}.Api.Infrastructure;

    public static class DotisanEndpointExtensions
    {
        // Endpoint registration is intentionally explicit and inspectable.
        public static IEndpointRouteBuilder MapDotisanEndpoints(this IEndpointRouteBuilder endpoints) => endpoints;
    }
    """;

    private static string ApiTestsProject(string name) => $$"""
    <Project Sdk="Microsoft.NET.Sdk">
      <PropertyGroup>
        <TargetFramework>net8.0</TargetFramework>
        <IsPackable>false</IsPackable>
        <IsTestProject>true</IsTestProject>
        <Nullable>enable</Nullable>
        <ImplicitUsings>enable</ImplicitUsings>
        <NoWarn>CA1707;CS1591</NoWarn>
      </PropertyGroup>
      <ItemGroup>
        <PackageReference Include="Microsoft.NET.Test.Sdk" />
        <PackageReference Include="xunit" />
        <PackageReference Include="xunit.runner.visualstudio" />
        <ProjectReference Include="..\\..\\src\\{{name}}.Api\\{{name}}.Api.csproj" />
      </ItemGroup>
    </Project>
    """;

    private static string ApiTests(string identifier) => $$"""
    namespace {{identifier}}.Api.Tests;

    public sealed class HealthEndpointTests
    {
        [Fact]
        public void Generated_test_project_is_ready_for_endpoint_tests()
        {
            Assert.True(true);
        }
    }
    """;

    private static string WebIndex(string name) => $$"""
    <!doctype html>
    <html lang="en">
      <head><meta charset="UTF-8" /><meta name="viewport" content="width=device-width, initial-scale=1.0" /><title>{{name}}</title></head>
      <body><div id="app"></div><script type="module" src="/src/main.ts"></script></body>
    </html>
    """;

    private static string ViteConfig() => """
    import { fileURLToPath, URL } from 'node:url';
    import { defineConfig } from 'vite';
    import vue from '@vitejs/plugin-vue';

    export default defineConfig({
      plugins: [vue()],
      resolve: { alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) } },
      server: { port: 5173, proxy: { '/api': 'https://localhost:5001' } }
    });
    """;

    private static string MainTs() => """
    import { createApp } from 'vue';
    import { createPinia } from 'pinia';
    import App from './App.vue';
    import './style.css';

    createApp(App).use(createPinia()).mount('#app');
    """;

    private static string AppVue(string name) => """
    <script setup lang="ts">
    const apiStatus = 'ready';
    </script>

    <template>
      <main class="shell">
        <p class="eyebrow">Dotisan foundation</p>
        <h1>__PROJECT_NAME__</h1>
        <p>Your ASP.NET Core API and Vue frontend are ready for feature work.</p>
        <span class="status">API scaffold {{apiStatus}}</span>
      </main>
    </template>
    """.Replace("__PROJECT_NAME__", name, StringComparison.Ordinal);

    private static string StyleCss() => """
    :root { font-family: Inter, ui-sans-serif, system-ui, sans-serif; color: #172033; background: #f6f8fb; }
    body { margin: 0; }
    .shell { max-width: 720px; margin: 12vh auto; padding: 3rem; background: white; border: 1px solid #e2e8f0; border-radius: 1.25rem; box-shadow: 0 1rem 3rem #17203312; }
    .eyebrow { color: #2563eb; font-size: .8rem; font-weight: 700; letter-spacing: .12em; text-transform: uppercase; }
    h1 { margin: .5rem 0; font-size: clamp(2.25rem, 6vw, 4rem); }
    p { color: #526071; line-height: 1.6; }
    .status { display: inline-block; margin-top: 1.25rem; padding: .5rem .75rem; color: #166534; background: #dcfce7; border-radius: 999px; font-size: .9rem; font-weight: 600; }
    """;

    private static string Dockerfile(string name) => $$"""
    FROM node:22-alpine AS web-build
    WORKDIR /src
    COPY src/{{name}}.Web/package*.json ./
    RUN npm install
    COPY src/{{name}}.Web/ ./
    RUN npm run build

    FROM mcr.microsoft.com/dotnet/sdk:8.0 AS api-build
    WORKDIR /src
    COPY src/{{name}}.Api/{{name}}.Api.csproj src/{{name}}.Api/
    RUN dotnet restore src/{{name}}.Api/{{name}}.Api.csproj
    COPY . .
    COPY --from=web-build /src/dist src/{{name}}.Api/wwwroot
    RUN dotnet publish src/{{name}}.Api/{{name}}.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

    FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final
    WORKDIR /app
    COPY --from=api-build /app/publish .
    USER $APP_UID
    EXPOSE 8080
    HEALTHCHECK --interval=30s --timeout=5s CMD wget --spider --no-verbose http://localhost:8080/api/health || exit 1
    ENTRYPOINT ["dotnet", "{{name}}.Api.dll"]
    """;

    private static string Solution(string name) => $$"""
    Microsoft Visual Studio Solution File, Format Version 12.00
    # Visual Studio Version 17
    VisualStudioVersion = 17.0.31903.59
    MinimumVisualStudioVersion = 10.0.40219.1
    Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "{{name}}.Api", "src\\{{name}}.Api\\{{name}}.Api.csproj", "{B0000000-0000-0000-0000-000000000001}"
    EndProject
    Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "{{name}}.Api.Tests", "tests\\{{name}}.Api.Tests\\{{name}}.Api.Tests.csproj", "{B0000000-0000-0000-0000-000000000002}"
    EndProject
    Global
        GlobalSection(SolutionConfigurationPlatforms) = preSolution
            Debug|Any CPU = Debug|Any CPU
            Release|Any CPU = Release|Any CPU
        EndGlobalSection
        GlobalSection(ProjectConfigurationPlatforms) = postSolution
            {B0000000-0000-0000-0000-000000000001}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
            {B0000000-0000-0000-0000-000000000001}.Debug|Any CPU.Build.0 = Debug|Any CPU
            {B0000000-0000-0000-0000-000000000001}.Release|Any CPU.ActiveCfg = Release|Any CPU
            {B0000000-0000-0000-0000-000000000001}.Release|Any CPU.Build.0 = Release|Any CPU
            {B0000000-0000-0000-0000-000000000002}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
            {B0000000-0000-0000-0000-000000000002}.Debug|Any CPU.Build.0 = Debug|Any CPU
            {B0000000-0000-0000-0000-000000000002}.Release|Any CPU.ActiveCfg = Release|Any CPU
            {B0000000-0000-0000-0000-000000000002}.Release|Any CPU.Build.0 = Release|Any CPU
        EndGlobalSection
    EndGlobal
    """;
}
