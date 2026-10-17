namespace Toushir.Wholesale;

public record AssignPortfolioInput(string AdvisorId, int Version, string Reason);
public record CreateCrmTaskInput(string Kind, string Title, string Priority, DateTimeOffset DueAt, DateTimeOffset? VisitEnd);
public record RescheduleCrmTaskInput(int Version, string Title, string Priority, DateTimeOffset DueAt, DateTimeOffset? VisitEnd, string Reason);
public record FinishCrmTaskInput(int Version, string Outcome, string Note, string NextAction, DateTimeOffset ObservedAt, DateTimeOffset? FollowUpAt);
public record CancelCrmTaskInput(int Version, string Reason);
public record LogCrmContactInput(string Kind, string Outcome, string Note, string NextAction, DateTimeOffset ObservedAt, DateTimeOffset? FollowUpAt);
public record GrantCrmAssistanceInput(string AdvisorId, int PortfolioVersion, string AddressId, List<LineInput> Items, string? Note, DateTimeOffset ExpiresAt);
public record RevokeCrmAssistanceInput(int Version, string Reason);
public record UseCrmAssistanceInput(int Version, string IdempotencyKey);
