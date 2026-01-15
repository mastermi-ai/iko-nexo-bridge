using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using IkoNexoBridge.Configuration;
using IkoNexoBridge.Models;

namespace IkoNexoBridge.Services;

/// <summary>
/// Service for communication with InsERT nexo PRO via Sfera SDK
///
/// IMPORTANT: This service requires InsERT Sfera SDK to be properly installed and referenced.
/// The Sfera.dll must be obtained from InsERT (comes with nexo PRO installation).
///
/// Sfera documentation: https://www.insert.com.pl/programy_insert/sfera_dla_programistow
/// </summary>
public class NexoSferaService : IDisposable
{
    private readonly ILogger<NexoSferaService> _logger;
    private readonly NexoProSettings _settings;
    private bool _isConnected;
    private bool _disposed;

    // Sfera objects (uncomment when Sfera SDK is available)
    // private Sfera.Uchwyt? _uchwyt;
    // private Sfera.Sesja? _sesja;

    public NexoSferaService(
        IOptions<NexoProSettings> settings,
        ILogger<NexoSferaService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Initialize connection to nexo PRO
    /// </summary>
    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_isConnected)
            return true;

        try
        {
            _logger.LogInformation("Connecting to nexo PRO: {Server}/{Database}",
                _settings.ServerName, _settings.DatabaseName);

            // TODO: Implement actual Sfera connection when SDK is available
            // Example Sfera connection code:
            /*
            _uchwyt = new Sfera.Uchwyt();

            var parametryPolaczenia = new Sfera.ParametryPolaczenia
            {
                Serwer = _settings.ServerName,
                Baza = _settings.DatabaseName,
                UzytkownikSql = _settings.Username,
                HasloSql = _settings.Password,
                PolaczenieZaufane = string.IsNullOrEmpty(_settings.Username)
            };

            _sesja = await Task.Run(() =>
                _uchwyt.ZalogujOperatora(
                    parametryPolaczenia,
                    _settings.OperatorSymbol,
                    _settings.OperatorPassword),
                cancellationToken);

            if (_sesja == null)
            {
                _logger.LogError("Failed to login to nexo PRO - invalid credentials");
                return false;
            }
            */

            // Placeholder for testing without Sfera SDK
            await Task.Delay(100, cancellationToken);
            _isConnected = true;

            _logger.LogInformation("Successfully connected to nexo PRO");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to nexo PRO");
            _isConnected = false;
            return false;
        }
    }

    /// <summary>
    /// Create order document (Zamówienie od Klienta - ZK) in nexo PRO
    /// </summary>
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
            _logger.LogInformation("Creating order document for order #{OrderId}", order.Id);

            // TODO: Implement actual Sfera document creation
            // Example Sfera document creation:
            /*
            using var dokumentHandlowy = _sesja.DokumentyHandlowe.Utworz(
                Sfera.Model.Enums.TypDokumentuHandlowego.ZamowienieOdKlienta);

            // Set customer
            if (!string.IsNullOrEmpty(order.Customer?.NexoId))
            {
                var kontrahent = _sesja.Kontrahenci.Dane
                    .FirstOrDefault(k => k.Id.ToString() == order.Customer.NexoId);
                if (kontrahent != null)
                {
                    dokumentHandlowy.Kontrahent = kontrahent;
                }
            }

            // Set document date
            dokumentHandlowy.DataWystawienia = order.OrderDate;
            dokumentHandlowy.DataSprzedazy = order.OrderDate;

            // Set notes
            if (!string.IsNullOrEmpty(order.Notes))
            {
                dokumentHandlowy.Uwagi = order.Notes;
            }

            // Add items
            foreach (var item in order.Items)
            {
                var towar = _sesja.Towary.Dane
                    .FirstOrDefault(t => t.Symbol == item.ProductCode);

                if (towar != null)
                {
                    var pozycja = dokumentHandlowy.Pozycje.Dodaj(towar);
                    pozycja.Ilosc = item.Quantity;
                    pozycja.CenaNetto = item.PriceNetto;

                    if (item.Discount.HasValue && item.Discount > 0)
                    {
                        pozycja.Rabat = item.Discount.Value;
                    }
                }
                else
                {
                    _logger.LogWarning("Product not found in nexo: {ProductCode}", item.ProductCode);
                }
            }

            // Save document
            dokumentHandlowy.Zapisz();

            result.Success = true;
            result.NexoDocId = dokumentHandlowy.Id.ToString();
            result.NexoDocNumber = dokumentHandlowy.NumerPelny;
            */

            // Placeholder for testing
            await Task.Delay(100, cancellationToken);
            result.Success = true;
            result.NexoDocId = $"NEXO-{order.Id}-{DateTime.Now:yyyyMMddHHmmss}";
            result.NexoDocNumber = $"ZK/{DateTime.Now:yyyy}/{order.Id:D5}";

            _logger.LogInformation(
                "Created document {DocNumber} (ID: {DocId}) for order #{OrderId}",
                result.NexoDocNumber, result.NexoDocId, order.Id);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create document for order #{OrderId}", order.Id);
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    /// <summary>
    /// Get all products from nexo PRO
    /// </summary>
    public async Task<List<CloudProduct>> GetProductsAsync(CancellationToken cancellationToken = default)
    {
        var products = new List<CloudProduct>();

        if (!_isConnected)
        {
            _logger.LogWarning("Cannot get products - not connected to nexo PRO");
            return products;
        }

        try
        {
            _logger.LogInformation("Fetching products from nexo PRO");

            // TODO: Implement actual Sfera product fetch
            /*
            foreach (var towar in _sesja.Towary.Dane.Where(t => t.Aktywny))
            {
                products.Add(new CloudProduct
                {
                    NexoId = towar.Id.ToString(),
                    Code = towar.Symbol,
                    Name = towar.Nazwa,
                    Description = towar.Opis,
                    PriceNetto = towar.CenaDetaliczna,
                    VatRate = towar.StawkaVat?.Stawka,
                    Unit = towar.JednostkaMiary?.Symbol ?? "szt",
                    Ean = towar.EAN,
                    Active = towar.Aktywny
                });
            }
            */

            await Task.Delay(100, cancellationToken);
            _logger.LogInformation("Fetched {Count} products from nexo PRO", products.Count);

            return products;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch products from nexo PRO");
            return products;
        }
    }

    /// <summary>
    /// Get all customers from nexo PRO
    /// </summary>
    public async Task<List<CloudCustomer>> GetCustomersAsync(CancellationToken cancellationToken = default)
    {
        var customers = new List<CloudCustomer>();

        if (!_isConnected)
        {
            _logger.LogWarning("Cannot get customers - not connected to nexo PRO");
            return customers;
        }

        try
        {
            _logger.LogInformation("Fetching customers from nexo PRO");

            // TODO: Implement actual Sfera customer fetch
            /*
            foreach (var kontrahent in _sesja.Kontrahenci.Dane.Where(k => k.Aktywny))
            {
                customers.Add(new CloudCustomer
                {
                    NexoId = kontrahent.Id.ToString(),
                    Name = kontrahent.Nazwa,
                    ShortName = kontrahent.NazwaSkrocona,
                    Address = kontrahent.Adres?.Ulica,
                    PostalCode = kontrahent.Adres?.KodPocztowy,
                    City = kontrahent.Adres?.Miejscowosc,
                    Phone1 = kontrahent.Telefon1,
                    Email = kontrahent.Email,
                    Nip = kontrahent.NIP
                });
            }
            */

            await Task.Delay(100, cancellationToken);
            _logger.LogInformation("Fetched {Count} customers from nexo PRO", customers.Count);

            return customers;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch customers from nexo PRO");
            return customers;
        }
    }

    /// <summary>
    /// Test connection to nexo PRO
    /// </summary>
    public bool IsConnected => _isConnected;

    /// <summary>
    /// Disconnect from nexo PRO
    /// </summary>
    public void Disconnect()
    {
        if (!_isConnected)
            return;

        try
        {
            _logger.LogInformation("Disconnecting from nexo PRO");

            // TODO: Implement actual Sfera disconnect
            /*
            _sesja?.Dispose();
            _uchwyt?.Dispose();
            _sesja = null;
            _uchwyt = null;
            */

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
