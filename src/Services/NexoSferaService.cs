using System.Data.SqlClient;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using IkoNexoBridge.Configuration;
using IkoNexoBridge.Models;

namespace IkoNexoBridge.Services;

public class NexoSferaService : IDisposable
{
    private readonly ILogger<NexoSferaService> _logger;
    private readonly NexoProSettings _settings;
    private readonly SferaProxySettings _sferaProxySettings;
    private readonly HttpClient _httpClient;
    private bool _isConnected;
    private bool _disposed;
    private SqlConnection? _sqlConnection;

    // Retry settings
    private const int MaxRetries = 3;
    private static readonly TimeSpan[] RetryDelays = new[]
    {
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10)
    };

    public NexoSferaService(
        IOptions<NexoProSettings> settings,
        IOptions<SferaProxySettings> sferaProxySettings,
        ILogger<NexoSferaService> logger)
    {
        _settings = settings.Value;
        _sferaProxySettings = sferaProxySettings.Value;
        _logger = logger;
        // Increased timeout to handle Sfera SDK processing time
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
    }

    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_isConnected)
            return true;

        try
        {
            _logger.LogInformation("Connecting to nexo PRO: {Server}/{Database}",
                _settings.ServerName, _settings.DatabaseName);

            // Połączenie SQL do odczytu danych (produkty, klienci)
            var connectionString = BuildConnectionString();
            _sqlConnection = new SqlConnection(connectionString);
            await _sqlConnection.OpenAsync(cancellationToken);

            _isConnected = true;
            _logger.LogInformation("Connected to nexo PRO database (SQL)");

            // Sprawdź czy Sfera Proxy jest dostępne
            if (_sferaProxySettings.Enabled)
            {
                try
                {
                    var healthUrl = $"{_sferaProxySettings.Url}/health";
                    var response = await _httpClient.GetAsync(healthUrl, cancellationToken);
                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("Sfera Proxy is available at {Url}", _sferaProxySettings.Url);
                    }
                    else
                    {
                        _logger.LogWarning("Sfera Proxy returned {StatusCode} - documents creation may fail",
                            response.StatusCode);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Sfera Proxy not available at {Url}: {Error}",
                        _sferaProxySettings.Url, ex.Message);
                }
            }
            else
            {
                _logger.LogWarning("Sfera Proxy is DISABLED - documents will NOT be created in nexo PRO");
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to nexo PRO");
            _isConnected = false;
            return false;
        }
    }

    private string BuildConnectionString()
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = _settings.ServerName,
            InitialCatalog = _settings.DatabaseName,
            TrustServerCertificate = true,
            MultipleActiveResultSets = true
        };

        if (!string.IsNullOrEmpty(_settings.Username))
        {
            builder.UserID = _settings.Username;
            builder.Password = _settings.Password;
        }
        else
        {
            builder.IntegratedSecurity = true;
        }

        return builder.ConnectionString;
    }

    public async Task<OrderProcessingResult> CreateOrderDocumentAsync(
        CloudOrder order,
        CancellationToken cancellationToken = default)
    {
        var result = new OrderProcessingResult { OrderId = order.Id };

        if (!_isConnected)
        {
            result.ErrorMessage = "Not connected to nexo PRO";
            return result;
        }

        try
        {
            _logger.LogInformation("Creating ZK document for order #{OrderId}, Customer: {Customer}",
                order.Id, order.Customer?.Name ?? "NEW_CUSTOMER");

            // Jeśli Sfera Proxy jest włączone, użyj go do tworzenia dokumentu
            if (_sferaProxySettings.Enabled)
            {
                return await CreateOrderViaSferaProxyAsync(order, cancellationToken);
            }

            // Tryb testowy - bez Sfery
            _logger.LogWarning("SFERA PROXY DISABLED - document will not be created in nexo");
            await Task.Delay(100, cancellationToken);

            result.Success = true;
            result.NexoDocId = $"TEST-{order.Id}-{DateTime.Now:yyyyMMddHHmmss}";
            result.NexoDocNumber = $"ZK-TEST/{DateTime.Now:yyyy}/{order.Id:D5}";

            _logger.LogInformation("Simulated ZK {DocNumber} for order #{OrderId}",
                result.NexoDocNumber, order.Id);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create ZK for order #{OrderId}", order.Id);
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    private async Task<OrderProcessingResult> CreateOrderViaSferaProxyAsync(
        CloudOrder order,
        CancellationToken cancellationToken)
    {
        var result = new OrderProcessingResult { OrderId = order.Id };
        Exception? lastException = null;

        var requestBody = new
        {
            OrderId = order.Id,
            CustomerNexoId = order.Customer?.NexoId,
            CustomerName = order.Customer?.Name,
            Notes = order.Notes,
            Items = order.Items.Select(i => new
            {
                ProductCode = i.ProductCode,
                ProductName = i.ProductName,
                Quantity = i.Quantity,
                PriceNetto = i.PriceNetto,
                PriceBrutto = i.PriceBrutto,
                Notes = i.Notes
            }).ToArray()
        };

        var json = JsonSerializer.Serialize(requestBody);
        var url = $"{_sferaProxySettings.Url}/create-zk";

        // Retry loop with exponential backoff
        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                if (attempt > 0)
                {
                    var delay = RetryDelays[Math.Min(attempt - 1, RetryDelays.Length - 1)];
                    _logger.LogWarning("Retry {Attempt}/{MaxRetries} for order #{OrderId} after {Delay}s delay",
                        attempt, MaxRetries, order.Id, delay.TotalSeconds);
                    await Task.Delay(delay, cancellationToken);
                }

                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _logger.LogDebug("Sending request to Sfera Proxy: {Url} (attempt {Attempt})",
                    url, attempt + 1);

                var response = await _httpClient.PostAsync(url, content, cancellationToken);
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                _logger.LogDebug("Sfera Proxy response: {StatusCode} - {Body}",
                    response.StatusCode, responseBody);

                if (response.IsSuccessStatusCode)
                {
                    var proxyResult = JsonSerializer.Deserialize<SferaProxyResponse>(responseBody,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (proxyResult?.Success == true)
                    {
                        result.Success = true;
                        result.NexoDocId = proxyResult.DocumentId;
                        result.NexoDocNumber = proxyResult.DocumentNumber;

                        _logger.LogInformation("Created ZK {DocNumber} via Sfera Proxy for order #{OrderId} (attempt {Attempt})",
                            result.NexoDocNumber, order.Id, attempt + 1);

                        return result; // Success - exit immediately
                    }
                    else
                    {
                        // Sfera returned error - don't retry business logic errors
                        result.ErrorMessage = proxyResult?.ErrorMessage ?? "Unknown error from Sfera Proxy";
                        _logger.LogError("Sfera Proxy error: {Error}", result.ErrorMessage);
                        return result;
                    }
                }
                else if ((int)response.StatusCode >= 500)
                {
                    // Server error - retry
                    lastException = new HttpRequestException($"HTTP {response.StatusCode}");
                    _logger.LogWarning("Sfera Proxy server error {StatusCode}, will retry...", response.StatusCode);
                    continue;
                }
                else
                {
                    // Client error - don't retry
                    result.ErrorMessage = $"Sfera Proxy HTTP error: {response.StatusCode}";
                    _logger.LogError("Sfera Proxy HTTP error: {StatusCode} - {Body}",
                        response.StatusCode, responseBody);
                    return result;
                }
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                // Timeout - retry
                lastException = ex;
                _logger.LogWarning("Sfera Proxy timeout for order #{OrderId}, will retry...", order.Id);
                continue;
            }
            catch (HttpRequestException ex)
            {
                // Connection error - retry
                lastException = ex;
                _logger.LogWarning("Cannot connect to Sfera Proxy: {Error}, will retry...", ex.Message);
                continue;
            }
            catch (Exception ex)
            {
                // Other error - don't retry
                result.ErrorMessage = ex.Message;
                _logger.LogError(ex, "Error calling Sfera Proxy");
                return result;
            }
        }

        // All retries failed
        result.ErrorMessage = $"Failed after {MaxRetries + 1} attempts: {lastException?.Message ?? "Unknown error"}";
        _logger.LogError("Order #{OrderId} failed after all retries: {Error}", order.Id, result.ErrorMessage);

        return result;
    }

    private class SferaProxyResponse
    {
        public bool Success { get; set; }
        public int OrderId { get; set; }
        public string? DocumentId { get; set; }
        public string? DocumentNumber { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public async Task<List<CloudProduct>> GetProductsAsync(CancellationToken cancellationToken = default)
    {
        var products = new List<CloudProduct>();

        if (!_isConnected || _sqlConnection == null)
        {
            _logger.LogWarning("Cannot get products - not connected to nexo PRO");
            return products;
        }

        try
        {
            _logger.LogInformation("Fetching products with images from nexo PRO");

            // Pobiera produkty z dostępnym stanem + zdjęcia z galerii
            var query = @"
                SELECT
                    a.Id,
                    a.Symbol AS Code,
                    a.Nazwa AS Name,
                    a.Opis AS Description,
                    ISNULL(c.CenaNetto, 0) AS PriceNetto,
                    ISNULL(c.CenaBrutto, 0) AS PriceBrutto,
                    23 AS VatRate,
                    'szt' AS Unit,
                    a.Symbol AS Ean,
                    ISNULL(sm.IloscDostepna, 0) AS StockQuantity,
                    m.Dane AS ThumbnailData,
                    m.Typ AS ImageType
                FROM ModelDanychContainer.Asortymenty a
                LEFT JOIN ModelDanychContainer.WartosciCen c ON a.Id = c.Id
                LEFT JOIN ModelDanychContainer.StanyMagazynowe sm ON a.Id = sm.Asortyment_Id
                LEFT JOIN ModelDanychContainer.MediaDokumentElementy_MediaDokumentElement_Asortyment mda ON a.Id = mda.Obiekt_Id
                LEFT JOIN ModelDanychContainer.MediaDokumentElementy mde ON mda.Id = mde.Id
                LEFT JOIN ModelDanychContainer.DokumentyDane_MediaDokument md ON mde.MediaDokument_Id = md.Id
                LEFT JOIN ModelDanychContainer.Miniatury m ON md.Id = m.MediaDokument_Id
                WHERE a.IsInRecycleBin = 0
                  AND ISNULL(sm.IloscDostepna, 0) > 0
                ORDER BY a.Symbol";

            using var command = new SqlCommand(query, _sqlConnection);
            command.CommandTimeout = 120; // 2 minuty na duże zapytanie ze zdjęciami
            using var reader = await command.ExecuteReaderAsync(cancellationToken);

            int imagesCount = 0;
            while (await reader.ReadAsync(cancellationToken))
            {
                string? thumbnailBase64 = null;
                string? imageType = null;

                // Konwertuj dane binarne na base64
                if (reader["ThumbnailData"] != DBNull.Value)
                {
                    var imageBytes = (byte[])reader["ThumbnailData"];
                    thumbnailBase64 = Convert.ToBase64String(imageBytes);
                    imageType = reader["ImageType"]?.ToString() ?? "jpg";
                    imagesCount++;
                }

                products.Add(new CloudProduct
                {
                    NexoId = reader["Id"].ToString()!,
                    Code = reader["Code"]?.ToString() ?? "",
                    Name = reader["Name"]?.ToString() ?? "",
                    Description = reader["Description"]?.ToString(),
                    PriceNetto = Convert.ToDecimal(reader["PriceNetto"]),
                    PriceBrutto = Convert.ToDecimal(reader["PriceBrutto"]),
                    VatRate = Convert.ToDecimal(reader["VatRate"]),
                    Unit = reader["Unit"]?.ToString() ?? "szt",
                    Ean = reader["Ean"]?.ToString(),
                    Active = true,
                    ThumbnailBase64 = thumbnailBase64,
                    ImageType = imageType
                });
            }

            _logger.LogInformation("Fetched {Count} products ({ImagesCount} with images) from nexo PRO", 
                products.Count, imagesCount);
            return products;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch products from nexo PRO");
            return products;
        }
    }

    public async Task<List<CloudCustomer>> GetCustomersAsync(CancellationToken cancellationToken = default)
    {
        var customers = new List<CloudCustomer>();

        if (!_isConnected || _sqlConnection == null)
        {
            _logger.LogWarning("Cannot get customers - not connected to nexo PRO");
            return customers;
        }

        try
        {
            _logger.LogInformation("Fetching customers from nexo PRO");

            var query = @"
                SELECT
                    p.Id,
                    p.NazwaSkrocona AS Name,
                    p.NazwaSkrocona AS ShortName,
                    p.Telefon AS Phone1,
                    p.NIP AS Nip,
                    p.Aktywny AS Active,
                    p.LimitKredytuKupieckiego AS CreditLimit
                FROM ModelDanychContainer.Podmioty p
                WHERE p.Aktywny = 1 AND p.Kontrahent = 1
                ORDER BY p.NazwaSkrocona";

            using var command = new SqlCommand(query, _sqlConnection);
            using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                customers.Add(new CloudCustomer
                {
                    NexoId = reader["Id"].ToString()!,
                    Name = reader["Name"]?.ToString() ?? "",
                    ShortName = reader["ShortName"]?.ToString(),
                    Phone1 = reader["Phone1"]?.ToString(),
                    Nip = reader["Nip"]?.ToString()
                });
            }

            _logger.LogInformation("Fetched {Count} customers from nexo PRO", customers.Count);
            return customers;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch customers from nexo PRO");
            return customers;
        }
    }

    public bool IsConnected => _isConnected && (_sqlConnection?.State == System.Data.ConnectionState.Open);

    public async Task<CustomerBalance?> GetCustomerBalanceAsync(
        string nexoId,
        CancellationToken cancellationToken = default)
    {
        if (!_isConnected || _sqlConnection == null)
        {
            _logger.LogWarning("Cannot get customer balance - not connected to nexo PRO");
            return null;
        }

        try
        {
            var query = @"
                SELECT
                    p.Id,
                    p.NazwaSkrocona,
                    p.LimitKredytuKupieckiego AS CreditLimit,
                    0 AS Balance
                FROM ModelDanychContainer.Podmioty p
                WHERE p.Id = @NexoId";

            using var command = new SqlCommand(query, _sqlConnection);
            command.Parameters.AddWithValue("@NexoId", int.Parse(nexoId));

            using var reader = await command.ExecuteReaderAsync(cancellationToken);

            if (await reader.ReadAsync(cancellationToken))
            {
                return new CustomerBalance
                {
                    NexoId = nexoId,
                    Balance = Convert.ToDecimal(reader["Balance"]),
                    CreditLimit = reader["CreditLimit"] != DBNull.Value
                        ? Convert.ToDecimal(reader["CreditLimit"])
                        : null,
                    UpdatedAt = DateTime.UtcNow
                };
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch balance for customer {NexoId}", nexoId);
            return null;
        }
    }

    public async Task<List<CustomerBalance>> GetAllCustomerBalancesAsync(
        CancellationToken cancellationToken = default)
    {
        var balances = new List<CustomerBalance>();

        if (!_isConnected || _sqlConnection == null)
        {
            _logger.LogWarning("Cannot get customer balances - not connected to nexo PRO");
            return balances;
        }

        try
        {
            _logger.LogInformation("Fetching all customer balances from nexo PRO");

            var query = @"
                SELECT
                    p.Id AS NexoId,
                    p.LimitKredytuKupieckiego AS CreditLimit,
                    0 AS Balance
                FROM ModelDanychContainer.Podmioty p
                WHERE p.Aktywny = 1 AND p.Kontrahent = 1";

            using var command = new SqlCommand(query, _sqlConnection);
            using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                balances.Add(new CustomerBalance
                {
                    NexoId = reader["NexoId"].ToString()!,
                    Balance = Convert.ToDecimal(reader["Balance"]),
                    CreditLimit = reader["CreditLimit"] != DBNull.Value
                        ? Convert.ToDecimal(reader["CreditLimit"])
                        : null,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            _logger.LogInformation("Fetched balances for {Count} customers", balances.Count);
            return balances;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch customer balances");
            return balances;
        }
    }

    public async Task<ProductImage?> GetProductImageAsync(
        string nexoId,
        int maxWidth = 200,
        int maxHeight = 200,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Product images not yet implemented for nexo PRO schema");
        return null;
    }

    public async Task<List<string>> GetProductsWithImagesAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Product images not yet implemented for nexo PRO schema");
        return new List<string>();
    }

    /// <summary>
    /// Pobiera historię dokumentów sprzedaży (FS, PA, WZ) dla wszystkich kontrahentów
    /// </summary>
    public async Task<List<OrderHistoryDocument>> GetOrderHistoryAsync(
        int daysBack = 365,
        CancellationToken cancellationToken = default)
    {
        var documents = new List<OrderHistoryDocument>();

        if (!_isConnected || _sqlConnection == null)
        {
            _logger.LogWarning("Cannot get order history - not connected to nexo PRO");
            return documents;
        }

        try
        {
            _logger.LogInformation("Fetching order history from nexo PRO (last {Days} days)", daysBack);

            // Pobiera dokumenty sprzedaży: FS (Faktura), PA (Paragon), WZ (Wydanie), FP (Faktura proforma), ZK (Zamówienie)
            // Używamy poprawnych nazw kolumn z nexo PRO
            var query = @"
                SELECT 
                    d.Id AS NexoDocId,
                    d.PodmiotId AS NexoCustomerId,
                    ISNULL(d.NumerWewnetrzny_PelnaSygnatura, d.Symbol + ' ' + CAST(d.Id AS VARCHAR)) AS DocumentNumber,
                    d.Symbol AS DocumentType,
                    ISNULL(d.DataSprzedazy, d.DataWydaniaWystawienia) AS DocumentDate,
                    ISNULL(d.Wartosc_NettoPoRabacie, 0) AS TotalNetto,
                    ISNULL(d.Wartosc_BruttoPoRabacie, 0) AS TotalBrutto
                FROM ModelDanychContainer.Dokumenty d
                WHERE d.PodmiotId IS NOT NULL
                  AND d.Symbol IN ('FS', 'PA', 'WZ', 'FP', 'ZK')
                  AND ISNULL(d.DataSprzedazy, d.DataWydaniaWystawienia) >= DATEADD(day, -@DaysBack, GETDATE())
                ORDER BY ISNULL(d.DataSprzedazy, d.DataWydaniaWystawienia) DESC";

            using var command = new SqlCommand(query, _sqlConnection);
            command.Parameters.AddWithValue("@DaysBack", daysBack);
            command.CommandTimeout = 120;

            using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                documents.Add(new OrderHistoryDocument
                {
                    NexoDocId = reader["NexoDocId"].ToString()!,
                    NexoCustomerId = reader["NexoCustomerId"].ToString()!,
                    DocumentNumber = reader["DocumentNumber"]?.ToString() ?? "",
                    DocumentType = reader["DocumentType"]?.ToString() ?? "",
                    DocumentDate = Convert.ToDateTime(reader["DocumentDate"]),
                    TotalNetto = Convert.ToDecimal(reader["TotalNetto"]),
                    TotalBrutto = Convert.ToDecimal(reader["TotalBrutto"])
                });
            }

            _logger.LogInformation("Fetched {Count} order history documents from nexo PRO", documents.Count);
            return documents;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch order history from nexo PRO");
            return documents;
        }
    }

    public void Disconnect()
    {
        if (!_isConnected)
            return;

        try
        {
            _logger.LogInformation("Disconnecting from nexo PRO");

            _sqlConnection?.Close();
            _sqlConnection?.Dispose();
            _sqlConnection = null;

            _isConnected = false;
            _logger.LogInformation("Disconnected from nexo PRO");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disconnecting from nexo PRO");
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Disconnect();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
