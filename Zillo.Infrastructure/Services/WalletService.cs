using Zillo.Application.Services;
using Zillo.Domain.Entities;
using Zillo.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Passbook.Generator;
using Passbook.Generator.Fields;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace Zillo.Infrastructure.Services;

public class WalletService : IWalletService
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly IWalletDeviceRegistrationRepository _walletDeviceRepository;
    private readonly ILocationRepository _locationRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WalletService> _logger;
    private readonly HttpClient _httpClient;

    public WalletService(
        ICustomerRepository customerRepository,
        IAccountRepository accountRepository,
        IWalletDeviceRegistrationRepository walletDeviceRepository,
        ILocationRepository locationRepository,
        IConfiguration configuration,
        ILogger<WalletService> logger,
        HttpClient httpClient)
    {
        _customerRepository = customerRepository;
        _accountRepository = accountRepository;
        _walletDeviceRepository = walletDeviceRepository;
        _locationRepository = locationRepository;
        _configuration = configuration;
        _logger = logger;
        _httpClient = httpClient;
    }

    #region Apple Wallet

    public async Task<byte[]> GenerateApplePassAsync(Guid customerId)
    {
        var customer = await _customerRepository.GetByIdAsync(customerId)
            ?? throw new ArgumentException("Customer not found");

        var account = await _accountRepository.GetByIdAsync(customer.AccountId)
            ?? throw new ArgumentException("Account not found");

        if (!account.WalletPassEnabled)
        {
            throw new InvalidOperationException("Wallet passes are disabled for this account");
        }

        // Ensure customer has a portal token for Apple Wallet authentication
        if (string.IsNullOrEmpty(customer.PortalToken))
        {
            customer.PortalToken = Guid.NewGuid().ToString();
            customer.PortalTokenExpiresAt = DateTime.UtcNow.AddYears(10); // Long-lived for wallet passes
            await _customerRepository.UpdateAsync(customer);
            _logger.LogInformation("Generated portal token for customer {CustomerId} for Apple Wallet pass", customerId);
        }

        return await CreateApplePassPackage(customer, account);
    }

    public async Task<byte[]> GetLatestApplePassAsync(string serialNumber, string authToken)
    {
        if (!await ValidateAuthTokenAsync(serialNumber, authToken))
        {
            throw new UnauthorizedAccessException("Invalid authentication token");
        }

        if (!Guid.TryParse(serialNumber, out var customerId))
        {
            throw new ArgumentException("Invalid serial number");
        }

        return await GenerateApplePassAsync(customerId);
    }

    public async Task RegisterAppleDeviceAsync(
        string deviceLibraryIdentifier,
        string pushToken,
        string passTypeIdentifier,
        string serialNumber,
        string authToken)
    {
        if (!await ValidateAuthTokenAsync(serialNumber, authToken))
        {
            throw new UnauthorizedAccessException("Invalid authentication token");
        }

        if (!Guid.TryParse(serialNumber, out var customerId))
        {
            throw new ArgumentException("Invalid serial number");
        }

        // Check if registration already exists
        var existing = await _walletDeviceRepository.GetByDeviceAndPassAsync(deviceLibraryIdentifier, serialNumber);

        if (existing != null)
        {
            // Update push token if changed
            if (existing.PushToken != pushToken)
            {
                existing.PushToken = pushToken;
                await _walletDeviceRepository.UpdateAsync(existing);
            }
            return;
        }

        // Create new registration
        var registration = new WalletDeviceRegistration
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            DeviceLibraryIdentifier = deviceLibraryIdentifier,
            PushToken = pushToken,
            WalletType = WalletType.Apple,
            PassIdentifier = serialNumber
        };

        await _walletDeviceRepository.CreateAsync(registration);
        _logger.LogInformation("Registered Apple device {DeviceId} for customer {CustomerId}", deviceLibraryIdentifier, customerId);
    }

    public async Task UnregisterAppleDeviceAsync(
        string deviceLibraryIdentifier,
        string passTypeIdentifier,
        string serialNumber,
        string authToken)
    {
        if (!await ValidateAuthTokenAsync(serialNumber, authToken))
        {
            throw new UnauthorizedAccessException("Invalid authentication token");
        }

        await _walletDeviceRepository.DeleteAsync(deviceLibraryIdentifier, serialNumber);
        _logger.LogInformation("Unregistered Apple device {DeviceId} for pass {SerialNumber}", deviceLibraryIdentifier, serialNumber);
    }

    public async Task<IEnumerable<string>> GetSerialNumbersForAppleDeviceAsync(string deviceLibraryIdentifier, string passTypeIdentifier)
    {
        return await _walletDeviceRepository.GetPassIdentifiersForDeviceAsync(deviceLibraryIdentifier, WalletType.Apple);
    }

    private async Task<byte[]> CreateApplePassPackage(Customer customer, Account account)
    {
        // Fetch active locations for the account
        var locations = await _locationRepository.GetActiveByAccountIdAsync(account.Id);

        var passTypeIdentifier = _configuration["Wallet:Apple:PassTypeIdentifier"] ?? "pass.com.wrapped.app";
        var teamIdentifier = _configuration["Wallet:Apple:TeamIdentifier"] ?? "";
        var webServiceUrl = "https://a5d9f5b24d06.ngrok-free.app"; // _configuration["Wallet:WebServiceUrl"] ?? "";
        var certPath = _configuration["Wallet:Apple:CertificatePath"];
        var certPassword = _configuration["Wallet:Apple:CertificatePassword"];
        var wwdrCertPath = _configuration["Wallet:Apple:WWDRCertificatePath"];

        // Format balance based on loyalty system type
        var balanceDisplay = account.LoyaltySystemType == "cashback"
            ? $"${customer.CashbackBalance / 100m:F2}"
            : $"${customer.PointsBalance}";

        var earnRateDisplay = account.LoyaltySystemType == "cashback"
            ? $"Earn {account.CashbackRate}% cashback on every purchase"
            : $"Earn ${account.PointsRate:F2} for every $10 spent";

        // Use custom description or generate default
        var description = !string.IsNullOrEmpty(account.WalletPassDescription)
            ? account.WalletPassDescription
            : $"{account.CompanyName} Loyalty Card";

        // Create pass generator request
        var generator = new PassGenerator();
        var request = new PassGeneratorRequest
        {
            PassTypeIdentifier = passTypeIdentifier,
            TeamIdentifier = teamIdentifier,
            SerialNumber = customer.Id.ToString(),
            Description = description,
            OrganizationName = account.CompanyName,
            LogoText = account.CompanyName,
            BackgroundColor = account.BrandingBackgroundColor,
            ForegroundColor = account.WalletPassForegroundColor,
            LabelColor = account.WalletPassLabelColor,
            Style = PassStyle.StoreCard,
            AuthenticationToken = customer.PortalToken!, // Guaranteed to exist after GenerateApplePassAsync check
            WebServiceUrl = $"{webServiceUrl}/api/wallet/apple"
        };

        // Load certificates
        if (!string.IsNullOrEmpty(certPath) && File.Exists(certPath))
        {
            request.PassbookCertificate = new X509Certificate2(certPath, certPassword);
        }

        if (!string.IsNullOrEmpty(wwdrCertPath) && File.Exists(wwdrCertPath))
        {
            request.AppleWWDRCACertificate = new X509Certificate2(wwdrCertPath);
        }

        // Add header field for balance
        var balanceField = new StandardField
        {
            Key = "balance",
            Value = balanceDisplay,
            Label = "BALANCE",
            ChangeMessage = "You have %@"
        };
        request.AddHeaderField(balanceField);

        // Add primary field for member name
        request.AddPrimaryField(new StandardField("name", "MEMBER", customer.Name));

        // Add secondary fields
        request.AddSecondaryField(new StandardField("company", "STORE", account.CompanyName));

        // Add announcement field as secondary - changeMessage will trigger notification
        var announcementMessage = customer.LastAnnouncementMessage ?? "Welcome!";
        var announcementField = new StandardField
        {
            Key = "message",
            Label = "Message",
            Value = announcementMessage,
            ChangeMessage = "%@"  // This will display the message value in the notification
        };
        request.AddBackField(announcementField);

        // Add back fields
        request.AddBackField(new StandardField("earnRate", "Earn Rate", earnRateDisplay));
        request.AddBackField(new StandardField("customerId", "Member ID", customer.Id.ToString()));

        // Add barcode with customer portal token
        request.AddBarcode(BarcodeType.PKBarcodeFormatQR, customer.PortalToken ?? customer.Id.ToString(), "ISO-8859-1", customer.PortalToken ?? customer.Id.ToString());

        // Add locations
        foreach (var location in locations.Take(10))
        {
            // Skip locations without coordinates
            if (!location.Latitude.HasValue || !location.Longitude.HasValue)
                continue;

            var relevantText = !string.IsNullOrEmpty(location.Name)
                ? $"Welcome to {location.Name}! Show your pass for rewards."
                : $"Welcome! Show your pass at {account.CompanyName} for rewards.";
            request.AddLocation(location.Latitude.Value, location.Longitude.Value, relevantText);
        }

        // Fetch and add icon images
        var iconUrl = !string.IsNullOrEmpty(account.WalletPassIconUrl)
            ? account.WalletPassIconUrl
            : account.BrandingLogoUrl;

        if (!string.IsNullOrEmpty(iconUrl))
        {
            try
            {
                var iconData = await ReadImageFromUrlAsync(iconUrl);
                if (iconData != null && iconData.Length > 0)
                {
                    request.Images.Add(PassbookImage.Icon, iconData);
                    request.Images.Add(PassbookImage.Icon2X, iconData);
                    request.Images.Add(PassbookImage.Icon3X, iconData);
                    request.Images.Add(PassbookImage.Logo, iconData);
                    request.Images.Add(PassbookImage.Logo2X, iconData);
                    request.Images.Add(PassbookImage.Logo3X, iconData);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch icon from {IconUrl}", iconUrl);
            }
        }

        // Generate the pass
        return generator.Generate(request);
    }

    private async Task<byte[]?> ReadImageFromUrlAsync(string imageUrl)
    {
        try
        {
            // Use HttpClient with proper headers to avoid 403 errors
            var request = new HttpRequestMessage(HttpMethod.Get, imageUrl);
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            request.Headers.Add("Accept", "image/png,image/jpeg,image/webp,image/*,*/*");
            request.Headers.Add("Referer", imageUrl);

            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsByteArrayAsync();
            }

            _logger.LogWarning("Failed to fetch image from {ImageUrl}: {StatusCode}", imageUrl, response.StatusCode);

            // Return a simple 29x29 transparent PNG as fallback
            return CreateFallbackIcon();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Exception fetching image from {ImageUrl}", imageUrl);
            return CreateFallbackIcon();
        }
    }

    private static byte[] CreateFallbackIcon()
    {
        // Simple 1x1 transparent PNG as fallback (Apple Wallet will scale it)
        return Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");
    }

    #endregion

    #region Google Wallet

    public async Task<string> GetGoogleWalletSaveUrlAsync(Guid customerId)
    {
        var customer = await _customerRepository.GetByIdAsync(customerId)
            ?? throw new ArgumentException("Customer not found");

        var account = await _accountRepository.GetByIdAsync(customer.AccountId)
            ?? throw new ArgumentException("Account not found");

        if (!account.WalletPassEnabled)
        {
            throw new InvalidOperationException("Wallet passes are disabled for this account");
        }

        var objectId = await CreateOrUpdateGooglePassAsync(customerId);
        var jwt = await CreateGoogleWalletJwt(objectId);

        return $"https://pay.google.com/gp/v/save/{jwt}";
    }

    public async Task<string> CreateOrUpdateGooglePassAsync(Guid customerId)
    {
        var customer = await _customerRepository.GetByIdAsync(customerId)
            ?? throw new ArgumentException("Customer not found");

        var account = await _accountRepository.GetByIdAsync(customer.AccountId)
            ?? throw new ArgumentException("Account not found");

        var issuerId = _configuration["Wallet:Google:IssuerId"] ?? "";
        var classId = $"{issuerId}.loyalty_{account.Id}";
        var objectId = $"{issuerId}.loyalty_{customer.Id}";

        // Ensure class exists for this account
        await EnsureGoogleLoyaltyClassExists(account, classId);

        // Create or update the loyalty object
        await CreateOrUpdateGoogleLoyaltyObject(customer, account, classId, objectId);

        return objectId;
    }

    private async Task EnsureGoogleLoyaltyClassExists(Account account, string classId)
    {
        var serviceAccountPath = _configuration["Wallet:Google:ServiceAccountKeyPath"];
        if (string.IsNullOrEmpty(serviceAccountPath) || !File.Exists(serviceAccountPath))
        {
            _logger.LogWarning("Google Wallet service account not configured");
            return;
        }

        try
        {
            var accessToken = await GetGoogleAccessToken();

            // Try to get existing class
            var getRequest = new HttpRequestMessage(HttpMethod.Get,
                $"https://walletobjects.googleapis.com/walletobjects/v1/loyaltyClass/{classId}");
            getRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var getResponse = await _httpClient.SendAsync(getRequest);

            if (getResponse.IsSuccessStatusCode)
            {
                // Class already exists
                return;
            }

            // Create new class - use wallet pass icon if set, otherwise fall back to branding logo
            var logoUrl = !string.IsNullOrEmpty(account.WalletPassIconUrl)
                ? account.WalletPassIconUrl
                : account.BrandingLogoUrl;

            var loyaltyClass = new
            {
                id = classId,
                issuerName = account.CompanyName,
                programName = $"{account.CompanyName} Rewards",
                programLogo = !string.IsNullOrEmpty(logoUrl) ? new
                {
                    sourceUri = new { uri = logoUrl },
                    contentDescription = new
                    {
                        defaultValue = new { language = "en", value = account.CompanyName }
                    }
                } : null,
                hexBackgroundColor = account.BrandingBackgroundColor,
                reviewStatus = "UNDER_REVIEW"
            };

            var createRequest = new HttpRequestMessage(HttpMethod.Post,
                "https://walletobjects.googleapis.com/walletobjects/v1/loyaltyClass");
            createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            createRequest.Content = new StringContent(
                JsonSerializer.Serialize(loyaltyClass),
                Encoding.UTF8,
                "application/json");

            var createResponse = await _httpClient.SendAsync(createRequest);
            if (!createResponse.IsSuccessStatusCode)
            {
                var error = await createResponse.Content.ReadAsStringAsync();
                _logger.LogError("Failed to create Google loyalty class: {Error}", error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ensuring Google loyalty class exists");
        }
    }

    private async Task CreateOrUpdateGoogleLoyaltyObject(Customer customer, Account account, string classId, string objectId)
    {
        var serviceAccountPath = _configuration["Wallet:Google:ServiceAccountKeyPath"];
        if (string.IsNullOrEmpty(serviceAccountPath) || !File.Exists(serviceAccountPath))
        {
            _logger.LogWarning("Google Wallet service account not configured");
            return;
        }

        try
        {
            var accessToken = await GetGoogleAccessToken();

            // Format balance
            var balanceMicros = account.LoyaltySystemType == "cashback"
                ? (long)(customer.CashbackBalance * 10000) // cents to micros
                : customer.PointsBalance * 1000000; // points as whole dollars in micros

            var loyaltyObject = new
            {
                id = objectId,
                classId,
                state = "ACTIVE",
                accountId = customer.Id.ToString(),
                accountName = customer.Name,
                loyaltyPoints = new
                {
                    label = account.LoyaltySystemType == "cashback" ? "Cash Balance" : "Points",
                    balance = account.LoyaltySystemType == "cashback"
                        ? (object)new { money = new { currencyCode = "USD", micros = balanceMicros } }
                        : new { @int = customer.PointsBalance }
                },
                barcode = new
                {
                    type = "QR_CODE",
                    value = customer.PortalToken ?? customer.Id.ToString()
                }
            };

            // Try to update first, then create if not exists
            var updateRequest = new HttpRequestMessage(HttpMethod.Put,
                $"https://walletobjects.googleapis.com/walletobjects/v1/loyaltyObject/{objectId}");
            updateRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            updateRequest.Content = new StringContent(
                JsonSerializer.Serialize(loyaltyObject),
                Encoding.UTF8,
                "application/json");

            var updateResponse = await _httpClient.SendAsync(updateRequest);

            if (updateResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Create new object
                var createRequest = new HttpRequestMessage(HttpMethod.Post,
                    "https://walletobjects.googleapis.com/walletobjects/v1/loyaltyObject");
                createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                createRequest.Content = new StringContent(
                    JsonSerializer.Serialize(loyaltyObject),
                    Encoding.UTF8,
                    "application/json");

                var createResponse = await _httpClient.SendAsync(createRequest);
                if (!createResponse.IsSuccessStatusCode)
                {
                    var error = await createResponse.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to create Google loyalty object: {Error}", error);
                }
            }
            else if (!updateResponse.IsSuccessStatusCode)
            {
                var error = await updateResponse.Content.ReadAsStringAsync();
                _logger.LogError("Failed to update Google loyalty object: {Error}", error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating/updating Google loyalty object");
        }
    }

    private async Task<string> CreateGoogleWalletJwt(string objectId)
    {
        var serviceAccountPath = _configuration["Wallet:Google:ServiceAccountKeyPath"];
        if (string.IsNullOrEmpty(serviceAccountPath) || !File.Exists(serviceAccountPath))
        {
            throw new InvalidOperationException("Google Wallet service account not configured");
        }

        var serviceAccountJson = await File.ReadAllTextAsync(serviceAccountPath);
        var serviceAccount = JsonSerializer.Deserialize<JsonElement>(serviceAccountJson);

        var privateKeyPem = serviceAccount.GetProperty("private_key").GetString()!;
        var clientEmail = serviceAccount.GetProperty("client_email").GetString()!;

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var header = new { alg = "RS256", typ = "JWT" };
        var payload = new
        {
            iss = clientEmail,
            aud = "google",
            typ = "savetowallet",
            iat = now,
            origins = new[] { _configuration["Wallet:WebServiceUrl"] ?? "" },
            payload = new
            {
                loyaltyObjects = new[]
                {
                    new { id = objectId }
                }
            }
        };

        var headerBase64 = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(header));
        var payloadBase64 = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload));
        var dataToSign = $"{headerBase64}.{payloadBase64}";

        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem);

        var signature = rsa.SignData(
            Encoding.UTF8.GetBytes(dataToSign),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        var signatureBase64 = Base64UrlEncode(signature);

        return $"{dataToSign}.{signatureBase64}";
    }

    private async Task<string> GetGoogleAccessToken()
    {
        var serviceAccountPath = _configuration["Wallet:Google:ServiceAccountKeyPath"];
        var serviceAccountJson = await File.ReadAllTextAsync(serviceAccountPath!);
        var serviceAccount = JsonSerializer.Deserialize<JsonElement>(serviceAccountJson);

        var privateKeyPem = serviceAccount.GetProperty("private_key").GetString()!;
        var clientEmail = serviceAccount.GetProperty("client_email").GetString()!;
        var tokenUri = serviceAccount.GetProperty("token_uri").GetString()!;

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var header = new { alg = "RS256", typ = "JWT" };
        var payload = new
        {
            iss = clientEmail,
            scope = "https://www.googleapis.com/auth/wallet_object.issuer",
            aud = tokenUri,
            iat = now,
            exp = now + 3600
        };

        var headerBase64 = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(header));
        var payloadBase64 = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload));
        var dataToSign = $"{headerBase64}.{payloadBase64}";

        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem);

        var signature = rsa.SignData(
            Encoding.UTF8.GetBytes(dataToSign),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        var signatureBase64 = Base64UrlEncode(signature);
        var jwt = $"{dataToSign}.{signatureBase64}";

        // Exchange JWT for access token
        var tokenRequest = new HttpRequestMessage(HttpMethod.Post, tokenUri);
        tokenRequest.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
            ["assertion"] = jwt
        });

        var tokenResponse = await _httpClient.SendAsync(tokenRequest);
        var tokenJson = await tokenResponse.Content.ReadAsStringAsync();
        var tokenData = JsonSerializer.Deserialize<JsonElement>(tokenJson);

        return tokenData.GetProperty("access_token").GetString()!;
    }

    private static string Base64UrlEncode(byte[] data)
    {
        return Convert.ToBase64String(data)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    #endregion

    #region Push Notifications

    public async Task SendBalanceUpdateNotificationsAsync(Guid customerId)
    {
        var registrations = await _walletDeviceRepository.GetByCustomerIdAsync(customerId);

        foreach (var registration in registrations)
        {
            try
            {
                if (registration.WalletType == WalletType.Apple)
                {
                    await SendApplePushNotification(registration.PushToken);
                }
                else if (registration.WalletType == WalletType.Google)
                {
                    // Google Wallet automatically updates when we call the API
                    // Just update the object to trigger a refresh
                    await CreateOrUpdateGooglePassAsync(customerId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send wallet notification to device {DeviceId}", registration.DeviceLibraryIdentifier);
            }
        }
    }

    public async Task SendCustomMessageNotificationAsync(Guid customerId, string message)
    {
        // Update customer with the last announcement message
        var customer = await _customerRepository.GetByIdAsync(customerId);
        if (customer != null)
        {
            customer.LastAnnouncementMessage = message;
            customer.LastAnnouncementAt = DateTime.UtcNow;
            customer.UpdatedAt = DateTime.UtcNow;
            await _customerRepository.UpdateAsync(customer);
        }

        var registrations = await _walletDeviceRepository.GetByCustomerIdAsync(customerId);

        foreach (var registration in registrations)
        {
            try
            {
                if (registration.WalletType == WalletType.Apple)
                {
                    await SendApplePushNotification(registration.PushToken, message);
                }
                // Note: Google Wallet doesn't support custom message notifications
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send custom message to device {DeviceId}", registration.DeviceLibraryIdentifier);
            }
        }
    }

    private async Task SendApplePushNotification(string pushToken, string? message = null)
    {
        var certPath = _configuration["Wallet:Apple:CertificatePath"];
        var certPassword = _configuration["Wallet:Apple:CertificatePassword"];

        if (string.IsNullOrEmpty(certPath) || !File.Exists(certPath))
        {
            _logger.LogWarning("Apple Wallet certificate not configured, skipping push notification");
            return;
        }

        try
        {
            // Apple Wallet uses HTTP/2 APNs
            // The push payload for Wallet passes is empty - just signals an update is available
            var certificate = new X509Certificate2(certPath, certPassword,
                X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet);

            // Extract topic from certificate subject (format: ...UID=pass.com.example.app...)
            var topic = _configuration["Wallet:Apple:PassTypeIdentifier"];
            if (string.IsNullOrEmpty(topic) && certificate.Subject.Contains("UID="))
            {
                var uidStart = certificate.Subject.LastIndexOf("UID=") + 4;
                var uidEnd = certificate.Subject.IndexOf(',', uidStart);
                topic = uidEnd > uidStart
                    ? certificate.Subject.Substring(uidStart, uidEnd - uidStart)
                    : certificate.Subject.Substring(uidStart);
            }

            var handler = new HttpClientHandler
            {
                ClientCertificateOptions = ClientCertificateOption.Manual
            };
            handler.ClientCertificates.Add(certificate);

            using var apnsClient = new HttpClient(handler);

            var request = new HttpRequestMessage(HttpMethod.Post,
                $"https://api.push.apple.com/3/device/{pushToken}");
            request.Version = new Version(2, 0);
            request.Headers.Add("apns-topic", topic);

            // Build push payload - either empty (balance update) or with message
            string payload;
            if (string.IsNullOrEmpty(message))
            {
                // Empty payload for balance updates - just triggers pass refresh
                payload = "{}";
            }
            else
            {
                // Payload with alert message for custom notifications
                payload = System.Text.Json.JsonSerializer.Serialize(new
                {
                    aps = new { alert = message }
                });
            }

            request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            _logger.LogInformation("Sending APNs push to production endpoint for device {PushToken} with topic {Topic} (hasMessage: {HasMessage})",
                pushToken.Substring(0, Math.Min(8, pushToken.Length)) + "...", topic, !string.IsNullOrEmpty(message));

            var response = await apnsClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("APNs push failed: {StatusCode} - {Error}", response.StatusCode, error);
            }
            else
            {
                _logger.LogInformation("APNs push notification sent successfully");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending Apple push notification");
        }
    }

    #endregion

    #region Utility

    public async Task<bool> ValidateAuthTokenAsync(string serialNumber, string authToken)
    {
        if (string.IsNullOrEmpty(authToken) || !Guid.TryParse(serialNumber, out var customerId))
        {
            return false;
        }

        var customer = await _customerRepository.GetByIdAsync(customerId);

        if (customer == null)
        {
            return false;
        }

        // Check if the auth token matches the customer's portal token
        return customer.PortalToken == authToken;
    }

    #endregion
}
