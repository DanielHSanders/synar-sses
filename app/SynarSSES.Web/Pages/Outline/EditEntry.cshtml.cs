using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SynarSSES.Core.Repositories;

namespace SynarSSES.Web.Pages.Outline;

// Edit-or-create page for entries. /Outline/EditEntry?id=0&sectionId=N = new
// in that section, otherwise edit the entry with that id.
public class EditEntryModel : PageModel
{
    private readonly OutlineRepository _repo;

    public EditEntryModel(OutlineRepository repo) { _repo = repo; }

    [BindProperty(SupportsGet = true)] public int Id { get; set; }
    [BindProperty(SupportsGet = true)] public int SectionId { get; set; }
    [BindProperty] public int SortOrder { get; set; }
    [BindProperty] public int? Year { get; set; }
    [BindProperty] public string Label { get; set; } = "";
    [BindProperty] public DateOnly? CompletedOn { get; set; }
    [BindProperty] public string NotesMd { get; set; } = "";

    public bool IsNew => Id == 0;
    public string SectionTitle { get; private set; } = "";

    public async Task<IActionResult> OnGetAsync()
    {
        if (SectionId == 0 && Id != 0)
        {
            // Pull SectionId from the entry being edited.
            var entry = await _repo.GetEntryAsync(Id);
            if (entry is null) return NotFound();
            SectionId = entry.SectionId;
        }
        var section = await _repo.GetSectionAsync(SectionId);
        if (section is null) return NotFound();
        SectionTitle = section.Title;

        if (IsNew)
        {
            SortOrder = 10;
            Year = DateTime.UtcNow.Year;
        }
        else
        {
            var entry = await _repo.GetEntryAsync(Id);
            if (entry is null) return NotFound();
            SortOrder   = entry.SortOrder;
            Year        = entry.Year;
            Label       = entry.Label;
            CompletedOn = entry.CompletedOn;
            NotesMd     = entry.NotesMd;
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Label))
        {
            ModelState.AddModelError(nameof(Label), "Label is required.");
            var section = await _repo.GetSectionAsync(SectionId);
            SectionTitle = section?.Title ?? "";
            return Page();
        }
        if (IsNew)
            await _repo.CreateEntryAsync(SectionId, SortOrder, Year, Label, CompletedOn, NotesMd ?? "");
        else
            await _repo.UpdateEntryAsync(Id, SortOrder, Year, Label, CompletedOn, NotesMd ?? "");
        return RedirectToPage("Index");
    }
}
