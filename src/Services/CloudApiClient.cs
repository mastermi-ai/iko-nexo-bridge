using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using IkoNexoBridge.Configuration;
using IkoNexoBridge.Models;

namespace IkoNexoBridge.Services;

/// <summary>
/// HTTP client for IKO Cloud API communication
/// </summary>
public class CloudApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CloudApiClient> _logger;
    private readonly CloudApiSettings _settings;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public CloudApiClient(
        HttpClient httpClient,
        IOptions<CloudApiSettings> settings,
        ILogger<CloudApiClient> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;

        _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
        _httpClient.DefaultRequestHeaders.Add("X-Bridge-Api-Key", _settings.ApiKey);
        _httpClient.Timeout = TimeSpan.FromSeconds(_settings.RequestTimeoutSeconds);
    }

    /// <summary>
    /// Get all pending orders that need to be processed
    /// </summary>
    public async Task<List<CloudOrder>> GetPendingOrdersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching pending orders from Cloud API for client {ClientId}", _settings.ClientId);

            var response = await _httpClient.GetAsync($"/bridge/orders/pending?client_id={_settings.ClientId}", cancellationToken);
            response.EnsureSuccessStatusCode();

            var orders = await response.Content.ReadFromJsonAsync<List<CloudOrder>>(JsonOptions, cancellationToken);

            _logger.LogInformation("Fetched {Count} pending orders", orders?.Count ?? 0);
            return orders ?? new List<CloudOrder>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch pending orders from Cloud API");
            throw;
        }
    }

    /// <summary>
    /// Get a specific order by ID
    /// </summary>
    public async Task<CloudOrder?> GetOrderAsync(int orderId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/bridge/orders/{orderId}", cancellationToken);

            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<CloudOrder>(JsonOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch order {OrderId}", orderId);
            throw;
        }
    }

    /// <summary>
    /// Update order status after processing
    /// </summary>
    public async Task<bool> UpdateOrderStatusAsync(
        int orderId,
        string status,
        string? nexoDocId = null,
        string? errorMessage = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Updating order {OrderId} status to {Status}", orderId, status);

            var request = new UpdateOrderStatusRequest
            {
                Status = status,
                NexoDocId = nexoDocId,
                ErrorMessage = errorMessage
            };

            var response = await _httpClient.PatchAsJsonAsync(
                $"/bridge/orders/{orderId}/status",
                request,
                JsonOptions,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Order {OrderId} status updated to {Status}", orderId, status);
                return true;
            }

            _logger.LogWarning("Failed to update order {OrderId} status: {StatusCode}",
                orderId, response.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating order {OrderId} status", orderId);
            return false;
        }
    }

    /// <summary>
    /// Get all products from nexo for sync to Cloud API
    /// </summary>
    public async Task<bool> SyncProductsToCloudAsync(
        List<CloudProduct> products,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Syncing {Count} products to Cloud API for client {ClientId}", products.Count, _settings.ClientId);

            var payload = new { client_id = _settings.ClientId, products };
            var response = await _httpClient.PostAsJsonAsync(
                "/bridge/sync/products",
                payload,
                JsonOptions,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully synced products to Cloud API");
                return true;
            }

            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Failed to sync products: {Error}", error);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing products to Cloud API");
            return false;
        }
    }

    /// <summary>
    /// Sync customers from nexo to Cloud API
    /// </summary>
    public async Task<bool> SyncCustomersToCloudAsync(
        List<CloudCustomer> customers,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Syncing {Count} customers to Cloud API for client {ClientId}", customers.Count, _settings.ClientId);

            var payload = new { client_id = _settings.ClientId, customers };
            var response = await _httpClient.PostAsJsonAsync(
                "/bridge/sync/customers",
                payload,
                JsonOptions,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully synced customers to Cloud API");
                return true;
            }

            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Failed to sync customers: {Error}", error);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing customers to Cloud API");
            return false;
        }
    }

    /// <summary>
    /// Health check for Cloud API connection
    /// </summary>
    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/bridge/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
