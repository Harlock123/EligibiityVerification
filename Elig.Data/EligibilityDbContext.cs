using Elig.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Elig.Data;

public class EligibilityDbContext : DbContext
{
    public EligibilityDbContext(DbContextOptions<EligibilityDbContext> options)
        : base(options)
    {
    }

    // Lookup/Reference Tables
    public DbSet<RequestStatus> RequestStatuses { get; set; }
    public DbSet<ResponseStatus> ResponseStatuses { get; set; }
    public DbSet<EligibilityDisposition> EligibilityDispositions { get; set; }
    public DbSet<BenefitType> BenefitTypes { get; set; }

    // Core Entity Tables
    public DbSet<Payer> Payers { get; set; }
    public DbSet<Provider> Providers { get; set; }
    public DbSet<Member> Members { get; set; }

    // Transaction Tables
    public DbSet<EligibilityRequest> EligibilityRequests { get; set; }
    public DbSet<EligibilityResponse> EligibilityResponses { get; set; }
    public DbSet<BenefitDetail> BenefitDetails { get; set; }

    // Audit/Tracking Tables
    public DbSet<RequestStatusHistory> RequestStatusHistories { get; set; }
    public DbSet<ResponseStatusHistory> ResponseStatusHistories { get; set; }
    public DbSet<TransactionErrorLog> TransactionErrorLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure RequestStatus
        modelBuilder.Entity<RequestStatus>(entity =>
        {
            entity.ToTable("request_status");
            entity.HasKey(e => e.StatusCode);
            entity.Property(e => e.StatusCode).HasColumnName("status_code").HasMaxLength(20);
            entity.Property(e => e.StatusDescription).HasColumnName("status_description").HasMaxLength(100);
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("GETDATE()");
        });

        // Configure ResponseStatus
        modelBuilder.Entity<ResponseStatus>(entity =>
        {
            entity.ToTable("response_status");
            entity.HasKey(e => e.StatusCode);
            entity.Property(e => e.StatusCode).HasColumnName("status_code").HasMaxLength(20);
            entity.Property(e => e.StatusDescription).HasColumnName("status_description").HasMaxLength(100);
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("GETDATE()");
        });

        // Configure EligibilityDisposition
        modelBuilder.Entity<EligibilityDisposition>(entity =>
        {
            entity.ToTable("eligibility_disposition");
            entity.HasKey(e => e.DispositionCode);
            entity.Property(e => e.DispositionCode).HasColumnName("disposition_code").HasMaxLength(20);
            entity.Property(e => e.DispositionDescription).HasColumnName("disposition_description").HasMaxLength(100);
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("GETDATE()");
        });

        // Configure BenefitType
        modelBuilder.Entity<BenefitType>(entity =>
        {
            entity.ToTable("benefit_type");
            entity.HasKey(e => e.BenefitCode);
            entity.Property(e => e.BenefitCode).HasColumnName("benefit_code").HasMaxLength(10);
            entity.Property(e => e.BenefitDescription).HasColumnName("benefit_description").HasMaxLength(200);
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("GETDATE()");
        });

        // Configure Payer
        modelBuilder.Entity<Payer>(entity =>
        {
            entity.ToTable("payer");
            entity.HasKey(e => e.PayerId);
            entity.Property(e => e.PayerId).HasColumnName("payer_id").ValueGeneratedOnAdd();
            entity.Property(e => e.PayerName).HasColumnName("payer_name").HasMaxLength(200).IsRequired();
            entity.Property(e => e.PayerIdentifier).HasColumnName("payer_identifier").HasMaxLength(50).IsRequired();
            entity.Property(e => e.PayerIdentifierType).HasColumnName("payer_identifier_type").HasMaxLength(10).IsRequired();
            entity.Property(e => e.PayerAddressLine1).HasColumnName("payer_address_line1").HasMaxLength(100);
            entity.Property(e => e.PayerAddressLine2).HasColumnName("payer_address_line2").HasMaxLength(100);
            entity.Property(e => e.PayerCity).HasColumnName("payer_city").HasMaxLength(50);
            entity.Property(e => e.PayerState).HasColumnName("payer_state").HasMaxLength(2);
            entity.Property(e => e.PayerZip).HasColumnName("payer_zip").HasMaxLength(10);
            entity.Property(e => e.PayerPhone).HasColumnName("payer_phone").HasMaxLength(20);
            entity.Property(e => e.PayerFax).HasColumnName("payer_fax").HasMaxLength(20);
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("GETDATE()");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("GETDATE()");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by").HasMaxLength(50);
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by").HasMaxLength(50);

            entity.HasIndex(e => new { e.PayerIdentifier, e.PayerIdentifierType }).IsUnique();
        });

