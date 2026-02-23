using Elig.Data;
using Elig.Data.Models;
using Elig.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Linq;

namespace Elig.Console;

class Program
{
    // All benefit_type codes present in the seed data
    private static readonly string[] SeededBenefitCodes =
    [
        "1",  "2",  "4",  "5",  "6",  "7",  "8",  "12", "13", "18",
        "20", "30", "33", "35", "40", "42", "45", "47", "48", "50",
        "51", "52", "53", "62", "65", "68", "73", "76", "78", "80",
        "81", "82", "86", "88", "91", "92", "93", "98", "99", "A0",
        "A3", "A6", "A7", "A8", "AD", "AE", "AF", "AG", "AI", "AL",
        "BG", "MH", "UC"
    ];

    static async Task Main(string[] args)
    {
        System.Console.WriteLine("=== EDI 270/271 Eligibility Verification System ===\n");

        // Load configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        var connectionString = configuration.GetConnectionString("EligibilityDb");
        var senderId = configuration["EdiSettings:SenderId"] ?? "SENDER123";
        var receiverId = configuration["EdiSettings:ReceiverId"] ?? "RECEIVER456";
        var outputDir = configuration["EdiSettings:OutputDirectory"] ?? "./edi_output";
        var inputDir = configuration["EdiSettings:InputDirectory"] ?? "./edi_input";
        var maxMembersPerFile = int.TryParse(configuration["EdiSettings:MaxMembersPerFile"], out var maxMembers) ? maxMembers : 10000;
        var fileNamePattern = configuration["EdiSettings:FileNamePattern"] ?? "270_{payerId}_{providerId}_{batchId}_{timestamp}.txt";

        // Ensure directories exist
        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(inputDir);

        // Setup DbContext
        var optionsBuilder = new DbContextOptionsBuilder<EligibilityDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        await using var context = new EligibilityDbContext(optionsBuilder.Options);

        // Initialize services
        var edi270Generator = new Edi270Generator(context);
        var edi271Parser = new Edi271Parser(context);

        bool running = true;
        while (running)
        {
            System.Console.WriteLine("\nSelect an option:");
            System.Console.WriteLine("1. Create sample data");
            System.Console.WriteLine("2. Generate 270 request file");
            System.Console.WriteLine("3. Parse 271 response file");
            System.Console.WriteLine("4. View eligibility requests");
            System.Console.WriteLine("5. View eligibility responses");
            System.Console.WriteLine("6. Generate 270 for all DRAFT requests");
            System.Console.WriteLine("7. Generate 270 batch file(s)");
            System.Console.WriteLine("8. Smoke test multi-member 271 parser");
            System.Console.WriteLine("9. Exit");
            System.Console.Write("\nChoice: ");

            var choice = System.Console.ReadLine();

            try
            {
                switch (choice)
                {
                    case "1":
                        await CreateSampleDataAsync(context);
                        break;
                    case "2":
                        await Generate270FileAsync(context, edi270Generator, senderId, receiverId, outputDir);
                        break;
                    case "3":
                        await Parse271FileAsync(context, edi271Parser, inputDir);
                        break;
                    case "4":
                        await ViewEligibilityRequestsAsync(context);
                        break;
                    case "5":
                        await ViewEligibilityResponsesAsync(context);
                        break;
                    case "6":
                        await GenerateAllDraftRequestsAsync(context, edi270Generator, senderId, receiverId, outputDir);
                        break;
                    case "7":
                        await GenerateBatch270FilesAsync(context, edi270Generator, senderId, receiverId, outputDir, maxMembersPerFile, fileNamePattern);
                        break;
                    case "8":
                        await SmokeTest271ParserAsync(context, edi271Parser);
                        break;
                    case "9":
                        running = false;
                        break;
                    default:
                        System.Console.WriteLine("Invalid choice. Please try again.");
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"\nError: {ex.Message}");
                System.Console.WriteLine($"Details: {ex.InnerException?.Message}");
            }
        }

        System.Console.WriteLine("\nGoodbye!");
    }

