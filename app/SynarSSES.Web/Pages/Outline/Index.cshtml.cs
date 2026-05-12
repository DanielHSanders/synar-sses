using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SynarSSES.Core.Models;
using SynarSSES.Core.Repositories;
using SynarSSES.Core.Services;

namespace SynarSSES.Web.Pages.Outline;

public class IndexModel : PageModel
{
    private readonly OutlineRepository _repo;
    public MarkdownRenderer Md { get; }

    public IndexModel(OutlineRepository repo, MarkdownRenderer md)
    {
        _repo = repo;
        Md = md;
    }

    public List<OutlineSection> Sections { get; private set; } = new();

    public async Task OnGetAsync()
    {
        Sections = await _repo.GetAllAsync();
    }

    // Inline POST handlers for one-click deletes so we don't need separate
    // pages for every destructive action.
    public async Task<IActionResult> OnPostDeleteEntryAsync(int entryId)
    {
        await _repo.DeleteEntryAsync(entryId);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteSectionAsync(int sectionId)
    {
        await _repo.DeleteSectionAsync(sectionId);
        return RedirectToPage();
    }
}
