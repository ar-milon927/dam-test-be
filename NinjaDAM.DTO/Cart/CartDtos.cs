namespace NinjaDAM.DTO.Cart
{
    public class CartItemDto
    {
        public Guid Id { get; set; }
        public Guid AssetId { get; set; }
        public string? FileName { get; set; }
        public string? FileType { get; set; }
        public long? FileSize { get; set; }
        public string? ThumbnailPath { get; set; }
        public string? FilePath { get; set; }
        public DateTime AddedAt { get; set; }
    }

    public class AddToCartRequestDto
    {
        public List<Guid> AssetIds { get; set; } = new();
    }

    public class RemoveFromCartRequestDto
    {
        public List<Guid> CartItemIds { get; set; } = new();
    }

    public class CartSummaryDto
    {
        public int TotalItems { get; set; }
        public List<CartItemDto> Items { get; set; } = new();
    }
}
