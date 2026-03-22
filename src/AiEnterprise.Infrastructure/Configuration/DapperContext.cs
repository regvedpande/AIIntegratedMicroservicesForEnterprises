using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace AiEnterprise.Infrastructure.Configuration;

public class DapperContext
{
    internal readonly string ConnectionString;

    public DapperContext(IConfiguration configuration)
    {
        ConnectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection string is not configured.");
    }

    public IDbConnection CreateConnection() => new SqlConnection(ConnectionString);

    public IDbConnection CreateMasterConnection()
    {
        var builder = new SqlConnectionStringBuilder(ConnectionString);
        builder.InitialCatalog = "master";
        return new SqlConnection(builder.ConnectionString);
    }

    public string DatabaseName => new SqlConnectionStringBuilder(ConnectionString).InitialCatalog;
}
