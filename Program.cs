using System.Net.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using TransformingProxy;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<HttpClient>();
builder.Services.AddSingleton<TransformingProxyMiddlewareConfiguration>();
builder.Services.AddScoped<TransformingProxyMiddleware>();

var logger = new LoggerConfiguration()
    .WriteTo.File("transformingProxy.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();
builder.Services.AddSingleton<ILogger>(logger);

var app = builder.Build();


if (args.Length > 0)
{
    var configuration = app.Services.GetService<IConfiguration>()!;
    configuration["Ruleset"] = args[0];
}

var middlewareConfiguration =  app.Services.GetService<TransformingProxyMiddlewareConfiguration>();
middlewareConfiguration!.Init();

app.UseMiddleware<TransformingProxyMiddleware>();

app.Run();
