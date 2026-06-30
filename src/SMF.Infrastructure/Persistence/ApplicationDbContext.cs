using Microsoft.EntityFrameworkCore;
using SMF.Application.Common.Interfaces;
using SMF.Domain.Entities;
using SMF.Infrastructure.Persistence.Entities;

namespace SMF.Infrastructure.Persistence;

public sealed class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Member> Members => Set<Member>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<StrikeEvent> StrikeEvents => Set<StrikeEvent>();
    public DbSet<ScoreOverrideEvent> ScoreOverrideEvents => Set<ScoreOverrideEvent>();
    public DbSet<RoundTimerEvent> RoundTimerEvents => Set<RoundTimerEvent>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<Club> Clubs => Set<Club>();
    public DbSet<FederationEvent> Events => Set<FederationEvent>();
    public DbSet<EventRegistration> EventRegistrations => Set<EventRegistration>();
    public DbSet<Tournament> Tournaments => Set<Tournament>();
    public DbSet<BracketMatch> BracketMatches => Set<BracketMatch>();
    public DbSet<Certificate> Certificates => Set<Certificate>();
    public DbSet<NewsArticle> NewsArticles => Set<NewsArticle>();
    public DbSet<SafeguardingReport> SafeguardingReports => Set<SafeguardingReport>();

    // Advanced features (PDF §9): e-learning + multi-tenant scoring.
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Lesson> Lessons => Set<Lesson>();
    public DbSet<CourseEnrollment> CourseEnrollments => Set<CourseEnrollment>();
    public DbSet<LessonCompletion> LessonCompletions => Set<LessonCompletion>();
    public DbSet<ScoringTenant> ScoringTenants => Set<ScoringTenant>();

    // Financial system + e-commerce store.
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Invoice> Invoices => Set<Invoice>();

    // Communication & engagement.
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<DeviceRegistration> DeviceRegistrations => Set<DeviceRegistration>();
    public DbSet<BroadcastCampaign> BroadcastCampaigns => Set<BroadcastCampaign>();
    public DbSet<Feedback> Feedbacks => Set<Feedback>();
    public DbSet<SocialHighlight> SocialHighlights => Set<SocialHighlight>();
    public DbSet<ChatConversation> ChatConversations => Set<ChatConversation>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    // Compliance & governance.
    public DbSet<GovernanceDocument> GovernanceDocuments => Set<GovernanceDocument>();
    public DbSet<PolicyDocument> PolicyDocuments => Set<PolicyDocument>();
    public DbSet<PolicyAcceptance> PolicyAcceptances => Set<PolicyAcceptance>();
    public DbSet<ParentalConsent> ParentalConsents => Set<ParentalConsent>();
    public DbSet<ConsentLog> ConsentLogs => Set<ConsentLog>();

    internal DbSet<MemberSequence> MemberSequences => Set<MemberSequence>();
    internal DbSet<BillingSequence> BillingSequences => Set<BillingSequence>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    // Explicit interface mapping — IApplicationDbContext.SaveChangesAsync
    // forwards to the base DbContext implementation without exposing the
    // full surface to the Application project.
    Task<int> IApplicationDbContext.SaveChangesAsync(CancellationToken cancellationToken)
        => base.SaveChangesAsync(cancellationToken);
}
