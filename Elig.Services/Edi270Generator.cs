using Elig.Data;
using Elig.Data.Models;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace Elig.Services;

/// <summary>
/// Generates EDI X12 270 Eligibility Inquiry files from database records
/// </summary>
public class Edi270Generator
{
    private readonly EligibilityDbContext _context;
    private const char ElementSeparator = '*';
    private const char SegmentTerminator = '~';
    private const char SubelementSeparator = ':';

    public Edi270Generator(EligibilityDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Generates a 270 EDI file for a specific eligibility request
    /// </summary>
    public async Task<string> Generate270FileAsync(long requestId, string senderId, string receiverId)
    {
        var request = await _context.EligibilityRequests
            .Include(r => r.Member)
            .Include(r => r.Provider)
            .Include(r => r.Payer)
            .FirstOrDefaultAsync(r => r.RequestId == requestId);

        if (request == null)
            throw new ArgumentException($"Request with ID {requestId} not found");

        var sb = new StringBuilder();
        var now = DateTime.Now;
        var interchangeControlNumber = now.ToString("yyyyMMddHHmmss").Substring(4, 9); // Last 9 digits
        var groupControlNumber = now.ToString("HHmmss");
        var transactionControlNumber = requestId.ToString().PadLeft(4, '0');

        // ISA - Interchange Control Header
        sb.AppendLine($"ISA{ElementSeparator}00{ElementSeparator}          {ElementSeparator}00{ElementSeparator}          " +
                     $"{ElementSeparator}ZZ{ElementSeparator}{senderId.PadRight(15)}{ElementSeparator}ZZ{ElementSeparator}{receiverId.PadRight(15)}" +
                     $"{ElementSeparator}{now:yyMMdd}{ElementSeparator}{now:HHmm}{ElementSeparator}^{ElementSeparator}00501" +
                     $"{ElementSeparator}{interchangeControlNumber}{ElementSeparator}0{ElementSeparator}P{ElementSeparator}{SubelementSeparator}{SegmentTerminator}");

        // GS - Functional Group Header
        sb.AppendLine($"GS{ElementSeparator}HS{ElementSeparator}{senderId}{ElementSeparator}{receiverId}" +
                     $"{ElementSeparator}{now:yyyyMMdd}{ElementSeparator}{now:HHmmss}{ElementSeparator}{groupControlNumber}" +
                     $"{ElementSeparator}X{ElementSeparator}005010X279A1{SegmentTerminator}");

        // ST - Transaction Set Header
        sb.AppendLine($"ST{ElementSeparator}270{ElementSeparator}{transactionControlNumber}" +
                     $"{ElementSeparator}005010X279A1{SegmentTerminator}");

        // BHT - Beginning of Hierarchical Transaction
        sb.AppendLine($"BHT{ElementSeparator}0022{ElementSeparator}13{ElementSeparator}{request.TraceNumber}" +
                     $"{ElementSeparator}{now:yyyyMMdd}{ElementSeparator}{now:HHmmss}{SegmentTerminator}");

        int hlCounter = 1;

        // HL - Information Source (Payer)
        sb.AppendLine($"HL{ElementSeparator}{hlCounter}{ElementSeparator}{ElementSeparator}20{ElementSeparator}1{SegmentTerminator}");
        sb.AppendLine($"NM1{ElementSeparator}PR{ElementSeparator}2{ElementSeparator}{request.Payer.PayerName}" +
                     $"{ElementSeparator}{ElementSeparator}{ElementSeparator}{ElementSeparator}{ElementSeparator}{ElementSeparator}" +
                     $"{request.Payer.PayerIdentifierType}{ElementSeparator}{request.Payer.PayerIdentifier}{SegmentTerminator}");

        int payerHL = hlCounter++;

        // HL - Information Receiver (Provider)
        sb.AppendLine($"HL{ElementSeparator}{hlCounter}{ElementSeparator}{payerHL}{ElementSeparator}21{ElementSeparator}1{SegmentTerminator}");

        var providerName = !string.IsNullOrEmpty(request.Provider.ProviderOrganizationName)
            ? request.Provider.ProviderOrganizationName
            : $"{request.Provider.ProviderLastName}{ElementSeparator}{request.Provider.ProviderFirstName}";

        var entityTypeCode = !string.IsNullOrEmpty(request.Provider.ProviderOrganizationName) ? "2" : "1";

        sb.AppendLine($"NM1{ElementSeparator}1P{ElementSeparator}{entityTypeCode}{ElementSeparator}{providerName}" +
                     $"{ElementSeparator}{ElementSeparator}{ElementSeparator}{ElementSeparator}{ElementSeparator}{ElementSeparator}" +
                     $"XX{ElementSeparator}{request.Provider.ProviderNpi}{SegmentTerminator}");

        if (!string.IsNullOrEmpty(request.Provider.ProviderTaxId))
        {
            sb.AppendLine($"REF{ElementSeparator}EI{ElementSeparator}{request.Provider.ProviderTaxId}{SegmentTerminator}");
        }

        int providerHL = hlCounter++;

        // HL - Subscriber
        sb.AppendLine($"HL{ElementSeparator}{hlCounter}{ElementSeparator}{providerHL}{ElementSeparator}22{ElementSeparator}0{SegmentTerminator}");

        sb.AppendLine($"TRN{ElementSeparator}1{ElementSeparator}{request.TraceNumber}{ElementSeparator}{senderId}{SegmentTerminator}");

        sb.AppendLine($"NM1{ElementSeparator}IL{ElementSeparator}1{ElementSeparator}{request.Member.MemberLastName}" +
                     $"{ElementSeparator}{request.Member.MemberFirstName}" +
                     $"{ElementSeparator}{request.Member.MemberMiddleName ?? ""}{ElementSeparator}{ElementSeparator}{ElementSeparator}" +
                     $"{ElementSeparator}MI{ElementSeparator}{request.Member.MemberSubscriberId ?? ""}{SegmentTerminator}");

        // N3 - Subscriber Address
        if (!string.IsNullOrEmpty(request.Member.MemberAddressLine1))
        {
            sb.AppendLine($"N3{ElementSeparator}{request.Member.MemberAddressLine1}" +
                         $"{(string.IsNullOrEmpty(request.Member.MemberAddressLine2) ? "" : $"{ElementSeparator}{request.Member.MemberAddressLine2}")}{SegmentTerminator}");
        }

        // N4 - Subscriber City/State/ZIP
        if (!string.IsNullOrEmpty(request.Member.MemberCity))
        {
            sb.AppendLine($"N4{ElementSeparator}{request.Member.MemberCity}{ElementSeparator}{request.Member.MemberState}" +
                         $"{ElementSeparator}{request.Member.MemberZip}{SegmentTerminator}");
        }

        // DMG - Subscriber Demographic Information
        sb.AppendLine($"DMG{ElementSeparator}D8{ElementSeparator}{request.Member.MemberDateOfBirth:yyyyMMdd}" +
                     $"{ElementSeparator}{request.Member.MemberGender ?? "U"}{SegmentTerminator}");

        // DTP - Service Date (if specified)
        if (request.ServiceDate.HasValue)
        {
            sb.AppendLine($"DTP{ElementSeparator}472{ElementSeparator}D8{ElementSeparator}{request.ServiceDate.Value:yyyyMMdd}{SegmentTerminator}");
        }

        // EQ - Eligibility or Benefit Inquiry
        var serviceTypeCode = string.IsNullOrEmpty(request.ServiceTypeCode) ? "30" : request.ServiceTypeCode;
        sb.AppendLine($"EQ{ElementSeparator}{serviceTypeCode}{SegmentTerminator}");

        // Count segments for SE
        var segments = sb.ToString().Split(new[] { SegmentTerminator }, StringSplitOptions.RemoveEmptyEntries);
        var segmentCount = segments.Count(s => !s.StartsWith("ISA") && !s.StartsWith("GS")) + 1; // +1 for SE itself

        // SE - Transaction Set Trailer
        sb.AppendLine($"SE{ElementSeparator}{segmentCount}{ElementSeparator}{transactionControlNumber}{SegmentTerminator}");

        // GE - Functional Group Trailer
        sb.AppendLine($"GE{ElementSeparator}1{ElementSeparator}{groupControlNumber}{SegmentTerminator}");

        // IEA - Interchange Control Trailer
        sb.AppendLine($"IEA{ElementSeparator}1{ElementSeparator}{interchangeControlNumber}{SegmentTerminator}");

        var edi270Content = sb.ToString();

        // Update the request record
        request.Raw270Data = edi270Content;
        request.RequestStatusCode = "QUEUED";
        request.LastSubmissionDate = DateTime.Now;
        request.SubmissionAttemptCount++;
        request.UpdatedAt = DateTime.Now;

        await _context.SaveChangesAsync();

        return edi270Content;
    }

    /// <summary>
    /// Generates 270 files for all requests with a specific status
    /// </summary>
    public async Task<Dictionary<long, string>> Generate270FilesForStatusAsync(string statusCode, string senderId, string receiverId)
    {
        var requests = await _context.EligibilityRequests
            .Where(r => r.RequestStatusCode == statusCode)
            .ToListAsync();

        var results = new Dictionary<long, string>();

        foreach (var request in requests)
        {
            try
            {
                var ediContent = await Generate270FileAsync(request.RequestId, senderId, receiverId);
                results[request.RequestId] = ediContent;
            }
            catch (Exception ex)
            {
                // Log error
                var errorLog = new TransactionErrorLog
                {
                    RequestId = request.RequestId,
                    ErrorType = "GENERATION",
                    ErrorCode = "GEN_ERROR",
                    ErrorMessage = ex.Message,
                    ErrorDetails = ex.StackTrace,
                    ErrorDate = DateTime.Now
                };
                _context.TransactionErrorLogs.Add(errorLog);
            }
        }

        await _context.SaveChangesAsync();
        return results;
    }

    /// <summary>
    /// Saves a 270 file to disk
    /// </summary>
    public async Task SaveToFileAsync(long requestId, string senderId, string receiverId, string filePath)
    {
        var content = await Generate270FileAsync(requestId, senderId, receiverId);
        await File.WriteAllTextAsync(filePath, content);
    }
}
