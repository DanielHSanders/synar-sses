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
    private readonly CheckTypeAssigner _assigner;
    private readonly SampleSizeWorkbookWriter _writer;

    public SampleSizeModel(
        MicrodataRepository microdataRepo,
        MapLocationRepository mapRepo,
        SampleSizeCalculator calculator,
        SampleDrawer drawer,
        CheckTypeAssigner assigner,
        SampleSizeWorkbookWriter writer)
    {
        _microdataRepo = microdataRepo;
        _mapRepo = mapRepo;
        _calculator = calculator;
        _drawer = drawer;
        _assigner = assigner;
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
    // When on, the frame is restricted to outlets that also hold an ABC
    // license for the planning year -- the most reliable sales-of-cigarettes
    // signal. Defaults on because 2026 onward this is the primary list.
    [BindProperty] public bool RequireAbcLicense { get; set; } = true;
    [BindProperty] public double CigarettePercent  { get; set; } = 37.5;
    [BindProperty] public double SmokelessPercent  { get; set; } = 32.5;
    [BindProperty] public double ElectronicPercent { get; set; } = 20.0;
    [BindProperty] public double MentholPercent    { get; set; } = 10.0;

    public PriorYearStats? PriorYear { get; set; }
    public int ValidOutletCount { get; set; }
    public int ValidWithAbcCount { get; set; }

    public async Task OnGetAsync()
    {
        // Pre-fill from the prior year's actuals if any data exists.
        var priorYear = DateTime.UtcNow.Year - 1;
        PriorYear = await _microdataRepo.GetPriorYearStatsAsync(priorYear);
        ValidOutletCount  = await _mapRepo.CountValidAsync();
        ValidWithAbcCount = await _mapRepo.CountValidAsync(PlanningYear);
        FrameSize = RequireAbcLicense ? ValidWithAbcCount : ValidOutletCount;
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
            FrameFilterDescription = RequireAbcLicense
                ? $"Valid outlets with {PlanningYear} ABC license"
                : "Valid outlets",
        };
        var result = _calculator.Compute(input);
        var frame = await _mapRepo.GetValidOutletsAsync(
            RequireAbcLicense ? PlanningYear : (int?)null);
        var drawn = _drawer.Draw(frame, Math.Min(result.OriginalSampleSize, frame.Count));
        var assignment = _assigner.Assign(drawn,
            CigarettePercent, SmokelessPercent, ElectronicPercent, MentholPercent);

        // synarcheckid is NOT NULL with no auto-generation; assign the next
        // block sequentially from max+1 so the xlsx can be loaded directly.
        var firstId = await _microdataRepo.GetMaxSynarcheckIdAsync() + 1;

        var bytes = _writer.Write(result, drawn, assignment, PlanningYear, StateCode, firstId);
        var fileName = $"SynarSample{PlanningYear}_{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx";
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }
}
