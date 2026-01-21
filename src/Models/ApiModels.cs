using System.Text.Json.Serialization;

namespace IkoNexoBridge.Models;

/// <summary>
/// Order from Cloud API
/// </summary>
public class CloudOrder
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("clientId")]
    public int ClientId { get; set; }

    [JsonPropertyName("salesmanId")]
    public int SalesmanId { get; set; }

    [JsonPropertyName("customerId")]
    public int? CustomerId { get; set; }

    [JsonPropertyName("orderNumber")]
    public string? OrderNumber { get; set; }

    [JsonPropertyName("nexoDocId")]
    public string? NexoDocId { get; set; }

    [JsonPropertyName("orderDate")]
    public DateTime OrderDate { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "pending";

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("totalNetto")]
    public decimal TotalNetto { get; set; }

    [JsonPropertyName("totalBrutto")]
    public decimal? TotalBrutto { get; set; }

    [JsonPropertyName("items")]
    public List<CloudOrderItem> Items { get; set; } = new();

    [JsonPropertyName("customer")]
    public CloudCustomer? Customer { get; set; }
}

/// <summary>
/// Order item from Cloud API
/// </summary>
public class CloudOrderItem
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("productId")]
    public int ProductId { get; set; }

    [JsonPropertyName("productCode")]
    public string ProductCode { get; set; } = string.Empty;

    [JsonPropertyName("productName")]
    public string ProductName { get; set; } = string.Empty;

    [JsonPropertyName("quantity")]
    public decimal Quantity { get; set; }

    [JsonPropertyName("priceNetto")]
    public decimal PriceNetto { get; set; }

    [JsonPropertyName("priceBrutto")]
    public decimal? PriceBrutto { get; set; }

    [JsonPropertyName("vatRate")]
    public decimal? VatRate { get; set; }

    [JsonPropertyName("discount")]
    public decimal? Discount { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("total")]
    public decimal Total { get; set; }
}

/// <summary>
/// Customer from Cloud API
/// </summary>
public class CloudCustomer
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("clientId")]
    public int ClientId { get; set; }

    [JsonPropertyName("nexoId")]
    public string? NexoId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("shortName")]
    public string? ShortName { get; set; }

    [JsonPropertyName("address")]
    public string? Address { get; set; }

    [JsonPropertyName("postalCode")]
    public string? PostalCode { get; set; }

    [JsonPropertyName("city")]
    public string? City { get; set; }

    [JsonPropertyName("phone1")]
    public string? Phone1 { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("nip")]
    public string? Nip { get; set; }
}

/// <summary>
/// Product from Cloud API
/// </summary>
public class CloudProduct
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("clientId")]
    public int ClientId { get; set; }

    [JsonPropertyName("nexoId")]
    public string? NexoId { get; set; }

    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("priceNetto")]
    public decimal PriceNetto { get; set; }

    [JsonPropertyName("priceBrutto")]
    public decimal? PriceBrutto { get; set; }

    [JsonPropertyName("vatRate")]
    public decimal? VatRate { get; set; }

    [JsonPropertyName("unit")]
    public string Unit { get; set; } = "szt";

    [JsonPropertyName("ean")]
    public string? Ean { get; set; }

    [JsonPropertyName("active")]
    public bool Active { get; set; } = true;
}

/// <summary>
/// Update order status request
/// </summary>
public class UpdateOrderStatusRequest
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("nexoDocId")]
    public string? NexoDocId { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Result of order processing
/// </summary>
public class OrderProcessingResult
{
    public bool Success { get; set; }
    public int OrderId { get; set; }
    public string? NexoDocId { get; set; }
    public string? NexoDocNumber { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Sync result for products/customers
/// </summary>
public class SyncResult
{
    public int ItemsProcessed { get; set; }
    public int ItemsAdded { get; set; }
    public int ItemsUpdated { get; set; }
    public int ItemsFailed { get; set; }
    public List<string> Errors { get; set; } = new();
}

// ========================================================================
// ROZRACHUNKI (WYMAGANIE KLIENTA)
// ========================================================================

/// <summary>
/// Saldo rozrachunków klienta z nexo PRO
/// </summary>
public class CustomerBalance
{
    /// <summary>
    /// ID kontrahenta w nexo
    /// </summary>
    [JsonPropertyName("nexoId")]
    public string NexoId { get; set; } = string.Empty;

    /// <summary>
    /// Saldo należności (dodatnie = klient winien, ujemne = nadpłata)
    /// </summary>
    [JsonPropertyName("balance")]
    public decimal Balance { get; set; }

    /// <summary>
    /// Limit kredytowy kontrahenta
    /// </summary>
    [JsonPropertyName("creditLimit")]
    public decimal? CreditLimit { get; set; }

    /// <summary>
    /// Data pobrania salda z nexo
    /// </summary>
    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Czy klient przekroczył limit kredytowy
    /// </summary>
    [JsonIgnore]
    public bool IsOverCreditLimit => CreditLimit.HasValue && Balance > CreditLimit.Value;

    /// <summary>
    /// Czy klient ma zaległości
    /// </summary>
    [JsonIgnore]
    public bool HasDebt => Balance > 0;
}

// ========================================================================
// ZDJĘCIA PRODUKTÓW (WYMAGANIE KLIENTA - z cache!)
// ========================================================================

/// <summary>
/// Zdjęcie produktu z nexo PRO
/// </summary>
public class ProductImage
{
    /// <summary>
    /// ID produktu w nexo
    /// </summary>
    [JsonPropertyName("nexoId")]
    public string NexoId { get; set; } = string.Empty;

    /// <summary>
    /// Zdjęcie jako Base64 (thumbnail max 200x200px dla wydajności)
    /// </summary>
    [JsonPropertyName("base64Data")]
    public string Base64Data { get; set; } = string.Empty;

    /// <summary>
    /// Typ MIME (image/jpeg, image/png)
    /// </summary>
    [JsonPropertyName("mimeType")]
    public string MimeType { get; set; } = "image/jpeg";

    /// <summary>
    /// Data pobrania z nexo
    /// </summary>
    [JsonPropertyName("fetchedAt")]
    public DateTime FetchedAt { get; set; }

    /// <summary>
    /// Zwraca pełny data URL do osadzenia w img src
    /// </summary>
    [JsonIgnore]
    public string DataUrl => $"data:{MimeType};base64,{Base64Data}";
}
