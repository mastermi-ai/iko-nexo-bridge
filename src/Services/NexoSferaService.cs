using System.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using IkoNexoBridge.Configuration;
using IkoNexoBridge.Models;

namespace IkoNexoBridge.Services;

public class NexoSferaService : IDisposable
{
    private readonly ILogger<NexoSferaService> _logger;
    private readonly NexoProSettings _settings;
    private bool _isConnected;
    private bool _disposed;
    private SqlConnection? _sqlConnection;

#if USE_SFERA
    private Sfera.Uchwyt? _uchwyt;
    private Sfera.Sesja? _sesja;
#endif

    public NexoSferaService(
        IOptions<NexoProSettings> settings,
        ILogger<NexoSferaService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_isConnected)
            return true;

        try
        {
            _logger.LogInformation("Connecting to nexo PRO: {Server}/{Database}",
                _settings.ServerName, _settings.DatabaseName);

#if USE_SFERA
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
                _logger.LogError("Failed to login to nexo PRO - invalid operator credentials");
                return false;
            }

            _isConnected = true;
            _logger.LogInformation("Connected to nexo PRO via Sfera SDK");
            return true;
#else
            var connectionString = BuildConnectionString();
            _sqlConnection = new SqlConnection(connectionString);
            await _sqlConnection.OpenAsync(cancellationToken);

            _isConnected = true;
            _logger.LogInformation("Connected to nexo PRO database");
            return true;
#endif
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

#if USE_SFERA
            if (_sesja == null)
            {
                result.ErrorMessage = "Sfera session not initialized";
                return result;
            }

            using var dokumentHandlowy = _sesja.DokumentyHandlowe.Utworz(
                Sfera.Model.Enums.TypDokumentuHandlowego.ZamowienieOdKlienta);

            if (!string.IsNullOrEmpty(order.Customer?.NexoId))
            {
                var kontrahent = _sesja.Kontrahenci.Dane
                    .FirstOrDefault(k => k.Id.ToString() == order.Customer.NexoId);

                if (kontrahent != null)
                {
                    dokumentHandlowy.Kontrahent = kontrahent;
                }
                else
                {
                    _logger.LogWarning("Customer not found in nexo: {NexoId}", order.Customer.NexoId);
                }
            }

            dokumentHandlowy.DataWystawienia = order.OrderDate;
            dokumentHandlowy.DataRealizacji = order.OrderDate.AddDays(7);

            if (!string.IsNullOrEmpty(order.Notes))
            {
                dokumentHandlowy.Uwagi = order.Notes;
            }

            foreach (var item in order.Items)
            {
                var towar = _sesja.Towary.Dane
                    .FirstOrDefault(t => t.Symbol == item.ProductCode);

                if (towar != null)
                {
                    var pozycja = dokumentHandlowy.Pozycje.Dodaj(towar);
                    pozycja.Ilosc = item.Quantity;
                    pozycja.CenaNetto = item.PriceNetto;

                    if (!string.IsNullOrEmpty(item.Notes))
                    {
                        pozycja.Uwagi = item.Notes;
                    }
                }
                else
                {
                    _logger.LogWarning("Product not found in nexo: {ProductCode}", item.ProductCode);
                }
            }

            dokumentHandlowy.Przelicz();
            dokumentHandlowy.Zapisz();

            result.Success = true;
            result.NexoDocId = dokumentHandlowy.Id.ToString();
            result.NexoDocNumber = dokumentHandlowy.NumerPelny;

            _logger.LogInformation("Created ZK {DocNumber} for order #{OrderId}",
                result.NexoDocNumber, order.Id);
#else
            _logger.LogWarning("TEST MODE - document will not be created in nexo");
            await Task.Delay(100, cancellationToken);

            result.Success = true;
            result.NexoDocId = $"TEST-{order.Id}-{DateTime.Now:yyyyMMddHHmmss}";
            result.NexoDocNumber = $"ZK-TEST/{DateTime.Now:yyyy}/{order.Id:D5}";

