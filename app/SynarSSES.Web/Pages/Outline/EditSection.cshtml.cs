using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SynarSSES.Core.Repositories;

namespace SynarSSES.Web.Pages.Outline;

// Edit-or-create page for sections. /Outline/EditSection?id=0 = new, otherwise
// edit the section with that id. Both flows share this one model + form.
public class EditSectionModel : PageModel
{
    private readonly OutlineRepository _repo;

    public EditSectionModel(OutlineRepository repo) { _repo = repo; }

    [BindProperty(SupportsGet = true)] public int Id { get; set; }
    [BindProperty] public int SortOrder { get; set; }
    [BindProperty] public string Title { get; set; } = "";
    [BindProperty] public string DateRange { get; set; } = "";
    [BindProperty] public string BodyMd { get; set; } = "";

    public bool IsNew => Id == 0;

    public async Task<IActionResult> OnGetAsync()
    {
        if (IsNew)
        {
            // Sensible default: put new sections at the end.
            var existing = await _repo.GetAllAsync();
            SortOrder = (existing.Count == 0 ? 0 : existing.Max(s => s.SortOrder)) + 10;
            return Page();
        }
        var section = await _repo.GetSectionAsync(Id);
        if (section is null) return NotFound();
        SortOrder = section.SortOrder;
        Title     = section.Title;
        DateRange = section.DateRange;
        BodyMd    = section.BodyMd;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Title))
        {
            ModelState.AddModelError(nameof(Title), "Title is required.");
            return Page();
        }
        if (IsNew)
            await _repo.CreateSectionAsync(SortOrder, Title, DateRange ?? "", BodyMd ?? "");
        else
            await _repo.UpdateSectionAsync(Id, SortOrder, Title, DateRange ?? "", BodyMd ?? "");
        return RedirectToPage("Index");
    }
}
