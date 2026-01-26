using System.Text.Json.Serialization;

namespace IkoNexoBridge.Models;

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

    [JsonPropertyName("thumbnailBase64")]
    public string? ThumbnailBase64 { get; set; }

    [JsonPropertyName("imageType")]
    public string? ImageType { get; set; }
}

public class UpdateOrderStatusRequest
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("nexoDocId")]
    public string? NexoDocId { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }
}

public class OrderProcessingResult
{
    public bool Success { get; set; }
    public int OrderId { get; set; }
    public string? NexoDocId { get; set; }
    public string? NexoDocNumber { get; set; }
    public string? ErrorMessage { get; set; }
}

public class SyncResult
{
    public int ItemsProcessed { get; set; }
    public int ItemsAdded { get; set; }
    public int ItemsUpdated { get; set; }
    public int ItemsFailed { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class CustomerBalance
{
    [JsonPropertyName("nexoId")]
    public string NexoId { get; set; } = string.Empty;

    [JsonPropertyName("balance")]
    public decimal Balance { get; set; }

    [JsonPropertyName("creditLimit")]
    public decimal? CreditLimit { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; }

    [JsonIgnore]
    public bool IsOverCreditLimit => CreditLimit.HasValue && Balance > CreditLimit.Value;

    [JsonIgnore]
    public bool HasDebt => Balance > 0;
}

public class ProductImage
{
    [JsonPropertyName("nexoId")]
    public string NexoId { get; set; } = string.Empty;

    [JsonPropertyName("base64Data")]
    public string Base64Data { get; set; } = string.Empty;

    [JsonPropertyName("mimeType")]
    public string MimeType { get; set; } = "image/jpeg";

    [JsonPropertyName("fetchedAt")]
    public DateTime FetchedAt { get; set; }

    [JsonIgnore]
    public string DataUrl => $"data:{MimeType};base64,{Base64Data}";
}

public class OrderHistoryDocument
{
    [JsonPropertyName("nexoDocId")]
    public string NexoDocId { get; set; } = string.Empty;

    [JsonPropertyName("nexoCustomerId")]
    public string NexoCustomerId { get; set; } = string.Empty;

    [JsonPropertyName("documentNumber")]
    public string DocumentNumber { get; set; } = string.Empty;

    [JsonPropertyName("documentType")]
    public string DocumentType { get; set; } = string.Empty;

    [JsonPropertyName("documentDate")]
    public DateTime DocumentDate { get; set; }

    [JsonPropertyName("totalNetto")]
    public decimal TotalNetto { get; set; }

    [JsonPropertyName("totalBrutto")]
    public decimal TotalBrutto { get; set; }
}
