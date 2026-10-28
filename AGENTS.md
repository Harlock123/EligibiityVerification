# AGENTS.md

Notes for anyone, human or AI agent, changing this code. `README.md` and `QUICK_START.md` cover setup, the menu
and the EDI formats; this file covers how the code is put together and what to watch when changing it.

## What this is

A demonstration of healthcare eligibility checks with X12 EDI 270 (inquiry) and 271 (response) files, in C# on
.NET 9 with Entity Framework Core 9 on SQL Server. A console menu creates sample data, writes 270 files from
request rows, and reads 271 files back into response and benefit rows. The repository and its solution are
spelled differently: the solution is `EligibilityVerification.sln`.

## Commands

```bash
dotnet build EligibilityVerification.sln
dotnet run --project Elig.Console/Elig.Console.csproj      # interactive menu; needs the database below
sqlcmd -S localhost -d EligibilityVerification -i DBScripts/eligibility_verification_schema_mssql.sql
```

There are no tests yet. The console reads `appsettings.json` from the current directory, so run it from a folder
that has one (the project copies it to the build output).

## Layout

- `Elig.Data/`: EF Core 9 model.
  - `EligibilityDbContext.cs` maps every entity to the snake_case tables and columns of the SQL scripts with
    the fluent API (`ToTable`, `HasColumnName`), and uses SQL Server defaults such as `GETDATE()`.
  - `Models/`: one class per table: lookups (`RequestStatus`, `ResponseStatus`, `EligibilityDisposition`,
    `BenefitType`), `Payer`, `Provider`, `Member`, `EligibilityRequest`, `EligibilityResponse`, `BenefitDetail`,
    and the history and error-log tables.
- `Elig.Services/`:
  - `Edi270Generator.cs` builds the ISA/GS/ST ... SE/GE/IEA envelope and body for one request, saves it, or does
    every request in a given status.
  - `Edi271Parser.cs` splits a 271 on `~` and `*`, reads `ST`, `BHT`, `TRN`, `EB`, `DTP`, `AAA` and the name and
    address segments, and writes a response with its benefit details.
- `Elig.Console/Program.cs`: loads the settings, creates the context with `UseSqlServer`, and runs the
  seven-option menu.
- `Elig.Console/appsettings.json`: connection string `EligibilityDb` and `EdiSettings` (sender, receiver, input
  and output folders, created on start).
- `DBScripts/`: the schema and sample queries, in a SQL Server version (`*_mssql.sql`, the one the code uses) and
  a PostgreSQL version.

## Rules that keep it working

- **The scripts are the schema.** There are no EF migrations. Change a table in the SQL scripts and in
  `EligibilityDbContext` together, and keep the two script dialects in step.
- **Build output is committed.** `bin/` and `obj/` under each project are tracked (there is no `.gitignore`), so
  building changes tracked files. Do not commit those changes with source edits.
- **Control numbers are not real.** The interchange and group numbers come from the clock and the transaction
  number from the request id; the README lists this and other gaps under production considerations.
- **No real data.** Members carry SSNs and member ids in plain text. Use only made-up records and keep generated
  EDI files out of the repository.

## Making a change

- Run the menu against a scratch database: create sample data, generate a 270, feed a 271 back in, and view the
  results.
- Update `README.md` when the menu, settings or schema change.
