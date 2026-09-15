var builder = DistributedApplication.CreateBuilder(args);

// 1. Adiciona o container do PostgreSQL e cria o banco transacional "marketplacedb"
var postgres = builder.AddPostgres("postgres")
                      .WithPgAdmin()
                      .AddDatabase("marketplacedb");

// 2. Adiciona o container do Redis para Cache Distribuído, Output Cache e Locks
var redis = builder.AddRedis("redis")
                   .WithRedisCommander();

// 3. Adiciona o container do MinIO para Armazenamento de Fotos das Cartas (Object Storage)
var minio = builder.AddContainer("minio", "minio/minio", "latest")
                   .WithImageRegistry("quay.io")
                   .WithArgs("server", "/data", "--console-address", ":9001")
                   .WithEnvironment("MINIO_ROOT_USER", "admin")
                   .WithEnvironment("MINIO_ROOT_PASSWORD", "password123")
                   .WithHttpEndpoint(port: 9000, targetPort: 9000, name: "s3")
                   .WithHttpEndpoint(port: 9001, targetPort: 9001, name: "console");

// 4. Adiciona o container do Meilisearch para Busca Textual e Filtros de Catálogo
var meilisearch = builder.AddContainer("meilisearch", "getmeili/meilisearch", "v1.12")
                         .WithEnvironment("MEILI_NO_ANALYTICS", "true")
                         .WithEnvironment("MEILI_MASTER_KEY", "masterKey123")
                         .WithHttpEndpoint(port: 7700, targetPort: 7700, name: "meili-http");

// 5. Injeta as conexões na API e aguarda os recursos ficarem prontos
var apiService = builder.AddProject<Projects.MarketplaceApi_ApiService>("apiservice")
                        .WithReference(postgres)
                        .WithReference(redis)
                        .WithEnvironment("Storage__S3Endpoint", minio.GetEndpoint("s3"))
                        .WithEnvironment("Storage__AccessKey", "admin")
                        .WithEnvironment("Storage__SecretKey", "password123")
                        .WithEnvironment("Storage__BucketName", "lhzstore-cards")
                        .WithEnvironment("Meilisearch__Endpoint", meilisearch.GetEndpoint("meili-http"))
                        .WithEnvironment("Meilisearch__ApiKey", "masterKey123")
                        .WaitFor(postgres)
                        .WaitFor(redis);

var webfrontend = builder.AddProject<Projects.MarketplaceApi_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(apiService)
    .WaitFor(apiService); // Pode adicionar aqui também para o frontend esperar a API

builder.Build().Run();