        // Configure Provider
        modelBuilder.Entity<Provider>(entity =>
        {
            entity.ToTable("provider");
            entity.HasKey(e => e.ProviderId);
            entity.Property(e => e.ProviderId).HasColumnName("provider_id").ValueGeneratedOnAdd();
            entity.Property(e => e.ProviderNpi).HasColumnName("provider_npi").HasMaxLength(10).IsRequired();
            entity.Property(e => e.ProviderTaxId).HasColumnName("provider_tax_id").HasMaxLength(20);
            entity.Property(e => e.ProviderFirstName).HasColumnName("provider_first_name").HasMaxLength(50);
            entity.Property(e => e.ProviderLastName).HasColumnName("provider_last_name").HasMaxLength(50);
            entity.Property(e => e.ProviderOrganizationName).HasColumnName("provider_organization_name").HasMaxLength(200);
            entity.Property(e => e.ProviderType).HasColumnName("provider_type").HasMaxLength(20);
            entity.Property(e => e.ProviderSpecialty).HasColumnName("provider_specialty").HasMaxLength(100);
            entity.Property(e => e.ProviderAddressLine1).HasColumnName("provider_address_line1").HasMaxLength(100);
            entity.Property(e => e.ProviderAddressLine2).HasColumnName("provider_address_line2").HasMaxLength(100);
            entity.Property(e => e.ProviderCity).HasColumnName("provider_city").HasMaxLength(50);
            entity.Property(e => e.ProviderState).HasColumnName("provider_state").HasMaxLength(2);
            entity.Property(e => e.ProviderZip).HasColumnName("provider_zip").HasMaxLength(10);
            entity.Property(e => e.ProviderPhone).HasColumnName("provider_phone").HasMaxLength(20);
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("GETDATE()");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("GETDATE()");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by").HasMaxLength(50);
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by").HasMaxLength(50);

            entity.HasIndex(e => e.ProviderNpi).IsUnique();
        });

