namespace DevControl.Application.Email;

public sealed record EmailMessage(
    string ToEmail,
    string Subject,
    string TextBody,
    string HtmlBody);
