using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SynarSSES.Core.Models;
using SynarSSES.Core.Repositories;
using SynarSSES.Core.Services;

namespace SynarSSES.Web.Pages;

public class IndexModel : PageModel
{
    private readonly MicrodataRepository _repo;
    private readonly SampleRepository _samples;
    private readonly SssCalculator _calculator;
    private readonly SssWorkbookWriter _writer;

    public IndexModel(MicrodataRepository repo, SampleRepository samples,
                      SssCalculator calculator, SssWorkbookWriter writer)
    {
        _repo = repo;
        _samples = samples;
        _calculator = calculator;
        _writer = writer;
    }

    [BindProperty] public int CheckYear { get; set; }
    [BindProperty] public int FrameSize { get; set; }
    [BindProperty] public string StateCode { get; set; } = "KY";
    [BindProperty] public int FederalFiscalYear { get; set; }
    // SSES prompts for these two at run time: they are the sample-size
    // calculator results for the year, printed on Table 1.
    [BindProperty] public int? EffectiveSampleSize { get; set; }
    [BindProperty] public int? TargetSampleSize { get; set; }

    // Surfaced so the page can explain where the frame size came from, and
    // warn when there is no recorded draw to take it from.
    public SampleRecord? Sample { get; private set; }

    public async Task OnGetAsync()
    {
        // The survey year is whatever has data loaded, not a hardcoded year.
        CheckYear = await _repo.GetLatestCheckYearAsync() ?? DateTime.UtcNow.Year;
        // A survey run in year N is submitted in FFY N+1.
        FederalFiscalYear = CheckYear + 1;

        // The frame must be the population the sample was actually drawn from.
        // Counting map_location now would give a different number -- it keeps
        // changing after the draw.
        Sample = await _samples.GetForYearAsync(CheckYear);
        FrameSize = Sample?.FrameSize ?? 0;
        EffectiveSampleSize = Sample?.EffectiveSampleSize;
        TargetSampleSize = Sample?.TargetSampleSize;
    }

    // SSES stamps Table 1 with local time; the server runs on UTC.
    private static DateTime KentuckyNow()
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        }
        catch (TimeZoneNotFoundException)
        {
            return DateTime.Now;
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var microdata = await _repo.GetMicrodataAsync(CheckYear, FrameSize);
        var input = new SssInput
        {
            StateCode = StateCode,
            FederalFiscalYear = FederalFiscalYear,
            Microdata = microdata,
            EffectiveSampleSize = EffectiveSampleSize,
            TargetSampleSize = TargetSampleSize,
            DataSource = $"synarcheck, checkyear {CheckYear}",
            GeneratedAt = KentuckyNow(),
        };
        var report = _calculator.Compute(input, withFpc: true);
        var bytes = _writer.Write(report);

        var fileName = $"Synar{CheckYear}_SSES_FFY{FederalFiscalYear}.xlsx";
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }
}
