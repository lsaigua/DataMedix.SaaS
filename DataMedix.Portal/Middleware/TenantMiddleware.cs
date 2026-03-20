using DataMedix.Application.Services;
using DataMedix.Application.Interfaces;

namespace DataMedix.Portal.Middleware
{
    public class TenantMiddleware
    {
        private readonly RequestDelegate _next;

        public TenantMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, ITenantResolver resolver)
        {
            var host = context.Request.Host.Host;

            var tenant = await resolver.ResolveAsync(host);

            if (tenant == null)
            {
                context.Response.StatusCode = 404;
                await context.Response.WriteAsync("Tenant no encontrado");
                return;
            }

            context.Items["Tenant"] = tenant;

            await _next(context);
        }
    }
}
