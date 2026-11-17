using System.Net.Mail;

namespace TaskHub.Api.Validation;

// Checks the shape of text that users type in
public static class InputValidator
{
    public static bool IsValidEmail(string value) =>
        value.Length <= 254
        && MailAddress.TryCreate(value, out MailAddress? parsed)
        && parsed.Address == value;

    public static bool IsValidHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}