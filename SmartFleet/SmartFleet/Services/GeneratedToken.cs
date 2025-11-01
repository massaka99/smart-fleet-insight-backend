namespace SmartFleet.Services;

public readonly record struct GeneratedToken(string Token, DateTime ExpiresAt, Guid SessionId);

