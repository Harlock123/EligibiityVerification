using System;
using System.Collections.Generic;

namespace Elig.Data.Models;

public class RequestBatch
{
    public long RequestBatchId { get; set; }
    public string? FileName { get; set; }
    public string SenderId { get; set; } = null!;
    public string ReceiverId { get; set; } = null!;
    public long PayerId { get; set; }
    public long ProviderId { get; set; }
    public string InterchangeControlNumber { get; set; } = null!;
    public string GroupControlNumber { get; set; } = null!;
    public string TransactionControlNumber { get; set; } = null!;
    public int RequestCount { get; set; }
    public string Status { get; set; } = "QUEUED";
    public string? Raw270Data { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? SubmittedAt { get; set; }

    public virtual Payer Payer { get; set; } = null!;
    public virtual Provider Provider { get; set; } = null!;
    public virtual ICollection<EligibilityRequest> Requests { get; set; } = new List<EligibilityRequest>();
}
