using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.AspNetCore.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
using Microsoft.InformationProtection;
using Microsoft.InformationProtection.File;
using Microsoft.OpenApi.Models;
using MipsTestApp.Configuration;
using MipsTestApp.Models;
using MipsTestApp.Models.Protection.File;
using MipsTestApp.Services.Protection;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace MipsTestApp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            var configuration = builder.Configuration;
            builder.Services.AddSingleton(configuration);
            builder.Services.AddApplicationInsightsTelemetry(configuration); // Add Telemetry logging (Application Insights)
          
            builder.Services.AddOptions(); // Allow .Configure to be called..
            // Add services to the container.
            
            builder.Services.AddControllers(conf =>
                {
                    conf.ModelMetadataDetailsProviders.Add(new JsonPropertyDisplayMetadataProvider());
                })
            .AddNewtonsoftJson(options =>
                 {
                     options.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
                     // to make Enums in swagger appear as their string values, not as their int values
                     options.SerializerSettings.Converters.Add(new StringEnumConverter());
                 });
            builder.Services.AddSwaggerGenNewtonsoftSupport();


            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddEndpointsApiExplorer();

            builder.Services.AddSwaggerGen(
                options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "MipsTestApp API",
                    Version = "v1",
                    Description = "API documentation for MipsTestApp"
                });
                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer", // This is crucial
                    BearerFormat = "JWT"
                });
                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });
            }
            );

            builder.Services.Configure<ApplicationInsightsServiceOptions>(
                   options =>
                   {
                       options.EnableAdaptiveSampling = false;
                   });

            builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            builder.Services.AddSingleton<IActionContextAccessor, ActionContextAccessor>();

            // Business Services
            
            builder.Services.AddSingleton<ApplicationInfo>(x =>
            {
                AzureAdOptions AdOptions = x.GetRequiredService<IOptions<AzureAdOptions>>().Value;
                ApplicationInfo appInfo = new()
                {
                    ApplicationId = AdOptions.ClientId,
                    ApplicationName = "Mips Pdf Performance Tester",
                    ApplicationVersion = "1.0.0"
                };
                return appInfo;
            });


            MIP.Initialize(MipComponent.File);
            builder.Services.AddSingleton<MipContext>(x =>
            {
                TelemetryClient logger = x.GetRequiredService<TelemetryClient>();
                ApplicationInfo appInfo = x.GetRequiredService<ApplicationInfo>();
                logger.TrackTrace($"Add Singleton CreateMipContext", Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Verbose);

                MipConfiguration mipConfiguration = new(appInfo, "mip_data", Microsoft.InformationProtection.LogLevel.Trace, false, CacheStorageType.InMemory)
                {
                    LoggerConfigurationOverride = new LoggerConfiguration(4, 60, false)
                };

                //This does not perform better , leaving in just as an example of how to set FlightingFeature flags
                //mipConfiguration.FeatureSettingsOverride ??= [];
                //mipConfiguration.FeatureSettingsOverride[FlightingFeature.OptimizePdfMemory] = false; // default is true

                return MIP.CreateMipContext(mipConfiguration);
            });

            builder.Services.AddSingleton<IFileProfile>(x =>
            {
                TelemetryClient logger = x.GetRequiredService<TelemetryClient>();
                logger.TrackTrace($"Add Singleton IFileProfile", Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Verbose);

                MipContext mipContext = x.GetRequiredService<MipContext>();
                FileProfileSettings profileSettings = new(mipContext, CacheStorageType.OnDisk, new ConsentDelegateImplementation());
                return Task.Run(async () => await MIP.LoadFileProfileAsync(profileSettings)).Result;
                ;
            });

            // In the future add a factory class that can be DI'd into the controller and the method can call factory.createFileEngine if needed
            // This would allow for better error handling/retries if needed on failure to initialize the FileEngine
            // As well it would allow for parameter validation / and need to be determined before trying to create the FileEngine
            //  This is a TEST app so keeping it simple for now
            builder.Services.AddScoped<AuthDelegateUserImplementation>(x =>
            {
                IHttpContextAccessor httpContextAccessor = x.GetService<IHttpContextAccessor>();
                ApplicationInfo appInfo = x.GetRequiredService<ApplicationInfo>();
                TelemetryClient logger = x.GetRequiredService<TelemetryClient>();
                AzureAdOptions AdOptions = x.GetRequiredService<IOptions<AzureAdOptions>>().Value;
                //string secret = configuration.GetValue<string>("MICROSOFT_PROVIDER_AUTHENTICATION_SECRET");  //Deployed version & Local version is in "secrets"
                return new(appInfo, httpContextAccessor, AdOptions.ClientSecret, AdOptions.TenantId, logger);
            });

            builder.Services.AddScoped<IFileEngine>(x => //IDisposable
            {
                IHttpContextAccessor httpContextAccessor = x.GetService<IHttpContextAccessor>();
                if (httpContextAccessor?.HttpContext?.User is null)
                {
                    return new ThrowIfCalledFileEngine();
                }

                IFileProfile fileProfile = x.GetRequiredService<IFileProfile>();

                // Ensure the name is unique and set
                string email = httpContextAccessor.HttpContext?.User?.Identity?.Name;
                string engineName = $"{email}_{Guid.NewGuid().ToString("N")}";
                Identity id = new(email, engineName);

                TelemetryClient logger = x.GetRequiredService<TelemetryClient>();
                logger.TrackTrace($"Add Scoped IFileEngine: {engineName} ", Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Verbose);

                AuthDelegateUserImplementation AuthDelegate = x.GetRequiredService<AuthDelegateUserImplementation>();
                FileEngineSettings engineSettings = new(engineName, AuthDelegate, string.Empty, "en-US")
                {
                    Identity = id,
                    DelegatedUserEmail = email,
                    LoggerContext = logger
                };

                IFileEngine fileEngine = Task.Run(() => fileProfile.AddEngineAsync(engineSettings)).Result;

                return new FileEngineWithDelegate(fileProfile, fileEngine, AuthDelegate, id, logger);
            });

            builder.Services.AddScoped<MipsWorker>();

            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddMicrosoftIdentityWebApi(configuration);


            builder.Services.Configure<FormOptions>(options =>
            {
                options.MultipartBodyLengthLimit = 100 * 1024 * 1024; // 100 MB
            });
            builder.WebHost.ConfigureKestrel(serverOptions =>
            {
                serverOptions.Limits.MaxRequestBodySize = 100 * 1024 * 1024; // 100 MB
            });
            var app = builder.Build();

            // Configure the HTTP request pipeline.
            //if (app.Environment.IsDevelopment() || app.Environment.IsStaging() || app.Environment.IsProduction())
            //{
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.DisplayRequestDuration();
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "MipsTestApp API V1");
                options.RoutePrefix = "swagger"; // Swagger UI at root
            });
            //  }

            app.UseHttpsRedirection();
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII = true;

            app.Run();
        }
    }
}