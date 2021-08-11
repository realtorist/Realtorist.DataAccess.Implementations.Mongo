using MongoDB.Driver;
using Realtorist.DataAccess.Abstractions;
using Realtorist.DataAccess.Implementations.Mongo.Settings;
using Realtorist.Models.Helpers;
using Realtorist.Models.Settings;
using System;
using System.Threading.Tasks;

namespace Realtorist.DataAccess.Implementations.Mongo.DataAccess
{
    /// <summary>
    /// Implements access to settings in MongoDB
    /// </summary>
    public class SettingsDataAccess : ISettingsDataAccess
    {
        private readonly IMongoCollection<Setting> _settingsCollection;

        public SettingsDataAccess(IDatabaseSettings databaseSettings)
        {
            if (databaseSettings == null) throw new ArgumentNullException(nameof(databaseSettings));

            var client = new MongoClient(databaseSettings.ConnectionString);
            var database = client.GetDatabase(databaseSettings.DatabaseName);

            _settingsCollection = database.GetCollection<Setting>(nameof(Setting) + "s");
        }

        public async Task<T> GetSettingAsync<T>(string type)
        {
            var setting = await _settingsCollection.Find(s => s.Id == type).FirstOrDefaultAsync();
            return setting != null ? Extensions.FromJson<T>(Extensions.ToJson(setting.Value)) : null;
        }

        public async Task<dynamic> GetSettingAsync(string type)
        {
            var setting = await _settingsCollection.Find(s => s.Id == type).FirstOrDefaultAsync();
            return setting != null ? setting?.Value : null;
        }

        public async Task UpdateSettingsAsync(string type, object newSettings)
        {
            var existing = await _settingsCollection.FindAsync(s => s.Id == type);
            if (await existing.AnyAsync())
            {
                var update = Builders<Setting>.Update.Set(s => s.Value, newSettings);
                await _settingsCollection.FindOneAndUpdateAsync(s => s.Id == type, update);

                return;
            }

            await _settingsCollection.InsertOneAsync(new Setting {
                Id =  type,
                Value = newSettings
            });
        }
    }
}
