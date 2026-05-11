using SynarSSES.Core.Models;

namespace SynarSSES.Core.Services;

// Port of the SAMHSA SSES v7.0 calculation engine, Stratified-SRS-with-FPC path.
//
// Source VBA in `Synar/SSES/vba_source/`:
//   CSPStrata.ComputeStatistics_Step1   - per-row r/d/Resp/OTC/VM/YI flags
//   CSPVarianceStrata.ComputeStatistics_Step2  - per-stratum WT, AdjFct, AWT
//   CSPStrata.ComputeStatistics_Step3   - per-stratum NH_HAT, overall R/N_HAT/CV_W
//   CSPVarianceStrata.ComputeStatistics_Step4  - per-row ZHI; MZH; VSRS accumulator
//   CSPSSES.ComputeStatistics_Step5     - per-stratum SH2; SE = sqrt(sum SH2)
//   CSPSSES.ComputeStatistics_Step6     - CI, design effects, accuracy/completion rates
//
// The math is Taylor linearization with domain estimation per the SSES manual
// section "SSES Variance Estimation Approach". The validation target is to
// reproduce Synar2025_SSES_Final.xlsx from the 2025 KY microdata.
public sealed class SssCalculator
{
    // Per-row scratch state. Mirrors the helper columns the VBA writes into
    // the output worksheet (col_RCode, col_D, col_Resp, col_OTCIND, COL_VMIND,
    // col_YI, col_AdFct, col_AWT, col_ZHI).
    private sealed class Row
    {
        public required MicrodataRow Source { get; init; }
        public int RCode;
        public int D;
        public int Resp;
        public int Otc;
        public int Vm;
        public int Yi;
        public double AdjFct;
        public double Awt;
        public double Zhi;
    }

    private sealed class Stratum
    {
        public required string Id { get; init; }
        public required int Vcapn { get; init; }
        public required List<Row> Rows { get; init; }
        public int Vn => Rows.Count;
        public double Wt;
        public int Mh;
        public double Mzh;
        public double Fpc;
        public double Sh2;
    }

    public SssReport Compute(SssInput input)
    {
        return Compute(input, withFpc: true);
    }

    public SssReport Compute(SssInput input, bool withFpc)
    {
        var (strata, rows) = ClassifyAndGroup(input);

        Step2_PerStratumWeights(strata);
        var overall = Step3_OverallRates(strata);
        Step4_TaylorResiduals(strata, overall);
        Step5_StandardError(strata, overall, withFpc);
        Step6_Summaries(rows, strata, overall);

        return BuildReport(input, strata, rows, overall);
    }

    // VBA: CSPStrata.ComputeStatistics_Step1. Walks each row to classify it
    // (d, Resp, OTC/VM, yI flags) and groups rows into variance strata. KY
    // 2025 has a single stratum but the algorithm is written for the general
    // multi-stratum case.
    private static (List<Stratum> Strata, List<Row> Rows) ClassifyAndGroup(SssInput input)
    {
        var rows = new List<Row>(input.Microdata.Count);
        foreach (var m in input.Microdata)
        {
            var r = DispositionCodes.ToRCode(m.DispositionCode);
            var (d, resp) = r switch
            {
                DispositionCodes.Complete    => (1, 1),
                DispositionCodes.Noncomplete => (1, 0),
                _                            => (0, 0),  // Ineligible
            };
            var outlet = (m.OutletType ?? "").Trim().ToUpperInvariant();
            var otc = outlet == "OTC" ? 1 : 0;
            var vm  = outlet == "VM"  ? 1 : 0;
            var yi  = (r == DispositionCodes.Complete && m.Violation == true) ? 1 : 0;

            rows.Add(new Row
            {
                Source = m,
                RCode = r, D = d, Resp = resp,
                Otc = otc, Vm = vm, Yi = yi,
            });
        }

        // Group into variance strata while preserving microdata order within
        // each stratum (the VBA walks rows in order; sortByVStrat is a stable
        // sort by stratum id).
        var byStratum = rows
            .GroupBy(x => x.Source.VarianceStratum)
            .Select(g => new Stratum
            {
                Id = g.Key,
                Vcapn = g.First().Source.VarianceStratumPopulation,
                Rows = g.ToList(),
            })
            .ToList();

        // Sanity: every row inside a stratum must agree on its population.
        foreach (var s in byStratum)
        {
            var distinct = s.Rows.Select(r => r.Source.VarianceStratumPopulation).Distinct().Count();
            if (distinct > 1)
                throw new InvalidOperationException(
                    $"Variance stratum '{s.Id}' has inconsistent VarianceStratumPopulation across rows.");
            if (s.Vcapn < s.Vn)
                throw new InvalidOperationException(
                    $"Variance stratum '{s.Id}': population ({s.Vcapn}) is smaller than sample size ({s.Vn}). Correct and rerun.");
        }

        return (byStratum, rows);
    }

