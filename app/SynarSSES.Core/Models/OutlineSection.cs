namespace SynarSSES.Core.Models;

// A section in the Synar process outline (e.g. "Data Gathering", "Sampling").
// All fields are editable via the UI -- no schema constraints beyond title
// being required. Sort order controls display sequence.
public sealed class OutlineSection
{
    public int SectionId { get; init; }
    public int SortOrder { get; init; }
    public string Title { get; init; } = "";
    public string DateRange { get; init; } = "";   // free-text e.g. "Feb-Mar"
    public string BodyMd { get; init; } = "";
    public DateTime UpdatedAt { get; init; }

    // Hydrated by repository, not stored on the row itself.
    public List<OutlineEntry> Entries { get; set; } = new();
}

// A year-tagged completion record within a section (e.g. "ABC list provided on
// 2026-01-28"). Year and date are both optional so users can record items
// before the date is known.
public sealed class OutlineEntry
{
    public int EntryId { get; init; }
    public int SectionId { get; init; }
    public int SortOrder { get; init; }
    public int? Year { get; init; }
    public string Label { get; init; } = "";
    public DateOnly? CompletedOn { get; init; }
    public string NotesMd { get; init; } = "";
    public DateTime UpdatedAt { get; init; }
}
