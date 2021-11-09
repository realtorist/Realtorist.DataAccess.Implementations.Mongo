using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Bson.Serialization;
using Realtorist.DataAccess.Abstractions;
using Realtorist.DataAccess.Implementations.Mongo.DataAccess;
using Realtorist.DataAccess.Implementations.Mongo.Serialization;
using Realtorist.DataAccess.Implementations.Mongo.Settings;
using Realtorist.Extensions.Base;
using Realtorist.Extensions.Base.Helpers;

namespace Realtorist.DataAccess.Implementations.Mongo
{
    /// <summary>
    /// Describes extension for MongoDB data access
    /// </summary>
    public class MongoDataAccessExtension : IConfigureServicesExtension
    {
        public int Priority => (int)ExtensionPriority.RegisterDefaultImplementations;

        public void ConfigureServices(IServiceCollection services, IServiceProvider serviceProvider)
        {
            var configuration = serviceProvider.GetService<IConfiguration>();
            services.Configure<DatabaseSettings>(configuration.GetSection(nameof(DatabaseSettings)));

            services.AddSingleton<IDatabaseSettings>(sp => sp.GetRequiredService<IOptions<DatabaseSettings>>().Value);

            services.AddSingletonServiceIfNotRegisteredYet<IListingsDataAccess, ListingsDataAccess>();
            services.AddSingletonServiceIfNotRegisteredYet<ICustomerRequestsDataAccess, CustomerRequestsDataAccess>();
            services.AddSingletonServiceIfNotRegisteredYet<IBlogDataAccess, BlogDataAccess>();
            services.AddSingletonServiceIfNotRegisteredYet<IPagesDataAccess, PagesDataAccess>();
            services.AddSingletonServiceIfNotRegisteredYet<ISettingsDataAccess, SettingsDataAccess>();
            services.AddSingletonServiceIfNotRegisteredYet<IEventsDataAccess, EventsDataAccess>();

            BsonSerializer.RegisterSerializationProvider(new EnumSerializerProvider());
            BsonSerializer.RegisterSerializationProvider(new JTokenSerializerProvider());
        }
    }
}
