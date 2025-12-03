using Aspire.Hosting.Keycloak;
using Microsoft.Extensions.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin();

var tenantDb = postgres.AddDatabase("treetopic-tenants");
var appDb = postgres.AddDatabase("SharedApp");

var projectBuilder = builder.AddProject<Projects.TreeTopic>("treetopic")
    .WithReference(tenantDb)
    .WithReference(appDb)
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
