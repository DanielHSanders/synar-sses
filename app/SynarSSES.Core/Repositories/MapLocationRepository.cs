using Dapper;
using Npgsql;
using SynarSSES.Core.Models;

namespace SynarSSES.Core.Repositories;

// Reads the sampling frame from `map_location`. The Synar frame is the
// subset where status='Valid' -- pins that are confirmed to be operating
// tobacco retailers eligible for inspection.
public sealed class MapLocationRepository
{
    private readonly string _connectionString;

    public MapLocationRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<int> CountValidAsync()
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        return await conn.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM map_location WHERE status = 'Valid'");
    }

    public async Task<IReadOnlyList<SampledOutlet>> GetValidOutletsAsync()
    {
        const string sql = """
            SELECT synar_maps_id,
                   canonical_name    AS name,
                   canonical_address AS address,
                   canonical_city    AS city,
                   canonical_state   AS state,
                   canonical_zip     AS zip,
                   latitude,
                   longitude,
                   business_type
            FROM map_location
            WHERE status = 'Valid'
            ORDER BY synar_maps_id
            """;
        await using var conn = new NpgsqlConnection(_connectionString);
        var rows = await conn.QueryAsync<SampledOutlet>(sql);
        return rows.ToList();
    }
}
