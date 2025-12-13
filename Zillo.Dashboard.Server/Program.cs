using Zillo.Application.Services;
using Zillo.Domain.Interfaces;
using Zillo.Infrastructure.Repositories;
using Zillo.Infrastructure.Services;
using Supabase;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Load secrets from AWS Secrets Manager in production
if (builder.Environment.IsProduction())
{
    await LoadSecretsFromAwsAsync(builder);
}

// Add services to the container.

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
builder.Services.AddScoped<ILocationRepository, LocationRepository>();
builder.Services.AddScoped<IAccountUserRepository, AccountUserRepository>();
builder.Services.AddScoped<IExternalLocationRepository, ExternalLocationRepository>();

// Register services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<ITerminalService, TerminalService>();
builder.Services.AddScoped<IStripeConnectService, StripeConnectService>();
builder.Services.AddScoped<IAccountSwitchService, AccountSwitchService>();

// Add HttpClient factory for geocoding and other HTTP calls
builder.Services.AddHttpClient();

// Add memory cache for terminal API key validation
builder.Services.AddMemoryCache();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.MapFallbackToFile("/index.html");

app.Run();

// Helper function to load secrets - handles both direct JSON injection and Secrets Manager ARN
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

        // Check if the value is a JSON object (injected by App Runner) or an ARN (needs fetching)
        if (secretsValue.TrimStart().StartsWith("{"))
        {
            // Value is already JSON - App Runner injected the secret value directly
            secretsJson = secretsValue;
            Console.WriteLine("APP_SECRETS_ARN contains JSON, using directly");
        }
        else if (secretsValue.StartsWith("arn:aws:secretsmanager:"))
        {
            // Value is an ARN - fetch from Secrets Manager
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
            Console.WriteLine($"APP_SECRETS_ARN has unexpected format: {secretsValue.Substring(0, Math.Min(50, secretsValue.Length))}...");
            return;
        }

        if (!string.IsNullOrEmpty(secretsJson))
        {
            var secrets = JsonSerializer.Deserialize<Dictionary<string, string>>(secretsJson);
            if (secrets != null)
            {
                // Add secrets as in-memory configuration source
                var secretsDict = new Dictionary<string, string?>();
                foreach (var kvp in secrets)
                {
                    // Convert double underscore to colon for .NET configuration hierarchy
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
