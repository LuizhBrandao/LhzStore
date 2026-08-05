using System.Data;
using Microsoft.Data.SqlClient;
using System.Reflection;
using DbUp;
using MarketplaceApi.ApiService.Interfaces;
using MarketplaceApi.ApiService.Repositories;

var builder = WebApplication.CreateBuilder(args);

// 1. Registra a conexão com o banco de dados gerenciado pelo Aspire
builder.AddSqlServerClient("sqldata");

builder.Services.AddScoped<IDbConnection>(sp => sp.GetRequiredService<SqlConnection>());

// (Mantenha seus outros registros aqui, como o AddEndpointsApiExplorer, Swagger, etc)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Registra o seu repositório (caso ainda não tenha feito)
builder.Services.AddScoped<IProductRepository, ProductRepository>();

var app = builder.Build();

// ==========================================
// 2. EXECUÇÃO DO DBUP (MIGRATIONS)
// ==========================================
using (var scope = app.Services.CreateScope())
{
    // Pega a string de conexão que o Aspire injetou magicamente
    var connectionString = builder.Configuration.GetConnectionString("sqldata");

    // Garante que o banco de dados exista dentro do container Docker
    EnsureDatabase.For.SqlDatabase(connectionString);

    // Configura o DbUp para ler os arquivos .sql da pasta Migrations
    var upgrader = DeployChanges.To
        .SqlDatabase(connectionString)
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