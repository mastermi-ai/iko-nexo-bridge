using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using IkoNexoBridge.Configuration;
using IkoNexoBridge.Models;

namespace IkoNexoBridge.Services;

/// <summary>
/// Background worker that processes pending orders from Cloud API and creates documents in nexo PRO
/// </summary>
public class OrderProcessingWorker : BackgroundService
{
    private readonly ILogger<OrderProcessingWorker> _logger;
    private readonly CloudApiClient _cloudApi;
    private readonly NexoSferaService _nexoService;
    private readonly CloudApiSettings _apiSettings;
    private readonly SyncSettings _syncSettings;

    private DateTime _lastProductSync = DateTime.MinValue;
    private DateTime _lastCustomerSync = DateTime.MinValue;

    public OrderProcessingWorker(
        ILogger<OrderProcessingWorker> logger,
        CloudApiClient cloudApi,
        NexoSferaService nexoService,
        IOptions<CloudApiSettings> apiSettings,
        IOptions<SyncSettings> syncSettings)
    {
        _logger = logger;
        _cloudApi = cloudApi;
        _nexoService = nexoService;
        _apiSettings = apiSettings.Value;
        _syncSettings = syncSettings.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("IKO Nexo Bridge Worker starting...");

        // Initial connection to nexo PRO
        await InitializeNexoConnectionAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Ensure nexo connection is alive
                if (!_nexoService.IsConnected)
                {
                    await InitializeNexoConnectionAsync(stoppingToken);
                }

                // Process pending orders
                if (_syncSettings.SyncOrdersEnabled)
                {
                    await ProcessPendingOrdersAsync(stoppingToken);
                }

                // Sync products from nexo to cloud (periodic)
                if (_syncSettings.SyncProductsEnabled && ShouldSyncProducts())
                {
                    await SyncProductsAsync(stoppingToken);
                }

                // Sync customers from nexo to cloud (periodic)
                if (_syncSettings.SyncCustomersEnabled && ShouldSyncCustomers())
                {
                    await SyncCustomersAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in worker loop");
            }

            // Wait before next polling cycle
            await Task.Delay(
                TimeSpan.FromSeconds(_apiSettings.PollingIntervalSeconds),
                stoppingToken);
        }

        _logger.LogInformation("IKO Nexo Bridge Worker stopping...");
        _nexoService.Disconnect();
    }

    private async Task InitializeNexoConnectionAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Initializing connection to nexo PRO...");

        var connected = await _nexoService.ConnectAsync(cancellationToken);

        if (!connected)
        {
            _logger.LogWarning("Failed to connect to nexo PRO. Will retry on next cycle.");
        }
    }

    private async Task ProcessPendingOrdersAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Check Cloud API health
            var apiHealthy = await _cloudApi.HealthCheckAsync(cancellationToken);
            if (!apiHealthy)
            {
                _logger.LogWarning("Cloud API is not responding. Skipping order processing.");
                return;
            }

            // Fetch pending orders
            var pendingOrders = await _cloudApi.GetPendingOrdersAsync(cancellationToken);

            if (pendingOrders.Count == 0)
            {
                _logger.LogDebug("No pending orders to process");
                return;
            }

            _logger.LogInformation("Processing {Count} pending orders", pendingOrders.Count);

            foreach (var order in pendingOrders)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                await ProcessSingleOrderAsync(order, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing pending orders");
        }
    }

    private async Task ProcessSingleOrderAsync(CloudOrder order, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing order #{OrderId} ({CustomerName})",
            order.Id, order.Customer?.Name ?? "Unknown");

        try
        {
            // Mark as processing
            await _cloudApi.UpdateOrderStatusAsync(
                order.Id,
                "processing",
                cancellationToken: cancellationToken);

            // Create document in nexo PRO
            var result = await _nexoService.CreateOrderDocumentAsync(order, cancellationToken);

            if (result.Success)
            {
                // Mark as completed
                await _cloudApi.UpdateOrderStatusAsync(
                    order.Id,
                    "completed",
                    result.NexoDocId,
                    cancellationToken: cancellationToken);

                _logger.LogInformation(
                    "Order #{OrderId} successfully processed. Nexo document: {DocNumber}",
                    order.Id, result.NexoDocNumber);
            }
            else
            {
                // Mark as failed
                await _cloudApi.UpdateOrderStatusAsync(
                    order.Id,
                    "failed",
                    errorMessage: result.ErrorMessage,
                    cancellationToken: cancellationToken);

                _logger.LogWarning(
                    "Order #{OrderId} processing failed: {Error}",
                    order.Id, result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception processing order #{OrderId}", order.Id);

            await _cloudApi.UpdateOrderStatusAsync(
                order.Id,
                "failed",
                errorMessage: ex.Message,
                cancellationToken: cancellationToken);
        }
    }

    private bool ShouldSyncProducts()
    {
        var elapsed = DateTime.UtcNow - _lastProductSync;
        return elapsed.TotalMinutes >= _syncSettings.ProductsSyncIntervalMinutes;
    }

    private bool ShouldSyncCustomers()
    {
        var elapsed = DateTime.UtcNow - _lastCustomerSync;
        return elapsed.TotalMinutes >= _syncSettings.CustomersSyncIntervalMinutes;
    }

    private async Task SyncProductsAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting products sync from nexo PRO to Cloud API");

            var products = await _nexoService.GetProductsAsync(cancellationToken);

            if (products.Count > 0)
            {
                var success = await _cloudApi.SyncProductsToCloudAsync(products, cancellationToken);

                if (success)
                {
                    _logger.LogInformation("Successfully synced {Count} products", products.Count);
                }
            }

            _lastProductSync = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing products");
        }
    }

    private async Task SyncCustomersAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting customers sync from nexo PRO to Cloud API");

            var customers = await _nexoService.GetCustomersAsync(cancellationToken);

            if (customers.Count > 0)
            {
                var success = await _cloudApi.SyncCustomersToCloudAsync(customers, cancellationToken);

                if (success)
                {
                    _logger.LogInformation("Successfully synced {Count} customers", customers.Count);
                }
            }

            _lastCustomerSync = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing customers");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping IKO Nexo Bridge Worker...");
        _nexoService.Disconnect();
        await base.StopAsync(cancellationToken);
    }
}