            _logger.LogInformation("Simulated ZK {DocNumber} for order #{OrderId}",
                result.NexoDocNumber, order.Id);
#endif

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create ZK for order #{OrderId}", order.Id);
            result.ErrorMessage = ex.Message;
            return result;
        }
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
            _logger.LogInformation("Fetching products from nexo PRO");

            var query = @"
                SELECT
                    t.tw_Id AS Id,
                    t.tw_Symbol AS Code,
                    t.tw_Nazwa AS Name,
                    t.tw_Opis AS Description,
                    ISNULL(c.ce_WartoscNetto, 0) AS PriceNetto,
                    ISNULL(c.ce_WartoscBrutto, 0) AS PriceBrutto,
                    ISNULL(sv.sv_Stawka, 23) AS VatRate,
                    ISNULL(jm.jm_Symbol, 'szt') AS Unit,
                    t.tw_EAN AS Ean,
                    t.tw_Aktywny AS Active
                FROM tw__Towar t
                LEFT JOIN ce__Cena c ON t.tw_Id = c.ce_IdObiektu AND c.ce_IdRodzajuCeny = 1
                LEFT JOIN sv__StawkaVat sv ON t.tw_IdStawkiVat = sv.sv_Id
                LEFT JOIN jm__JednostkaMiary jm ON t.tw_IdJednostkiMiary = jm.jm_Id
                WHERE t.tw_Aktywny = 1
                ORDER BY t.tw_Symbol";

            using var command = new SqlCommand(query, _sqlConnection);
            using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
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
                    Active = Convert.ToBoolean(reader["Active"])
                });
            }

            _logger.LogInformation("Fetched {Count} products from nexo PRO", products.Count);
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
                    k.kh_Id AS Id,
                    k.kh_Nazwa AS Name,
                    k.kh_NazwaSkrocona AS ShortName,
                    a.adr_Ulica AS Address,
                    a.adr_KodPocztowy AS PostalCode,
                    a.adr_Miejscowosc AS City,
                    k.kh_Telefon1 AS Phone1,
                    k.kh_Telefon2 AS Phone2,
                    k.kh_Email AS Email,
                    k.kh_NIP AS Nip,
                    k.kh_REGON AS Regon,
                    k.kh_Aktywny AS Active
                FROM kh__Kontrahent k
                LEFT JOIN adr__Adres a ON k.kh_IdAdresuPodstawowego = a.adr_Id
                WHERE k.kh_Aktywny = 1
                ORDER BY k.kh_Nazwa";

            using var command = new SqlCommand(query, _sqlConnection);
            using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                customers.Add(new CloudCustomer
                {
                    NexoId = reader["Id"].ToString()!,
                    Name = reader["Name"]?.ToString() ?? "",
                    ShortName = reader["ShortName"]?.ToString(),
                    Address = reader["Address"]?.ToString(),
                    PostalCode = reader["PostalCode"]?.ToString(),
                    City = reader["City"]?.ToString(),
                    Phone1 = reader["Phone1"]?.ToString(),
                    Phone2 = reader["Phone2"]?.ToString(),
                    Email = reader["Email"]?.ToString(),
                    Nip = reader["Nip"]?.ToString(),
                    Regon = reader["Regon"]?.ToString()
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
                    k.kh_Id,
                    k.kh_Nazwa,
                    k.kh_LimitKredytowy AS CreditLimit,
                    ISNULL(SUM(CASE
                        WHEN r.rk_Typ = 'N' THEN r.rk_KwotaPozostala
                        WHEN r.rk_Typ = 'Z' THEN -r.rk_KwotaPozostala
                        ELSE 0
                    END), 0) AS Balance
                FROM kh__Kontrahent k
                LEFT JOIN rk__Rozrachunek r ON k.kh_Id = r.rk_IdKontrahenta
                    AND r.rk_KwotaPozostala > 0
                WHERE k.kh_Id = @NexoId
                GROUP BY k.kh_Id, k.kh_Nazwa, k.kh_LimitKredytowy";

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
                    k.kh_Id AS NexoId,
                    k.kh_LimitKredytowy AS CreditLimit,
                    ISNULL(SUM(CASE
                        WHEN r.rk_Typ = 'N' THEN r.rk_KwotaPozostala
                        WHEN r.rk_Typ = 'Z' THEN -r.rk_KwotaPozostala
                        ELSE 0
                    END), 0) AS Balance
                FROM kh__Kontrahent k
                LEFT JOIN rk__Rozrachunek r ON k.kh_Id = r.rk_IdKontrahenta
                    AND r.rk_KwotaPozostala > 0
                WHERE k.kh_Aktywny = 1
                GROUP BY k.kh_Id, k.kh_LimitKredytowy";

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
        if (!_isConnected || _sqlConnection == null)
        {
            _logger.LogWarning("Cannot get product image - not connected to nexo PRO");
            return null;
        }

        try
        {
            var query = @"
                SELECT
                    t.tw_Id,
                    z.zdj_Dane AS ImageData,
                    z.zdj_TypMIME AS MimeType
                FROM tw__Towar t
                INNER JOIN zdj__Zdjecie z ON t.tw_IdZdjeciaPodstawowego = z.zdj_Id
                WHERE t.tw_Id = @NexoId";

            using var command = new SqlCommand(query, _sqlConnection);
            command.Parameters.AddWithValue("@NexoId", int.Parse(nexoId));

            using var reader = await command.ExecuteReaderAsync(cancellationToken);

            if (await reader.ReadAsync(cancellationToken))
            {
                var imageData = reader["ImageData"] as byte[];
                var mimeType = reader["MimeType"]?.ToString() ?? "image/jpeg";

                if (imageData != null && imageData.Length > 0)
                {
                    var base64 = Convert.ToBase64String(imageData);

                    return new ProductImage
                    {
                        NexoId = nexoId,
                        Base64Data = base64,
                        MimeType = mimeType,
                        FetchedAt = DateTime.UtcNow
                    };
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch image for product {NexoId}", nexoId);
            return null;
        }
    }

    public async Task<List<string>> GetProductsWithImagesAsync(
        CancellationToken cancellationToken = default)
    {
        var productIds = new List<string>();

        if (!_isConnected || _sqlConnection == null)
        {
            _logger.LogWarning("Cannot check product images - not connected to nexo PRO");
            return productIds;
        }

        try
        {
            var query = @"
                SELECT t.tw_Id
                FROM tw__Towar t
                WHERE t.tw_IdZdjeciaPodstawowego IS NOT NULL
                  AND t.tw_Aktywny = 1";

            using var command = new SqlCommand(query, _sqlConnection);
            using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                productIds.Add(reader["tw_Id"].ToString()!);
            }

            _logger.LogInformation("Found {Count} products with images", productIds.Count);
            return productIds;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check product images");
            return productIds;
        }
    }

    public void Disconnect()
    {
        if (!_isConnected)
            return;

        try
        {
            _logger.LogInformation("Disconnecting from nexo PRO");

#if USE_SFERA
            _sesja?.Dispose();
            _uchwyt?.Dispose();
#endif

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
