namespace Toushir.Wholesale;

public record InviteShopMemberInput(string Email, string Role, DateTimeOffset ExpiresAt);
public record MemberInvitationTokenInput(string Token);
public record AcceptShopMemberInput(string Token, string Name, string Phone, string Password);
public record UpdateShopMemberInput(int Version, string Role, string Reason);
public record RevokeShopMemberInput(int Version, string Reason);
public record RevokeShopInvitationInput(int Version, string Reason);
