using System.Data.Common;
using Microsoft.Extensions.Configuration;
using MySqlConnector;

namespace CIS.Phase2.CrowdsourcedIdeation.Infrastructure.Fallback.HealthChecks;

internal sealed class MySqlConnectionFactory(IConfiguration configuration) : IMySqlConnectionFactory
{
    public DbConnection Create()
    {
        var cs = configuration.GetConnectionString("DefaultConnection")
                 ?? "Server=localhost;Port=3307;Database=sd3;User Id=sd3user;Password=sd3pass;SslMode=None;AllowPublicKeyRetrieval=true;";
        // Ensure probes fail fast when the database is down.
        if (!cs.Contains("ConnectionTimeout", StringComparison.OrdinalIgnoreCase))
            cs += ";ConnectionTimeout=3";
        if (!cs.Contains("DefaultCommandTimeout", StringComparison.OrdinalIgnoreCase))
            cs += ";DefaultCommandTimeout=3";
        return new MySqlConnection(cs);
    }
}
