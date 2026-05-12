using Dapper;
using Npgsql;
using SynarSSES.Core.Models;

namespace SynarSSES.Core.Repositories;

// Dapper repo for outline_section + outline_entry. Anyone can edit -- the
// page has no auth -- so every mutating method is just a plain CRUD call.
public sealed class OutlineRepository
{
    private readonly string _connectionString;

    public OutlineRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<List<OutlineSection>> GetAllAsync()
    {
        const string sectionsSql = "SELECT * FROM outline_section ORDER BY sort_order, section_id";
        const string entriesSql  = "SELECT * FROM outline_entry ORDER BY section_id, sort_order, entry_id";
        await using var conn = new NpgsqlConnection(_connectionString);
        var sections = (await conn.QueryAsync<OutlineSection>(sectionsSql)).ToList();
        var entries  = (await conn.QueryAsync<OutlineEntry>(entriesSql)).ToList();
        var bySection = entries.GroupBy(e => e.SectionId).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var s in sections)
            s.Entries = bySection.TryGetValue(s.SectionId, out var list) ? list : new();
        return sections;
    }

    public async Task<OutlineSection?> GetSectionAsync(int sectionId)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        return await conn.QuerySingleOrDefaultAsync<OutlineSection>(
            "SELECT * FROM outline_section WHERE section_id = @SectionId",
            new { SectionId = sectionId });
    }

    public async Task<OutlineEntry?> GetEntryAsync(int entryId)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        return await conn.QuerySingleOrDefaultAsync<OutlineEntry>(
            "SELECT * FROM outline_entry WHERE entry_id = @EntryId",
            new { EntryId = entryId });
    }

    public async Task<int> CreateSectionAsync(int sortOrder, string title, string dateRange, string bodyMd)
    {
        const string sql = """
            INSERT INTO outline_section (sort_order, title, date_range, body_md)
            VALUES (@SortOrder, @Title, @DateRange, @BodyMd)
            RETURNING section_id
            """;
        await using var conn = new NpgsqlConnection(_connectionString);
        return await conn.ExecuteScalarAsync<int>(sql,
            new { SortOrder = sortOrder, Title = title, DateRange = dateRange, BodyMd = bodyMd });
    }

    public async Task UpdateSectionAsync(int sectionId, int sortOrder, string title, string dateRange, string bodyMd)
    {
        const string sql = """
            UPDATE outline_section
               SET sort_order = @SortOrder, title = @Title,
                   date_range = @DateRange, body_md = @BodyMd,
                   updated_at = now()
             WHERE section_id = @SectionId
            """;
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.ExecuteAsync(sql,
            new { SectionId = sectionId, SortOrder = sortOrder, Title = title,
                  DateRange = dateRange, BodyMd = bodyMd });
    }

    public async Task DeleteSectionAsync(int sectionId)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.ExecuteAsync("DELETE FROM outline_section WHERE section_id = @SectionId",
            new { SectionId = sectionId });
    }

    public async Task<int> CreateEntryAsync(int sectionId, int sortOrder, int? year,
                                            string label, DateOnly? completedOn, string notesMd)
    {
        const string sql = """
            INSERT INTO outline_entry (section_id, sort_order, year, label, completed_on, notes_md)
            VALUES (@SectionId, @SortOrder, @Year, @Label, @CompletedOn, @NotesMd)
            RETURNING entry_id
            """;
        await using var conn = new NpgsqlConnection(_connectionString);
        return await conn.ExecuteScalarAsync<int>(sql,
            new { SectionId = sectionId, SortOrder = sortOrder, Year = year,
                  Label = label, CompletedOn = completedOn, NotesMd = notesMd });
    }

    public async Task UpdateEntryAsync(int entryId, int sortOrder, int? year,
                                       string label, DateOnly? completedOn, string notesMd)
    {
        const string sql = """
            UPDATE outline_entry
               SET sort_order = @SortOrder, year = @Year, label = @Label,
                   completed_on = @CompletedOn, notes_md = @NotesMd,
                   updated_at = now()
             WHERE entry_id = @EntryId
            """;
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.ExecuteAsync(sql,
            new { EntryId = entryId, SortOrder = sortOrder, Year = year,
                  Label = label, CompletedOn = completedOn, NotesMd = notesMd });
    }

    public async Task DeleteEntryAsync(int entryId)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.ExecuteAsync("DELETE FROM outline_entry WHERE entry_id = @EntryId",
            new { EntryId = entryId });
    }
}
