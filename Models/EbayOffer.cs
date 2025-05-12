namespace EbayAutomation.Models
{
        public class EbayOffer
        {
            public string ListingId { get; set; }
            public string Title { get; set; }
            public string Description { get; set; }
            public decimal Price { get; set; }
            public string Currency { get; set; } = "USD";
            public int Quantity { get; set; }
            public string ConditionId { get; set; }
            public string CategoryId { get; set; }
            public List<string> ImageUrls { get; set; } = new List<string>();
            public Dictionary<string, string> ItemSpecifics { get; set; } = new Dictionary<string, string>();
            public string ShippingPolicyId { get; set; }
            public string ReturnPolicyId { get; set; }
            public string PaymentPolicyId { get; set; }
            public string ListingFormat { get; set; } = "FIXED_PRICE";
            public DateTime? ScheduleStartTime { get; set; }
            public DateTime? ScheduleEndTime { get; set; }
        }
}
