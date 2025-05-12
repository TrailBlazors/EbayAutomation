using EbayAutomation.Models;

namespace EbayAutomation.Interfaces
{
    public interface IEbayOfferService
    {
        Task<string> CreateOfferAsync(EbayOffer offer);
        Task<bool> DeleteOfferAsync(string listingId);
        Task<List<EbayOffer>> GetActiveOffersAsync(int pageSize = 100, int pageNumber = 1);
        Task<EbayOffer> GetOfferAsync(string listingId);
        Task<bool> UpdateOfferAsync(string listingId, EbayOffer updatedOffer);
    }
}