    // VBA: CSPVarianceStrata.ComputeStatistics_Step2. Per stratum:
    //   WT = Vcapn / VN
    //   MH = count of rows with r in {1, 3} (EC + ineligible respondents)
    //   For each row:
    //     r=1 (EC):           AdjFct = numD/numRESP, AWT = AdjFct * WT
    //     r=2 (noncomplete):  AdjFct = 0,            AWT = 0
    //     r=3 (ineligible):   AdjFct = 1,            AWT = WT
    private static void Step2_PerStratumWeights(List<Stratum> strata)
    {
        foreach (var s in strata)
        {
            var numD = s.Rows.Sum(r => r.D);
            var numResp = s.Rows.Sum(r => r.Resp);
            var numMh = s.Rows.Count(r => r.RCode == DispositionCodes.Complete
                                       || r.RCode == DispositionCodes.Ineligible);

            if (numResp == 0)
                throw new InvalidOperationException($"Variance stratum '{s.Id}': sum(RESP) = 0.");
            if (numMh == 0)
                throw new InvalidOperationException($"Variance stratum '{s.Id}': MH = 0.");
            if (numMh == 1 && s.Vcapn > 1)
                throw new InvalidOperationException($"Variance stratum '{s.Id}': MH = 1 and VCAPN > 1. Combine strata and rerun.");

            s.Wt = (double)s.Vcapn / s.Vn;
            s.Mh = numMh;
            var adjEC = (double)numD / numResp;

            foreach (var r in s.Rows)
            {
                if (r.RCode == DispositionCodes.Complete)
                {
                    r.AdjFct = adjEC;
                    r.Awt = adjEC * s.Wt;
                }
                else if (r.RCode == DispositionCodes.Noncomplete)
                {
                    r.AdjFct = 0;
                    r.Awt = 0;
                }
                else // Ineligible
                {
                    r.AdjFct = 1;
                    r.Awt = s.Wt;
                }
            }
        }
    }

    // Working aggregates accumulated in Step3, then read by Steps 4-6.
    private sealed class Overall
    {
        public double NHat;
        public double NHatOtc;
        public double NHatVm;
        public double YHat;
        public double YHatOtc;
        public double YHatVm;
        public double FHat;
        public double CvW;
        public double R;
        public double RotC;
        public double RvM;
        public int SumD;
        public int SumYi;
        public int SumResp;
        public int M;            // sumMHI = total eligible respondents (Step 4)
        public double Vsrs;      // simple-random-sample reference variance for design effects
        public double Se;        // overall weighted-RVR standard error
        public int Nn;           // sum of Vcapn across strata
    }

    // VBA: CSPStrata.ComputeStatistics_Step3. Sums d*AWT, etc. and divides at
    // the end to get the weighted RVR R = y_hat / N_HAT.
    private static Overall Step3_OverallRates(List<Stratum> strata)
    {
        var o = new Overall();
        foreach (var s in strata)
        {
            foreach (var r in s.Rows)
            {
                o.FHat   += r.Awt;
                o.CvW    += r.D * r.Awt * r.Awt;

                if (r.D == 1 && r.Yi == 1)
                {
                    o.YHat += r.Awt;
                    if (r.Otc == 1) o.YHatOtc += r.Awt;
                    else if (r.Vm == 1) o.YHatVm += r.Awt;
                }
                o.NHat    += r.D * r.Awt;
                o.NHatOtc += r.D * r.Awt * r.Otc;
                o.NHatVm  += r.D * r.Awt * r.Vm;

                o.SumD    += r.D;
                o.SumYi   += r.Yi;
                o.SumResp += r.Resp;
            }
        }
        if (o.NHat > 0) o.R = o.YHat / o.NHat;
        if (o.NHatOtc > 0) o.RotC = o.YHatOtc / o.NHatOtc;
        if (o.NHatVm > 0) o.RvM = o.YHatVm / o.NHatVm;
        if (o.NHat > 0) o.CvW = o.CvW / (o.NHat * o.NHat);
        return o;
    }

