using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SynarSSES.Core.Models;
using SynarSSES.Core.Repositories;
using SynarSSES.Core.Services;

namespace SynarSSES.Web.Pages;

public class SampleSizeModel : PageModel
{
    private readonly MicrodataRepository _microdataRepo;
    private readonly MapLocationRepository _mapRepo;
    private readonly SampleSizeCalculator _calculator;
    private readonly SampleDrawer _drawer;
    private readonly SampleSizeWorkbookWriter _writer;

    public SampleSizeModel(
        MicrodataRepository microdataRepo,
        MapLocationRepository mapRepo,
        SampleSizeCalculator calculator,
        SampleDrawer drawer,
        SampleSizeWorkbookWriter writer)
    {
        _microdataRepo = microdataRepo;
        _mapRepo = mapRepo;
        _calculator = calculator;
        _drawer = drawer;
        _writer = writer;
    }

    [BindProperty] public string StateCode { get; set; } = "KY";
    [BindProperty] public int PlanningYear { get; set; } = DateTime.UtcNow.Year;
    [BindProperty] public int FrameSize { get; set; }
    [BindProperty] public double ExpectedRvrPercent { get; set; }
    [BindProperty] public double DesignEffect { get; set; } = 1.0;
    [BindProperty] public double AccuracyRatePercent { get; set; }
    [BindProperty] public double CompletionRatePercent { get; set; } = 100.0;
    [BindProperty] public double SafetyMarginPercent { get; set; } = 0.0;
    [BindProperty] public bool UseOneSidedCi { get; set; } = true;

    public PriorYearStats? PriorYear { get; set; }

    public async Task OnGetAsync()
    {
        // Pre-fill from the prior year's actuals if any data exists.
        var priorYear = DateTime.UtcNow.Year - 1;
        PriorYear = await _microdataRepo.GetPriorYearStatsAsync(priorYear);
        FrameSize = await _mapRepo.CountValidAsync();
        if (PriorYear is not null)
        {
            ExpectedRvrPercent    = Math.Round(PriorYear.ViolationRatePercent, 2);
            AccuracyRatePercent   = Math.Round(PriorYear.AccuracyRatePercent, 2);
            CompletionRatePercent = Math.Round(PriorYear.CompletionRatePercent, 2);
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var input = new SampleSizeInput
        {
            FrameSize = FrameSize,
            ExpectedRvrPercent = ExpectedRvrPercent,
            DesignEffect = DesignEffect,
            AccuracyRatePercent = AccuracyRatePercent,
            CompletionRatePercent = CompletionRatePercent,
            SafetyMarginPercent = SafetyMarginPercent,
            UseOneSidedCi = UseOneSidedCi,
        };
        var result = _calculator.Compute(input);
        var frame = await _mapRepo.GetValidOutletsAsync();
        var drawn = _drawer.Draw(frame, Math.Min(result.OriginalSampleSize, frame.Count));

        var bytes = _writer.Write(result, drawn, PlanningYear, StateCode);
        var fileName = $"SynarSample{PlanningYear}_{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx";
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }
}
