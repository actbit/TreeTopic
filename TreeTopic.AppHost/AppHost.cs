var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.TreeTopic>("treetopic");

builder.Build().Run();
