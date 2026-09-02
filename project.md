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

`dotisan new` creates and applies the initial development migration automatically when restore is enabled. `dotisan migrate` applies existing migrations; production migrations must be reviewed and committed first.

## Providers
Providers are a set of classes that help a user connect to a set of providers much faster. For example one of the base providers will be mail trap, so an email notification system can be tested. Another is Microsoft Exchange, with classes prewritten, that easily allow you to send emails with the microsoft exchange.

### Planned provider commands
```
Provider installation commands are planned and are not implemented in v0.6.2.
```

Providers we are looking at adding from the beginning:
* Mail trap
* Microsoft Exchange
* S3 buckets

When adding providers it can add multiple items. These can include controllers, database migrations, lookups, enums, and integrations, among others.


### Remove Provider
```
`dotisan remove provider` is planned and is not implemented in v0.6.2.
```

> [!WARNING]  
> When a provider is removed, we only remove items, that we can, there is still a chance that code is left behind, especially when code was altered and doesn't match our exact records. This also includes references to classes that have been removed. These will be caught if they are causing build errors


