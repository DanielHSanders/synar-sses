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

    public async Task<int> CountValidAsync(int? abcLicenseYear = null)
    {
        var filter = abcLicenseYear.HasValue
            ? "AND EXISTS (SELECT 1 FROM source_record sr WHERE sr.synar_maps_id = m.synar_maps_id "
              + "AND sr.source = 'ABC' AND sr.source_year = @Year)"
            : "";
        await using var conn = new NpgsqlConnection(_connectionString);
        return await conn.ExecuteScalarAsync<int>(
            $"SELECT count(*) FROM map_location m WHERE m.status = 'Valid' {filter}",
            new { Year = abcLicenseYear });
    }

    // Returns the sampling frame. If `abcLicenseYear` is set, the frame is
    // restricted to outlets that also hold a current ABC license -- the most
    // reliable signal that the outlet actually sells tobacco.
    public async Task<IReadOnlyList<SampledOutlet>> GetValidOutletsAsync(int? abcLicenseYear = null)
    {
        var filter = abcLicenseYear.HasValue
            ? "AND EXISTS (SELECT 1 FROM source_record sr WHERE sr.synar_maps_id = m.synar_maps_id "
              + "AND sr.source = 'ABC' AND sr.source_year = @Year)"
            : "";
        var sql = $"""
            SELECT m.synar_maps_id,
                   m.canonical_name    AS name,
                   m.canonical_address AS address,
                   m.canonical_city    AS city,
                   m.canonical_state   AS state,
                   m.canonical_zip     AS zip,
                   m.latitude,
                   m.longitude,
                   m.business_type
            FROM map_location m
            WHERE m.status = 'Valid' {filter}
            ORDER BY m.synar_maps_id
            """;
        await using var conn = new NpgsqlConnection(_connectionString);
        var rows = await conn.QueryAsync<SampledOutlet>(sql, new { Year = abcLicenseYear });
        return rows.ToList();
    }
}
