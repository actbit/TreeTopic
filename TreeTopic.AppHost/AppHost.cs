using Aspire.Hosting.Keycloak;
using Microsoft.Extensions.Hosting;
using Projects;
var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin();

var tenantDb = postgres.AddDatabase("treetopic-tenants");
var appDb = postgres.AddDatabase("SharedApp");

var projectBuilder = builder.AddProject<TreeTopic>("treetopic")
    .WithReference(tenantDb)
    .WithReference(appDb)
    .WithHttpsEndpoint(port: 7047, name: "https-external", isProxied: false)
    .WithHttpEndpoint(port: 5266, name: "http-external", isProxied: false)
    .WithEnvironment("Authentication__PublicBaseUrl", "https://localhost:7047")
    .WaitFor(postgres);

if (builder.Environment.IsDevelopment())
{
    var keycloakAdminPassword = builder.AddParameter("keycloak-admin-password", secret: true);

    var keycloak = builder.AddKeycloak("keycloak", port: 8080, adminPassword: keycloakAdminPassword)
        .WithDataVolume();

    projectBuilder
        .WithReference(keycloak)
        .WaitFor(keycloak);
}

builder.Build().Run();
