namespace SIMS.Application.DTOs.Auth;

public class MicrosoftLoginRequestDto
{
    /// <summary>The ID token returned by MSAL after a successful Microsoft sign-in.</summary>
    public string IdToken { get; set; } = string.Empty;
}
