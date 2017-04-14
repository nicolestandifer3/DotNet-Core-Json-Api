# JSON API .Net Core

[![Build status](https://ci.appveyor.com/api/projects/status/9fvgeoxdikwkom10?svg=true)](https://ci.appveyor.com/project/jaredcnance/json-api-dotnet-core)
[![Travis](https://img.shields.io/travis/Research-Institute/json-api-dotnet-core.svg?maxAge=3600&label=travis)](https://travis-ci.org/Research-Institute/json-api-dotnet-core)
[![NuGet](https://img.shields.io/nuget/v/JsonApiDotNetCore.svg)](https://www.nuget.org/packages/JsonApiDotNetCore/)
[![MyGet CI](https://img.shields.io/myget/research-institute/vpre/JsonApiDotNetCore.svg)](https://www.myget.org/feed/research-institute/package/nuget/JsonApiDotNetCore)
[![Join the chat at https://gitter.im/json-api-dotnet-core/Lobby](https://badges.gitter.im/json-api-dotnet-core/Lobby.svg)](https://gitter.im/json-api-dotnet-core/Lobby?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

JsonApiDotnetCore provides a framework for building [json:api](http://jsonapi.org/) compliant web servers. Unlike other .Net implementations, this library provides all the required middleware to build a complete server. All you need to focus on is defining the resources. However, the library is also fully extensible so you can customize the implementation to meet your specific needs.

# Table Of Contents
- [Comprehensive Demo](#comprehensive-demo)
- [Installation](#installation)
- [Generators](#generators)
- [Usage](#usage)
	- [Middleware and Services](#middleware-and-services)
	- [Defining Models](#defining-models)
		- [Specifying Public Attributes](#specifying-public-attributes)
		- [Relationships](#relationships)
	- [Defining Controllers](#defining-controllers)
		- [Non-Integer Type Keys](#non-integer-type-keys)
	- [Routing](#routing)
		- [Namespacing and Versioning URLs](#namespacing-and-versioning-urls)
	- [Defining Custom Data Access Methods](#defining-custom-data-access-methods)
	- [Pagination](#pagination)
	- [Filtering](#filtering)
		- [Custom Filters](#custom-filters)
	- [Sorting](#sorting)
    - [Meta](#meta)
    - [Client Generated Ids](#client-generated-ids)
    - [Custom Errors](#custom-errors)
    - [Sparse Fieldsets](#sparse-fieldsets)
- [Tests](#tests)

## Comprehensive Demo

The following is a WIP demo showing how to create a web application using this library, EmberJS and PostgreSQL. If there are specific topics you'd like to see in future videos, comment on the playlist.

[![Goto Playlist](https://img.youtube.com/vi/KAMuo6K7VcE/0.jpg)](https://www.youtube.com/watch?v=KAMuo6K7VcE&list=PLu4Bq53iqJJAo1RF0TY4Q5qCG7n9AqSZf)

## Installation

- Visual Studio
```
Install-Package JsonApiDotnetCore
```

- project.json
```json
"JsonApiDotNetCore": "1.3.0"
```

- *.csproj
```xml
<ItemGroup>
    <!-- ... -->
    <PackageReference Include="JsonApiDotNetCore" Version="1.3.0" />
</ItemGroup>
```

Click [here](https://www.nuget.org/packages/JsonApiDotnetCore/) for the latest NuGet version.

For pre-releases, add the [MyGet](https://www.myget.org/feed/Details/research-institute) package feed 
(https://www.myget.org/F/research-institute/api/v3/index.json) 
to your nuget configuration.

## Generators

You can install the [Yeoman generators](https://github.com/Research-Institute/json-api-dotnet-core-generators) 
to make building applications much easier.

## Usage

You need to do 3 things:

- Add Middleware and Services
- Define Models
- Define Controllers

I recommend reading the details below, but once you're familiar with the
setup, you can use the Yeoman generator to generate the required classes.

### Middleware and Services

Add the following to your `Startup.ConfigureServices` method. 
Replace `AppDbContext` with your DbContext. 

```csharp
services.AddJsonApi<AppDbContext>();
```

Add the middleware to the `Startup.Configure` method. 
Note that under the hood, this will call `app.UseMvc()` 
so there is no need to add that as well.

```csharp
app.UseJsonApi();
```

### Defining Models

Your models should inherit `Identifiable<TId>` where `TId` is the type of the primary key, like so:

```csharp
public class Person : Identifiable<Guid>
{ }
```

You can use the non-generic `Identifiable` if your primary key is an integer:

```csharp
public class Person : Identifiable
{ }
```

If you need to hang annotations or attributes on the `Id` property, you can override the virtual member:

```csharp
public class Person : Identifiable
{ 
    [Key]
    [Column("person_id")]
    public override int Id { get; set; }
}
```

#### Specifying Public Attributes

If you want an attribute on your model to be publicly available, 
add the `AttrAttribute` and provide the outbound name.

```csharp
public class Person : Identifiable<int>
{
    [Attr("first-name")]
    public string FirstName { get; set; }
}
```

#### Relationships

In order for navigation properties to be identified in the model, 
they should be labeled with the appropriate attribute (either `HasOne` or `HasMany`).

```csharp
public class Person : Identifiable<int>
{
    [Attr("first-name")]
    public string FirstName { get; set; }

    [HasMany("todo-items")]
    public virtual List<TodoItem> TodoItems { get; set; }
}
```

Dependent relationships should contain a property in the form `{RelationshipName}Id`. 
For example, a `TodoItem` may have an `Owner` and so the Id attribute should be `OwnerId` like so:

```csharp
public class TodoItem : Identifiable<int>
{
    [Attr("description")]
    public string Description { get; set; }

    public int OwnerId { get; set; }

    [HasOne("owner")]
    public virtual Person Owner { get; set; }
}
```

### Defining Controllers

You need to create controllers that inherit from `JsonApiController<TEntity>` or `JsonApiController<TEntity, TId>`
where `TEntity` is the model that inherits from `Identifiable<TId>`.

```csharp
[Route("api/[controller]")]
public class ThingsController : JsonApiController<Thing>
{
    public ThingsController(
        IJsonApiContext jsonApiContext,
        IResourceService<Thing> resourceService,
        ILoggerFactory loggerFactory) 
    : base(jsonApiContext, resourceService, loggerFactory)
    { }
}
```

#### Non-Integer Type Keys

If your model is using a type other than `int` for the primary key,
you should explicitly declare it in the controller
and repository generic type definitions:

```csharp
[Route("api/[controller]")]
public class ThingsController : JsonApiController<Thing, Guid>
{
    public ThingsController(
        IJsonApiContext jsonApiContext,
        IResourceService<Thing, Guid> resourceService,
        ILoggerFactory loggerFactory) 
    : base(jsonApiContext, resourceService, loggerFactory)
    { }
}
```

### Routing

By default the library will configure routes for each controller. 
Based on the [recommendations](http://jsonapi.org/recommendations/)
outlined in the JSONAPI spec, routes are hyphenated. For example:

```
/todo-items --> TodoItemsController
NOT /todoItems
```

#### Namespacing and Versioning URLs

You can add a namespace to the URL by specifying it in `ConfigureServices`:

```csharp
services.AddJsonApi<AppDbContext>(
    opt => opt.Namespace = "api/v1");
```

### Defining Custom Data Access Methods

By default, data retrieval is distributed across 3 layers:

1. `JsonApiController`
2. `EntityResourceService`
3. `DefaultEntityRepository`

Customization can be done at any of these layers. However, it is recommended that you make your customizations at the service or the repository layer when possible to keep the controllers free of unnecessary logic.

#### Not Using Entity Framework?

Out of the box, the library uses your `DbContext` to create a "ContextGraph" or map of all your models and their relationships. If, however, you have models that are not members of a `DbContext`, you can manually create this graph like so:

```csharp
// Startup.cs
public void ConfigureServices(IServiceCollection services)
{
    // Add framework services.
    var mvcBuilder = services.AddMvc();

    services.AddJsonApi(options => {
        options.Namespace = "api/v1";
        options.BuildContextGraph((builder) => {
            builder.AddResource<MyModel>("my-models");
        });
    }, mvcBuilder);
    // ...
}
```

#### Custom Resource Service Implementation

By default, this library uses Entity Framework. If you'd like to use another ORM that does not implement `IQueryable`, you can inject a custom service like so:

```csharp
// Startup.cs
public void ConfigureServices(IServiceCollection services)
{
    services.AddScoped<IResourceService<MyModel>, MyModelService>();
    // ...
}
```

```csharp
// MyModelService.cs
public class MyModelService : IResourceService<MyModel>
{
    private readonly IMyModelDAL _dal;
    public MyModelService(IMyModelDAL dal)
    { 
        _dal = dal;
    } 
    public Task<IEnumerable<MyModel>> GetAsync()
    {
        return await _dal.GetModelAsync();
    }
}
```

#### Custom Entity Repository Implementation

If you want to use EF, but need additional data access logic (such as authorization), you can implement custom methods for accessing the data by creating an implementation of 
`IEntityRepository<TEntity, TId>`. If you only need minor changes you can override the 
methods defined in `DefaultEntityRepository<TEntity, TId>`. The repository should then be
add to the service collection in `Startup.ConfigureServices` like so:

```csharp
services.AddScoped<IEntityRepository<MyEntity,Guid>, MyAuthorizedEntityRepository>();
```

A sample implementation might look like:

```csharp
public class MyAuthorizedEntityRepository : DefaultEntityRepository<MyEntity>
{
    private readonly ILogger _logger;
    private readonly AppDbContext _context;
    private readonly IAuthenticationService _authenticationService;

    public MyAuthorizedEntityRepository(AppDbContext context,
        ILoggerFactory loggerFactory,
        IJsonApiContext jsonApiContext,
        IAuthenticationService authenticationService)
    : base(context, loggerFactory, jsonApiContext)
    { 
        _context = context;
        _logger = loggerFactory.CreateLogger<MyEntityRepository>();
        _authenticationService = authenticationService;
    }

    public override IQueryable<MyEntity> Get()
    {
        return base.Get().Where(e => e.UserId == _authenticationService.UserId);
    }
}
```

For more examples, take a look at the customization tests 
in `./test/JsonApiDotNetCoreExampleTests/Acceptance/Extensibility`.

### Pagination

Resources can be paginated. 
The following query would set the page size to 10 and get page 2.

```
?page[size]=10&page[number]=2
```

If you would like pagination implemented by default, you can specify the page size
when setting up the services:

```csharp
 services.AddJsonApi<AppDbContext>(
     opt => opt.DefaultPageSize = 10);
```

**Total Record Count**

The total number of records can be added to the document meta by setting it in the options:

```csharp
services.AddJsonApi<AppDbContext>(opt =>
{
    opt.DefaultPageSize = 5;
    opt.IncludeTotalRecordCount = true;
});
```

### Filtering

You can filter resources by attributes using the `filter` query parameter. 
By default, all attributes are filterable.
The filtering strategy we have selected, uses the following form:

```
?filter[attribute]=value
```

For operations other than equality, the query can be prefixed with an operation
identifier):

```
?filter[attribute]=eq:value
?filter[attribute]=lt:value
?filter[attribute]=gt:value
?filter[attribute]=le:value
?filter[attribute]=ge:value
?filter[attribute]=like:value
```

#### Custom Filters

You can customize the filter implementation by overriding the method in the `DefaultEntityRepository` like so:

```csharp
public class MyEntityRepository : DefaultEntityRepository<MyEntity>
{
    public MyEntityRepository(
    	AppDbContext context,
        ILoggerFactory loggerFactory,
        IJsonApiContext jsonApiContext)
    : base(context, loggerFactory, jsonApiContext)
    { }
    
    public override IQueryable<TEntity> Filter(IQueryable<TEntity> entities,  FilterQuery filterQuery)
    {
        // use the base filtering method    
        entities = base.Filter(entities, filterQuery);
	
	// implement custom method
	return ApplyMyCustomFilter(entities, filterQuery);
    }
}
```

### Sorting

Resources can be sorted by an attribute:

```
?sort=attribute // ascending
?sort=-attribute // descending
```

### Meta

Meta objects can be assigned in two ways:
 - Resource meta
 - Request Meta

Resource meta can be defined by implementing `IHasMeta` on the model class:

```csharp
public class Person : Identifiable<int>, IHasMeta
{
    // ...

    public Dictionary<string, object> GetMeta(IJsonApiContext context)
    {
        return new Dictionary<string, object> {
            { "copyright", "Copyright 2015 Example Corp." },
            { "authors", new string[] { "Jared Nance" } }
        };
    }
}
```

Request Meta can be added by injecting a service that implements `IRequestMeta`.
In the event of a key collision, the Request Meta will take precendence. 

### Client Generated Ids

By default, the server will respond with a `403 Forbidden` HTTP Status Code if a `POST` request is
received with a client generated id. However, this can be allowed by setting the `AllowClientGeneratedIds`
flag in the options:

```csharp
services.AddJsonApi<AppDbContext>(opt =>
{
    opt.AllowClientGeneratedIds = true;
    // ..
});
```

### Custom Errors

By default, errors will only contain the properties defined by the internal [Error](https://github.com/Research-Institute/json-api-dotnet-core/blob/master/src/JsonApiDotNetCore/Internal/Error.cs) class. However, you can create your own by inheriting from `Error` and either throwing it in a `JsonApiException` or returning the error from your controller.

```csharp
// custom error definition
public class CustomError : Error {
    public CustomError(string status, string title, string detail, string myProp)
    : base(status, title, detail)
    {
        MyCustomProperty = myProp;
    }
    public string MyCustomProperty { get; set; }
}

// throwing a custom error
public void MyMethod() {
    var error = new CustomError("507", "title", "detail", "custom");
    throw new JsonApiException(error);
}

// returning from controller
[HttpPost]
public override async Task<IActionResult> PostAsync([FromBody] MyEntity entity)
{
    if(_db.IsFull)
        return new ObjectResult(new CustomError("507", "Database is full.", "Theres no more room.", "Sorry."));

    // ...
}
```

### Sparse Fieldsets

We currently support top-level field selection. 
What this means is you can restrict which fields are returned by a query using the `fields` query parameter, but this does not yet apply to included relationships.

- Currently valid:
```http
GET /articles?fields[articles]=title,body HTTP/1.1
Accept: application/vnd.api+json
```

- Not yet supported:
```http
GET /articles?include=author&fields[articles]=title,body&fields[people]=name HTTP/1.1
Accept: application/vnd.api+json
```

## Tests

I am using DotNetCoreDocs to generate sample requests and documentation.

1. To run the tests, start a postgres server and verify the connection properties define in `/test/JsonApiDotNetCoreExampleTests/appsettings.json`
2. `cd ./test/JsonApiDotNetCoreExampleTests`
3. `dotnet test`
4. `cd ./src/JsonApiDotNetCoreExample`
5. `dotnet run`
6. `open http://localhost:5000/docs`
