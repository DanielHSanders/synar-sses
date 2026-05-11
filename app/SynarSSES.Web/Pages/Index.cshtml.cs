using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SynarSSES.Core.Models;
using SynarSSES.Core.Repositories;
using SynarSSES.Core.Services;

namespace SynarSSES.Web.Pages;

public class IndexModel : PageModel
{
    private readonly MicrodataRepository _repo;
    private readonly SssCalculator _calculator;
    private readonly SssWorkbookWriter _writer;

    public IndexModel(MicrodataRepository repo, SssCalculator calculator, SssWorkbookWriter writer)
    {
        _repo = repo;
        _calculator = calculator;
        _writer = writer;
    }

    [BindProperty] public int CheckYear { get; set; } = 2025;
    [BindProperty] public int FrameSize { get; set; } = 4452;
    [BindProperty] public string StateCode { get; set; } = "KY";
    [BindProperty] public int FederalFiscalYear { get; set; } = DateTime.UtcNow.Year + 1;

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        var microdata = await _repo.GetMicrodataAsync(CheckYear, FrameSize);
        var input = new SssInput
        {
            StateCode = StateCode,
            FederalFiscalYear = FederalFiscalYear,
            Microdata = microdata,
        };
        var report = _calculator.Compute(input, withFpc: true);
        var bytes = _writer.Write(report);

        var fileName = $"Synar{CheckYear}_SSES_{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx";
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }
}
