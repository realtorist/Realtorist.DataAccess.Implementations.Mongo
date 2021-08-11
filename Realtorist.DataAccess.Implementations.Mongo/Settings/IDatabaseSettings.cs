using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Realtorist.DataAccess.Implementations.Mongo.Settings
{
    /// <summary>
    /// Describes settings for the database connection
    /// </summary>
    public interface IDatabaseSettings
    {
        /// <summary>
        /// Gets or sets connection string
        /// </summary>
        string ConnectionString { get; set; }

        /// <summary>
        /// Gets or sets database name
        /// </summary>
        string DatabaseName { get; set; }
    }
}
