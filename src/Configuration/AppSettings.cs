namespace IkoNexoBridge.Configuration;

/// <summary>
/// Cloud API connection settings
/// </summary>
public class CloudApiSettings
{
    public const string SectionName = "CloudApi";

    public string BaseUrl { get; set; } = "http://localhost:3000";
    public string ApiKey { get; set; } = string.Empty;
    public int PollingIntervalSeconds { get; set; } = 30;
    public int RequestTimeoutSeconds { get; set; } = 30;
    public int MaxRetryAttempts { get; set; } = 3;
}

/// <summary>
/// InsERT nexo PRO connection settings
/// </summary>
public class NexoProSettings
{
    public const string SectionName = "NexoPro";

    /// <summary>
    /// SQL Server instance name (e.g., "localhost\NEXOPRO")
    /// </summary>
    public string ServerName { get; set; } = string.Empty;

    /// <summary>
    /// Database name in SQL Server
    /// </summary>
    public string DatabaseName { get; set; } = string.Empty;

    /// <summary>
    /// SQL Server username (empty for Windows Auth)
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// SQL Server password (empty for Windows Auth)
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Nexo operator symbol for document operations
    /// </summary>
    public string OperatorSymbol { get; set; } = "ADMIN";

    /// <summary>
    /// Nexo operator password
    /// </summary>
    public string OperatorPassword { get; set; } = string.Empty;

    /// <summary>
    /// Default warehouse symbol for orders
    /// </summary>
    public string DefaultWarehouse { get; set; } = "MAG";

    /// <summary>
    /// Default document type (ZK = Zamówienie od Klienta)
    /// </summary>
    public string DefaultDocumentType { get; set; } = "ZK";

    /// <summary>
    /// Connection string for Sfera
    /// </summary>
    public string GetConnectionString()
    {
        if (string.IsNullOrEmpty(Username))
        {
            // Windows Authentication
            return $"Server={ServerName};Database={DatabaseName};Trusted_Connection=True;TrustServerCertificate=True;";
        }
        else
        {
            // SQL Server Authentication
            return $"Server={ServerName};Database={DatabaseName};User Id={Username};Password={Password};TrustServerCertificate=True;";
        }
    }
}

/// <summary>
/// Sfera Proxy settings (for creating documents via separate .NET Framework service)
/// </summary>
public class SferaProxySettings
{
    public const string SectionName = "SferaProxy";

    /// <summary>
    /// URL of Sfera Proxy service (e.g., "http://localhost:5801")
    /// </summary>
    public string Url { get; set; } = "http://localhost:5801";

    /// <summary>
    /// Enable document creation via Sfera Proxy
    /// </summary>
    public bool Enabled { get; set; } = false;
}

/// <summary>
/// Synchronization settings
/// </summary>
public class SyncSettings
{
    public const string SectionName = "Sync";

    public bool SyncOrdersEnabled { get; set; } = true;
    public bool SyncProductsEnabled { get; set; } = true;
    public bool SyncCustomersEnabled { get; set; } = true;
    public int ProductsSyncIntervalMinutes { get; set; } = 60;
    public int CustomersSyncIntervalMinutes { get; set; } = 60;
}
