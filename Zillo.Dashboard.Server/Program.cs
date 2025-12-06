using Zillo.Application.Services;
using Zillo.Domain.Interfaces;
using Zillo.Infrastructure.Repositories;
using Zillo.Infrastructure.Services;
using Zillo.Dashboard.Server.Middleware;
using Supabase;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Load secrets from AWS Secrets Manager in production
if (builder.Environment.IsProduction())
{
    await LoadSecretsFromAwsAsync(builder.Configuration);
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
builder.Services.AddScoped<IUnclaimedTransactionRepository, UnclaimedTransactionRepository>();
builder.Services.AddScoped<IWalletDeviceRegistrationRepository, WalletDeviceRegistrationRepository>();
builder.Services.AddScoped<ILocationRepository, LocationRepository>();

// Register services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<ITerminalService, TerminalService>();
builder.Services.AddScoped<ICustomerPortalService, CustomerPortalService>();
builder.Services.AddScoped<IWalletService, WalletService>();

// Add HttpClient for wallet services (Google Wallet API calls)
builder.Services.AddHttpClient<IWalletService, WalletService>();

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

// Apply terminal authentication middleware to Terminal endpoints that require authentication
app.UseWhen(
    context =>
    {
        var path = context.Request.Path.Value?.ToLower() ?? "";
        // Apply to endpoints that require terminal authentication
        // connection_token doesn't need auth (Stripe handles it)
        // But create/capture/lookup/apply/capture_with_redemption do need auth
        return (path.StartsWith("/api/create_payment_intent") ||
                path.StartsWith("/api/update_payment_intent") ||
                path.StartsWith("/api/capture_payment_intent") ||
                path.StartsWith("/api/lookup_customer_credit") ||
                path.StartsWith("/api/apply_redemption") ||
                path.StartsWith("/api/capture_with_redemption") ||
                path.StartsWith("/api/terminal/branding"));
    },
    appBuilder =>
    {
        appBuilder.UseTerminalAuth();
    });

app.UseAuthorization();

app.MapControllers();

app.MapFallbackToFile("/index.html");

app.Run();

// Helper function to load secrets from AWS Secrets Manager
static async Task LoadSecretsFromAwsAsync(IConfiguration configuration)
{
    var secretsArn = Environment.GetEnvironmentVariable("APP_SECRETS_ARN");
    if (string.IsNullOrEmpty(secretsArn))
    {
        Console.WriteLine("APP_SECRETS_ARN not set, skipping AWS Secrets Manager");
        return;
    }

    try
    {
        using var client = new AmazonSecretsManagerClient();

        var response = await client.GetSecretValueAsync(new GetSecretValueRequest
        {
            SecretId = secretsArn
        });

        if (!string.IsNullOrEmpty(response.SecretString))
        {
            var secrets = JsonSerializer.Deserialize<Dictionary<string, string>>(response.SecretString);
            if (secrets != null)
            {
                foreach (var kvp in secrets)
                {
                    // Convert double underscore to colon for .NET configuration
                    var key = kvp.Key.Replace("__", ":");
                    Environment.SetEnvironmentVariable(key.Replace(":", "__"), kvp.Value);

                    // Also set directly in configuration memory
                    if (configuration is IConfigurationRoot configRoot)
                    {
                        // Use environment variable format
                        Environment.SetEnvironmentVariable(kvp.Key, kvp.Value);
                    }
                }
                Console.WriteLine($"Loaded {secrets.Count} secrets from AWS Secrets Manager");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Warning: Failed to load secrets from AWS: {ex.Message}");
    }
}
