var builder = DistributedApplication.CreateBuilder(args);

// 1. Adiciona o container do PostgreSQL e cria o banco transacional "marketplacedb"
var postgres = builder.AddPostgres("postgres")
                      .WithPgAdmin()
                      .AddDatabase("marketplacedb");

// 2. Injeta a conexão do banco de dados na API e aguarda o container iniciar
var apiService = builder.AddProject<Projects.MarketplaceApi_ApiService>("apiservice")
                        .WithReference(postgres)
                        .WaitFor(postgres);

var webfrontend = builder.AddProject<Projects.MarketplaceApi_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(apiService)
    .WaitFor(apiService); // Pode adicionar aqui também para o frontend esperar a API

builder.Build().Run();