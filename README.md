# synar-sses

A web replacement for SAMHSA's legacy SSES (Synar Survey Estimation System)
Excel-VBA program. Generates the SSES Tables 1–8 xlsx workbook that states
submit to SAMHSA each year.

The legacy program (`SSES_v7_0.xlsm`, August 2018) is a macro-enabled
workbook that is increasingly hard to run on modern locked-down Excel
installs. This app implements the same Taylor-linearization variance
estimator in C# and produces a byte-equivalent output.

## Status

Early scaffold. Validation target: reproduce
`Synar2025_SSES_Final.xlsx` from Kentucky 2025 microdata.

## Stack

- .NET 10, ASP.NET Core Razor Pages
- Dapper + Npgsql (Postgres)
- ClosedXML (Excel I/O)
- nginx + systemd on the existing Reach DigitalOcean droplet (sses.reacheval.com)

## Repo layout

```
app/
  SynarSSES.slnx
  SynarSSES.Core/          # Models, Services (port of VBA logic), Repositories
  SynarSSES.Web/           # Razor Pages
migrations/                # SQL migrations (synar_inspector etc.)
deploy/                    # nginx + systemd configs for production
```
