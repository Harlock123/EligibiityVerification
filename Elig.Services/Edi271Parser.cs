using Elig.Data;
using Elig.Data.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using System.Linq;

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
    /// Parses a 271 EDI file and creates/updates database records. Supports multiple subscriber loops in one 271.
    /// </summary>
    public async Task<long> Parse271FileAsync(string ediContent, string? currentUser = null)
    {
        try
        {
            var segments = ParseSegments(ediContent);

            // Track responses and benefit details per subscriber trace
            var responses = new Dictionary<string, (EligibilityResponse response, List<BenefitDetail> benefits)>();

            EligibilityResponse? currentResponse = null;
            List<BenefitDetail>? currentBenefits = null;
            string? currentTrace = null;
            string? transactionControlNumber = null;

            void StartNewSubscriber()
            {
                currentResponse = null;
                currentBenefits = null;
                currentTrace = null;
            }

            for (int i = 0; i < segments.Count; i++)
            {
                var segment = segments[i];
                var elements = segment.Split(ElementSeparator);
                if (elements.Length == 0) 
                    continue;

                var segmentId = elements[0];

                switch (segmentId)
                {
                    case "ST":
                        if (elements.Length > 1)
                            transactionControlNumber = elements[1];
                        break;

                    case "HL":
                        // Start of a subscriber/dependent loop (2000C/2000D)
                        if (elements.Length > 3 && (elements[3] == "22" || elements[3] == "23"))
                        {
                            StartNewSubscriber();
                        }
                        break;

                    case "TRN":
                        // Trace number identifies the request/subscriber
                        if (elements.Length > 2)
                        {
                            currentTrace = elements[2];

                            if (!responses.TryGetValue(currentTrace, out var entry))
                            {
                                var resp = new EligibilityResponse
                                {
                                    TraceNumber = currentTrace,
                                    ResponseReceivedDate = DateTime.Now,
                                    ResponseStatusCode = "RECEIVED",
                                    Raw271Data = ediContent,
                                    CreatedBy = currentUser,
                                    UpdatedBy = currentUser,
                                    TransactionControlNumber = transactionControlNumber
                                };
                                entry = (resp, new List<BenefitDetail>());
                                responses[currentTrace] = entry;
                            }

                            currentResponse = entry.response;
                            currentBenefits = entry.benefits;
                        }
                        break;

                    case "BHT":
                        break;

                    case "EB":
                        if (currentResponse != null && currentBenefits != null)
                        {
                            // Set response-level coverage status and plan name from the first EB per subscriber
                            if (currentResponse.CoverageStatus is null)
                                ParseEBSegment(elements, currentResponse);

                            var benefit = BuildBenefitFromEBSegment(segments, ref i, currentResponse.ResponseId);
                            currentBenefits.Add(benefit);
                        }
                        break;

                    case "DTP":
                        if (currentResponse != null)
                        {
                            ParseDTPSegment(elements, currentResponse);
                        }
                        break;

                    case "AAA":
                        if (currentResponse != null)
                        {
                            ParseAAASegment(elements, currentResponse);
                        }
                        break;

                    case "NM1":
                        if (currentResponse != null)
                        {
                            ParseNM1Segment(elements, currentResponse);
                        }
                        break;
                }
            }

            if (!responses.Any())
                throw new InvalidOperationException("No subscriber trace numbers found in 271 response.");

            // Persist responses and update matching requests
            foreach (var kvp in responses)
            {
                var trace = kvp.Key;
                var (resp, _) = kvp.Value;

                var request = await _context.EligibilityRequests.FirstOrDefaultAsync(r => r.TraceNumber == trace)
                    ?? throw new InvalidOperationException($"No matching request found for trace number: {trace}");

                resp.RequestId = request.RequestId;
                resp.ResponseStatusCode = "PROCESSED";
                resp.ProcessedDate = DateTime.Now;

                request.RequestStatusCode = "RESPONSE_RECEIVED";
                request.UpdatedAt = DateTime.Now;
                request.UpdatedBy = currentUser;

                _context.EligibilityResponses.Add(resp);
            }

            await _context.SaveChangesAsync();

            // Persist benefit details now that response IDs are available
            foreach (var entry in responses.Values)
            {
                var respId = entry.response.ResponseId;
                foreach (var benefit in entry.benefits)
                {
                    benefit.ResponseId = respId;
                }

                if (entry.benefits.Any())
                {
                    _context.BenefitDetails.AddRange(entry.benefits);
                }
            }

            await _context.SaveChangesAsync();

            // Return the first response id for backward compatibility
            return responses.Values.First().response.ResponseId;
        }
        catch (Exception ex)
        {
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

    private BenefitDetail BuildBenefitFromEBSegment(List<string> segments, ref int index, long responseId)
    {
        var segment = segments[index];
        var elements = segment.Split(ElementSeparator);

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

        // Look ahead for related segments until next EB/HL/ST/SE
        for (int j = index + 1; j < segments.Count; j++)
        {
            var nextSegment = segments[j];
            if (nextSegment.StartsWith("EB") || nextSegment.StartsWith("HL") || nextSegment.StartsWith("SE") || nextSegment.StartsWith("ST"))
            {
                index = j - 1; // outer loop will ++ to next segment
                break;
            }

            var nextElements = nextSegment.Split(ElementSeparator);
            if (nextElements.Length == 0) continue;

            if (nextElements[0] == "REF" && nextElements.Length > 2)
            {
                if (nextElements[1] == "1J")
                {
                    benefit.PriorAuthorizationNumber = nextElements[2];
                }
            }
            else if (nextElements[0] == "DTP")
            {
                ParseBenefitDTP(nextElements, benefit);
            }

            index = j; // advance with inner processing
        }

        return benefit;
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
