namespace DentalClinic.Infrastructure
{
    /// <summary>Adds conservative security headers to every response.</summary>
    public static class SecurityHeaders
    {
        // The site loads nothing from third parties except the OpenStreetMap embed on the contacts page.
        // Inline styles are used for a few one-off layout tweaks in the views; scripts are never inline.
        private const string Csp =
            "default-src 'self'; " +
            "img-src 'self' data:; " +
            "style-src 'self' 'unsafe-inline'; " +
            "script-src 'self'; " +
            "font-src 'self'; " +
            "frame-src https://www.openstreetmap.org; " +
            "base-uri 'self'; " +
            "form-action 'self'; " +
            "frame-ancestors 'none'";

        public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
            app.Use(async (context, next) =>
            {
                var headers = context.Response.Headers;
                headers["X-Content-Type-Options"] = "nosniff";
                headers["X-Frame-Options"] = "DENY";
                headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
                headers["Content-Security-Policy"] = Csp;
                await next();
            });
    }
}
