using Elig.Data;
using Elig.Data.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace Elig.Services;

/// <summary>
/// Parses EDI X12 271 Eligibility Response files and updates the database
/// </summary>
public class Edi271Parser
{
    private readonly EligibilityDbContext _context;
    private const char ElementSeparator = '*';
    private const char SegmentTerminator = '~';
    private const char SubelementSeparator = ':';

    public Edi271Parser(EligibilityDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Parses a 271 EDI file and creates/updates database records
    /// </summary>
    public async Task<long> Parse271FileAsync(string ediContent, string? currentUser = null)
    {
        try
        {
            var segments = ParseSegments(ediContent);
            var response = new EligibilityResponse
            {
                ResponseReceivedDate = DateTime.Now,
                ResponseStatusCode = "RECEIVED",
                Raw271Data = ediContent,
                CreatedBy = currentUser,
                UpdatedBy = currentUser
            };

            string? traceNumber = null;
            string? transactionControlNumber = null;
            long? requestId = null;

            // Parse main segments
            foreach (var segment in segments)
            {
                var elements = segment.Split(ElementSeparator);
                var segmentId = elements[0];

                switch (segmentId)
                {
                    case "ST":
                        // Transaction Set Header
                        if (elements.Length > 1)
                            transactionControlNumber = elements[1];
                        break;

                    case "BHT":
                        // Beginning of Hierarchical Transaction - contains trace number
                        if (elements.Length > 3)
                            traceNumber = elements[3];
                        break;

                    case "TRN":
                        // Trace - another place to find trace number
                        if (elements.Length > 2 && string.IsNullOrEmpty(traceNumber))
                            traceNumber = elements[2];
                        break;

                    case "EB":
                        // Eligibility or Benefit Information
                        ParseEBSegment(elements, response);
                        break;

                    case "DTP":
                        // Date or Time or Period
                        ParseDTPSegment(elements, response);
                        break;

                    case "AAA":
                        // Request Validation - error/rejection info
                        ParseAAASegment(elements, response);
                        break;

                    case "NM1":
                        // Name - can contain plan information
                        ParseNM1Segment(elements, response);
                        break;

                    case "N3":
                    case "N4":
                    case "REF":
                    case "PER":
                        // Additional info - can be parsed if needed
                        break;
                }
            }

            // Find the matching request
            if (!string.IsNullOrEmpty(traceNumber))
            {
                var request = await _context.EligibilityRequests
                    .FirstOrDefaultAsync(r => r.TraceNumber == traceNumber);

                if (request != null)
                {
                    requestId = request.RequestId;
                    response.RequestId = requestId.Value;
                    response.TraceNumber = traceNumber;

                    // Update request status
                    request.RequestStatusCode = "RESPONSE_RECEIVED";
                    request.UpdatedAt = DateTime.Now;
                    request.UpdatedBy = currentUser;
                }
                else
                {
                    throw new InvalidOperationException($"No matching request found for trace number: {traceNumber}");
                }
            }
            else
            {
                throw new InvalidOperationException("No trace number found in 271 response");
            }

            response.TransactionControlNumber = transactionControlNumber;
            response.ProcessedDate = DateTime.Now;
            response.ResponseStatusCode = "PROCESSED";

            _context.EligibilityResponses.Add(response);
            await _context.SaveChangesAsync();

            // Parse and save benefit details
            await ParseBenefitDetailsAsync(segments, response.ResponseId, currentUser);

            return response.ResponseId;
        }
        catch (Exception ex)
        {
            // Log the error
            var errorLog = new TransactionErrorLog
            {
                ErrorType = "PARSING",
                ErrorCode = "PARSE_ERROR",
                ErrorMessage = ex.Message,
                ErrorDetails = ex.StackTrace,
                ErrorDate = DateTime.Now
            };
            _context.TransactionErrorLogs.Add(errorLog);
            await _context.SaveChangesAsync();

            throw;
        }
    }

    /// <summary>
    /// Parses a 271 file from disk
    /// </summary>
    public async Task<long> Parse271FileFromPathAsync(string filePath, string? currentUser = null)
    {
        var content = await File.ReadAllTextAsync(filePath);
        return await Parse271FileAsync(content, currentUser);
    }

    private List<string> ParseSegments(string ediContent)
    {
        // Remove newlines and split by segment terminator
        var normalized = ediContent.Replace("\r", "").Replace("\n", "");
        var segments = normalized.Split(SegmentTerminator, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        return segments;
    }

    private void ParseEBSegment(string[] elements, EligibilityResponse response)
    {
        // EB segment contains eligibility/benefit information
        // EB*1*FAM*30**GOLD PLAN*27~
        // EB01 - Eligibility or Benefit Information Code
        // EB02 - Coverage Level Code
        // EB03 - Service Type Code
        // EB04 - Insurance Type Code
        // EB05 - Plan Coverage Description

        if (elements.Length > 1)
        {
            var eligCode = elements[1];
            // Map eligibility codes to disposition
            response.EligibilityDispositionCode = MapEligibilityCode(eligCode);
        }

        if (elements.Length > 2)
        {
            // Coverage level
            var coverageLevel = elements[2];
        }

        if (elements.Length > 5)
        {
            response.PlanName = elements[5];
        }

        // Set coverage status based on eligibility code
        if (elements.Length > 1 && elements[1] == "1")
        {
            response.CoverageStatus = "ACTIVE";
        }
    }

    private void ParseDTPSegment(string[] elements, EligibilityResponse response)
    {
        // DTP segment contains dates
        // DTP*291*D8*20240101~
        // DTP01 - Date/Time Qualifier
        // DTP02 - Date Time Period Format Qualifier
        // DTP03 - Date Time Period

        if (elements.Length < 4) return;

        var qualifier = elements[1];
        var dateValue = elements[3];

        DateTime? parsedDate = ParseDate(dateValue);

        switch (qualifier)
        {
            case "291": // Plan Begin
                response.PlanBeginDate = parsedDate;
                break;
            case "292": // Plan End/Benefit End
                response.PlanEndDate = parsedDate;
                break;
            case "346": // Plan Coverage Effective
                response.CoverageEffectiveDate = parsedDate;
                break;
            case "347": // Plan Coverage Termination
                response.CoverageTerminationDate = parsedDate;
                break;
        }
    }

    private void ParseAAASegment(string[] elements, EligibilityResponse response)
    {
        // AAA segment contains error/rejection information
        // AAA*Y*42*79~
        // AAA01 - Valid Request Indicator
        // AAA02 - Agency Qualifier Code
        // AAA03 - Reject Reason Code

        if (elements.Length > 1 && elements[1] == "N")
        {
            response.ResponseStatusCode = "REJECTED";
        }

        if (elements.Length > 3)
        {
            response.RejectReasonCode = elements[3];
        }

        if (elements.Length > 4)
        {
            response.RejectReasonDescription = elements[4];
        }
    }

    private void ParseNM1Segment(string[] elements, EligibilityResponse response)
    {
        // NM1 segment contains name information
        // Can contain plan sponsor, payer, etc.
        if (elements.Length < 2) return;

        var entityIdentifier = elements[1];

        // IN - Insurer/Payer
        // P5 - Plan Sponsor
        if (entityIdentifier == "P5" && elements.Length > 3)
        {
            response.GroupName = elements[3];
        }
    }

    private async Task ParseBenefitDetailsAsync(List<string> segments, long responseId, string? currentUser)
    {
        var benefits = new List<BenefitDetail>();

        for (int i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            var elements = segment.Split(ElementSeparator);

            if (elements[0] == "EB")
            {
                var benefit = new BenefitDetail
                {
                    ResponseId = responseId,
                    BenefitTypeCode = "30" // Default - Health Benefit Plan Coverage
                };

                // EB*1*FAM*30**GOLD PLAN*27*50*****Y~
                if (elements.Length > 1)
                {
                    // Eligibility or Benefit Information
                    var infoCode = elements[1];
                    benefit.AuthorizationRequired = infoCode == "6" || infoCode == "C";
                }

                if (elements.Length > 2)
                {
                    benefit.CoverageLevelCode = elements[2]; // IND, FAM, etc.
                }

                if (elements.Length > 3)
                {
                    benefit.ServiceTypeCode = elements[3];
                    if (!string.IsNullOrEmpty(elements[3]))
                        benefit.BenefitTypeCode = elements[3];
                }

                if (elements.Length > 5)
                {
                    benefit.BenefitDescription = elements[5];
                }

                if (elements.Length > 6)
                {
                    // Time Period Qualifier
                    benefit.TimePeriodQualifier = elements[6];
                }

                if (elements.Length > 7 && decimal.TryParse(elements[7], out decimal amount))
                {
                    benefit.BenefitAmount = amount;
                }

                if (elements.Length > 8 && decimal.TryParse(elements[8], out decimal percentage))
                {
                    benefit.BenefitPercentage = percentage;
                }

                if (elements.Length > 12)
                {
                    benefit.InNetworkIndicator = elements[12] == "Y";
                }

                // Look ahead for REF segments that might contain additional benefit info
                for (int j = i + 1; j < segments.Count && j < i + 5; j++)
                {
                    var nextSegment = segments[j];
                    if (nextSegment.StartsWith("EB")) break; // Next benefit

                    var nextElements = nextSegment.Split(ElementSeparator);
                    if (nextElements[0] == "REF" && nextElements.Length > 2)
                    {
                        if (nextElements[1] == "1J") // Prior Authorization Number
                        {
                            benefit.PriorAuthorizationNumber = nextElements[2];
                        }
                    }
                    else if (nextElements[0] == "DTP")
                    {
                        ParseBenefitDTP(nextElements, benefit);
                    }
                }

                benefits.Add(benefit);
            }
        }

        if (benefits.Any())
        {
            _context.BenefitDetails.AddRange(benefits);
            await _context.SaveChangesAsync();
        }
    }

    private void ParseBenefitDTP(string[] elements, BenefitDetail benefit)
    {
        if (elements.Length < 4) return;

        var qualifier = elements[1];
        var dateValue = elements[3];
        DateTime? parsedDate = ParseDate(dateValue);

        switch (qualifier)
        {
            case "291":
                benefit.BenefitBeginDate = parsedDate;
                break;
            case "292":
            case "347":
                benefit.BenefitEndDate = parsedDate;
                break;
        }
    }

    private DateTime? ParseDate(string dateString)
    {
        if (string.IsNullOrEmpty(dateString)) return null;

        // Handle CCYYMMDD format
        if (dateString.Length == 8 &&
            int.TryParse(dateString.Substring(0, 4), out int year) &&
            int.TryParse(dateString.Substring(4, 2), out int month) &&
            int.TryParse(dateString.Substring(6, 2), out int day))
        {
            try
            {
                return new DateTime(year, month, day);
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    private string MapEligibilityCode(string code)
    {
        return code switch
        {
            "1" => "ACTIVE", // Active Coverage
            "2" => "ACTIVE", // Active - Full Risk Capitation
            "3" => "ACTIVE", // Active - Services Capitated
            "4" => "ACTIVE", // Active - Services Capitated to Primary Care Physician
            "5" => "ACTIVE", // Active - Pending Investigation
            "6" => "ACTIVE", // Inactive
            "7" => "INACTIVE", // Inactive - Pending Eligibility Update
            "8" => "INACTIVE", // Inactive - Pending Investigation
            "C" => "PENDING", // Information Not Available
            "D" => "INELIGIBLE", // Deductible
            "E" => "ELIGIBLE", // Eligible
            "I" => "INELIGIBLE", // Inactive
            "T" => "TERMINATED", // Terminated
            "U" => "UNKNOWN", // Unknown
            _ => "UNKNOWN"
        };
    }

    /// <summary>
    /// Updates specific financial fields on an existing response
    /// </summary>
    public async Task UpdateResponseFinancialsAsync(long responseId, decimal? copay = null,
        decimal? coinsurance = null, decimal? deductible = null, decimal? deductibleRemaining = null,
        decimal? oopMax = null, decimal? oopRemaining = null)
    {
        var response = await _context.EligibilityResponses.FindAsync(responseId);
        if (response == null)
            throw new ArgumentException($"Response with ID {responseId} not found");

        if (copay.HasValue) response.CopayAmount = copay.Value;
        if (coinsurance.HasValue) response.CoinsurancePercentage = coinsurance.Value;
        if (deductible.HasValue) response.DeductibleAmount = deductible.Value;
        if (deductibleRemaining.HasValue) response.DeductibleRemaining = deductibleRemaining.Value;
        if (oopMax.HasValue) response.OutOfPocketMax = oopMax.Value;
        if (oopRemaining.HasValue) response.OutOfPocketRemaining = oopRemaining.Value;

        response.UpdatedAt = DateTime.Now;
        await _context.SaveChangesAsync();
    }
}