        // Configure Member
        modelBuilder.Entity<Member>(entity =>
        {
            entity.ToTable("member");
            entity.HasKey(e => e.MemberId);
            entity.Property(e => e.MemberId).HasColumnName("member_id").ValueGeneratedOnAdd();
            entity.Property(e => e.MemberFirstName).HasColumnName("member_first_name").HasMaxLength(50).IsRequired();
            entity.Property(e => e.MemberLastName).HasColumnName("member_last_name").HasMaxLength(50).IsRequired();
            entity.Property(e => e.MemberMiddleName).HasColumnName("member_middle_name").HasMaxLength(50);
            entity.Property(e => e.MemberDateOfBirth).HasColumnName("member_date_of_birth").HasColumnType("date").IsRequired();
            entity.Property(e => e.MemberGender).HasColumnName("member_gender").HasMaxLength(1);
            entity.Property(e => e.MemberSsn).HasColumnName("member_ssn").HasMaxLength(11);
            entity.Property(e => e.MemberSubscriberId).HasColumnName("member_subscriber_id").HasMaxLength(50);
            entity.Property(e => e.MemberGroupNumber).HasColumnName("member_group_number").HasMaxLength(50);
            entity.Property(e => e.MemberAddressLine1).HasColumnName("member_address_line1").HasMaxLength(100);
            entity.Property(e => e.MemberAddressLine2).HasColumnName("member_address_line2").HasMaxLength(100);
            entity.Property(e => e.MemberCity).HasColumnName("member_city").HasMaxLength(50);
            entity.Property(e => e.MemberState).HasColumnName("member_state").HasMaxLength(2);
            entity.Property(e => e.MemberZip).HasColumnName("member_zip").HasMaxLength(10);
            entity.Property(e => e.MemberPhone).HasColumnName("member_phone").HasMaxLength(20);
            entity.Property(e => e.MemberEmail).HasColumnName("member_email").HasMaxLength(100);
            entity.Property(e => e.IsSubscriber).HasColumnName("is_subscriber").HasDefaultValue(true);
            entity.Property(e => e.SubscriberMemberId).HasColumnName("subscriber_member_id");
            entity.Property(e => e.RelationshipToSubscriber).HasColumnName("relationship_to_subscriber").HasMaxLength(20);
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("GETDATE()");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("GETDATE()");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by").HasMaxLength(50);
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by").HasMaxLength(50);

            entity.HasOne(e => e.SubscriberMember)
                .WithMany(e => e.Dependents)
                .HasForeignKey(e => e.SubscriberMemberId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure EligibilityRequest
        modelBuilder.Entity<EligibilityRequest>(entity =>
        {
            entity.ToTable("eligibility_request");
            entity.HasKey(e => e.RequestId);
            entity.Property(e => e.RequestId).HasColumnName("request_id").ValueGeneratedOnAdd();
            entity.Property(e => e.TraceNumber).HasColumnName("trace_number").HasMaxLength(50).IsRequired();
            entity.Property(e => e.TransactionControlNumber).HasColumnName("transaction_control_number").HasMaxLength(9);
            entity.Property(e => e.RequestBatchId).HasColumnName("request_batch_id");
            entity.Property(e => e.MemberId).HasColumnName("member_id").IsRequired();
            entity.Property(e => e.ProviderId).HasColumnName("provider_id").IsRequired();
            entity.Property(e => e.PayerId).HasColumnName("payer_id").IsRequired();
            entity.Property(e => e.ServiceTypeCode).HasColumnName("service_type_code").HasMaxLength(10);
            entity.Property(e => e.ServiceDate).HasColumnName("service_date").HasColumnType("date");
            entity.Property(e => e.InquiryDate).HasColumnName("inquiry_date").HasDefaultValueSql("GETDATE()");
            entity.Property(e => e.RequestStatusCode).HasColumnName("request_status_code").HasMaxLength(20).HasDefaultValue("DRAFT");
            entity.Property(e => e.SubmissionAttemptCount).HasColumnName("submission_attempt_count").HasDefaultValue(0);
            entity.Property(e => e.LastSubmissionDate).HasColumnName("last_submission_date");
            entity.Property(e => e.AcknowledgementDate).HasColumnName("acknowledgement_date");
            entity.Property(e => e.Raw270Data).HasColumnName("raw_270_data");
            entity.Property(e => e.ErrorCode).HasColumnName("error_code").HasMaxLength(20);
            entity.Property(e => e.ErrorMessage).HasColumnName("error_message");
            entity.Property(e => e.ErrorDate).HasColumnName("error_date");
            entity.Property(e => e.ExpectedResponseTime).HasColumnName("expected_response_time").HasDefaultValue(30);
            entity.Property(e => e.TimeoutDate).HasColumnName("timeout_date");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("GETDATE()");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("GETDATE()");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by").HasMaxLength(50);
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by").HasMaxLength(50);

            entity.HasIndex(e => e.TraceNumber).IsUnique();
            entity.HasOne(e => e.Member).WithMany(e => e.EligibilityRequests).HasForeignKey(e => e.MemberId);
            entity.HasOne(e => e.Provider).WithMany(e => e.EligibilityRequests).HasForeignKey(e => e.ProviderId);
            entity.HasOne(e => e.Payer).WithMany(e => e.EligibilityRequests).HasForeignKey(e => e.PayerId);
            entity.HasOne(e => e.RequestStatus).WithMany(e => e.EligibilityRequests).HasForeignKey(e => e.RequestStatusCode);
        });

        // Configure EligibilityResponse
        modelBuilder.Entity<EligibilityResponse>(entity =>
        {
            entity.ToTable("eligibility_response");
            entity.HasKey(e => e.ResponseId);
            entity.Property(e => e.ResponseId).HasColumnName("response_id").ValueGeneratedOnAdd();
            entity.Property(e => e.RequestId).HasColumnName("request_id").IsRequired();
            entity.Property(e => e.TraceNumber).HasColumnName("trace_number").HasMaxLength(50).IsRequired();
            entity.Property(e => e.TransactionControlNumber).HasColumnName("transaction_control_number").HasMaxLength(9);
            entity.Property(e => e.ResponseBatchId).HasColumnName("response_batch_id");
            entity.Property(e => e.ResponseReceivedDate).HasColumnName("response_received_date").HasDefaultValueSql("GETDATE()");
            entity.Property(e => e.ResponseStatusCode).HasColumnName("response_status_code").HasMaxLength(20).HasDefaultValue("RECEIVED");
            entity.Property(e => e.EligibilityDispositionCode).HasColumnName("eligibility_disposition_code").HasMaxLength(20);
            entity.Property(e => e.CoverageStatus).HasColumnName("coverage_status").HasMaxLength(20);
            entity.Property(e => e.CoverageEffectiveDate).HasColumnName("coverage_effective_date").HasColumnType("date");
            entity.Property(e => e.CoverageTerminationDate).HasColumnName("coverage_termination_date").HasColumnType("date");
            entity.Property(e => e.PlanBeginDate).HasColumnName("plan_begin_date").HasColumnType("date");
            entity.Property(e => e.PlanEndDate).HasColumnName("plan_end_date").HasColumnType("date");
            entity.Property(e => e.PlanName).HasColumnName("plan_name").HasMaxLength(200);
            entity.Property(e => e.PlanNumber).HasColumnName("plan_number").HasMaxLength(50);
            entity.Property(e => e.GroupName).HasColumnName("group_name").HasMaxLength(200);
            entity.Property(e => e.GroupNumber).HasColumnName("group_number").HasMaxLength(50);
            entity.Property(e => e.CopayAmount).HasColumnName("copay_amount").HasColumnType("decimal(10,2)");
            entity.Property(e => e.CoinsurancePercentage).HasColumnName("coinsurance_percentage").HasColumnType("decimal(5,2)");
            entity.Property(e => e.DeductibleAmount).HasColumnName("deductible_amount").HasColumnType("decimal(10,2)");
            entity.Property(e => e.DeductibleRemaining).HasColumnName("deductible_remaining").HasColumnType("decimal(10,2)");
            entity.Property(e => e.OutOfPocketMax).HasColumnName("out_of_pocket_max").HasColumnType("decimal(10,2)");
            entity.Property(e => e.OutOfPocketRemaining).HasColumnName("out_of_pocket_remaining").HasColumnType("decimal(10,2)");
            entity.Property(e => e.RejectReasonCode).HasColumnName("reject_reason_code").HasMaxLength(10);
            entity.Property(e => e.RejectReasonDescription).HasColumnName("reject_reason_description");
            entity.Property(e => e.FollowUpActionCode).HasColumnName("follow_up_action_code").HasMaxLength(10);
            entity.Property(e => e.Raw271Data).HasColumnName("raw_271_data");
            entity.Property(e => e.ProcessedDate).HasColumnName("processed_date");
            entity.Property(e => e.ProcessingNotes).HasColumnName("processing_notes");
            entity.Property(e => e.ErrorCode).HasColumnName("error_code").HasMaxLength(20);
            entity.Property(e => e.ErrorMessage).HasColumnName("error_message");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("GETDATE()");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("GETDATE()");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by").HasMaxLength(50);
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by").HasMaxLength(50);

            entity.HasOne(e => e.Request).WithMany(e => e.Responses).HasForeignKey(e => e.RequestId);
            entity.HasOne(e => e.ResponseStatus).WithMany(e => e.EligibilityResponses).HasForeignKey(e => e.ResponseStatusCode);
            entity.HasOne(e => e.EligibilityDisposition).WithMany(e => e.EligibilityResponses).HasForeignKey(e => e.EligibilityDispositionCode);
        });

        // Configure BenefitDetail
        modelBuilder.Entity<BenefitDetail>(entity =>
        {
            entity.ToTable("benefit_detail");
            entity.HasKey(e => e.BenefitDetailId);
            entity.Property(e => e.BenefitDetailId).HasColumnName("benefit_detail_id").ValueGeneratedOnAdd();
            entity.Property(e => e.ResponseId).HasColumnName("response_id").IsRequired();
            entity.Property(e => e.BenefitTypeCode).HasColumnName("benefit_type_code").HasMaxLength(10).IsRequired();
            entity.Property(e => e.ServiceTypeCode).HasColumnName("service_type_code").HasMaxLength(10);
            entity.Property(e => e.CoverageLevelCode).HasColumnName("coverage_level_code").HasMaxLength(10);
            entity.Property(e => e.BenefitDescription).HasColumnName("benefit_description");
            entity.Property(e => e.InNetworkIndicator).HasColumnName("in_network_indicator");
            entity.Property(e => e.NetworkName).HasColumnName("network_name").HasMaxLength(100);
            entity.Property(e => e.BenefitAmount).HasColumnName("benefit_amount").HasColumnType("decimal(10,2)");
            entity.Property(e => e.BenefitPercentage).HasColumnName("benefit_percentage").HasColumnType("decimal(5,2)");
            entity.Property(e => e.BenefitQuantity).HasColumnName("benefit_quantity");
            entity.Property(e => e.BenefitQuantityQualifier).HasColumnName("benefit_quantity_qualifier").HasMaxLength(10);
            entity.Property(e => e.TimePeriodQualifier).HasColumnName("time_period_qualifier").HasMaxLength(10);
            entity.Property(e => e.AuthorizationRequired).HasColumnName("authorization_required");
            entity.Property(e => e.CertificationRequired).HasColumnName("certification_required");
            entity.Property(e => e.PriorAuthorizationNumber).HasColumnName("prior_authorization_number").HasMaxLength(50);
            entity.Property(e => e.BenefitBeginDate).HasColumnName("benefit_begin_date").HasColumnType("date");
            entity.Property(e => e.BenefitEndDate).HasColumnName("benefit_end_date").HasColumnType("date");
            entity.Property(e => e.BenefitNotes).HasColumnName("benefit_notes");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("GETDATE()");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("GETDATE()");

            entity.HasOne(e => e.Response).WithMany(e => e.BenefitDetails).HasForeignKey(e => e.ResponseId);
            entity.HasOne(e => e.BenefitType).WithMany(e => e.BenefitDetails).HasForeignKey(e => e.BenefitTypeCode);
        });

        // Configure RequestStatusHistory
        modelBuilder.Entity<RequestStatusHistory>(entity =>
        {
            entity.ToTable("request_status_history");
            entity.HasKey(e => e.HistoryId);
            entity.Property(e => e.HistoryId).HasColumnName("history_id").ValueGeneratedOnAdd();
            entity.Property(e => e.RequestId).HasColumnName("request_id").IsRequired();
            entity.Property(e => e.OldStatusCode).HasColumnName("old_status_code").HasMaxLength(20);
            entity.Property(e => e.NewStatusCode).HasColumnName("new_status_code").HasMaxLength(20).IsRequired();
            entity.Property(e => e.StatusChangeDate).HasColumnName("status_change_date").HasDefaultValueSql("GETDATE()");
            entity.Property(e => e.StatusChangeReason).HasColumnName("status_change_reason").HasMaxLength(500);
            entity.Property(e => e.ChangedBy).HasColumnName("changed_by").HasMaxLength(50);

            entity.HasOne(e => e.Request).WithMany(e => e.StatusHistory).HasForeignKey(e => e.RequestId);
            entity.HasOne(e => e.OldStatus).WithMany().HasForeignKey(e => e.OldStatusCode).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.NewStatus).WithMany().HasForeignKey(e => e.NewStatusCode).OnDelete(DeleteBehavior.Restrict);
        });

        // Configure ResponseStatusHistory
        modelBuilder.Entity<ResponseStatusHistory>(entity =>
        {
            entity.ToTable("response_status_history");
            entity.HasKey(e => e.HistoryId);
            entity.Property(e => e.HistoryId).HasColumnName("history_id").ValueGeneratedOnAdd();
            entity.Property(e => e.ResponseId).HasColumnName("response_id").IsRequired();
            entity.Property(e => e.OldStatusCode).HasColumnName("old_status_code").HasMaxLength(20);
            entity.Property(e => e.NewStatusCode).HasColumnName("new_status_code").HasMaxLength(20).IsRequired();
            entity.Property(e => e.StatusChangeDate).HasColumnName("status_change_date").HasDefaultValueSql("GETDATE()");
            entity.Property(e => e.StatusChangeReason).HasColumnName("status_change_reason").HasMaxLength(500);
            entity.Property(e => e.ChangedBy).HasColumnName("changed_by").HasMaxLength(50);

            entity.HasOne(e => e.Response).WithMany(e => e.StatusHistory).HasForeignKey(e => e.ResponseId);
            entity.HasOne(e => e.OldStatus).WithMany().HasForeignKey(e => e.OldStatusCode).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.NewStatus).WithMany().HasForeignKey(e => e.NewStatusCode).OnDelete(DeleteBehavior.Restrict);
        });

        // Configure TransactionErrorLog
        modelBuilder.Entity<TransactionErrorLog>(entity =>
        {
            entity.ToTable("transaction_error_log");
            entity.HasKey(e => e.ErrorLogId);
            entity.Property(e => e.ErrorLogId).HasColumnName("error_log_id").ValueGeneratedOnAdd();
            entity.Property(e => e.RequestId).HasColumnName("request_id");
            entity.Property(e => e.ResponseId).HasColumnName("response_id");
            entity.Property(e => e.ErrorType).HasColumnName("error_type").HasMaxLength(50).IsRequired();
            entity.Property(e => e.ErrorCode).HasColumnName("error_code").HasMaxLength(20);
            entity.Property(e => e.ErrorMessage).HasColumnName("error_message");
            entity.Property(e => e.ErrorDetails).HasColumnName("error_details");
            entity.Property(e => e.ErrorDate).HasColumnName("error_date").HasDefaultValueSql("GETDATE()");
            entity.Property(e => e.Resolved).HasColumnName("resolved").HasDefaultValue(false);
            entity.Property(e => e.ResolvedDate).HasColumnName("resolved_date");
            entity.Property(e => e.ResolvedBy).HasColumnName("resolved_by").HasMaxLength(50);
            entity.Property(e => e.ResolutionNotes).HasColumnName("resolution_notes");

            entity.HasOne(e => e.Request).WithMany(e => e.ErrorLogs).HasForeignKey(e => e.RequestId);
            entity.HasOne(e => e.Response).WithMany(e => e.ErrorLogs).HasForeignKey(e => e.ResponseId);
        });
    }
}