    // VBA: CSPVarianceStrata.ComputeStatistics_Step4. Per row:
    //   ZHI = d * AWT * (yI - R) / N_HAT
    //   VSRS += d * AWT * (yI - R)^2     (accumulated across all strata)
    // Per stratum: MZH = sum(ZHI in stratum) / MH; FPC = 1 - MH / Vcapn.
    // Overall: VSRS = M * VSRS * cv_w / ((M - 1) * N_HAT).
    private static void Step4_TaylorResiduals(List<Stratum> strata, Overall o)
    {
        double vsrsAccum = 0;
        int sumMhi = 0;
        foreach (var s in strata)
        {
            double sumZhi = 0;
            foreach (var r in s.Rows)
            {
                var mhiFlag = (r.RCode == DispositionCodes.Complete
                            || r.RCode == DispositionCodes.Ineligible) ? 1 : 0;
                sumMhi += mhiFlag;

                if (r.Awt > 0)
                {
                    r.Zhi = r.D * r.Awt * (r.Yi - o.R) / o.NHat;
                    sumZhi += r.Zhi;
                    var zhiP = r.Yi - o.R;
                    vsrsAccum += r.D * r.Awt * zhiP * zhiP;
                }
                else
                {
                    r.Zhi = 0;
                }
            }
            s.Mzh = sumZhi / s.Mh;
            s.Fpc = 1.0 - (double)s.Mh / s.Vcapn;
            o.Nn += s.Vcapn;
        }
        o.M = sumMhi;
        // VBA: overallVals.Item("VSRS").value = sumMHI * VSRS * cv_w / ((sumMHI - 1) * n_Hat)
        if (sumMhi > 1 && o.NHat > 0)
            o.Vsrs = sumMhi * vsrsAccum * o.CvW / ((sumMhi - 1) * o.NHat);
    }

    // VBA: CSPSSES.ComputeStatistics_Step5. Per stratum:
    //   SH2 = MH * FPC * sum((ZHI - MZH)^2 over rows with r != 2) / (MH - 1)
    // Overall: SE = sqrt(sum SH2).
    private static void Step5_StandardError(List<Stratum> strata, Overall o, bool withFpc)
    {
        double seSq = 0;
        foreach (var s in strata)
        {
            var fpc = withFpc ? s.Fpc : 1.0;
            if (s.Mh == 1 && s.Vcapn == 1)
            {
                s.Sh2 = 0;
                continue;
            }
            if (s.Mh <= 1)
                throw new InvalidOperationException(
                    $"Variance stratum '{s.Id}': MH = {s.Mh} insufficient for variance estimation.");

            double sumSq = 0;
            foreach (var r in s.Rows)
            {
                if (r.RCode == DispositionCodes.Noncomplete) continue;  // r != 2
                var dz = r.Zhi - s.Mzh;
                sumSq += dz * dz;
            }
            s.Sh2 = s.Mh * fpc * sumSq / (s.Mh - 1);
            seSq += s.Sh2;
        }
        o.Se = Math.Sqrt(seSq);
    }

    // VBA: CSPSSES.ComputeStatistics_Step6. Confidence intervals, design
    // effects, completion / accuracy rates -- all the summary numbers that
    // appear on Table 1. Values flow through Overall and are read by the
    // report builder.
    private static void Step6_Summaries(List<Row> rows, List<Stratum> strata, Overall o)
    {
        // No-op -- BuildReport reads o.R, o.Se, o.M, etc. directly. CI and
        // design-effect formulas live in BuildReport for clarity.
    }

