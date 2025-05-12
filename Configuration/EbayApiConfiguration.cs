namespace EbayAutomation.Configuration
{

    public class EbayApiConfiguration
    {
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string RedirectUri { get; set; }
        public string RefreshToken { get; set; }
        public string Environment { get; set; } // "Production" or "Sandbox"
        public string ScopesCsv { get; set; } // Comma-separated list of required scopes
    }

}