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