    private static SssReport BuildReport(
        SssInput input, List<Stratum> strata, List<Row> rows, Overall o)
    {
        var stratumResults = strata.Select(s => new SamplingStratumResult
        {
            SamplingStratumId = s.Id,
            VarianceStratumId = s.Id,
            OutletFrameSize = s.Vcapn,
            EstimatedPopulationSize = s.Rows.Sum(r => r.D * r.Awt),
            OutletSampleSize = s.Vn,
            EligibleOutletsInSample = s.Rows.Sum(r => r.D),
            InspectedCount = s.Rows.Sum(r => r.Resp),
            ViolationCount = s.Rows.Sum(r => r.Yi),
            ViolationRate = s.Rows.Sum(r => r.D * r.Awt) > 0
                ? s.Rows.Sum(r => r.D * r.Awt * r.Yi) / s.Rows.Sum(r => r.D * r.Awt)
                : 0,
            StandardError = null,  // per-stratum SE not produced on Table 2 (only Total row)
        }).ToList();

        var inspectedRows = rows.Where(r => r.RCode == DispositionCodes.Complete).ToList();

        // CIs from CSPSSES.ComputeStatistics_Step6:
        //   LL = max(0, R - 1.96*SE)   UL = min(1, R + 1.96*SE)
        //   UL2 = R + 1.645*SE (one-sided 95% upper)
        var ll = Math.Max(0.0, o.R - 1.96 * o.Se);
        var ul = Math.Min(1.0, o.R + 1.96 * o.Se);
        var ul2 = o.R + 1.645 * o.Se;
        var samhsaMet = 1.645 * o.Se <= 0.03;
        // Design effects: DEFF1 / DEFF2 / DEFF3 per the VBA. For an SRS-only
        // KY design these tend to converge near 1.0 since the survey *is*
        // simple random within the single stratum.
        double f = o.Nn > 0 ? (double)o.M / o.Nn : 0;
        double deff1 = (o.R > 0 && o.R < 1 && o.M > 1)
            ? (o.M - 1) * o.Se * o.Se / (o.R * (1 - o.R)) : 0;
        double deff2 = (o.R > 0 && o.R < 1 && o.M > 1 && f < 1)
            ? (o.M - 1) * o.Se * o.Se / (o.R * (1 - o.R) * (1 - f)) : 0;
        double deff3 = (o.Vsrs > 0 && f < 1)
            ? o.Se * o.Se / (o.Vsrs * (1 - f)) : 0;

        var overall = new OverallStats
        {
            FrameSize = strata.Sum(s => s.Vcapn),
            EstimatedPopulationSize = o.NHat,
            SampleSize = rows.Count,
            EligibleSampleSize = o.SumD,
            InspectedCount = o.SumResp,
            ViolationCount = o.SumYi,
            WeightedRvr = o.R,
            UnweightedRvr = o.SumResp > 0 ? (double)o.SumYi / o.SumResp : 0,
            StandardError = o.Se,
            WeightedAccuracyRate = o.FHat > 0 ? o.NHat / o.FHat : 0,
            UnweightedAccuracyRate = rows.Count > 0 ? (double)o.SumD / rows.Count : 0,
            CompletionRate = o.SumD > 0 ? (double)o.SumResp / o.SumD : 0,
            CiLower95 = ll,
            CiUpper95 = ul,
            CiUpperOneSided95 = ul2,
            SamhsaPrecisionMet = samhsaMet,
            DesignEffect1 = deff1,
            DesignEffect2 = deff2,
            DesignEffect3 = deff3,
        };

        var tally = new SampleTally
        {
            CountsByCode = rows
                .GroupBy(r => r.Source.DispositionCode)
                .ToDictionary(g => g.Key, g => g.Count())
        };
        var inspectors = BuildInspectorDemographics(inspectedRows);
        var productCrossTab = BuildCrossTab(
            "Product Type", inspectedRows,
            r => MapProductType(r.Source.ProductType));
        var outletCrossTab = BuildCrossTab(
            "Retail Outlet", inspectedRows,
            r => MapRetailOutlet(r.Source.RetailOutletType));
        var askedCrossTab = BuildCrossTab(
            "Clerk Asked for ID", inspectedRows,
            r => MapAskedForId(r.Source.AskedForId));

        return new SssReport
        {
            StateCode = input.StateCode,
            FederalFiscalYear = input.FederalFiscalYear,
            GeneratedAt = DateTime.UtcNow,
            Overall = overall,
            StratumResults = stratumResults,
            Tally = tally,
            Inspectors = inspectors,
            Microdata = input.Microdata,
            ProductCrossTab = productCrossTab,
            OutletCrossTab = outletCrossTab,
            AskedForIdCrossTab = askedCrossTab,
        };
    }

