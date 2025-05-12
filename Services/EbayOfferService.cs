using EbayAutomation.Interfaces;
using EbayAutomation.Models;
using Newtonsoft.Json;


namespace EbayAutomation.Services
{
    public class EbayOfferService : IEbayOfferService
    {
        private readonly EbayApiClient _apiClient;

        public EbayOfferService(EbayApiClient apiClient)
        {
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        }

        public async Task<string> CreateOfferAsync(EbayOffer offer)
        {
            // First create inventory item
            var sku = Guid.NewGuid().ToString();

            var inventoryItem = new
            {
                availability = new
                {
                    shipToLocationAvailability = new
                    {
                        quantity = offer.Quantity
                    }
                },
                condition = offer.ConditionId,
                product = new
                {
                    title = offer.Title,
                    description = offer.Description,
                    aspects = offer.ItemSpecifics,
                    imageUrls = offer.ImageUrls
                }
            };

            await _apiClient.PutAsync<object>($"sell/inventory/v1/inventory_item/{sku}", inventoryItem);

            // Then create offer
            var offerData = new
            {
                sku = sku,
                marketplaceId = "EBAY_US",
                format = offer.ListingFormat,
                availableQuantity = offer.Quantity,
                categoryId = offer.CategoryId,
                listingPolicies = new
                {
                    fulfillmentPolicyId = offer.ShippingPolicyId,
                    paymentPolicyId = offer.PaymentPolicyId,
                    returnPolicyId = offer.ReturnPolicyId
                },
                pricingSummary = new
                {
                    price = new
                    {
                        value = offer.Price.ToString(),
                        currency = offer.Currency
                    }
                },
                listingDescription = offer.Description
            };

            var offerResponse = await _apiClient.PostAsync<CreateOfferResponse>("sell/inventory/v1/offer", offerData);

            // Publish the offer
            var publishRequest = new
            {
                offerId = offerResponse.OfferId
            };

            var publishResponse = await _apiClient.PostAsync<PublishOfferResponse>("sell/inventory/v1/offer/publish", publishRequest);

            return publishResponse.ListingId;
        }

        public async Task<bool> UpdateOfferAsync(string listingId, EbayOffer updatedOffer)
        {
            // Get the offer by listing ID
            var offers = await _apiClient.GetAsync<GetOffersByListingIdResponse>($"sell/inventory/v1/offer/get_offers_by_listing_id?listing_id={listingId}");

            if (offers.Offers == null || offers.Offers.Count == 0)
                return false;

            var offer = offers.Offers[0];

            // Update inventory item
            var inventoryItem = new
            {
                availability = new
                {
                    shipToLocationAvailability = new
                    {
                        quantity = updatedOffer.Quantity
                    }
                },
                condition = updatedOffer.ConditionId,
                product = new
                {
                    title = updatedOffer.Title,
                    description = updatedOffer.Description,
                    aspects = updatedOffer.ItemSpecifics,
                    imageUrls = updatedOffer.ImageUrls
                }
            };

            await _apiClient.PutAsync<object>($"sell/inventory/v1/inventory_item/{offer.Sku}", inventoryItem);

            // Update offer
            var offerData = new
            {
                availableQuantity = updatedOffer.Quantity,
                categoryId = updatedOffer.CategoryId,
                listingPolicies = new
                {
                    fulfillmentPolicyId = updatedOffer.ShippingPolicyId,
                    paymentPolicyId = updatedOffer.PaymentPolicyId,
                    returnPolicyId = updatedOffer.ReturnPolicyId
                },
                pricingSummary = new
                {
                    price = new
                    {
                        value = updatedOffer.Price.ToString(),
                        currency = updatedOffer.Currency
                    }
                },
                listingDescription = updatedOffer.Description
            };

            await _apiClient.PutAsync<object>($"sell/inventory/v1/offer/{offer.OfferId}", offerData);

            // Publish the updated offer
            var publishRequest = new
            {
                offerId = offer.OfferId
            };

            await _apiClient.PostAsync<PublishOfferResponse>("sell/inventory/v1/offer/publish", publishRequest);

            return true;
        }

        public async Task<bool> DeleteOfferAsync(string listingId)
        {
            // Get the offer by listing ID
            var offers = await _apiClient.GetAsync<GetOffersByListingIdResponse>($"sell/inventory/v1/offer/get_offers_by_listing_id?listing_id={listingId}");

            if (offers.Offers == null || offers.Offers.Count == 0)
                return false;

            var offer = offers.Offers[0];

            // Delete the offer
            await _apiClient.DeleteAsync($"sell/inventory/v1/offer/{offer.OfferId}");

            // Optionally delete the inventory item
            await _apiClient.DeleteAsync($"sell/inventory/v1/inventory_item/{offer.Sku}");

            return true;
        }

