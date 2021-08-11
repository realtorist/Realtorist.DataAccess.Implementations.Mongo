using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Bson.Serialization;
using Realtorist.DataAccess.Abstractions;
using Realtorist.DataAccess.Mongo.DataAccess;
using Realtorist.DataAccess.Mongo.Serialization;
using Realtorist.DataAccess.Mongo.Settings;

namespace Realtorist.DataAccess.Mongo
{
    /// <summary>
    /// Provides dependency injection helper methods
    /// </summary>
    public static class DependencyInjectionHelper
    {
        /// <summary>
        /// Configures services related to data access
        /// </summary>
        /// <param name="services">Services collection</param>
        /// <param name="configuration">App configuration</param>
        public static void ConfigureMongoDataAccessServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<DatabaseSettings>(configuration.GetSection(nameof(DatabaseSettings)));

            services.AddSingleton<IDatabaseSettings>(sp => sp.GetRequiredService<IOptions<DatabaseSettings>>().Value);

            services.AddSingleton<IListingsDataAccess, ListingsDataAccess>();
            services.AddSingleton<ICustomerRequestsDataAccess, CustomerRequestsDataAccess>();
            services.AddSingleton<IBlogDataAccess, BlogDataAccess>();
            services.AddSingleton<IPagesDataAccess, PagesDataAccess>();
            services.AddSingleton<ISettingsDataAccess, SettingsDataAccess>();
            services.AddSingleton<IEventsDataAccess, EventsDataAccess>();

            BsonSerializer.RegisterSerializationProvider(new EnumSerializerProvider());
            BsonSerializer.RegisterSerializationProvider(new JTokenSerializerProvider());
        }
    }
}
