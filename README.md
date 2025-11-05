# EDI 270/271 Eligibility Verification System

A .NET application for managing healthcare eligibility verification using EDI X12 270/271 transactions.

## Overview

This application provides a complete solution for:
- Generating EDI 270 (Eligibility Inquiry) transaction files from database records
- Parsing EDI 271 (Eligibility Response) transaction files and updating the database
- Managing eligibility requests, responses, and benefit details
- Tracking the full lifecycle of eligibility verification transactions

## Project Structure

- **Elig.Data** - Entity Framework Core models and DbContext
- **Elig.Services** - EDI 270 generator and 271 parser services
- **Elig.Console** - Interactive console application for testing and demonstration

## Database Schema

The SQL schema (`DBScripts/eligibility_verification_schema_mssql.sql`) includes:

### Lookup Tables
- `request_status` - Request status codes (DRAFT, QUEUED, SUBMITTED, etc.)
- `response_status` - Response status codes (RECEIVED, PROCESSED, etc.)
- `eligibility_disposition` - Eligibility outcomes (ACTIVE, INACTIVE, etc.)
- `benefit_type` - Benefit type codes

### Core Entities
- `payer` - Insurance companies/payers
- `provider` - Healthcare providers
- `member` - Patients/members

### Transaction Tables
- `eligibility_request` - 270 EDI requests
- `eligibility_response` - 271 EDI responses
- `benefit_detail` - Detailed benefit information from 271 responses

### Audit Tables
- `request_status_history` - Tracks status changes
- `response_status_history` - Tracks response status changes
- `transaction_error_log` - Error logging

## Setup Instructions

### 1. Database Setup

1. Create a SQL Server database named `EligibilityVerification`
2. Run the schema script:
   ```bash
   sqlcmd -S localhost -d EligibilityVerification -i DBScripts/eligibility_verification_schema_mssql.sql
   ```

### 2. Configure Connection String

Edit `Elig.Console/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "EligibilityDb": "Server=YOUR_SERVER;Database=EligibilityVerification;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "EdiSettings": {
    "SenderId": "YOUR_SENDER_ID",
    "ReceiverId": "YOUR_RECEIVER_ID",
    "OutputDirectory": "./edi_output",
    "InputDirectory": "./edi_input"
  }
}
```

### 3. Build and Run

```bash
dotnet build
dotnet run --project Elig.Console/Elig.Console.csproj
```

## Usage

The console application provides an interactive menu:

### 1. Create Sample Data
Creates sample payer, provider, member, and eligibility request records for testing.

### 2. Generate 270 Request File
- Selects an eligibility request from the database
- Generates a properly formatted EDI X12 270 transaction file
- Updates the request status in the database
- Saves the file to the output directory

### 3. Parse 271 Response File
- Reads a 271 EDI response file from the input directory
- Parses the transaction and extracts eligibility information
- Creates eligibility_response and benefit_detail records
- Updates the corresponding eligibility_request status

### 4. View Eligibility Requests
Displays recent eligibility requests with their status.

### 5. View Eligibility Responses
Displays recent eligibility responses with coverage information.

### 6. Generate 270 for All DRAFT Requests
Batch generates 270 files for all requests with DRAFT status.

## EDI File Formats

### EDI 270 (Eligibility Inquiry)
The generator creates standard X12 270 transaction files containing:
- ISA/GS envelope segments
- ST/BHT transaction header
- HL hierarchical segments (Payer, Provider, Subscriber)
- NM1 name segments
- DTP date segments
- EQ eligibility inquiry
- SE/GE/IEA trailer segments

### EDI 271 (Eligibility Response)
The parser handles standard X12 271 transaction files containing:
- Same envelope structure as 270
- EB (Eligibility/Benefit Information) segments
- Financial information (copay, deductible, out-of-pocket)
- Coverage dates and plan information
- AAA error/rejection segments

## Key Classes

### Edi270Generator
```csharp
// Generate 270 file for a specific request
var content = await generator.Generate270FileAsync(requestId, senderId, receiverId);

// Save to file
await generator.SaveToFileAsync(requestId, senderId, receiverId, filePath);

// Batch generate for all requests with a specific status
var results = await generator.Generate270FilesForStatusAsync("DRAFT", senderId, receiverId);
```

### Edi271Parser
```csharp
// Parse 271 file content
var responseId = await parser.Parse271FileAsync(ediContent, currentUser);

// Parse from file path
var responseId = await parser.Parse271FileFromPathAsync(filePath, currentUser);

// Update financial information
await parser.UpdateResponseFinancialsAsync(responseId, copay: 25.00m, deductible: 1000.00m);
```

### EligibilityDbContext
Standard Entity Framework Core DbContext with all entity sets and fluent API configuration.

## Database Notes

The database includes several features:
- **Automatic triggers** for tracking status changes
- **Audit fields** (created_at, updated_at, created_by, updated_by)
- **Foreign key relationships** maintaining referential integrity
- **Views** for common queries (vw_eligibility_transactions, vw_pending_requests)

## EDI X12 270/271 Standards

This implementation follows the 005010X279A1 implementation guide for:
- 270 - Health Care Eligibility Benefit Inquiry
- 271 - Health Care Eligibility Benefit Response

For detailed specifications, refer to the ASC X12 standards documentation.

## Production Considerations

Before using in production:
1. **Encrypt sensitive data** (SSN, member IDs) - currently stored as plain text
2. **Implement proper authentication** for the SenderId/ReceiverId
3. **Add comprehensive error handling** and logging
4. **Implement retry logic** for failed transactions
5. **Add validation** for EDI file formats
6. **Configure proper security** for database connections
7. **Implement batch processing** for high-volume scenarios
8. **Add transaction timeout handling**
9. **Implement proper EDI envelope numbering** (ISA/GS control numbers)

## License

This is a demonstration project. Modify as needed for your specific requirements.
