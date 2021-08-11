using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Realtorist.DataAccess.Abstractions;
using Realtorist.DataAccess.Implementations.Mongo.Settings;
using Realtorist.Models.Listings;
using Realtorist.Models.Enums;
using Realtorist.Models.Enums.LookupTypes;
using Realtorist.Models.Helpers;
using Realtorist.Models.Models;
using Realtorist.Models.Pagination;
using Realtorist.Models.Search;
using Realtorist.Models.Search.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using System.Linq.Expressions;
using Realtorist.Models.Listings.Enums;
using Realtorist.Models.Exceptions;

namespace Realtorist.DataAccess.Implementations.Mongo.DataAccess
{
    /// <summary>
    /// Implements access to data in MongoDB
    /// </summary>
    public class ListingsDataAccess : IListingsDataAccess
    {
        private readonly IMongoCollection<Listing> _listings;

        private readonly IMapper _mapper;

        public ListingsDataAccess(IDatabaseSettings settings, IMapper mapper)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            var client = new MongoClient(settings.ConnectionString);
            var database = client.GetDatabase(settings.DatabaseName);

            _listings = database.GetCollection<Listing>((nameof(Listing) + "s"));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        public async Task<Guid> AddNewListingAsync(Listing listing)
        {
            if (listing is null) throw new ArgumentNullException(nameof(listing));

            await _listings.InsertOneAsync(listing);

            return listing.Id;
        }

        public async Task AddNewListingsAsync(IEnumerable<Listing> listings)
        {
            if (listings.IsNullOrEmpty()) throw new ArgumentNullException(nameof(listings));

            await _listings.InsertManyAsync(listings);
        }

        public async Task<List<Listing>> GetAllListingsAsync()
        {
            return await GetAllListingsAsync<Listing>();
        }

        public async Task<List<T>> GetAllListingsAsync<T>()
        {
            return await _listings
                .Find(FilterDefinition<Listing>.Empty)
                .Project<Listing, T>(_mapper)
                .ToListAsync();
        }

        public async Task<List<Listing>> GetListingsWithEmptyCoordinatesAsync()
        {
            return await _listings.Find(l => l.Address != null && l.Address.Coordinates == null).ToListAsync();
        }

        public async Task<List<Listing>> GetFeaturedListingsAsync(int limit = 10, bool takeRandomIfNotEnough = false)
        {
            var listings = await _listings
                .AsQueryable()
                .Where(listing => listing.Featured)
                .Sample(limit)
                .ToListAsync();
                
            if (listings.Count >= limit || !takeRandomIfNotEnough)
            {
                return listings;
            }

            var existingIds = listings.Select(listing => listing.Id).ToArray();
            
            var extraListings = await _listings
                .AsQueryable()
                .Where(listing => !existingIds.Contains(listing.Id) && listing.Photos.Length > 0)
                .Sample(limit - listings.Count)
                .ToListAsync();

            return listings.Union(extraListings).ToList();
        }

        public async Task<List<string>> GetIdsAsync(ListingSource source)
        {
            return await _listings
                .Find(l => l.Source == source)
                .Project(listing => listing.ExternalId)
                .ToListAsync();
        }

        public async Task<DateTime?> GetLatestUpdateDateTimeAsync(ListingSource source)
        {
            var listing = await _listings
                .Find(l => l.Source == source)
                .SortByDescending(l => l.LastUpdated)
                .FirstOrDefaultAsync();

            return listing?.LastUpdated;
        }

        public async Task<Listing> GetListingAsync(Guid id)
        {
            var listing = await _listings
                .Find(listing => listing.Id == id)
                .FirstOrDefaultAsync();

            if (listing is null)
            {
                throw new NotFoundException($"Listing with id {id} wasn't found");
            }

            return listing;
        }

        public async Task<ListingSearchSuggestion[]> GetListingSearchSuggestionsAsync(string query, int limit = 5)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                query = string.Empty;
            }

            query = query.Trim().ToLower();

            var builder = Builders<Listing>.Filter;
            var filter = builder.Where(x => x.MlsNumber.ToLower().Contains(query));
            filter |= builder.Text(query);