    static string BuildSmokeTest271(IReadOnlyList<(string traceNumber, string firstName, bool isActive, string planName, string benefitTypeCode)> subscribers)
    {
        var sb = new System.Text.StringBuilder();
        void Seg(string s) => sb.Append(s).Append('~').AppendLine();

        Seg("ISA*00*          *00*          *ZZ*RECEIVER456    *ZZ*SENDER123      *240101*1200*^*00501*000000001*0*P*:");
        Seg("GS*HB*RECEIVER456*SENDER123*20240101*1200*1*X*005010X279A1");
        Seg("ST*271*0001*005010X279A1");
        Seg("BHT*0022*11*SMOKETEST*20240101*1200");
        Seg("HL*1**20*1");
        Seg("NM1*PR*2*TEST PAYER*****PI*00001");
        Seg("HL*2*1*21*1");
        Seg("NM1*1P*2*TEST PROVIDER*****XX*9999999999");

        int hlIndex = 3;
        foreach (var (trace, firstName, isActive, planName, benefitTypeCode) in subscribers)
        {
            var eligCode = isActive ? "1" : "6"; // 1 = Active, 6 = Inactive
            Seg($"HL*{hlIndex}*2*22*0");
            Seg($"TRN*2*{trace}*9SMOKETEST");
            Seg($"NM1*IL*1*DOE*{firstName}****MI*TST{hlIndex:000}");
            // EB03 = service type code → maps to BenefitTypeCode — must exist in benefit_type seed data
            Seg($"EB*{eligCode}*FAM*{benefitTypeCode}**{planName}");
            Seg("DTP*291*D8*20240101");
            hlIndex++;
        }

        var segCount = sb.ToString().Split('~', StringSplitOptions.RemoveEmptyEntries).Length + 1; // +SE
        Seg($"SE*{segCount}*0001");
        Seg("GE*1*1");
        Seg("IEA*1*000000001");

        return sb.ToString();
    }

