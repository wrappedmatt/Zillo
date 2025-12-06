using System.Text.Json;
using Zillo.Domain.Entities;
using Zillo.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Zillo.Dashboard.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LocationsController : ControllerBase
{
    private readonly ILocationRepository _locationRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly ILogger<LocationsController> _logger;

    public LocationsController(
        ILocationRepository locationRepository,
        IAccountRepository accountRepository,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<LocationsController> logger)
    {
        _locationRepository = locationRepository;
        _accountRepository = accountRepository;
        _configuration = configuration;
        _httpClient = httpClientFactory.CreateClient();
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetLocations()
    {
        try
        {
            var account = await GetAccountFromToken();
            if (account == null)
                return Unauthorized(new { error = "Invalid or missing authentication token" });

            var locations = await _locationRepository.GetByAccountIdAsync(account.Id);

            return Ok(locations.Select(l => new
            {
                id = l.Id,
                name = l.Name,
                address = l.Address,
                latitude = l.Latitude,
                longitude = l.Longitude,
                relevantDistance = l.RelevantDistance,
                isActive = l.IsActive,
                createdAt = l.CreatedAt
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting locations");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetLocation(Guid id)
    {
        try
        {
            var account = await GetAccountFromToken();
            if (account == null)
                return Unauthorized(new { error = "Invalid or missing authentication token" });

            var location = await _locationRepository.GetByIdAsync(id);
            if (location == null || location.AccountId != account.Id)
                return NotFound(new { error = "Location not found" });

            return Ok(new
            {
                id = location.Id,
                name = location.Name,
                address = location.Address,
                latitude = location.Latitude,
                longitude = location.Longitude,
                relevantDistance = location.RelevantDistance,
                isActive = location.IsActive,
                createdAt = location.CreatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting location {LocationId}", id);
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateLocation([FromBody] CreateLocationRequest request)
    {
        try
        {
            var account = await GetAccountFromToken();
            if (account == null)
                return Unauthorized(new { error = "Invalid or missing authentication token" });

            var location = new Location
            {
                Id = Guid.NewGuid(),
                AccountId = account.Id,
                Name = request.Name,
                Address = request.Address,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                RelevantDistance = request.RelevantDistance ?? 100,
                IsActive = request.IsActive ?? true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var created = await _locationRepository.CreateAsync(location);

            return Ok(new
            {
                id = created.Id,
                name = created.Name,
                address = created.Address,
                latitude = created.Latitude,
                longitude = created.Longitude,
                relevantDistance = created.RelevantDistance,
                isActive = created.IsActive,
                createdAt = created.CreatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating location");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateLocation(Guid id, [FromBody] UpdateLocationRequest request)
    {
        try
        {
            var account = await GetAccountFromToken();
            if (account == null)
                return Unauthorized(new { error = "Invalid or missing authentication token" });

            var location = await _locationRepository.GetByIdAsync(id);
            if (location == null || location.AccountId != account.Id)
                return NotFound(new { error = "Location not found" });

            location.Name = request.Name;
            location.Address = request.Address;
            location.Latitude = request.Latitude;
            location.Longitude = request.Longitude;
            location.RelevantDistance = request.RelevantDistance ?? 100;
            location.IsActive = request.IsActive ?? true;
            location.UpdatedAt = DateTime.UtcNow;

            var updated = await _locationRepository.UpdateAsync(location);

            return Ok(new
            {
                id = updated.Id,
                name = updated.Name,
                address = updated.Address,
                latitude = updated.Latitude,
                longitude = updated.Longitude,
                relevantDistance = updated.RelevantDistance,
                isActive = updated.IsActive,
                createdAt = updated.CreatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating location {LocationId}", id);
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteLocation(Guid id)
    {
        try
        {
            var account = await GetAccountFromToken();
            if (account == null)
                return Unauthorized(new { error = "Invalid or missing authentication token" });

            var location = await _locationRepository.GetByIdAsync(id);
            if (location == null || location.AccountId != account.Id)
                return NotFound(new { error = "Location not found" });

            await _locationRepository.DeleteAsync(id);

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting location {LocationId}", id);
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("geocode")]
    public async Task<IActionResult> GeocodeAddress([FromBody] GeocodeRequest request)
    {
        try
        {
            var account = await GetAccountFromToken();
            if (account == null)
                return Unauthorized(new { error = "Invalid or missing authentication token" });

            if (string.IsNullOrWhiteSpace(request.Address))
                return BadRequest(new { error = "Address is required" });

            var apiKey = _configuration["Google:MapsApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogWarning("Google Maps API key not configured");
                return BadRequest(new { error = "Geocoding service not configured" });
            }

            var encodedAddress = Uri.EscapeDataString(request.Address);
            var url = $"https://maps.googleapis.com/maps/api/geocode/json?address={encodedAddress}&key={apiKey}";

            var response = await _httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();

            var jsonDoc = JsonDocument.Parse(content);
            var root = jsonDoc.RootElement;

            var status = root.GetProperty("status").GetString();
            if (status != "OK")
            {
                _logger.LogWarning("Geocoding failed with status: {Status}", status);
                return BadRequest(new { error = status == "ZERO_RESULTS" ? "Address not found" : "Geocoding failed" });
            }

            var results = root.GetProperty("results");
            if (results.GetArrayLength() == 0)
                return BadRequest(new { error = "Address not found" });

            var firstResult = results[0];
            var geometry = firstResult.GetProperty("geometry");
            var location = geometry.GetProperty("location");

            var lat = location.GetProperty("lat").GetDouble();
            var lng = location.GetProperty("lng").GetDouble();
            var formattedAddress = firstResult.GetProperty("formatted_address").GetString() ?? request.Address;

            return Ok(new GeocodeResult(lat, lng, formattedAddress));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error geocoding address");
            return BadRequest(new { error = "Failed to geocode address" });
        }
    }

    private async Task<Account?> GetAccountFromToken()
    {
        var authHeader = Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            return null;

        var token = authHeader["Bearer ".Length..];

        try
        {
            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);
            var supabaseUserId = jwtToken.Subject;

            if (string.IsNullOrEmpty(supabaseUserId))
                return null;

            return await _accountRepository.GetBySupabaseUserIdAsync(supabaseUserId);
        }
        catch
        {
            return null;
        }
    }
}

public record CreateLocationRequest(
    string Name,
    string? Address,
    double Latitude,
    double Longitude,
    double? RelevantDistance,
    bool? IsActive
);

public record UpdateLocationRequest(
    string Name,
    string? Address,
    double Latitude,
    double Longitude,
    double? RelevantDistance,
    bool? IsActive
);

public record GeocodeRequest(string Address);

public record GeocodeResult(
    double Latitude,
    double Longitude,
    string FormattedAddress
);
