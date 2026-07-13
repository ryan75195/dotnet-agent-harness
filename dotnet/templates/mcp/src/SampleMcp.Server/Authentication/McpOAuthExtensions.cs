using Microsoft.AspNetCore.Authentication.JwtBearer;
using ModelContextProtocol.AspNetCore.Authentication;
using ModelContextProtocol.Authentication;

namespace SampleMcp.Server.Authentication;

public static class McpOAuthExtensions
{
    public static IServiceCollection AddMcpOAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection("McpAuth");
        var authority = section["Authority"];
        if (string.IsNullOrWhiteSpace(authority))
        {
            return services;
        }

        var audience = section["Audience"];
        var resource = section["Resource"] ?? audience ?? authority;

        services
            .AddAuthentication(options =>
            {
                options.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.Authority = authority;
                options.Audience = audience;
            })
            .AddMcp(options =>
            {
                options.ResourceMetadata = new ProtectedResourceMetadata
                {
                    Resource = resource,
                    AuthorizationServers = { authority },
                };
            });

        services.AddAuthorization();
        return services;
    }

    public static bool IsMcpOAuthEnabled(this IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration["McpAuth:Authority"]);
}
