using AutoMapper;
using MongoDB.Driver;
using Realtorist.DataAccess.Abstractions;
using Realtorist.DataAccess.Implementations.Mongo.Settings;
using Realtorist.Models.CustomerRequests;
using Realtorist.Models.Dto;
using Realtorist.Models.Pagination;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Realtorist.DataAccess.Implementations.Mongo.DataAccess
{
    /// <summary>
    /// Implements access to customer requests in MongoDB
    /// </summary>
    public class CustomerRequestsDataAccess : ICustomerRequestsDataAccess
    {
        private readonly IMongoCollection<CustomerRequest> _requestsCollection;
        private readonly IMapper _mapper;

        public CustomerRequestsDataAccess(IDatabaseSettings databaseSettings, IMapper mapper)
        {
            if (databaseSettings == null) throw new ArgumentNullException(nameof(databaseSettings));

            var client = new MongoClient(databaseSettings.ConnectionString);
            var database = client.GetDatabase(databaseSettings.DatabaseName);

            _requestsCollection = database.GetCollection<CustomerRequest>(nameof(CustomerRequest) + "s");
            _mapper = mapper ??  throw new ArgumentNullException(nameof(mapper));
        }

        public async Task AddCustomerRequestAsync(RequestInformationModel customerRequest)
        {
            if (customerRequest is null) throw new ArgumentNullException(nameof(customerRequest));

            await _requestsCollection.InsertOneAsync(new CustomerRequest
            {
                DateTimeUtc = DateTime.UtcNow,
                Request = customerRequest
            });
        }

        public async Task DeleteCustomerRequestAsync(Guid id)
        {
            await _requestsCollection.DeleteOneAsync(r => r.Id == id);
        }

        public async Task<CustomerRequest> GetCustomerRequestAsync(Guid id)
        {
            return await _requestsCollection.Find(r => r.Id == id).FirstAsync();
        }

        public async Task<PaginationResult<CustomerRequest>> GetCustomerRequestsAsync(PaginationRequest paginationRequest)
        {
            return await GetCustomerRequestsAsync<CustomerRequest>(paginationRequest);
        }

        public async Task<PaginationResult<T>> GetCustomerRequestsAsync<T>(PaginationRequest paginationRequest)
        {
            return await _requestsCollection
                .Find(FilterDefinition<CustomerRequest>.Empty)
                .Project<CustomerRequest, T>(_mapper)
                .GetPaginationResultAsync(paginationRequest);
        }

        public async Task<int> GetUnreadCustomerRequestsCountAsync()
        {
            return (int)await _requestsCollection.CountDocumentsAsync(r => !r.Read);
        }

        public async Task MarkAllRequestsAsReadAsync(bool read = true)
        {
            await _requestsCollection.UpdateManyAsync(r => r.Read != read, new UpdateDefinitionBuilder<CustomerRequest>().Set(r => r.Read, read));
        }

        public async Task MarkRequestAsReadAsync(Guid id, bool read = true)
        {
            await _requestsCollection.UpdateOneAsync(r => r.Id == id, new UpdateDefinitionBuilder<CustomerRequest>().Set(r => r.Read, read));
        }

        public async Task MarkRequestsAsReadAsync(IEnumerable<Guid> ids, bool read = true)
        {
            await _requestsCollection.UpdateOneAsync(r => ids.Contains(r.Id), new UpdateDefinitionBuilder<CustomerRequest>().Set(r => r.Read, read));
        }

        public async Task ReplyAsync(Guid id, CustomerRequestReply reply)
        {
            if (reply is null) throw new ArgumentNullException(nameof(reply));

            var update = new UpdateDefinitionBuilder<CustomerRequest>().AddToSet(r => r.Replies, reply);
            await _requestsCollection.UpdateOneAsync(r => r.Id == id, update);
        }
    }
}