    static async Task SmokeTest271ParserAsync(EligibilityDbContext context, Edi271Parser parser)
    {
        System.Console.WriteLine("\n=== [TEST] Multi-Member 271 Parser Smoke Test ===");
        System.Console.Write("How many test subscribers? (default 3): ");
        var input = System.Console.ReadLine();
        int count = int.TryParse(input, out var n) && n > 0 ? n : 3;

        // --- 1. Seed a payer + provider if none exist ---
        var payer = await context.Payers.FirstOrDefaultAsync()
            ?? new Payer { PayerName = "Test Payer", PayerIdentifier = "00001", PayerIdentifierType = "PAYERID", CreatedBy = "SmokeTest" };
        if (payer.PayerId == 0) { context.Payers.Add(payer); await context.SaveChangesAsync(); }

        var provider = await context.Providers.FirstOrDefaultAsync()
            ?? new Provider { ProviderNpi = "9999999999", ProviderType = "ORGANIZATION", CreatedBy = "SmokeTest" };
        if (provider.ProviderId == 0) { context.Providers.Add(provider); await context.SaveChangesAsync(); }

        // --- 2. Seed test requests with unique trace numbers ---
        var testRequests = new List<EligibilityRequest>();
        for (int i = 0; i < count; i++)
        {
            var member = new Member
            {
                MemberFirstName = $"Smoke{i + 1}",
                MemberLastName = "Tester",
                MemberDateOfBirth = new DateTime(1990, 1, 1),
                MemberGender = "M",
                MemberSubscriberId = $"SMKTST{i + 1:000}",
                IsSubscriber = true,
                CreatedBy = "SmokeTest"
            };
            context.Members.Add(member);
            await context.SaveChangesAsync();

            var req = new EligibilityRequest
            {
                TraceNumber = $"SMKTEST{Guid.NewGuid():N}"[..20],
                MemberId = member.MemberId,
                ProviderId = provider.ProviderId,
                PayerId = payer.PayerId,
                RequestStatusCode = "QUEUED",
                CreatedBy = "SmokeTest"
            };
            context.EligibilityRequests.Add(req);
            await context.SaveChangesAsync();
            testRequests.Add(req);
        }

        System.Console.WriteLine($"\nSeeded {count} request(s). Building 271...");

        // --- 3. Build subscribers — cycle through seeded benefit codes, alternate active/inactive ---
        var subscribers = testRequests.Select((r, idx) =>
        (
            r.TraceNumber,
            $"Smoke{idx + 1}",
            isActive: idx % 2 == 0,
            planName: idx % 2 == 0 ? "GOLD PLAN" : "INACTIVE PLAN",
            benefitTypeCode: SeededBenefitCodes[idx % SeededBenefitCodes.Length]
        )).ToList();

        var edi271 = BuildSmokeTest271(subscribers);

        System.Console.Write("\nPrint generated 271? (y/n): ");
        if (System.Console.ReadLine()?.Trim().ToLower() == "y")
        {
            System.Console.WriteLine("\n" + edi271);
        }

        // --- 4. Parse ---
        System.Console.WriteLine("\nParsing...");
        await parser.Parse271FileAsync(edi271, "SmokeTest");

        // --- 5. Report results ---
        var responsesByTrace = await context.EligibilityResponses
            .Include(r => r.BenefitDetails)
            .Where(r => testRequests.Select(x => x.TraceNumber).Contains(r.TraceNumber))
            .ToDictionaryAsync(r => r.TraceNumber);

        var updatedRequests = await context.EligibilityRequests
            .Where(r => testRequests.Select(x => x.RequestId).Contains(r.RequestId))
            .ToDictionaryAsync(r => r.TraceNumber);

        System.Console.WriteLine($"\n{"#",-4} {"Trace",-22} {"Coverage",-10} {"Plan",-14} {"BenefitCode",-13} {"Benefits",-10} {"Req Status",-20} {"PASS?"}");
        System.Console.WriteLine(new string('-', 100));

        int passed = 0;
        for (int i = 0; i < subscribers.Count; i++)
        {
            var (trace, firstName, isActive, planName, benefitTypeCode) = subscribers[i];
            responsesByTrace.TryGetValue(trace, out var resp);
            updatedRequests.TryGetValue(trace, out var req);

            bool responseExists  = resp is not null;
            bool coverageCorrect = isActive ? resp?.CoverageStatus == "ACTIVE"
                                            : resp?.CoverageStatus != "ACTIVE";
            bool statusUpdated   = req?.RequestStatusCode == "RESPONSE_RECEIVED";
            bool benefitsPresent = resp?.BenefitDetails.Count > 0;
            bool codeCorrect     = resp?.BenefitDetails.Any(b => b.BenefitTypeCode == benefitTypeCode) ?? false;
            bool pass            = responseExists && coverageCorrect && statusUpdated && benefitsPresent && codeCorrect;

            if (pass) passed++;

            System.Console.WriteLine(
                $"{i + 1,-4} {trace,-22} {resp?.CoverageStatus ?? "MISSING",-10} " +
                $"{resp?.PlanName ?? "N/A",-14} {benefitTypeCode,-13} {resp?.BenefitDetails.Count ?? 0,-10} " +
                $"{req?.RequestStatusCode ?? "UNCHANGED",-20} {(pass ? "✓" : "✗ FAIL")}");
        }

        System.Console.WriteLine(new string('-', 100));
        System.Console.WriteLine($"\nResult: {passed}/{count} passed.");

        if (passed < count)
        {
            System.Console.WriteLine("\nFailed checks per subscriber:");
            for (int i = 0; i < subscribers.Count; i++)
            {
                var (trace, _, isActive, _, benefitTypeCode) = subscribers[i];
                responsesByTrace.TryGetValue(trace, out var resp);
                updatedRequests.TryGetValue(trace, out var req);

                var failures = new List<string>();
                if (resp is null)                                                        failures.Add("no response row created");
                if (isActive  && resp?.CoverageStatus != "ACTIVE")                      failures.Add($"expected ACTIVE, got '{resp?.CoverageStatus}'");
                if (!isActive && resp?.CoverageStatus == "ACTIVE")                      failures.Add("expected non-ACTIVE coverage");
                if (req?.RequestStatusCode != "RESPONSE_RECEIVED")                      failures.Add($"request status still '{req?.RequestStatusCode}'");
                if ((resp?.BenefitDetails.Count ?? 0) == 0)                             failures.Add("no benefit details saved");
                if (!(resp?.BenefitDetails.Any(b => b.BenefitTypeCode == benefitTypeCode) ?? false))
                                                                                         failures.Add($"benefit code '{benefitTypeCode}' not found on saved detail");

                if (failures.Count > 0)
                    System.Console.WriteLine($"  [{i + 1}] {trace}: {string.Join(", ", failures)}");
            }
        }
    }

    static async Task CreateSampleDataAsync(EligibilityDbContext context)
    {
        System.Console.WriteLine("\n=== Creating Sample Data ===");

        // Check if data already exists
        if (await context.Payers.AnyAsync())
        {
            System.Console.WriteLine("Sample data already exists. Skipping creation.");
            return;
        }

        // Create Payer
        var payer = new Payer
        {
            PayerName = "Blue Cross Blue Shield",
            PayerIdentifier = "12345",
            PayerIdentifierType = "PAYERID",
            PayerCity = "Chicago",
            PayerState = "IL",
            PayerZip = "60601",
            CreatedBy = "System"
        };
        context.Payers.Add(payer);

        // Create Provider
        var provider = new Provider
        {
            ProviderNpi = "1234567890",
            ProviderTaxId = "12-3456789",
            ProviderOrganizationName = "City Medical Center",
            ProviderType = "ORGANIZATION",
            ProviderCity = "New York",
            ProviderState = "NY",
            ProviderZip = "10001",
            CreatedBy = "System"
        };
        context.Providers.Add(provider);

        await context.SaveChangesAsync();

        // Create multiple members + requests for batching
        var requestsToAdd = new List<EligibilityRequest>();
        for (int i = 1; i <= 10; i++)
        {
            var m = new Member
            {
                MemberFirstName = $"John{i}",
                MemberLastName = "Doe",
                MemberDateOfBirth = new DateTime(1980, 5, 15),
                MemberGender = "M",
                MemberSubscriberId = $"ABC123{i:000}",
                MemberGroupNumber = "GRP001",
                MemberAddressLine1 = $"{100 + i} Main St",
                MemberCity = "New York",
                MemberState = "NY",
                MemberZip = "10001",
                IsSubscriber = true,
                CreatedBy = "System"
            };
            context.Members.Add(m);
            await context.SaveChangesAsync();

            requestsToAdd.Add(new EligibilityRequest
            {
                TraceNumber = Guid.NewGuid().ToString("N").Substring(0, 20),
                MemberId = m.MemberId,
                ProviderId = provider.ProviderId,
                PayerId = payer.PayerId,
                ServiceTypeCode = "30",
                ServiceDate = DateTime.Today.AddDays(7),
                RequestStatusCode = "DRAFT",
                CreatedBy = "System"
            });
        }

        context.EligibilityRequests.AddRange(requestsToAdd);
        await context.SaveChangesAsync();

        System.Console.WriteLine($"Sample data created successfully!");
        System.Console.WriteLine($"  Payer ID: {payer.PayerId}");
        System.Console.WriteLine($"  Provider ID: {provider.ProviderId}");
        System.Console.WriteLine($"  Member ID: {requestsToAdd.First().MemberId}");
        System.Console.WriteLine($"  Request ID: {requestsToAdd.First().RequestId}");
        System.Console.WriteLine($"  Trace Number: {requestsToAdd.First().TraceNumber}");
    }

    static async Task Generate270FileAsync(EligibilityDbContext context, Edi270Generator generator,
        string senderId, string receiverId, string outputDir)
    {
        System.Console.WriteLine("\n=== Generate 270 Request File ===");

        var requests = await context.EligibilityRequests
            .Include(r => r.Member)
            .Include(r => r.Provider)
            .Include(r => r.Payer)
            .Where(r => r.RequestStatusCode == "DRAFT" || r.RequestStatusCode == "QUEUED")
            .ToListAsync();

        if (!requests.Any())
        {
            System.Console.WriteLine("No eligible requests found. Create sample data first.");
            return;
        }

        System.Console.WriteLine($"\nFound {requests.Count} eligible request(s):");
        for (int i = 0; i < requests.Count; i++)
        {
            System.Console.WriteLine($"{i + 1}. Request ID: {requests[i].RequestId} - " +
                                   $"Member: {requests[i].Member.MemberFirstName} {requests[i].Member.MemberLastName} - " +
                                   $"Status: {requests[i].RequestStatusCode}");
        }

        System.Console.Write("\nEnter request number to generate (or 0 to cancel): ");
        if (!int.TryParse(System.Console.ReadLine(), out int choice) || choice < 1 || choice > requests.Count)
        {
            System.Console.WriteLine("Invalid selection.");
            return;
        }

        var selectedRequest = requests[choice - 1];
        var fileName = $"270_{selectedRequest.RequestId}_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
        var filePath = Path.Combine(outputDir, fileName);

        await generator.SaveToFileAsync(selectedRequest.RequestId, senderId, receiverId, filePath);

        System.Console.WriteLine($"\n270 file generated successfully!");
        System.Console.WriteLine($"File: {filePath}");
        System.Console.WriteLine($"Request status updated to: QUEUED");

        System.Console.Write("\nDisplay file contents? (y/n): ");
        if (System.Console.ReadLine()?.ToLower() == "y")
        {
            var content = await File.ReadAllTextAsync(filePath);
            System.Console.WriteLine("\n" + content);
        }
    }

    static async Task Parse271FileAsync(EligibilityDbContext context, Edi271Parser parser, string inputDir)
    {
        System.Console.WriteLine("\n=== Parse 271 Response File ===");

        var files = Directory.GetFiles(inputDir, "*.txt")
            .Concat(Directory.GetFiles(inputDir, "*.edi"))
            .Concat(Directory.GetFiles(inputDir, "*.271"))
            .ToArray();

        if (!files.Any())
        {
            System.Console.WriteLine($"No files found in {inputDir}");
            System.Console.WriteLine("Place your 271 response files (.txt, .edi, or .271) in this directory.");
            return;
        }

        System.Console.WriteLine($"\nFound {files.Length} file(s):");
        for (int i = 0; i < files.Length; i++)
        {
            System.Console.WriteLine($"{i + 1}. {Path.GetFileName(files[i])}");
        }

        System.Console.Write("\nEnter file number to parse (or 0 to cancel): ");
        if (!int.TryParse(System.Console.ReadLine(), out int choice) || choice < 1 || choice > files.Length)
        {
            System.Console.WriteLine("Invalid selection.");
            return;
        }

        var selectedFile = files[choice - 1];
        System.Console.WriteLine($"\nParsing file: {Path.GetFileName(selectedFile)}");

        var responseId = await parser.Parse271FileFromPathAsync(selectedFile, "System");

        System.Console.WriteLine($"\n271 file parsed successfully!");
        System.Console.WriteLine($"Response ID: {responseId}");

        var response = await context.EligibilityResponses
            .Include(r => r.Request)
            .Include(r => r.BenefitDetails)
            .FirstOrDefaultAsync(r => r.ResponseId == responseId);

        if (response != null)
        {
            System.Console.WriteLine($"Trace Number: {response.TraceNumber}");
            System.Console.WriteLine($"Request ID: {response.RequestId}");
            System.Console.WriteLine($"Coverage Status: {response.CoverageStatus}");
            System.Console.WriteLine($"Plan Name: {response.PlanName}");
            System.Console.WriteLine($"Benefits Parsed: {response.BenefitDetails.Count}");
        }
    }

    static async Task ViewEligibilityRequestsAsync(EligibilityDbContext context)
    {
        System.Console.WriteLine("\n=== Eligibility Requests ===");

        var requests = await context.EligibilityRequests
            .Include(r => r.Member)
            .Include(r => r.Provider)
            .Include(r => r.Payer)
            .OrderByDescending(r => r.CreatedAt)
            .Take(20)
            .ToListAsync();

        if (!requests.Any())
        {
            System.Console.WriteLine("No requests found.");
            return;
        }

        System.Console.WriteLine($"\nShowing {requests.Count} most recent request(s):\n");
        System.Console.WriteLine($"{"ID",-6} {"Trace Number",-22} {"Member",-25} {"Status",-20} {"Created",-20}");
        System.Console.WriteLine(new string('-', 100));

        foreach (var req in requests)
        {
            var memberName = $"{req.Member.MemberFirstName} {req.Member.MemberLastName}";
            System.Console.WriteLine($"{req.RequestId,-6} {req.TraceNumber,-22} {memberName,-25} {req.RequestStatusCode,-20} {req.CreatedAt:yyyy-MM-dd HH:mm}");
        }
    }

    static async Task ViewEligibilityResponsesAsync(EligibilityDbContext context)
    {
        System.Console.WriteLine("\n=== Eligibility Responses ===");

        var responses = await context.EligibilityResponses
            .Include(r => r.Request)
            .ThenInclude(r => r.Member)
            .Include(r => r.BenefitDetails)
            .OrderByDescending(r => r.ResponseReceivedDate)
            .Take(20)
            .ToListAsync();

        if (!responses.Any())
        {
            System.Console.WriteLine("No responses found.");
            return;
        }

        System.Console.WriteLine($"\nShowing {responses.Count} most recent response(s):\n");
        System.Console.WriteLine($"{"ID",-6} {"Req ID",-8} {"Member",-25} {"Coverage",-12} {"Benefits",-10} {"Received",-20}");
        System.Console.WriteLine(new string('-', 100));

        foreach (var resp in responses)
        {
            var memberName = $"{resp.Request.Member.MemberFirstName} {resp.Request.Member.MemberLastName}";
            var coverage = resp.CoverageStatus ?? "N/A";
            System.Console.WriteLine($"{resp.ResponseId,-6} {resp.RequestId,-8} {memberName,-25} {coverage,-12} {resp.BenefitDetails.Count,-10} {resp.ResponseReceivedDate:yyyy-MM-dd HH:mm}");
        }
    }

    static async Task GenerateAllDraftRequestsAsync(EligibilityDbContext context, Edi270Generator generator,
        string senderId, string receiverId, string outputDir)
    {
        System.Console.WriteLine("\n=== Generate 270 for All DRAFT Requests ===");

        var results = await generator.Generate270FilesForStatusAsync("DRAFT", senderId, receiverId);

        if (!results.Any())
        {
            System.Console.WriteLine("No DRAFT requests found.");
            return;
        }

        System.Console.WriteLine($"\nGenerated {results.Count} file(s):");

        foreach (var (requestId, content) in results)
        {
            var fileName = $"270_{requestId}_{DateTime.Now:yyyyMMdd_HHmms}.txt";
            var filePath = Path.Combine(outputDir, fileName);
            await File.WriteAllTextAsync(filePath, content);
            System.Console.WriteLine($"  Request {requestId}: {filePath}");
        }

        System.Console.WriteLine("\nAll files generated successfully!");
    }

    static string BuildBatchFileName(string pattern, long payerId, long providerId, long batchId)
    {
        return pattern
            .Replace("{payerId}", payerId.ToString())
            .Replace("{providerId}", providerId.ToString())
            .Replace("{batchId}", batchId.ToString())
            .Replace("{timestamp}", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
    }

    static async Task GenerateBatch270FilesAsync(
        EligibilityDbContext context,
        Edi270Generator generator,
        string senderId,
        string receiverId,
        string outputDir,
        int maxMembersPerFile,
        string fileNamePattern)
    {
        System.Console.WriteLine("\n=== Generate 270 Batch File(s) ===");

        var eligible = await context.EligibilityRequests
            .Include(r => r.Member)
            .Include(r => r.Provider)
            .Include(r => r.Payer)
            .Where(r => r.RequestStatusCode == "DRAFT" || r.RequestStatusCode == "QUEUED")
            .OrderBy(r => r.InquiryDate)
            .ToListAsync();

        if (!eligible.Any())
        {
            System.Console.WriteLine("No eligible requests found.");
            return;
        }

        var groups = eligible.GroupBy(r => new { r.PayerId, r.ProviderId });
        var filesWritten = 0;

        foreach (var group in groups)
        {
            foreach (var chunk in group.Chunk(maxMembersPerFile))
            {
                var (batchId, content) = await generator.Generate270BatchFileAsync(chunk.Select(r => r.RequestId), senderId, receiverId);
                var fileName = BuildBatchFileName(fileNamePattern, group.Key.PayerId, group.Key.ProviderId, batchId);
                var filePath = Path.Combine(outputDir, fileName);
                await File.WriteAllTextAsync(filePath, content);
                filesWritten++;
                System.Console.WriteLine($"Batch {batchId} -> {filePath} (members: {chunk.Length})");
            }
        }

        System.Console.WriteLine($"\nCompleted. Files generated: {filesWritten}");
    }
}
