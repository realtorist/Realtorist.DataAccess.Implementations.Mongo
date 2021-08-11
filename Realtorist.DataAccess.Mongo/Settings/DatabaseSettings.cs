namespace Realtorist.DataAccess.Mongo.Settings
{
    /// <summary>
    /// Implements settings for the database connection
    /// </summary>
    public class DatabaseSettings : IDatabaseSettings
    {
        /// <inheritdoc/>
        public string ConnectionString { get; set; }

        /// <inheritdoc/>
        public string DatabaseName { get; set; }
    }
}