            var results = await _listings
                .Find(filter)
                .Limit(limit)
                .Project(listing => new ListingSearchSuggestion
                {
                    ListingId = listing.Id,
                    MlsNumber = listing.MlsNumber,
                    TransactionType = listing.TransactionType ?? TransactionType.For_sale,
                    Address = listing.Address
                }).ToListAsync();

            return results.ToArray();
        }

        public async Task RemoveListingsAsync(IEnumerable<Guid> ids)
        {
            await _listings.DeleteManyAsync(listing => ids.Contains(listing.Id));
        }

        public async Task RemoveListingsAsync(ListingSource source, IEnumerable<string> externalIds)
        {
            await _listings.DeleteManyAsync(listing => listing.Source == source && externalIds.Contains(listing.ExternalId));
        }

        public async Task RemoveListingsAsync(params Guid[] ids)
        {
            await RemoveListingsAsync(ids.AsEnumerable());
        }

        public async Task<ListingSearchResult> SearchAsync(ListingSearchRequest search)
        {
            if (search is null) throw new ArgumentNullException(nameof(search));

            var builder = Builders<Listing>.Filter;
            var filter = builder.Empty;

            if (search.TransactionType.HasValue)
            {
                var transactionTypes = search.TransactionType == TransactionTypeSearch.ForSale ? Constants.SaleTypes : Constants.RentTypes;
                filter &= builder.Where(x => x.TransactionType != null && transactionTypes.Contains(x.TransactionType));
            }
            
            if (search.PropertyType != null)
            {
                filter &= builder.Eq(x => x.PropertyType, search.PropertyType.Value);
            }

            if (search.OwnershipType != null)
            {
                filter &= builder.Eq(x => x.OwnershipType, search.OwnershipType.Value);
            }

            if (search.BuildingType != null)
            {
                filter &= builder.Ne(x => x.Building, null);
                filter &= builder.AnyEq(x => x.Building.Type, search.BuildingType.Value);
            }

            if (search.ConstructionStyle != null)
            {
                filter &= builder.Ne(x => x.Building, null);
                filter &= builder.Eq(x => x.Building.ConstructionStyleAttachment, search.ConstructionStyle.Value);
            }

            if (search.Neighbourhood != null)
            {
                filter &= builder.Eq(x => x.Address.Neighbourhood, search.Neighbourhood);
            }

            if (search.CommunityName != null)
            {
                filter &= builder.Eq(x => x.Address.CommunityName, search.CommunityName);
            }

            if (search.Subdivision != null)
            {
                filter &= builder.Eq(x => x.Address.Subdivision, search.Subdivision);
            }

            if (search.City != null)
            {
                filter &= builder.Eq(x => x.Address.City, search.City);
            }

            if (search.PostalCode != null)
            {
                filter &= builder.Regex(x => x.Address.PostalCode, $"^{search.PostalCode}");
            }

            if (!search.Boundaries.IsNullOrEmpty())
            {
                filter &= builder.Where(x => x.Address.Coordinates != null
                    && x.Address.Coordinates.Latitude >= search.Boundaries.SouthWest.Latitude
                    && x.Address.Coordinates.Latitude <= search.Boundaries.NorthEast.Latitude
                    && x.Address.Coordinates.Longitude >= search.Boundaries.SouthWest.Longitude
                    && x.Address.Coordinates.Longitude <= search.Boundaries.NorthEast.Longitude);
            }

            if (search.MinPrice.HasValue)
            {
                filter &= builder.Gte(x => x.Price, search.MinPrice.Value);
            }

            if (search.MaxPrice.HasValue)
            {
                filter &= builder.Lte(x => x.Price, search.MaxPrice.Value);
            }

            if (search.MinAreqSqFt.HasValue)
            {
                filter &= builder.Where(x => x.Building.TotalFinishedArea != null && x.Building.TotalFinishedArea.Value >= search.MinAreqSqFt.Value);
            }

            if (search.Garage)
            {
                filter &= builder.Where(x => x.ParkingSpaces != null && x.ParkingSpaces.Any(p => Constants.GarageTypes.Contains(p.Name)));
            }

            if (search.Waterfront)
            {
                filter &= builder.Ne(x => x.WaterFront.Type, null);
            }

            if (search.Bedrooms != RoomNumberSearch.Any)
            {
                filter &= builder.Ne(x => x.Building.BedroomsTotal, null);
                filter &= search.Bedrooms < RoomNumberSearch.OnePlus
                    ? builder.Eq(x => x.Building.BedroomsTotal, (int)search.Bedrooms)
                    : builder.Gte(x => x.Building.BedroomsTotal, (int)search.Bedrooms - (int)RoomNumberSearch.OnePlus + 1);
            }

            if (search.Bathrooms != RoomNumberSearch.Any)
            {
                filter &= builder.Ne(x => x.Building.BathroomTotal, null);
                filter &= search.Bathrooms < RoomNumberSearch.OnePlus
                    ? builder.Eq(x => x.Building.BathroomTotal, (int)search.Bathrooms)
                    : builder.Gte(x => x.Building.BathroomTotal, (int)search.Bathrooms - (int)RoomNumberSearch.OnePlus + 1);
            }

            var cursor = _listings.Find(filter);
            var count = await cursor.CountDocumentsAsync();

            var coordinates = await cursor.Project(x => new ListingCoordinates
            {
                ListingId = x.Id,
                Type = x.PropertyType ?? PropertyType.Other,
                Latitude = x.Address.Coordinates.Latitude,
                Longitude = x.Address.Coordinates.Longitude
            }).ToListAsync();

            cursor = search.SortBy switch
            {
                ListingsSortBy.PriceAsc => cursor.SortBy(x => x.Price),
                ListingsSortBy.PriceDesc => cursor.SortByDescending(x => x.Price),
                ListingsSortBy.DateNewest => cursor.SortByDescending(x => x.LastUpdated),
                ListingsSortBy.DateOldest => cursor.SortBy(x => x.LastUpdated),
                _ => cursor
            };

            var selected = await cursor.Skip(search.Pagination.Offset).Limit(search.Pagination.Limit).ToListAsync();
            var result = new PaginationResult<Listing>
            {
                Limit = search.Pagination.Limit,
                Offset = search.Pagination.Offset,
                TotalRecords = (int)count,
                Results = selected.ToArray()
            };

            return new ListingSearchResult
            {
                Search = search,
                Result = result,
                Coordinates = coordinates.ToArray()
            };
        }

        // public async Task UpdateListingDetailsAsync(Guid id, PropertyDetails details)
        // {
        //     if (details is null) throw new ArgumentNullException(nameof(details));

        //     var listing = await _listings.Find(l => l.Id == id).FirstAsync();
        //     listing.Details = details;

        //     await _listings.ReplaceOneAsync(l => l.Id == id, listing);
        // }

        public async Task UpdateListingCoordinatesAsync(Guid id, Coordinates coordinates)
        {
            if (coordinates is null) throw new ArgumentNullException(nameof(coordinates));

            var listing = await _listings.Find(l => l.Id == id).FirstAsync();
            var update = new UpdateDefinitionBuilder<Listing>()
                .Set(l => l.Address.Coordinates, coordinates);

            await _listings.UpdateOneAsync(l => l.Id == id, update);
        }

        public async Task<bool> UpdateOrAddListingAsync(Guid id, Listing listing)
        {
            return await UpdateOrAddListingAsync(l => l.Id == id, listing);
        }

        public async Task<bool> UpdateOrAddListingAsync(string externalId, ListingSource source, Listing listing, bool saveCoordinates = false, bool saveDisabledAndFeatured = false)
        {
            return await UpdateOrAddListingAsync(l => l.ExternalId == externalId && l.Source == source, listing, saveCoordinates, saveDisabledAndFeatured);
        }

        public Task<PaginationResult<Listing>> GetListingsAsync(PaginationRequest request, IDictionary<string, string> filter)
        {
            return GetListingsAsync<Listing>(request, filter);
        }

        public async Task<PaginationResult<T>> GetListingsAsync<T>(PaginationRequest request, IDictionary<string, string> filter)
        {
            return await _listings
                .AsQueryable()
                .Project<Listing, T>(_mapper)
                .Filter(filter)
                .GetPaginationResultAsync(request);
        }

        public async Task MarkListingAsFeaturedAsync(Guid listingId, bool isFeatured)
        {
            var update = Builders<Listing>.Update.Set(l => l.Featured, isFeatured);
            await _listings.UpdateOneAsync(l => l.Id == listingId, update);
        }

        public async Task MarkListingAsDisabledAsync(Guid listingId, bool isDisabled)
        {
            var update = Builders<Listing>.Update.Set(l => l.Disabled, isDisabled);
            await _listings.UpdateOneAsync(l => l.Id == listingId, update);
        }

        private async Task<bool> UpdateOrAddListingAsync(Expression<Func<Listing, bool>> select, Listing listing, bool saveCoordinates = false, bool saveDisabledAndFeatured = false)
        {
            if (listing is null) throw new ArgumentNullException(nameof(listing));

            var oldListing = await _listings.Find(select).FirstOrDefaultAsync();
            if (oldListing is not null)
            {
                listing.Id = oldListing.Id;
                if (saveCoordinates && oldListing.Address?.Coordinates.IsNullOrEmpty() == false && oldListing.Address.EqualsNoCoordinates(listing.Address))
                {
                    listing.Address.Coordinates = oldListing.Address.Coordinates;
                }

                if (saveDisabledAndFeatured)
                {
                    listing.Disabled = oldListing.Disabled;
                    listing.Featured = oldListing.Featured;
                }

                await _listings.ReplaceOneAsync(select, listing);
                return true;
            }
            else
            {
                await AddNewListingAsync(listing);
                return false;
            }
        }

        public async Task IncrementListingViews(Guid listingId)
        {
            var update = new UpdateDefinitionBuilder<Listing>().Inc(l => l.Views, 1);

            await _listings.UpdateOneAsync(l => l.Id == listingId, update);
        }

        public async Task<List<Listing>> GetSimilarListingsAsync(Guid listingId, double maxPriceDelta = 0.1, double? maxDistinaceInKm = null, int maxNumberOfListings = 10)
        {
            var listing = await _listings.Find(l => l.Id == listingId).FirstAsync();

            var builder = new FilterDefinitionBuilder<Listing>();

            var query = builder.Ne(l => l.Id, listingId);
            
            query &= builder.Eq(l => l.TransactionType, listing.TransactionType);
            query &= builder.Eq(l => l.PropertyType, listing.PropertyType);

            if (listing.Building?.Type.IsNullOrEmpty() == false) 
            {
                query &= builder.Ne(l => l.Building, null);
                query &= builder.Ne(l => l.Building.Type, null);
                query &= builder.AnyIn(l => l.Building.Type, listing.Building.Type);
            }
            
            if (listing.Price.HasValue)
            {
                query &= builder.Ne(l => l.Price, null);
                var maxPrice = listing.Price * (1 + maxPriceDelta);
                var minPrice = listing.Price * (1 - maxPriceDelta);

                query &= builder.Gte(l => l.Price, minPrice);
                query &= builder.Lte(l => l.Price, maxPrice);
            }
            
            if (maxDistinaceInKm != null && listing.Address.Coordinates != null)
            {
                var boundaries = CoordinateBoundaries.FromCenterAndDistanceToCorner(listing.Address.Coordinates, maxDistinaceInKm.Value * 1000);
                query &= builder.Ne(x => x.Address.Coordinates, null);
                query &= builder.Gte(l => l.Address.Coordinates.Latitude, boundaries.SouthWest.Latitude);
                query &= builder.Lte(l => l.Address.Coordinates.Latitude, boundaries.NorthEast.Latitude);
                query &= builder.Gte(l => l.Address.Coordinates.Longitude, boundaries.SouthWest.Longitude);
                query &= builder.Lte(l => l.Address.Coordinates.Longitude, boundaries.NorthEast.Longitude);
            }

            return await _listings.Find(query).Limit(maxNumberOfListings).ToListAsync(); 
        }
    }
}
