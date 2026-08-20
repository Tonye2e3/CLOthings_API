namespace CLOthings_API.Middleware
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(
            RequestDelegate next,
            ILogger<ExceptionHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                // 讓 Request 繼續往下一層走
                await _next(context);
            }
            catch (Exception ex)
            {
                // 記錄真正的錯誤
                _logger.LogError(ex, "發生未處理的例外");

                context.Response.StatusCode =
                    StatusCodes.Status500InternalServerError;

                context.Response.ContentType =
                    "application/json";

                await context.Response.WriteAsJsonAsync(new
                {
                    status = 500,
                    message = "伺服器發生錯誤"
                });
            }
        }
    }
}