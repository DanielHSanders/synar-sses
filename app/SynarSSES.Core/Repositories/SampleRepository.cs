using Dapper;
using Npgsql;
using SynarSSES.Core.Models;

namespace SynarSSES.Core.Repositories;

// Reads and writes synar_sample, the record of what was drawn each year.
public sealed class SampleRepository
{
    private readonly string _connectionString;

    public SampleRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    // The draw a report for this year should be based on: the one marked
    // final if there is one, otherwise the most recent.
    public async Task<SampleRecord?> GetForYearAsync(int checkYear)
    {
        const string sql = """
            SELECT sample_id, check_year, state_code, drawn_at, frame_size,
                   frame_filter, original_sample_size, is_final, note
            FROM synar_sample
            WHERE check_year = @CheckYear
            ORDER BY is_final DESC, drawn_at DESC
            LIMIT 1
            """;
        await using var conn = new NpgsqlConnection(_connectionString);
        return await conn.QuerySingleOrDefaultAsync<SampleRecord>(
            sql, new { CheckYear = (short)checkYear });
    }

    public async Task<int> RecordAsync(
        int checkYear, string stateCode, SampleSizeInput input, SampleSizeResult result,
        IReadOnlyList<SampledOutlet> drawn, string? note)
    {
        const string insertSample = """
            INSERT INTO synar_sample
                (check_year, state_code, frame_size, frame_filter,
                 expected_rvr_pct, design_effect, accuracy_rate_pct,
                 completion_rate_pct, safety_margin_pct, one_sided_ci,
                 effective_sample_size, target_sample_size, original_sample_size,
                 note)
            VALUES
                (@CheckYear, @StateCode, @FrameSize, @FrameFilter,
                 @ExpectedRvr, @DesignEffect, @AccuracyRate,
                 @CompletionRate, @SafetyMargin, @OneSidedCi,
                 @Effective, @Target, @Original,
                 @Note)
            RETURNING sample_id
            """;
        const string insertOutlet = """
            INSERT INTO synar_sample_outlet (sample_id, synar_maps_id, check_type)
            VALUES (@SampleId, @SynarMapsId, @CheckType)
            ON CONFLICT (sample_id, synar_maps_id) DO NOTHING
            """;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        var sampleId = await conn.ExecuteScalarAsync<int>(insertSample, new
        {
            CheckYear = (short)checkYear,
            StateCode = stateCode,
            input.FrameSize,
            FrameFilter = input.FrameFilterDescription,
            ExpectedRvr = input.ExpectedRvrPercent,
            input.DesignEffect,
            AccuracyRate = input.AccuracyRatePercent,
            CompletionRate = input.CompletionRatePercent,
            SafetyMargin = input.SafetyMarginPercent,
            OneSidedCi = input.UseOneSidedCi,
            Effective = result.EffectiveSampleSize,
            Target = result.TargetSampleSize,
            Original = result.OriginalSampleSize,
            Note = note,
        }, tx);

        foreach (var o in drawn)
        {
            await conn.ExecuteAsync(insertOutlet, new
            {
                SampleId = sampleId,
                o.SynarMapsId,
                o.CheckType,
            }, tx);
        }

        await tx.CommitAsync();
        return sampleId;
    }
}
