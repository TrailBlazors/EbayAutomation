using EbayAutomation.Configuration;
using EbayAutomation.Services;
using Newtonsoft.Json;

namespace EbayAutomation.Migration
{
    public class EbaySandboxMigrationTool
    {
        private readonly EbayApiConfiguration _productionConfig;
        private readonly EbayApiConfiguration _sandboxConfig;

        public EbaySandboxMigrationTool(
            EbayApiConfiguration productionConfig,
            EbayApiConfiguration sandboxConfig)
        {
            _productionConfig = productionConfig ?? throw new ArgumentNullException(nameof(productionConfig));
            _sandboxConfig = sandboxConfig ?? throw new ArgumentNullException(nameof(sandboxConfig));
        }

        public async Task MigrateListingsToSandbox()
        {
            // Setup sandbox services
            var sandboxAuthService = new EbayAuthenticationService(_sandboxConfig);
            var sandboxApiClient = new EbayApiClient(sandboxAuthService, "https://api.sandbox.ebay.com/");
            var sandboxService = new EbayOfferService(sandboxApiClient);

            // Setup production services
            var productionAuthService = new EbayAuthenticationService(_productionConfig);
            var productionApiClient = new EbayApiClient(productionAuthService, "https://api.ebay.com/");
            var productionService = new EbayOfferService(productionApiClient);


            // Validate tokens after service initialization
            Console.WriteLine("Validating tokens for production and sandbox environments...");
            try
            {
                // Validate production token
                await productionAuthService.GetAccessTokenAsync();
                Console.WriteLine("Production token validated.");

                // Validate sandbox token
                await sandboxAuthService.GetAccessTokenAsync();
                Console.WriteLine("Sandbox token validated.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Token validation failed: {ex.Message}");
                throw new InvalidOperationException("Failed to validate eBay tokens. Please check your configuration.", ex);
            }

            Console.WriteLine("Starting migration of eBay listings from production to sandbox...");

            // Get production listings
            var productionListings = await productionService.GetActiveOffersAsync(10, 1);
            Console.WriteLine($"Found {productionListings.Count} active listings in production");

            // Import to sandbox
            int successCount = 0;
            int failureCount = 0;

            foreach (var listing in productionListings)
            {
                try
                {
                    // Need to get shipping, payment, and return policies from sandbox
                    // as they have different IDs than production
                    var sandboxPolicies = await GetSandboxPolicies(sandboxApiClient);

                    // Update the listing with sandbox policy IDs
                    listing.ShippingPolicyId = sandboxPolicies.ShippingPolicyId;
                    listing.PaymentPolicyId = sandboxPolicies.PaymentPolicyId;
                    listing.ReturnPolicyId = sandboxPolicies.ReturnPolicyId;

                    // Create in sandbox
                    await sandboxService.CreateOfferAsync(listing);
                    Console.WriteLine($"Successfully imported: {listing.Title}");
                    successCount++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to import {listing.Title}: {ex.Message}");
                    failureCount++;
                }

                // Add delay to avoid rate limits
                await Task.Delay(1000);
            }

            Console.WriteLine($"Migration complete. Success: {successCount}, Failed: {failureCount}");
        }

