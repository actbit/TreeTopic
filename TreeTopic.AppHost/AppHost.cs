var builder = DistributedApplication.CreateBuilder(args);

// Add PostgreSQL for tenant catalog
var postgres = builder.AddPostgres("postgres");

// Add TenantCatalog database
var tenantDb = postgres.AddDatabase("treetopic-tenants");

// Add SharedApp database (shared application data for all tenants)
var appDb = postgres.AddDatabase("SharedApp");

// Add TreeTopic project with database references
// Wait for PostgreSQL to be ready before starting the application
builder.AddProject<Projects.TreeTopic>("treetopic")
    .WithReference(tenantDb)
    .WithReference(appDb)
    .WaitFor(postgres);

builder.Build().Run();
