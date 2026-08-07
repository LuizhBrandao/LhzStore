using System.Data;
using Microsoft.Data.SqlClient;
using System.Reflection;
using DbUp;
using MarketplaceApi.ApiService.Interfaces;
using MarketplaceApi.ApiService.Repositories;

var builder = WebApplication.CreateBuilder(args);

// =========================================================================
// 1. AJUSTE PARA O ASPIRE (IGNORAR ERRO DE CERTIFICADO LOCAL)
// =========================================================================
// Captura a connection string que o Aspire gerou e injetou dinamicamente
var connectionString = builder.Configuration.GetConnectionString("sqldata");

// Se ela existir e ainda não tiver o TrustServerCertificate, nós adicionamos
if (!string.IsNullOrEmpty(connectionString) && !connectionString.Contains("TrustServerCertificate"))
{
    connectionString += ";TrustServerCertificate=True;";
    // Sobrescreve a configuração na memória com o parâmetro novo
    builder.Configuration["ConnectionStrings:sqldata"] = connectionString;
}
// =========================================================================

// 2. Registra a conexão com o banco de dados gerenciado pelo Aspire (agora com o certificado confiável)
builder.AddSqlServerClient("sqldata");

builder.Services.AddScoped<IDbConnection>(sp => sp.GetRequiredService<SqlConnection>());

// (Mantenha seus outros registros aqui, como o AddEndpointsApiExplorer, Swagger, etc)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Registra o seu repositório (caso ainda não tenha feito)
builder.Services.AddScoped<IProductRepository, ProductRepository>();

var app = builder.Build();

// ==========================================
// 3. EXECUÇÃO DO DBUP (MIGRATIONS)
// ==========================================
using (var scope = app.Services.CreateScope())
{
    // Pega a string de conexão ATUALIZADA (com o TrustServerCertificate)
    var dbUpConnectionString = app.Configuration.GetConnectionString("sqldata");

    // Garante que o banco de dados exista dentro do container Docker
    EnsureDatabase.For.SqlDatabase(dbUpConnectionString);

    // Configura o DbUp para ler os arquivos .sql da pasta Migrations
    var upgrader = DeployChanges.To
        .SqlDatabase(dbUpConnectionString)
        .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
        .LogToConsole()
        .Build();

    // Roda os scripts no banco
    var result = upgrader.PerformUpgrade();

    if (!result.Successful)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Erro nas migrations: {result.Error}");
        Console.ResetColor();
    }
    else
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("Migrations executadas com sucesso!");
        Console.ResetColor();
    }
}
// ==========================================

// (Mantenha o restante do seu código: app.UseSwagger(), map dos endpoints, etc.)

app.MapDefaultEndpoints();
app.Run();