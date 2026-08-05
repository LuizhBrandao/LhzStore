var builder = DistributedApplication.CreateBuilder(args);

// 1. Adiciona o container do SQL Server e cria um banco chamado "sqldata"
var sql = builder.AddSqlServer("sql")
                 .AddDatabase("sqldata");

// 2. Injeta a string de conexão desse banco de dados na sua API
var apiService = builder.AddProject<Projects.MarketplaceApi_ApiService>("apiservice")
                        .WithReference(sql);

var webfrontend = builder.AddProject<Projects.MarketplaceApi_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(apiService);

builder.Build().Run();