    // VBA: CSPInspectors.InitCounts. One cell per (gender, age) bucket
    // (M/F x 14..20) plus a 14..20 column for buy attempts and successes.
    // Inspector counts come from the distinct InspectorId seen for each
    // (gender, age) pair across the EC rows.
    private static InspectorDemographics BuildInspectorDemographics(List<Row> inspectedRows)
    {
        var cells = new List<InspectorAgeCell>();
        foreach (var gender in new[] { "M", "F" })
        {
            for (var age = 14; age <= 20; age++)
            {
                var bucket = inspectedRows
                    .Where(r => r.Source.InspectorGender == gender
                             && r.Source.InspectorAge == age)
                    .ToList();
                cells.Add(new InspectorAgeCell
                {
                    Gender = gender,
                    Age = age,
                    InspectorCount = bucket
                        .Select(r => r.Source.InspectorId)
                        .Where(id => !string.IsNullOrEmpty(id))
                        .Distinct()
                        .Count(),
                    AttemptedBuys  = bucket.Count,
                    SuccessfulBuys = bucket.Count(r => r.Yi == 1),
                });
            }
        }
        return new InspectorDemographics { Cells = cells };
    }

    // VBA: CSPProducts / CSPOutlets / CSPCKIDS InitCounts. All three share the
    // same shape: bucket inspected (rcode=1) rows by a categorical attribute,
    // emit attempted/successful counts + violation rate, and the same numbers
    // pivoted by inspector age x gender. Categories include a "Missing"
    // bucket for blank values (which is how KY records Clerk-asked-for-ID
    // and Retail-outlet today).
    private static CrossTab BuildCrossTab(
        string label,
        List<Row> inspectedRows,
        Func<Row, string> categorize)
    {
        var groups = inspectedRows
            .Select(r => (Cat: categorize(r), Row: r))
            .GroupBy(t => t.Cat)
            .Select(g => new CrossTabRow
            {
                Category = g.Key,
                AttemptedBuys  = g.Count(),
                SuccessfulBuys = g.Count(t => t.Row.Yi == 1),
                ViolationRate = g.Count() > 0
                    ? (double)g.Count(t => t.Row.Yi == 1) / g.Count() : 0,
                RateByGenderAge = BuildRateByGenderAge(g.Select(t => t.Row).ToList()),
            })
            .OrderBy(r => r.Category)
            .ToList();
        return new CrossTab { CategoryLabel = label, Rows = groups };
    }

    private static IReadOnlyDictionary<(string Gender, int Age), double> BuildRateByGenderAge(List<Row> rows)
    {
        var dict = new Dictionary<(string Gender, int Age), double>();
        foreach (var gender in new[] { "M", "F" })
        {
            for (var age = 14; age <= 20; age++)
            {
                var bucket = rows.Where(r => r.Source.InspectorGender == gender
                                          && r.Source.InspectorAge == age).ToList();
                dict[(gender, age)] = bucket.Count > 0
                    ? (double)bucket.Count(r => r.Yi == 1) / bucket.Count : 0;
            }
        }
        return dict;
    }

    private static string MapProductType(int? code) => code switch
    {
        1 => "Cigarette",
        2 => "Cigar",
        3 => "Smokeless",
        4 => "E-cig",
        5 => "Other",
        null => "Missing",
        _ => "Invalid",
    };

    private static string MapRetailOutlet(int? code) => code switch
    {
        null => "Missing",
        _    => $"Type {code}",
    };

    private static string MapAskedForId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Missing";
        var u = value.Trim().ToUpperInvariant();
        return u switch
        {
            "Y" or "YES" or "1" => "Yes",
            "N" or "NO"  or "0" => "No",
            _ => "Invalid",
        };
    }
}
