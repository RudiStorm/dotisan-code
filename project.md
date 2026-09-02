# Dotisan Command list

This file is retained as a lightweight command index. The authoritative user guide is `README.md`; the current quickstart is `docs/quickstart.md`.

## Create new Dotisan Project
```
dotisan new {projectname}
```

**What does this do?** 

1. Runs through a questionaire and generates dotisan.config 
2. dotnet new sln {project name}
3. dotnet new web "{projectname}.Api"
4. dotnet new classlib "{projectname}.Bll"
5. dotnet new classlib "{projectname}.Dal"
6. dotnet new classlib "{projectname}.Models"
7. dotnet new classlib "{projectname}.Integrations"
8. node -- version // this is to check if node is installed and the version is appropriate
9. Here we install a typescript version of the frontend that you would like including vanilla typescript
10. Then we setup the generator for the models to auto generate models as well as service end points for the frontend
11. We at some pont 

## Build and Run Commands
### Build
```
dotisan build [--no-frontend]
```
This builds the api project first, then generates the typescript models, then builds the typescript project.

### Run 
```
dotisan dev [--lean] [--observability] [--environment <name>]
```
This runs the API and Vue development servers together with hot reload.

## Make Commands
### Resource and endpoint scaffolding
```
dotisan make:resource {resourcename}
dotisan make:endpoint {endpointname}
dotisan make:crud {resourcename}
```

## Migrations
```
dotisan migrate
```

This runs migrations that havent run yet

```
dotisan migrate status
```

`dotisan new` does not create or apply migrations. Author migrations with `dotnet ef migrations add <Name> --project src/<Name>.Api`, review and commit them, then use `dotisan migrate` to apply existing migrations. To roll back safely, use the standard EF command `dotnet ef database update <MigrationName> --project src/<Name>.Api` after reviewing the target migration.

## Providers
Providers are explicit source/configuration recipes. `dotisan add:integration <name>` creates a reviewable recipe under `.dotisan/integrations`; package installation and registration remain ordinary .NET changes.

### Provider commands
```
dotisan add:integration <aspire|sendgrid|mailgun|signoz> [--dry-run]
dotisan remove:integration <name> --force
```

Providers we are looking at adding from the beginning:
* Mail trap
* Microsoft Exchange
* S3 buckets

When adding providers it can add multiple items. These can include controllers, database migrations, lookups, enums, and integrations, among others.


### Remove Provider
```
`remove:integration` removes only the Dotisan recipe file. It never attempts to remove packages or hand-edited source.
```

> [!WARNING]  
> When a provider is removed, we only remove items, that we can, there is still a chance that code is left behind, especially when code was altered and doesn't match our exact records. This also includes references to classes that have been removed. These will be caught if they are causing build errors


