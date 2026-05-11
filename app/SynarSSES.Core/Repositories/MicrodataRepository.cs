using Dapper;
using Npgsql;
using SynarSSES.Core.Models;

namespace SynarSSES.Core.Repositories;

// Assembles SSES microdata rows from the shared `synar` database. Reads
// inspections from `synarcheck` and joins inspector demographics from
// `synar_inspector` (gender + age). For Kentucky, all rows are treated as a
// single sampling stratum with frame size taken from a configured argument
// (matches the 2025 submission which reports stratum 1 with frame=4452).
public sealed class MicrodataRepository
{
    private readonly string _connectionString;

    public MicrodataRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    // Pull all inspections for a given check year, mapped to the Table 5
    // microdata shape. `samplingFrameSize` is the count of outlets in the
    // sampling frame for the year (the population N). For KY-2025 this is
    // 4452, matching `Synar2025_SSES_Final.xlsx` Table 2.
    public async Task<IReadOnlyList<MicrodataRow>> GetMicrodataAsync(
        int checkYear, int samplingFrameSize)
    {
        const string sql = """
            SELECT
                sc.synarfullid                                    AS synar_full_id,
                '1'                                                AS sampling_stratum,
                @FrameSize                                         AS sampling_stratum_population,
                '1'                                                AS variance_stratum,
                @FrameSize                                         AS variance_stratum_population,
                CASE
                    WHEN sc.status = 'Sale' OR sc.status = 'NS' THEN 'EC'
                    WHEN sc.ineligible::int = 1 THEN
                        CASE
                            WHEN UPPER(sc.ineligiblereason) LIKE '%OUT OF BUSINESS%'      THEN 'I1'
                            WHEN UPPER(sc.ineligiblereason) LIKE '%DOES NOT SELL TOBACCO%' THEN 'I2'
                            WHEN UPPER(sc.ineligiblereason) LIKE '%CLOSED FOR A PERIOD%'   THEN 'I5'
                            WHEN UPPER(sc.ineligiblereason) LIKE '%TEMPORARY CLOSURE%'     THEN 'I5'
                            WHEN UPPER(sc.ineligiblereason) LIKE '%INACCESSIBLE%'          THEN 'I3'
                            WHEN UPPER(sc.ineligiblereason) LIKE '%PRIVATE CLUB%'          THEN 'I4'
                            WHEN UPPER(sc.ineligiblereason) LIKE '%PRIVATE RESIDENCE%'     THEN 'I4'
                            WHEN UPPER(sc.ineligiblereason) LIKE '%CANT BE LOCATED%'       THEN 'I6'
                            WHEN UPPER(sc.ineligiblereason) LIKE '%CAN''T BE LOCATED%'     THEN 'I6'
                            WHEN UPPER(sc.ineligiblereason) LIKE '%WHOLESALE%'             THEN 'I7'
                            WHEN UPPER(sc.ineligiblereason) LIKE '%VENDING MACHINE%'       THEN 'I8'
                            WHEN UPPER(sc.ineligiblereason) LIKE '%DUPLICATE%'             THEN 'I9'
                            ELSE 'I10'
                        END
                    ELSE 'EC'
                END                                                AS disposition_code,
                CASE WHEN sc.soldtobacco::int = 1 THEN true
                     WHEN sc.soldtobacco::int = 0 THEN false
                     ELSE NULL END                                 AS violation,
                NULL                                               AS outlet_type,
                sc.ia                                              AS inspector_id,
                si.gender                                          AS inspector_gender,
                sc.iaage                                           AS inspector_age,
                NULL::int                                          AS vm_frame_size,
                CASE
                    WHEN sc.checktype = 'Cigarette' THEN 1
                    WHEN sc.checktype = 'Smokeless' THEN 3
                    WHEN sc.checktype = 'E-cig'     THEN 4
                    ELSE NULL END                                  AS product_type,
                NULL::int                                          AS retail_outlet_type,
                NULL                                               AS asked_for_id
            FROM synarcheck sc
            LEFT JOIN synar_inspector si
                   ON si.check_year = sc.checkyear
                  AND si.ia_code    = sc.ia
            WHERE sc.checkyear = @Year
            ORDER BY sc.synarcheckid
            """;

        await using var conn = new NpgsqlConnection(_connectionString);
        var rows = await conn.QueryAsync<MicrodataRow>(
            sql, new { Year = (short)checkYear, FrameSize = samplingFrameSize });
        return rows.ToList();
    }
}
