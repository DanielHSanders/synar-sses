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
// section "SSES Variance Estimation Approach". Over-the-counter outlets and
// vending machines are each treated as a domain of the full sample, which is
// how SSES gets a separate standard error for each on Table 2.
//
// SSES distinguishes sampling strata (Table 2 rows) from variance strata (the
// variance computation). This port groups on the variance stratum for both,
// which is exact whenever the two coincide -- true of every KY submission.
public sealed class SssCalculator
{
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
        public double ZhiOtc;
        public double ZhiVm;
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
        public double MzhOtc;
        public double MzhVm;
        public double Fpc;
        public double Sh2;
        public double Sh2Otc;
        public double Sh2Vm;
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

        return BuildReport(input, strata, rows, overall, withFpc);
    }

    // VBA: CSPStrata.ComputeStatistics_Step1. Walks each row to classify it
    // (d, Resp, OTC/VM, yI flags) and groups rows into variance strata.
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
            rows.Add(new Row
            {
                Source = m,
                RCode = r, D = d, Resp = resp,
                Otc = outlet == "OTC" ? 1 : 0,
                Vm = outlet == "VM" ? 1 : 0,
                Yi = (r == DispositionCodes.Complete && m.Violation == true) ? 1 : 0,
            });
        }

        // Group into variance strata while preserving microdata order within
        // each stratum (the VBA sortByVStrat is a stable sort by stratum id).
        var byStratum = rows
            .GroupBy(x => x.Source.VarianceStratum)
            .Select(g => new Stratum
            {
                Id = g.Key,
                Vcapn = g.First().Source.VarianceStratumPopulation,
                Rows = g.ToList(),
            })
            .ToList();

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
                else
                {
                    r.AdjFct = 1;
                    r.Awt = s.Wt;
                }
            }
        }
    }

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
        public double ROtc;
        public double RVm;
        public int SumD;
        public int SumYi;
        public int SumResp;
        public int M;            // sumMHI: EC + ineligible rows
        public double Vsrs;      // SRS reference variance for design effects
        public double Se;
        public double SeOtc;
        public double SeVm;
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
                o.FHat += r.Awt;
                o.CvW  += r.D * r.Awt * r.Awt;

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
        if (o.NHatOtc > 0) o.ROtc = o.YHatOtc / o.NHatOtc;
        if (o.NHatVm > 0) o.RVm = o.YHatVm / o.NHatVm;
        if (o.NHat > 0) o.CvW = o.CvW / (o.NHat * o.NHat);
        return o;
    }

    // VBA: CSPVarianceStrata.ComputeStatistics_Step4. Per row with AWT > 0:
    //   ZHI   = d * AWT * (yI - R)   / N_HAT
    //   ZHI_O = d * AWT * (yI - R_o) / N_HAT_O   (OTC rows only; 0 elsewhere)
    //   ZHI_V = d * AWT * (yI - R_v) / N_HAT_V   (VM rows only; 0 elsewhere)
    // Each MZH divides by the whole stratum MH rather than the domain count,
    // which is what makes this domain estimation.
    private static void Step4_TaylorResiduals(List<Stratum> strata, Overall o)
    {
        double vsrsAccum = 0;
        int sumMhi = 0;
        foreach (var s in strata)
        {
            double sumZhi = 0, sumZhiOtc = 0, sumZhiVm = 0;
            foreach (var r in s.Rows)
            {
                if (r.RCode == DispositionCodes.Complete || r.RCode == DispositionCodes.Ineligible)
                    sumMhi++;

                r.Zhi = r.ZhiOtc = r.ZhiVm = 0;
                if (r.Awt <= 0) continue;

                r.Zhi = r.D * r.Awt * (r.Yi - o.R) / o.NHat;
                sumZhi += r.Zhi;
                var zhiP = r.Yi - o.R;
                vsrsAccum += r.D * r.Awt * zhiP * zhiP;

                if (r.Otc == 1)
                {
                    r.ZhiOtc = o.NHatOtc > 0 ? r.D * r.Awt * (r.Yi - o.ROtc) / o.NHatOtc : 0;
                    sumZhiOtc += r.ZhiOtc;
                }
                else if (r.Vm == 1)
                {
                    r.ZhiVm = o.NHatVm > 0 ? r.D * r.Awt * (r.Yi - o.RVm) / o.NHatVm : 0;
                    sumZhiVm += r.ZhiVm;
                }
            }
            s.Mzh = sumZhi / s.Mh;
            s.MzhOtc = sumZhiOtc / s.Mh;
            s.MzhVm = sumZhiVm / s.Mh;
            s.Fpc = 1.0 - (double)s.Mh / s.Vcapn;
            o.Nn += s.Vcapn;
        }
        o.M = sumMhi;
        if (sumMhi > 1 && o.NHat > 0)
            o.Vsrs = sumMhi * vsrsAccum * o.CvW / ((sumMhi - 1) * o.NHat);
    }

    // VBA: CSPSSES.ComputeStatistics_Step5. Per stratum, over rows with r != 2:
    //   SH2 = MH * FPC * sum((ZHI - MZH)^2) / (MH - 1)
    // and likewise for the OTC and VM residuals. SE = sqrt(sum of SH2).
    private static void Step5_StandardError(List<Stratum> strata, Overall o, bool withFpc)
    {
        double seSq = 0, seSqOtc = 0, seSqVm = 0;
        foreach (var s in strata)
        {
            var fpc = withFpc ? s.Fpc : 1.0;
            if (s.Mh == 1 && s.Vcapn == 1)
            {
                s.Sh2 = s.Sh2Otc = s.Sh2Vm = 0;
                continue;
            }
            if (s.Mh <= 1)
                throw new InvalidOperationException(
                    $"Variance stratum '{s.Id}': MH = {s.Mh} insufficient for variance estimation.");

            double sq = 0, sqOtc = 0, sqVm = 0;
            foreach (var r in s.Rows)
            {
                if (r.RCode == DispositionCodes.Noncomplete) continue;
                sq    += Square(r.Zhi - s.Mzh);
                sqOtc += Square(r.ZhiOtc - s.MzhOtc);
                sqVm  += Square(r.ZhiVm - s.MzhVm);
            }
            s.Sh2    = s.Mh * fpc * sq / (s.Mh - 1);
            s.Sh2Otc = s.Mh * fpc * sqOtc / (s.Mh - 1);
            s.Sh2Vm  = s.Mh * fpc * sqVm / (s.Mh - 1);
            seSq += s.Sh2;
            seSqOtc += s.Sh2Otc;
            seSqVm += s.Sh2Vm;
        }
        o.Se = Math.Sqrt(seSq);
        o.SeOtc = Math.Sqrt(seSqOtc);
        o.SeVm = Math.Sqrt(seSqVm);
    }

    private static double Square(double x) => x * x;

    // VBA CLng rounds half to even. Table 2 rounds each outlet type's
    // estimated population this way before summing, so rows add to totals.
    private static long CLng(double x) => (long)Math.Round(x, MidpointRounding.ToEven);

    private static SssReport BuildReport(
        SssInput input, List<Stratum> strata, List<Row> rows, Overall o, bool withFpc)
    {
        var inspectedRows = rows.Where(r => r.RCode == DispositionCodes.Complete).ToList();

        // VBA: CSPSSES.ComputeStatistics_Step6.
        var ll = Math.Max(0.0, o.R - 1.96 * o.Se);
        var ul = Math.Min(1.0, o.R + 1.96 * o.Se);
        var ul2 = o.R + 1.645 * o.Se;
        double f = o.Nn > 0 ? (double)o.M / o.Nn : 0;
        double deff1 = (o.R > 0 && o.R < 1 && o.M > 1)
            ? (o.M - 1) * o.Se * o.Se / (o.R * (1 - o.R)) : 0;
        double deff2 = (o.R > 0 && o.R < 1 && o.M > 1 && f < 1)
            ? (o.M - 1) * o.Se * o.Se / (o.R * (1 - o.R) * (1 - f)) : 0;
        // Table 1 reports DEFF3, and SSES reports 1 when the SE is zero.
        double deff3 = o.Se == 0 ? 1 : (o.Vsrs > 0 && f < 1 ? o.Se * o.Se / (o.Vsrs * (1 - f)) : 0);

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
            SamhsaPrecisionMet = 1.645 * o.Se <= 0.03,
            DesignEffect1 = deff1,
            DesignEffect2 = deff2,
            DesignEffect3 = deff3,
            OverallSamplingRate = f,
        };

        var hasUnknown = rows.Any(r => r.Otc == 0 && r.Vm == 0);

        return new SssReport
        {
            StateCode = input.StateCode,
            FederalFiscalYear = input.FederalFiscalYear,
            GeneratedAt = input.GeneratedAt ?? DateTime.Now,
            DataSource = input.DataSource,
            AnalysisOption = withFpc ? "Stratified SRS with FPC" : "Stratified SRS without FPC",
            EffectiveSampleSize = input.EffectiveSampleSize,
            TargetSampleSize = input.TargetSampleSize,
            Overall = overall,
            Table2 = BuildTable2(strata, o, hasUnknown),
            HasUnknownOutletType = hasUnknown,
            Tally = new SampleTally
            {
                CountsByCode = rows
                    .GroupBy(r => r.Source.DispositionCode.Trim().ToUpperInvariant())
                    .ToDictionary(g => g.Key, g => g.Count()),
            },
            Inspectors = BuildInspectorDemographics(inspectedRows),
            Microdata = input.Microdata,
            ProductCrossTab = BuildCrossTab("Product Type", inspectedRows, r => MapProductType(r.Source.ProductType)),
            OutletCrossTab = BuildCrossTab("Retail Outlet", inspectedRows, r => MapRetailOutlet(r.Source.RetailOutletType)),
            AskedForIdCrossTab = BuildCrossTab("Clerk Asked for ID", inspectedRows, r => MapAskedForId(r.Source.AskedForId)),
        };
    }

    // VBA: CSPSSES.Table2byStratum. Three blocks -- all outlets, then
    // over-the-counter, then vending machines -- each with one row per
    // stratum and a total. Estimated population is rounded per outlet type
    // and the rounded parts summed, so each block adds up to its total.
    private static IReadOnlyList<Table2Section> BuildTable2(List<Stratum> strata, Overall o, bool hasUnknown)
    {
        var all = new List<Table2Row>();
        var otc = new List<Table2Row>();
        var vm = new List<Table2Row>();

        foreach (var s in strata)
        {
            var first = s.Rows[0].Source;
            var samplingId = first.SamplingStratum;
            var frame = first.SamplingStratumPopulation;
            // VM frame size for the stratum; the OTC frame is whatever is left.
            var vmFrame = s.Rows.Select(r => r.Source.VmFrameSize).FirstOrDefault(x => x.HasValue) ?? 0;
            var otcFrame = vmFrame > 0 ? frame - vmFrame : frame;

            double nh = 0, nhOtc = 0, nhVm = 0, nhMissing = 0, y = 0, yOtc = 0, yVm = 0;
            foreach (var r in s.Rows)
            {
                nh += r.D * r.Awt;
                nhOtc += r.D * r.Awt * r.Otc;
                nhVm += r.D * r.Awt * r.Vm;
                if (r.Otc == 0 && r.Vm == 0) nhMissing += r.D * r.Awt;
                if (r.D == 1 && r.Yi == 1)
                {
                    y += r.Awt;
                    if (r.Otc == 1) yOtc += r.Awt;
                    else if (r.Vm == 1) yVm += r.Awt;
                }
            }

            var estOtc = CLng(nhOtc);
            var estVm = CLng(nhVm);
            var estAll = estOtc + estVm + (hasUnknown ? CLng(nhMissing) : 0);

            all.Add(MakeRow(samplingId, s.Id, frame, estAll, s.Rows, _ => true, nh > 0 ? y / nh : 0));
            otc.Add(MakeRow(samplingId, s.Id, otcFrame, estOtc, s.Rows, r => r.Otc == 1, nhOtc > 0 ? yOtc / nhOtc : 0));
            vm.Add(MakeRow(samplingId, s.Id, vmFrame, estVm, s.Rows, r => r.Vm == 1, nhVm > 0 ? yVm / nhVm : 0));
        }

        return new[]
        {
            MakeSection("All Outlets", all, o.R, o.Se),
            MakeSection("Over the Counter Outlets", otc, o.ROtc, o.SeOtc),
            MakeSection("Vending Machines", vm, o.RVm, o.SeVm),
        };
    }

    private static Table2Row MakeRow(string samplingId, string varianceId, int frame, long est,
                                     List<Row> rows, Func<Row, bool> inDomain, double rate)
    {
        var d = rows.Where(inDomain).ToList();
        return new Table2Row
        {
            SamplingStratumId = samplingId,
            VarianceStratumId = varianceId,
            OutletFrameSize = frame,
            EstimatedPopulationSize = est,
            OutletSampleSize = d.Count,
            EligibleOutletsInSample = d.Sum(r => r.D),
            InspectedCount = d.Sum(r => r.Resp),
            ViolationCount = d.Sum(r => r.Yi),
            ViolationRate = rate,
        };
    }

    private static Table2Section MakeSection(string label, List<Table2Row> rows, double rate, double se) => new()
    {
        Label = label,
        Strata = rows,
        StandardError = se,
        Total = new Table2Row
        {
            SamplingStratumId = "",
            VarianceStratumId = "",
            OutletFrameSize = rows.Sum(r => r.OutletFrameSize),
            EstimatedPopulationSize = rows.Sum(r => r.EstimatedPopulationSize),
            OutletSampleSize = rows.Sum(r => r.OutletSampleSize),
            EligibleOutletsInSample = rows.Sum(r => r.EligibleOutletsInSample),
            InspectedCount = rows.Sum(r => r.InspectedCount),
            ViolationCount = rows.Sum(r => r.ViolationCount),
            ViolationRate = rate,
        },
    };

    // VBA: CSPInspectors.GenerateResults. Each inspector is counted once,
    // under their own gender and age, with every buy they attempted. An
    // inspector whose age is outside 14-20, or whose gender is missing, goes
    // to "Other". Inspectors with no completed inspection are dropped
    // (RemoveUnUsed), which falls out of working from completed rows only.
    private static InspectorDemographics BuildInspectorDemographics(List<Row> inspectedRows)
    {
        var cells = new Dictionary<(string Gender, int Age), (int Inspectors, int Attempts, int Sales)>();
        int otherCount = 0, otherAttempts = 0, otherSales = 0, total = 0;

        foreach (var inspector in inspectedRows.GroupBy(r => (r.Source.InspectorId ?? "").Trim()))
        {
            total++;
            var gender = inspector.Select(r => NormaliseGender(r.Source.InspectorGender))
                                  .FirstOrDefault(g => g is not null);
            var age = inspector.Select(r => r.Source.InspectorAge).FirstOrDefault(a => a.HasValue);
            var attempts = inspector.Count();
            var sales = inspector.Count(r => r.Yi == 1);

            if (gender is null || age is null || age < 14 || age > 20)
            {
                otherCount++;
                otherAttempts += attempts;
                otherSales += sales;
                continue;
            }
            var key = (gender, age.Value);
            cells.TryGetValue(key, out var c);
            cells[key] = (c.Inspectors + 1, c.Attempts + attempts, c.Sales + sales);
        }

        var list = new List<InspectorAgeCell>();
        foreach (var g in new[] { "M", "F" })
        {
            for (var a = 14; a <= 20; a++)
            {
                cells.TryGetValue((g, a), out var c);
                list.Add(new InspectorAgeCell
                {
                    Gender = g,
                    Age = a,
                    InspectorCount = c.Inspectors,
                    AttemptedBuys = c.Attempts,
                    SuccessfulBuys = c.Sales,
                });
            }
        }

        return new InspectorDemographics
        {
            Cells = list,
            OtherInspectorCount = otherCount,
            OtherAttemptedBuys = otherAttempts,
            OtherSuccessfulBuys = otherSales,
            TotalInspectorCount = total,
        };
    }

    // VBA: CSPProducts / CSPOutlets / CSPCKIDS InitCounts. The left side counts
    // completed inspections by category. The pivot keeps, for each gender, the
    // same counts by category and age, plus the all-category and all-age
    // margins that the right-hand tables print.
    private static CrossTab BuildCrossTab(string label, List<Row> inspectedRows, Func<Row, string> categorize)
    {
        var left = inspectedRows
            .GroupBy(categorize)
            .Select(g => new CrossTabRow
            {
                Category = g.Key,
                AttemptedBuys = g.Count(),
                SuccessfulBuys = g.Count(r => r.Yi == 1),
                ViolationRate = (double)g.Count(r => r.Yi == 1) / g.Count(),
            })
            .ToList();

        var pivot = new Dictionary<PivotKey, BuyCount>();
        void Add(PivotKey key, BuyCount value) =>
            pivot[key] = pivot.TryGetValue(key, out var cur) ? cur.Add(value) : value;

        foreach (var r in inspectedRows)
        {
            var gender = NormaliseGender(r.Source.InspectorGender);
            if (gender is null) continue;
            var cat = categorize(r);
            var one = new BuyCount(1, r.Yi);
            Add(new PivotKey(cat, gender, null), one);
            Add(new PivotKey(null, gender, null), one);
            if (r.Source.InspectorAge is int age)
            {
                Add(new PivotKey(cat, gender, age), one);
                Add(new PivotKey(null, gender, age), one);
            }
        }

        return new CrossTab { CategoryLabel = label, Rows = left, Pivot = pivot };
    }

    private static string? NormaliseGender(string? g)
    {
        var u = (g ?? "").Trim().ToUpperInvariant();
        return u is "M" or "F" ? u : null;
    }

    // Category labels are SAMHSA's, exactly as printed on Tables 6-8, and the
    // codes are those in Tables 5.3 and 5.4 of the SSES manual. The workbook
    // writer emits each table's full category list in a fixed order, so these
    // strings must match it character for character.
    private static string MapProductType(int? code) => code switch
    {
        1 => "Cigarettes",
        2 => "Small cigars/Cigarillos",
        3 => "Smokeless tobacco",
        4 => "ENDS",
        5 => "Other",
        null => "Missing",
        _ => "Invalid",
    };

    private static string MapRetailOutlet(int? code) => code switch
    {
        1 => "Gas Station",
        2 => "Tobacco Store",
        3 => "Restaurant",
        4 => "Hotel",
        5 => "Grocery Store",
        6 => "Drug Store",
        7 => "Other",
        null => "Missing",
        _ => "Invalid",
    };

    // The manual codes this column "Y" or "N" only; anything else is Invalid.
    private static string MapAskedForId(string? value)
    {
        var u = (value ?? "").Trim().ToUpperInvariant();
        return u switch
        {
            "" => "Missing",
            "Y" => "Yes",
            "N" => "No",
            _ => "Invalid",
        };
    }
}
