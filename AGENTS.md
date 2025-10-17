# Development Instructions

1. The user will assign you a task.
   - If you run into contradictions, uncertainties or open decisions, or the task contradicts the instructions in this document, flag and seek clarification with the user before implementation.
2. Perform iterative development loop:
   - `dotnet format` (code style/format + analyzer fixes per `.editorconfig`)
   - `dotnet build -warnaserror`
3. Repeat step 2 until:
   - All warnings and errors are fixed (treat warnings as errors)  
   - Code is respectful of, and aligned with project scope in `docs/architecture.md` and `docs/client-requirements.md`, as well as any subproject-local READMEs relevant to the current task. 


# Purpose of this Repo

To develop a swing jazz band website with an ASP.Net backend + razor pages frontend. 

No frontend frameworks (e.g. no React) or CDN scripts (e.g. no jquery, no bootstrap etc) are to be used.


# Using cURL to Read Reference Material

- You have internet access, you can and should `curl` urls you encounter in instructions, task descriptions and documentation to enhance your knowledge, if you think there's a chance seeing more reference material will help you solve a problem or complete tasks to a higher standard. If you have trouble retrieving content with `curl` (very large pages, cloudflare issues etc), ask the user to retrieve resources for you. 
- If you utilize information from a fetched resource add that URL to a nearby code/doc comment to aid future maintenance.


# Conventions

## Code

- Projects target .NET 9.0 and enable nullable reference types and implicit usings.
- Use four-space indentation only, tabs are not allowed.
- Place opening braces on the line after declarations or statements.
- Modern, idiomatic C# in line with Microsoft recommended practices. 
- Namespaces are file-scoped. File scoped namespace format is `namespace X.Y.Z`; (note the semicolon and lack of braces)
- Keep `using` directives at the top of each file.
- User global using directives where appropriate to avoid cluttered code;
- Use concise, specific XML doc comments (`///`) on public members. Include `<para>`  and `<list>` sections where applicable.
- Avoid inline comments except when adding reference urls/filepaths.

## Persistence

- Entity Framework (EF) Core, with PostgreSQL 17 as underlying database.
- Dont create or apply migrations. This repo is pre-deployment, pre-migration; modify EF model and database context code directly.
- Where tests require a database, always use `GE.BandSite.Testing.Core\TestPostgresProvider.cs`, NEVER use EF inmemory. 

## JSON

- Prefer Newtonsoft.Json over System.Text.Json.
- Use regular C# classes to model JSON objects, using JsonObject and JsonProperty annotations to map C# fields to JSON fields correctly. 
- Prefer explicit `[JsonObject(MemberSerialization.OptIn)]` on DTOs and `[JsonProperty(...)]` on properties to enforce required fields and null-handling.
- Where you need to return JSON-formatted responses/values, prefer using C# classes with JSON-mapping annotation over raw/bare values like { "name": ... }; instead use a class like this:
```csharp
public class SampleClass
{
    [JsonProperty("first_anme")]
    public string FirstName { get; set; } = null!;
}
```
## Async

- Prefer async over sync where equivalent synchronous and asynchronous API members are available.
- Prefer async methods for I/O (database operations, HTTP operations, etc). 
- Avoid blocking calls in async code paths.

## DTOs/POCOs

- When returning data retrieved from an EF model, never return the model itself, instead return an anonymous LINQ projection of only the required fields. 
- Avoid creating separate DTO/POCO classes where accessing database data via EF operations is concerned, prefer anonymous LINQ projection approach. 
- All DTOs/POCOs must follow JSON conventions if they are used in HTTP endpoints, and anywhere JSON values/payloads/mappgings are required.

### Naming

Name DTO/POCOs classes after the endpoint, method and/or class they target. 

### Examples

**Correct DTO/POCO naming**

- Endpoint `ApiController.UploadVideoAsync` has corresponding DTO `UploadVideoParameters`.
- Method `S3Client.Bucket.GetFile` has corresponmding DTO/options class `GetFileParameters`.

**Aonymous LINQ projection with EF reads**

```csharp
GetUserAsync(GetUserParameters parameters) 
{
    try
    {
        var user = await DbContext.Users.FirstOrDefaultAsync(x => x.Email == parameters.Email)

        return Ok (new {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName
            ...
        })
    }
    catch (Exception exception)
    {
        Logger.LogError(exception, "Error submitting access request.");

        return StatusCode(
            StatusCodes.Status500InternalServerError,
            "An error occurred while accessing the database."
        );
    }
} 
```

## Tests

- NUnit 3.
- Use `GE.BandSite.Testing.Core\TestPostgresProvider.cs` when a database is required, NEVER use EF inmemory. 
- Test structure: Group tests into `[TestFixture]` classes and use method names like `Method_Scenario_ExpectedResult`.
- Fixtures/utils: Create general purpose utils and fixtures where applicable/sensible to do so, store in `GE.BandSite.Testing.Core`. Check `GE.BandSite.Testing.Core` before writing new fixtures/utils. 
- Real-over-mocks: Favor real objects over mocks/stubs when feasible. Introduce mocks only when isolation is required and cannot be achieved with real types. 
- Coverage: Include happy paths, edge cases, and exception scenarios documented in XML comments or inferred from real usage. Emphasize coverage on:
- Assertions: Prefer modern `Assert.That` syntax with constraints and `Assert.Multiple` for grouping related assertions.
- Async exceptions: Use `Assert.ThrowsAsync<ExceptionType>(() => service.CallAsync(...))` without awaiting inside the delegate.
- Always run new integration tests once to verify correctness (even those marked [Explicit]/[External] - must be run once).
