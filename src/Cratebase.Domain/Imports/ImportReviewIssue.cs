namespace Cratebase.Domain.Imports;

public sealed record ImportReviewIssue(string Code, string Message, string Severity = "warning");
