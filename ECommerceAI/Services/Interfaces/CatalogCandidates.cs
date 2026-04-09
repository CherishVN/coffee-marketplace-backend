namespace ECommerceAI.Services.Interfaces;

/// <summary>Lightweight rows for building seller AI prompts (no DB embedding columns).</summary>
public record CategoryCandidate(long Id, string Name, short Level, long? ParentId);
public record TagCandidate(long Id, string Name);
public record MaterialCandidate(Guid Id, string Name);
