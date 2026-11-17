using System.Security.Claims;

namespace TaskHub.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    // Reads "who is asking?" out of a ticket that has already passed the stamp check
    public static int? GetUserId(this ClaimsPrincipal principal)
    {
        string? value = principal.FindFirst("sub")?.Value;
        return int.TryParse(value, out int id) ? id : null;
    }
}