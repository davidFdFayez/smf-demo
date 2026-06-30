using Microsoft.EntityFrameworkCore;
using SMF.Domain.Entities;

namespace SMF.Application.Common.Interfaces;

/// <summary>
/// Lightweight context abstraction for feature handlers that don't warrant
/// a full per-aggregate repository (pure EF queries + simple adds). Repo
/// interfaces are still preferred for aggregates with non-trivial invariants
/// (<see cref="IMatchRepository"/>, <see cref="IMemberRepository"/>).
/// </summary>
public interface IApplicationDbContext
{
    DbSet<Member> Members { get; }
    DbSet<Match> Matches { get; }
    DbSet<Payment> Payments { get; }
    DbSet<Club> Clubs { get; }
    DbSet<FederationEvent> Events { get; }
    DbSet<EventRegistration> EventRegistrations { get; }
    DbSet<Tournament> Tournaments { get; }
    DbSet<BracketMatch> BracketMatches { get; }
    DbSet<Certificate> Certificates { get; }
    DbSet<NewsArticle> NewsArticles { get; }
    DbSet<SafeguardingReport> SafeguardingReports { get; }

    // Advanced features (PDF §9)
    DbSet<Course> Courses { get; }
    DbSet<Lesson> Lessons { get; }
    DbSet<CourseEnrollment> CourseEnrollments { get; }
    DbSet<LessonCompletion> LessonCompletions { get; }
    DbSet<ScoringTenant> ScoringTenants { get; }

    // Phase 3 scoring: timekeeper round clock audit log
    DbSet<RoundTimerEvent> RoundTimerEvents { get; }

    // Financial system + e-commerce
    DbSet<ProductCategory> ProductCategories { get; }
    DbSet<Product> Products { get; }
    DbSet<Cart> Carts { get; }
    DbSet<Order> Orders { get; }
    DbSet<Invoice> Invoices { get; }

    // Communication & engagement
    DbSet<Notification> Notifications { get; }
    DbSet<DeviceRegistration> DeviceRegistrations { get; }
    DbSet<BroadcastCampaign> BroadcastCampaigns { get; }
    DbSet<Feedback> Feedbacks { get; }
    DbSet<SocialHighlight> SocialHighlights { get; }
    DbSet<ChatConversation> ChatConversations { get; }
    DbSet<ChatMessage> ChatMessages { get; }

    // Compliance & governance
    DbSet<GovernanceDocument> GovernanceDocuments { get; }
    DbSet<PolicyDocument> PolicyDocuments { get; }
    DbSet<PolicyAcceptance> PolicyAcceptances { get; }
    DbSet<ParentalConsent> ParentalConsents { get; }
    DbSet<ConsentLog> ConsentLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