        public async Task<EbayOffer?> GetOfferAsync(string listingId)
        {
            // Get the offer by listing ID
            var offers = await _apiClient.GetAsync<GetOffersByListingIdResponse>($"sell/inventory/v1/offer/get_offers_by_listing_id?listing_id={listingId}");

            if (offers.Offers == null || offers.Offers.Count == 0)
                return null;

            var offer = offers.Offers[0];

            // Get inventory item details
            var inventoryItem = await _apiClient.GetAsync<InventoryItem>($"sell/inventory/v1/inventory_item/{offer.Sku}");

            // Map to our model
            return new EbayOffer
            {
                ListingId = listingId,
                Title = inventoryItem.Product.Title,
                Description = offer.ListingDescription,
                Price = decimal.Parse(offer.PricingSummary.Price.Value),
                Currency = offer.PricingSummary.Price.Currency,
                Quantity = offer.AvailableQuantity,
                ConditionId = inventoryItem.Condition,
                CategoryId = offer.CategoryId,
                ImageUrls = inventoryItem.Product.ImageUrls,
                ItemSpecifics = inventoryItem.Product.Aspects,
                ShippingPolicyId = offer.ListingPolicies.FulfillmentPolicyId,
                ReturnPolicyId = offer.ListingPolicies.ReturnPolicyId,
                PaymentPolicyId = offer.ListingPolicies.PaymentPolicyId,
                ListingFormat = offer.Format
            };
        }

        public async Task<List<EbayOffer>> GetActiveOffersAsync(int pageSize = 100, int pageNumber = 1)
        {
            var result = new List<EbayOffer>();

            // Get inventory items
            var inventoryItems = await _apiClient.GetAsync<GetInventoryItemsResponse>($"sell/inventory/v1/inventory_item?limit={pageSize}&offset={(pageNumber - 1) * pageSize}");

            if (inventoryItems.InventoryItems == null)
                return result;

            foreach (var item in inventoryItems.InventoryItems)
            {
                // Get offers for each inventory item
                var offers = await _apiClient.GetAsync<GetOffersResponse>($"sell/inventory/v1/offer?sku={item.Sku}");

                if (offers.Offers == null)
                    continue;

                foreach (var offer in offers.Offers)
                {
                    if (offer.Status == "PUBLISHED")
                    {
                        result.Add(new EbayOffer
                        {
                            ListingId = offer.ListingId,
                            Title = item.Product.Title,
                            Description = offer.ListingDescription,
                            Price = decimal.Parse(offer.PricingSummary.Price.Value),
                            Currency = offer.PricingSummary.Price.Currency,
                            Quantity = offer.AvailableQuantity,
                            CategoryId = offer.CategoryId,
                            ConditionId = item.Condition,
                            ShippingPolicyId = offer.ListingPolicies.FulfillmentPolicyId,
                            ReturnPolicyId = offer.ListingPolicies.ReturnPolicyId,
                            PaymentPolicyId = offer.ListingPolicies.PaymentPolicyId,
                            ListingFormat = offer.Format,
                            ItemSpecifics = item.Product.Aspects,
                            ImageUrls = item.Product.ImageUrls
                        });
                    }
                }
            }

            return result;
        }
    }

    // Response classes for API calls
    internal class CreateOfferResponse
    {
        [JsonProperty("offerId")]
        public string OfferId { get; set; }
    }

    internal class PublishOfferResponse
    {
        [JsonProperty("listingId")]
        public string ListingId { get; set; }
    }

    internal class GetOffersByListingIdResponse
    {
        [JsonProperty("offers")]
        public List<OfferDetail> Offers { get; set; }
    }

    internal class GetOffersResponse
    {
        [JsonProperty("offers")]
        public List<OfferDetail> Offers { get; set; }
    }

    internal class OfferDetail
    {
        [JsonProperty("offerId")]
        public string OfferId { get; set; }

        [JsonProperty("sku")]
        public string Sku { get; set; }

        [JsonProperty("listingId")]
        public string ListingId { get; set; }

        [JsonProperty("format")]
        public string Format { get; set; }

        [JsonProperty("availableQuantity")]
        public int AvailableQuantity { get; set; }

        [JsonProperty("categoryId")]
        public string CategoryId { get; set; }

        [JsonProperty("listingDescription")]
        public string ListingDescription { get; set; }

        [JsonProperty("listingPolicies")]
        public ListingPolicies ListingPolicies { get; set; }

        [JsonProperty("pricingSummary")]
        public PricingSummary PricingSummary { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }
    }

    internal class ListingPolicies
    {
        [JsonProperty("fulfillmentPolicyId")]
        public string FulfillmentPolicyId { get; set; }

        [JsonProperty("paymentPolicyId")]
        public string PaymentPolicyId { get; set; }

        [JsonProperty("returnPolicyId")]
        public string ReturnPolicyId { get; set; }
    }

    internal class PricingSummary
    {
        [JsonProperty("price")]
        public Price Price { get; set; }
    }

    internal class Price
    {
        [JsonProperty("value")]
        public string Value { get; set; }

        [JsonProperty("currency")]
        public string Currency { get; set; }
    }

    internal class GetInventoryItemsResponse
    {
        [JsonProperty("inventoryItems")]
        public List<InventoryItemDetail> InventoryItems { get; set; }
    }

    internal class InventoryItemDetail
    {
        [JsonProperty("sku")]
        public string Sku { get; set; }

        [JsonProperty("condition")]
        public string Condition { get; set; }

        [JsonProperty("product")]
        public ProductDetail Product { get; set; }
    }

    internal class ProductDetail
    {
        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("aspects")]
        public Dictionary<string, string> Aspects { get; set; }

        [JsonProperty("imageUrls")]
        public List<string> ImageUrls { get; set; }
    }

    internal class InventoryItem
    {
        [JsonProperty("sku")]
        public string Sku { get; set; }

        [JsonProperty("condition")]
        public string Condition { get; set; }

        [JsonProperty("product")]
        public ProductDetail Product { get; set; }
    }
}