        private async Task<SandboxPolicies> GetSandboxPolicies(EbayApiClient sandboxApiClient)
        {
            try
            {
                // Get account policies from sandbox
                var fulfillmentPolicies = await sandboxApiClient.GetAsync<FulfillmentPoliciesResponse>(
                    "sell/account/v1/fulfillment_policy?marketplace_id=EBAY_US");

                var paymentPolicies = await sandboxApiClient.GetAsync<PaymentPoliciesResponse>(
                    "sell/account/v1/payment_policy?marketplace_id=EBAY_US");

                var returnPolicies = await sandboxApiClient.GetAsync<ReturnPoliciesResponse>(
                    "sell/account/v1/return_policy?marketplace_id=EBAY_US");

                // Use the first policy of each type (or create if none exist)
                return new SandboxPolicies
                {
                    ShippingPolicyId = fulfillmentPolicies.FulfillmentPolicies != null && fulfillmentPolicies.FulfillmentPolicies.Count > 0
                        ? fulfillmentPolicies.FulfillmentPolicies[0].FulfillmentPolicyId
                        : await CreateDefaultShippingPolicy(sandboxApiClient),

                    PaymentPolicyId = paymentPolicies.PaymentPolicies != null && paymentPolicies.PaymentPolicies.Count > 0
                        ? paymentPolicies.PaymentPolicies[0].PaymentPolicyId
                        : await CreateDefaultPaymentPolicy(sandboxApiClient),

                    ReturnPolicyId = returnPolicies.ReturnPolicies != null && returnPolicies.ReturnPolicies.Count > 0
                        ? returnPolicies.ReturnPolicies[0].ReturnPolicyId
                        : await CreateDefaultReturnPolicy(sandboxApiClient)
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting sandbox policies: {ex.Message}");
                throw new InvalidOperationException("Failed to get or create sandbox policies.", ex);
            }
        }

        private async Task<string> CreateDefaultShippingPolicy(EbayApiClient apiClient)
        {
            var policy = new
            {
                name = "Default Shipping Policy",
                marketplaceId = "EBAY_US",
                categoryTypes = new[]
                {
                    new { name = "ALL_EXCLUDING_MOTORS_VEHICLES" }
                },
                shippingOptions = new[]
                {
                    new
                    {
                        optionType = "DOMESTIC",
                        costType = "FLAT_RATE",
                        shippingServices = new[]
                        {
                            new
                            {
                                sortOrder = 1,
                                shippingCarrierCode = "USPS",
                                shippingServiceCode = "USPSPriority",
                                shippingCost = new { value = "4.99", currency = "USD" }
                            }
                        }
                    }
                }
            };

            var response = await apiClient.PostAsync<FulfillmentPolicyResponse>(
                "sell/account/v1/fulfillment_policy", policy);

            return response.FulfillmentPolicyId;
        }

        private async Task<string> CreateDefaultPaymentPolicy(EbayApiClient apiClient)
        {
            var policy = new
            {
                name = "Default Payment Policy",
                marketplaceId = "EBAY_US",
                categoryTypes = new[]
                {
                    new { name = "ALL_EXCLUDING_MOTORS_VEHICLES" }
                },
                paymentMethods = new[]
                {
                    new { paymentMethodType = "PAYPAL" }
                }
            };

            var response = await apiClient.PostAsync<PaymentPolicyResponse>(
                "sell/account/v1/payment_policy", policy);

            return response.PaymentPolicyId;
        }

        private async Task<string> CreateDefaultReturnPolicy(EbayApiClient apiClient)
        {
            var policy = new
            {
                name = "Default Return Policy",
                marketplaceId = "EBAY_US",
                categoryTypes = new[]
                {
                    new { name = "ALL_EXCLUDING_MOTORS_VEHICLES" }
                },
                returnsAccepted = true,
                returnPeriod = new { value = 30, unit = "DAY" },
                returnMethod = "REPLACEMENT_OR_MONEY_BACK",
                returnShippingCostPayer = "SELLER"
            };

            var response = await apiClient.PostAsync<ReturnPolicyResponse>(
                "sell/account/v1/return_policy", policy);

            return response.ReturnPolicyId;
        }
    }

    // Response classes remain unchanged
    internal class SandboxPolicies
    {
        public string ShippingPolicyId { get; set; }
        public string PaymentPolicyId { get; set; }
        public string ReturnPolicyId { get; set; }
    }

    internal class FulfillmentPoliciesResponse
    {
        [JsonProperty("fulfillmentPolicies")]
        public List<FulfillmentPolicy> FulfillmentPolicies { get; set; }
    }

    internal class FulfillmentPolicy
    {
        [JsonProperty("fulfillmentPolicyId")]
        public string FulfillmentPolicyId { get; set; }
    }

    internal class FulfillmentPolicyResponse
    {
        [JsonProperty("fulfillmentPolicyId")]
        public string FulfillmentPolicyId { get; set; }
    }

    internal class PaymentPoliciesResponse
    {
        [JsonProperty("paymentPolicies")]
        public List<PaymentPolicy> PaymentPolicies { get; set; }
    }

    internal class PaymentPolicy
    {
        [JsonProperty("paymentPolicyId")]
        public string PaymentPolicyId { get; set; }
    }

    internal class PaymentPolicyResponse
    {
        [JsonProperty("paymentPolicyId")]
        public string PaymentPolicyId { get; set; }
    }

    internal class ReturnPoliciesResponse
    {
        [JsonProperty("returnPolicies")]
        public List<ReturnPolicy> ReturnPolicies { get; set; }
    }

    internal class ReturnPolicy
    {
        [JsonProperty("returnPolicyId")]
        public string ReturnPolicyId { get; set; }
    }

    internal class ReturnPolicyResponse
    {
        [JsonProperty("returnPolicyId")]
        public string ReturnPolicyId { get; set; }
    }
}
