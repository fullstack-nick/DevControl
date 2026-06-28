using System.Security.Claims;
using DevControl.Application.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace DevControl.Api.Security;

public static class DevControlSecurityExtensions
{
    public const string GoogleScheme = "Google";

    public static IServiceCollection AddDevControlSecurity(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<CurrentUserAccessor>();
        services.AddScoped<TenantAccessService>();
        services.AddScoped<AuditLogWriter>();
        services.AddSingleton<InvitationTokenService>();
        services.AddSingleton<RegistrationTokenService>();
        services.AddSingleton(TimeProvider.System);

        services.AddAntiforgery(options =>
        {
            options.Cookie.Name = "DevControl.Csrf";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = environment.IsDevelopment() || environment.IsEnvironment("Test")
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;
            options.HeaderName = "X-CSRF-TOKEN";
        });

        var authenticationBuilder = services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = "DevControl.Session";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = environment.IsDevelopment() || environment.IsEnvironment("Test")
                    ? CookieSecurePolicy.SameAsRequest
                    : CookieSecurePolicy.Always;
                options.LoginPath = "/auth/login";
                options.AccessDeniedPath = "/auth/denied";

                options.Events.OnRedirectToLogin = context =>
                {
                    if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return Task.CompletedTask;
                    }

                    context.Response.Redirect(context.RedirectUri);
                    return Task.CompletedTask;
                };

                options.Events.OnRedirectToAccessDenied = context =>
                {
                    if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return Task.CompletedTask;
                    }

                    context.Response.Redirect(context.RedirectUri);
                    return Task.CompletedTask;
                };
            });

        if (IsGoogleAuthConfigured(configuration))
        {
            authenticationBuilder.AddOpenIdConnect(GoogleScheme, options =>
            {
                options.Authority = "https://accounts.google.com";
                options.ClientId = configuration["AUTH_GOOGLE_CLIENT_ID"]!;
                options.ClientSecret = configuration["AUTH_GOOGLE_CLIENT_SECRET"]!;
                options.ResponseType = OpenIdConnectResponseType.Code;
                options.CallbackPath = "/signin-google";
                options.SaveTokens = false;
                options.GetClaimsFromUserInfoEndpoint = true;
                options.Scope.Clear();
                options.Scope.Add("openid");
                options.Scope.Add("email");
                options.Scope.Add("profile");
                options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.TokenValidationParameters.NameClaimType = "name";
                options.ClaimActions.MapUniqueJsonKey(ClaimTypes.Email, "email");
                options.ClaimActions.MapUniqueJsonKey(ClaimTypes.Name, "name");

                options.Events.OnTokenValidated = context =>
                {
                    if (context.Principal?.Identity is not ClaimsIdentity identity)
                    {
                        return Task.CompletedTask;
                    }

                    if (!identity.HasClaim(claim => claim.Type == DevControlClaimTypes.Provider))
                    {
                        identity.AddClaim(new Claim(DevControlClaimTypes.Provider, "google"));
                    }

                    var subject = identity.FindFirst("sub")?.Value
                        ?? identity.FindFirst(ClaimTypes.NameIdentifier)?.Value
                        ?? string.Empty;

                    if (!string.IsNullOrWhiteSpace(subject) &&
                        !identity.HasClaim(claim => claim.Type == DevControlClaimTypes.Subject))
                    {
                        identity.AddClaim(new Claim(DevControlClaimTypes.Subject, subject));
                    }

                    return Task.CompletedTask;
                };
            });
        }

        services.AddAuthorization();

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

        return services;
    }

    public static bool IsGoogleAuthConfigured(IConfiguration configuration)
    {
        return !string.IsNullOrWhiteSpace(configuration["AUTH_GOOGLE_CLIENT_ID"]) &&
            !string.IsNullOrWhiteSpace(configuration["AUTH_GOOGLE_CLIENT_SECRET"]);
    }
}
