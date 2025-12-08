using Zillo.Application.Services;
using Zillo.Domain.Interfaces;
using Zillo.Infrastructure.Repositories;
using Zillo.Infrastructure.Services;
using Zillo.Rewards.Server.Middleware;
using Supabase;

var builder = WebApplication.CreateBuilder(args);

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

// Register services
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<ITerminalService, TerminalService>();
builder.Services.AddScoped<ICustomerPortalService, CustomerPortalService>();
builder.Services.AddScoped<IWalletService, WalletService>();

// Add HttpClient for wallet services (Google Wallet API calls)
builder.Services.AddHttpClient<IWalletService, WalletService>();

// Add memory cache for terminal API key validation
builder.Services.AddMemoryCache();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add CORS for development
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

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

app.UseCors();

// Apply terminal authentication middleware to Terminal endpoints (but not TerminalManagement pairing endpoints)
app.UseWhen(
    context =>
    {
        var path = context.Request.Path.Value?.ToLower() ?? "";
        // Apply to all /api/terminal endpoints (requires terminal authentication)
        // Exclude /api/terminalmanagement/pair and validate endpoints (public endpoints)
        return path.StartsWith("/api/terminal") &&
               !path.Contains("/pair") &&
               !path.Contains("/generate-pairing-code") &&
               !path.Contains("/validate");
    },
    appBuilder =>
    {
        appBuilder.UseTerminalAuth();
    });

app.UseAuthorization();

app.MapControllers();

app.MapFallbackToFile("/index.html");

app.Run();
