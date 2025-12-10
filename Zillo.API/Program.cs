using Zillo.Application.Services;
using Zillo.Domain.Interfaces;
using Zillo.Infrastructure.Repositories;
using Zillo.Infrastructure.Services;
using Zillo.API.Middleware;
using Supabase;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using System.Text.Json;
using Asp.Versioning;

var builder = WebApplication.CreateBuilder(args);

// Load secrets from AWS Secrets Manager in production
if (builder.Environment.IsProduction())
{
    await LoadSecretsFromAwsAsync(builder);
}

// Configure Supabase
var supabaseUrl = builder.Configuration["Supabase:Url"] ?? throw new InvalidOperationException("Supabase URL is required");
var supabaseKey = builder.Configuration["Supabase:Key"] ?? throw new InvalidOperationException("Supabase Key is required");

var options = new SupabaseOptions
{
    AutoConnectRealtime = false
};

builder.Services.AddSingleton(provider =>
{
    var client = new Client(supabaseUrl, supabaseKey, options);
    client.InitializeAsync().Wait();
    return client;
});

// Configure Stripe
var stripeSecretKey = builder.Configuration["Stripe:SecretKey"] ?? throw new InvalidOperationException("Stripe Secret Key is required");
Stripe.StripeConfiguration.ApiKey = stripeSecretKey;

// Register repositories
builder.Services.AddScoped<IAccountRepository, AccountRepository>();
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<ITerminalRepository, TerminalRepository>();
builder.Services.AddScoped<ICardRepository, CardRepository>();
builder.Services.AddScoped<IUnclaimedTransactionRepository, UnclaimedTransactionRepository>();

// Register services
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<ITerminalService, TerminalService>();

// Add memory cache for terminal API key validation
builder.Services.AddMemoryCache();

// Add API versioning
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new HeaderApiVersionReader("X-Api-Version")
    );
})
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "Zillo API", Version = "v1" });
});

// Add CORS for terminal app
builder.Services.AddCors(options =>
{
    options.AddPolicy("TerminalPolicy", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("TerminalPolicy");

// Apply terminal authentication middleware to protected endpoints
app.UseWhen(
    context =>
    {
        var path = context.Request.Path.Value?.ToLower() ?? "";
        // Apply to endpoints that require terminal authentication
        return path.Contains("/payments/") ||
               path.Contains("/customers/") ||
               path.EndsWith("/branding") ||
               path.EndsWith("/connection-token");
    },
    appBuilder =>
    {
        appBuilder.UseTerminalAuth();
    });

app.UseAuthorization();

app.MapControllers();

app.Run();

// Helper function to load secrets
static async Task LoadSecretsFromAwsAsync(WebApplicationBuilder builder)
{
    var secretsValue = Environment.GetEnvironmentVariable("APP_SECRETS_ARN");
    if (string.IsNullOrEmpty(secretsValue))
    {
        Console.WriteLine("APP_SECRETS_ARN not set, skipping secrets loading");
        return;
    }

    try
    {
        string? secretsJson;

        if (secretsValue.TrimStart().StartsWith("{"))
        {
            secretsJson = secretsValue;
            Console.WriteLine("APP_SECRETS_ARN contains JSON, using directly");
        }
        else if (secretsValue.StartsWith("arn:aws:secretsmanager:"))
        {
            Console.WriteLine("APP_SECRETS_ARN contains ARN, fetching from Secrets Manager");
            using var client = new AmazonSecretsManagerClient();
            var response = await client.GetSecretValueAsync(new GetSecretValueRequest
            {
                SecretId = secretsValue
            });
            secretsJson = response.SecretString;
        }
        else
        {
            Console.WriteLine($"APP_SECRETS_ARN has unexpected format");
            return;
        }

        if (!string.IsNullOrEmpty(secretsJson))
        {
            var secrets = JsonSerializer.Deserialize<Dictionary<string, string>>(secretsJson);
            if (secrets != null)
            {
                var secretsDict = new Dictionary<string, string?>();
                foreach (var kvp in secrets)
                {
                    var key = kvp.Key.Replace("__", ":");
                    secretsDict[key] = kvp.Value;
                }

                builder.Configuration.AddInMemoryCollection(secretsDict);
                Console.WriteLine($"Loaded {secrets.Count} secrets successfully");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Warning: Failed to load secrets: {ex.Message}");
    }
}
