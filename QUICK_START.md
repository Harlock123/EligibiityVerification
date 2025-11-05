# Quick Start Guide

## Step 1: Create the Database

```sql
-- Create database
CREATE DATABASE EligibilityVerification;
GO

-- Run the schema script
USE EligibilityVerification;
-- Then execute: DBScripts/eligibility_verification_schema_mssql.sql
```

## Step 2: Update Connection String

Edit `Elig.Console/appsettings.json`:
```json
{
  "ConnectionStrings": {
    "EligibilityDb": "Server=localhost;Database=EligibilityVerification;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

## Step 3: Run the Application

```bash
dotnet run --project Elig.Console/Elig.Console.csproj
```

## Quick Workflow

### Generate a 270 Request File

1. Run the console app
2. Choose option **1** to create sample data
3. Choose option **2** to generate a 270 file
4. The file will be saved to `./edi_output/270_[RequestId]_[Timestamp].txt`

### Parse a 271 Response File

1. Place your 271 response file in `./edi_input/`
2. Run the console app
3. Choose option **3** to parse the 271 file
4. The response will be saved to the database

### Using the Services Programmatically

#### Generate 270 File

```csharp
using var context = new EligibilityDbContext(options);
var generator = new Edi270Generator(context);

// Generate for a specific request
var edi270Content = await generator.Generate270FileAsync(
    requestId: 1,
    senderId: "SENDER123",
    receiverId: "PAYER456"
);

// Save to file
await generator.SaveToFileAsync(
    requestId: 1,
    senderId: "SENDER123",
    receiverId: "PAYER456",
    filePath: "./output/request.txt"
);
```

#### Parse 271 Response

```csharp
using var context = new EligibilityDbContext(options);
var parser = new Edi271Parser(context);

// Parse from file
var responseId = await parser.Parse271FileFromPathAsync(
    filePath: "./input/response.txt",
    currentUser: "System"
);

// Parse from string content
var responseId = await parser.Parse271FileAsync(
    ediContent: edi271String,
    currentUser: "System"
);
```

#### Query Data

```csharp
// Get pending requests (no response yet)
var pending = await context.EligibilityRequests
    .Include(r => r.Member)
    .Include(r => r.Payer)
    .Where(r => r.RequestStatusCode == "SUBMITTED")
    .Where(r => !r.Responses.Any())
    .ToListAsync();

// Get recent responses with benefits
var responses = await context.EligibilityResponses
    .Include(r => r.Request)
        .ThenInclude(r => r.Member)
    .Include(r => r.BenefitDetails)
    .OrderByDescending(r => r.ResponseReceivedDate)
    .Take(10)
    .ToListAsync();

foreach (var response in responses)
{
    Console.WriteLine($"Member: {response.Request.Member.MemberFirstName} {response.Request.Member.MemberLastName}");
    Console.WriteLine($"Coverage: {response.CoverageStatus}");
    Console.WriteLine($"Plan: {response.PlanName}");
    Console.WriteLine($"Benefits: {response.BenefitDetails.Count}");
}
```

## Sample EDI 270 Output

```
ISA*00*          *00*          *ZZ*SENDER123      *ZZ*RECEIVER456    *251105*1430*^*00501*202411051*0*P*:~
GS*HS*SENDER123*RECEIVER456*20251105*143000*143000*X*005010X279A1~
ST*270*0001*005010X279A1~
BHT*0022*13*abc123def456*20251105*143000~
HL*1**20*1~
NM1*PR*2*Blue Cross Blue Shield*****PAYERID*12345~
HL*2*1*21*1~
NM1*1P*2*City Medical Center*****XX*1234567890~
REF*EI*12-3456789~
HL*3*2*22*0~
TRN*1*abc123def456*SENDER123~
NM1*IL*1*Doe*John*A***MI*ABC123456~
N3*123 Main St~
N4*New York*NY*10001~
DMG*D8*19800515*M~
DTP*472*D8*20251112~
EQ*30~
SE*16*0001~
GE*1*143000~
IEA*1*202411051~
```

## Sample EDI 271 Response Format

The parser expects standard X12 271 format with:
- ISA/GS envelope
- ST*271 transaction header
- BHT with trace number
- HL hierarchical structure
- NM1 name segments
- EB eligibility/benefit segments
- DTP date segments
- SE/GE/IEA trailers

## Service Type Codes (Common)

- **30** - Health Benefit Plan Coverage (default)
- **1** - Medical Care
- **2** - Surgical
- **3** - Consultation
- **4** - Diagnostic X-Ray
- **5** - Diagnostic Lab
- **6** - Radiation Therapy
- **7** - Anesthesia
- **33** - Chiropractic
- **35** - Dental Care
- **47** - Hospital - Inpatient
- **48** - Hospital - Outpatient
- **50** - Hospital - Emergency
- **88** - Pharmacy
- **98** - Professional (Physician)

## Request Status Codes

- **DRAFT** - Not yet submitted
- **QUEUED** - Queued for submission
- **SUBMITTED** - Submitted to payer
- **ACKNOWLEDGED** - Acknowledged by payer
- **RESPONSE_RECEIVED** - Response received
- **ERROR** - Error in submission
- **TIMEOUT** - Request timed out
- **CANCELLED** - Request cancelled

## Eligibility Disposition Codes

- **ACTIVE** - Active coverage
- **INACTIVE** - Inactive coverage
- **ELIGIBLE** - Eligible for benefits
- **INELIGIBLE** - Ineligible for benefits
- **PENDING** - Eligibility pending
- **TERMINATED** - Coverage terminated
- **UNKNOWN** - Unknown status

## Troubleshooting

### Build Errors
```bash
# Clean and rebuild
dotnet clean
dotnet build
```

### Database Connection Issues
- Verify SQL Server is running
- Check connection string in appsettings.json
- Ensure database exists and schema is applied
- Check Windows Authentication or use SQL Server Authentication

### EDI Parsing Errors
- Verify the file is valid X12 format
- Check that trace numbers match between 270/271
- Ensure the request exists in the database before parsing 271
- Review transaction_error_log table for details

## Next Steps

1. Review the entity models in `Elig.Data/Models/`
2. Examine the EDI generation logic in `Elig.Services/Edi270Generator.cs`
3. Study the parsing logic in `Elig.Services/Edi271Parser.cs`
4. Customize the database schema if needed
5. Add your specific business logic
6. Implement proper error handling for production
7. Add authentication and authorization
8. Implement EDI transmission to actual payers
