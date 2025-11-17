using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
using MipsTestApp.Configuration;


namespace Microsoft.AspNetCore.Authentication
{
    public static class AzureAdServiceCollectionExtensions
    {
        public static AuthenticationBuilder AddMicrosoftIdentityWebApi(this AuthenticationBuilder builder, IConfiguration configuration)
        {
            builder.Services.Configure<AzureAdOptions>(options =>
            {
                configuration.Bind("AzureAd", options);
                options.TenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID")
                    ?? configuration.GetValue<string>("AZURE_TENANT_ID"); //Local version is in "secrets"

                options.ClientId = Environment.GetEnvironmentVariable("APPSETTING_WEBSITE_AUTH_CLIENT_ID")
                    ?? configuration.GetValue<string>("APPSETTING_WEBSITE_AUTH_CLIENT_ID"); //Local version is in "secrets"

                options.ClientSecret = Environment.GetEnvironmentVariable("MICROSOFT_PROVIDER_AUTHENTICATION_SECRET")
                    ?? configuration.GetValue<string>("MICROSOFT_PROVIDER_AUTHENTICATION_SECRET"); //Local version is in "secrets"

                options.AudienceIds = Environment.GetEnvironmentVariable("APPSETTING_WEBSITE_AUTH_ALLOWED_AUDIENCES")
                    ?? configuration.GetValue<string>("APPSETTING_WEBSITE_AUTH_ALLOWED_AUDIENCES"); //Local version is in "secrets"
            });
            builder.Services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureAzureOptions>();
            builder.Services.AddSingleton<IConfigureOptions<MicrosoftIdentityOptions>, ConfigureMIAzureOptions>();
            builder.Services.AddMicrosoftIdentityWebApiAuthentication(configuration);
            return builder;
        }

        private sealed class ConfigureMIAzureOptions(IOptions<AzureAdOptions> azureOptions) : IConfigureNamedOptions<MicrosoftIdentityOptions>
        {
            private readonly AzureAdOptions _azureOptions = azureOptions.Value;
            public void Configure(string name, MicrosoftIdentityOptions options)
            {
                try
                {
                    options.ClientId = _azureOptions.ClientId;
                    options.TenantId = _azureOptions.TenantId;
                    options.Authority = $"{_azureOptions.Instance}{_azureOptions.TenantId}";
                    options.TokenValidationParameters = new()
                    {
                        ValidAudiences = [.. _azureOptions.AudienceIds?.Split(',')],
                        AuthenticationType = "Bearer",
                        NameClaimType = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/upn",
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.Zero,
                        ValidIssuers =
                        [
                            $"https://login.microsoftonline.com/{_azureOptions.TenantId}/v2.0",
                            $"https://sts.windows.net/{_azureOptions.TenantId}/"
                        ]
                    };
                    options.SaveTokens = true;
                }
                catch { }
            }

            public void Configure(MicrosoftIdentityOptions options)
            {
                Configure(Options.DefaultName, options);
            }
        }
        private sealed class ConfigureAzureOptions(IOptions<AzureAdOptions> azureOptions) : IConfigureNamedOptions<JwtBearerOptions>
        {
            private readonly AzureAdOptions _azureOptions = azureOptions.Value;

            public void Configure(string name, JwtBearerOptions options)
            {
                try
                {
                    options.Audience = _azureOptions.ClientId;
                    options.Authority = $"{_azureOptions.Instance}{_azureOptions.TenantId}";
                    options.TokenValidationParameters = new()
                    {
                        ValidAudiences = [.. _azureOptions.AudienceIds?.Split(',')],
                        AuthenticationType = "Bearer",
                        NameClaimType = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/upn",
                        ClockSkew = TimeSpan.Zero,
                        ValidIssuers =
                        [
                            $"https://login.microsoftonline.com/{_azureOptions.TenantId}/v2.0",
                            $"https://sts.windows.net/{_azureOptions.TenantId}/"
                        ]
                    };
                    options.SaveToken = true;
                }
                catch { }
            }

            public void Configure(JwtBearerOptions options)
            {
                Configure(Options.DefaultName, options);
            }
        }

    }
}

