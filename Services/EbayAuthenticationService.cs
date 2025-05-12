using eBay.ApiClient.Auth.OAuth2;
using eBay.ApiClient.Auth.OAuth2.Model;
using EbayAutomation.Configuration;
using EbayAutomation.Interfaces;

namespace EbayAutomation.Services
{
    public class EbayAuthenticationService : IEbayAuthenticationService
    {
        private readonly EbayApiConfiguration _config;
        private readonly OAuth2Api _oauthClient;
        private readonly string _tokenFileName;
        private readonly OAuthEnvironment _environment;

        public EbayAuthenticationService(EbayApiConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            
            // Determine environment
            _environment = _config.Environment?.ToLower() == "sandbox" 
                ? OAuthEnvironment.SANDBOX 
                : OAuthEnvironment.PRODUCTION;
                
            // Set token file name based on environment
            _tokenFileName = _environment == OAuthEnvironment.SANDBOX 
                ? "sandbox_refresh_token.txt" 
                : "production_refresh_token.txt";
                
            // Initialize OAuth client
            _oauthClient = new OAuth2Api();
            
            // Load credentials from YAML file
            LoadCredentials();
        }
        
        private void LoadCredentials()
        {
            try
            {
                // Path to the YAML config file
                string configPath = Path.Combine(Directory.GetCurrentDirectory(), "ebay-config.yaml");
                
                if (!File.Exists(configPath))
                {
                    throw new FileNotFoundException($"eBay configuration file not found at: {configPath}");
                }
                
                // Load credentials from the YAML file
                CredentialUtil.Load(configPath);
                
                Console.WriteLine($"Credentials loaded from {configPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading credentials: {ex.Message}");
                throw;
            }
        }

        public async Task<string> GetAccessTokenAsync()
        {
            // Try to load refresh token from file if not already in config
            if (string.IsNullOrEmpty(_config.RefreshToken))
            {
                LoadRefreshToken();
            }

            // If still no refresh token, guide the user through authorization
            if (string.IsNullOrEmpty(_config.RefreshToken))
            {
                Console.WriteLine($"Refresh token for {_environment} is not configured.");
                Console.WriteLine("Please visit the following URL in your browser to authorize the application:");
                Console.WriteLine(GetAuthorizationUrl());
                Console.WriteLine("After authorization, you will be redirected to your redirect URI.");
                Console.WriteLine("Enter the authorization code from the URL parameter (after 'code='):");
                string authCode = Console.ReadLine();
                
                _config.RefreshToken = await ExchangeCodeForRefreshTokenAsync(authCode);
            }

            return await RefreshAccessTokenAsync(_config.RefreshToken);
        }

        public string GetAuthorizationUrl()
        {
            try
            {
                // Generate a random state parameter for CSRF protection
                string state = Guid.NewGuid().ToString();
                
                return _oauthClient.GenerateUserAuthorizationUrl(
                    _environment,
                    _config.ScopesCsv.Split(','),
                    state
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating authorization URL: {ex.Message}");
                throw;
            }
        }

        public async Task<string> ExchangeCodeForRefreshTokenAsync(string code)
        {
            try
            {
                var tokenResponse = _oauthClient.ExchangeCodeForAccessToken(
                    _environment,
                    code
                );

                string refreshToken = tokenResponse.RefreshToken.Token;
                
                // Save the refresh token to a file
                SaveRefreshToken(refreshToken);
                
                return refreshToken;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error exchanging code for refresh token: {ex.Message}");
                throw;
            }
        }

        public async Task<string> RefreshAccessTokenAsync(string refreshToken)
        {
            try
            {
                var tokenResponse = _oauthClient.GetAccessToken(
                    _environment,
                    refreshToken,
                    _config.ScopesCsv.Split(',')
                );

                return tokenResponse.AccessToken.Token;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error refreshing access token: {ex.Message}");
                throw;
            }
        }
        
        private void SaveRefreshToken(string refreshToken)
        {
            try
            {
                File.WriteAllText(_tokenFileName, refreshToken);
                Console.WriteLine($"Refresh token for {_environment} saved to {_tokenFileName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save refresh token to {_tokenFileName}: {ex.Message}");
            }
        }
        
        private void LoadRefreshToken()
        {
            try
            {
                if (File.Exists(_tokenFileName))
                {
                    string refreshToken = File.ReadAllText(_tokenFileName).Trim();
                    if (!string.IsNullOrEmpty(refreshToken))
                    {
                        _config.RefreshToken = refreshToken;
                        Console.WriteLine($"Loaded refresh token for {_environment} from {_tokenFileName}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading refresh token from {_tokenFileName}: {ex.Message}");
            }
        }
    }
}
