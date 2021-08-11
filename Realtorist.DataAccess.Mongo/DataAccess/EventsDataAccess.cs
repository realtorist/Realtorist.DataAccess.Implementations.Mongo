using AutoMapper;
using MongoDB.Driver;
using Realtorist.DataAccess.Abstractions;
using Realtorist.DataAccess.Mongo.Settings;
using Realtorist.Models.Blog;
using Realtorist.Models.Enums;
using Realtorist.Models.Events;
using Realtorist.Models.Pagination;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Realtorist.DataAccess.Mongo.DataAccess
{
    public class EventsDataAccess : IEventsDataAccess
    {
        private readonly IMongoCollection<Event> _eventsCollection;

        public EventsDataAccess(IDatabaseSettings databaseSettings)
        {
            if (databaseSettings == null) throw new ArgumentNullException(nameof(databaseSettings));

            var client = new MongoClient(databaseSettings.ConnectionString);
            var database = client.GetDatabase(databaseSettings.DatabaseName);

            _eventsCollection = database.GetCollection<Event>(nameof(Event) + "s");
        }

        public async Task<Guid> CreateEventAsync(Event eventToAdd)
        {
            eventToAdd.Id = Guid.NewGuid();

            await _eventsCollection.InsertOneAsync(eventToAdd);

            return eventToAdd.Id;
        }

        public async Task<long> DeleteAllEventsAsync()
        {
            var result = await _eventsCollection.DeleteManyAsync(e => true);
            return result.DeletedCount;
        }

        public async Task<long> DeleteOldEventsAsync(DateTime maxDate)
        {
            var result = await _eventsCollection.DeleteManyAsync(e => e.CreatedAt < maxDate);
            return result.DeletedCount;
        }

        public async Task<PaginationResult<Event>> GetEventsAsync(PaginationRequest request, IDictionary<string, string> filter)
        {
            return await _eventsCollection
                .AsQueryable()
                .Filter(filter)
                .GetPaginationResultAsync(request);
        }

        public async Task<List<Event>> GetEventsAsync(DateTime startTimeUtc)
        {
            var filter = new FilterDefinitionBuilder<Event>().Gte(e => e.CreatedAt, startTimeUtc);
            return await _eventsCollection.Find(filter).ToListAsync();
        }
    }
}
