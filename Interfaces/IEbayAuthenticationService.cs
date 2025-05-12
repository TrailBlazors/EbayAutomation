
namespace EbayAutomation.Interfaces
{
    public interface IEbayAuthenticationService
    {
        Task<string> ExchangeCodeForRefreshTokenAsync(string code);
        Task<string> GetAccessTokenAsync();
        string GetAuthorizationUrl();
        Task<string> RefreshAccessTokenAsync(string refreshToken);
    }
}