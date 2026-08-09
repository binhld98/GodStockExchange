using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Oee.MarketAcess.Extensions;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddLogging(cfg => cfg.AddConsole());

builder.Services.AddFixGateway();

using var host = builder.Build();

await host.RunAsync();
