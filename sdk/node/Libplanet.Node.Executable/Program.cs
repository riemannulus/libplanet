using System.Security.Cryptography;
using Libplanet.Action;
using Libplanet.Action.Loader;
using Libplanet.Blockchain.Policies;
using Libplanet.Crypto.Secp256k1;
using Libplanet.Node.API;
using Libplanet.Node.Extensions;
using Libplanet.Node.Options.Schema;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Serilog;
using Serilog.Events;

SynchronizationContext.SetSynchronizationContext(new());
var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddConsole();

// Logging setting
var loggerConfig = new LoggerConfiguration();
loggerConfig = loggerConfig.MinimumLevel.Information();
loggerConfig = loggerConfig
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console();

Log.Logger = loggerConfig.CreateLogger();


if (builder.Environment.IsDevelopment())
{
    builder.WebHost.ConfigureKestrel(options =>
    {
        // Setup a HTTP/2 endpoint without TLS.
        options.ListenLocalhost(5259, o => o.Protocols =
            HttpProtocols.Http1AndHttp2);
    });
}


// Additional configuration is required to successfully run gRPC on macOS.
// For instructions on how to configure Kestrel and gRPC clients on macOS,
// visit https://go.microsoft.com/fwlink/?linkid=2099682

string pluginPath = "/Users/bin_bash_shell/Workspaces/planetarium/NineChronicles/" +
    "lib9c/Lib9c.NCActionLoader/bin/Debug/net6.0/Lib9c.NCActionLoader.dll";
string actionLoaderType = "Lib9c.NCActionLoader.NineChroniclesActionLoader";
string blockPolicyType = "Lib9c.NCActionLoader.NineChroniclesPolicyActionRegistry";
IActionLoader actionLoader = PluginLoader.LoadActionLoader(pluginPath, actionLoaderType);
IPolicyActionsRegistry policyActionRegistry =
    PluginLoader.LoadPolicyActionRegistry(pluginPath, blockPolicyType);

Libplanet.Crypto.CryptoConfig.CryptoBackend = new Secp256k1CryptoBackend<SHA256>();

builder.Services.AddSingleton<IActionLoader>(actionLoader);
builder.Services.AddSingleton<IPolicyActionsRegistry>(policyActionRegistry);

// Add services to the container.
builder.Services.AddGrpc();
builder.Services.AddGrpcReflection();
var libplanetBuilder = builder.Services.AddLibplanetNode(builder.Configuration)
    .WithNode();

var app = builder.Build();
var handlerMessage = """
    Communication with gRPC endpoints must be made through a gRPC client. To learn how to
    create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909
    """;
var schema = await OptionsSchemaBuilder.GetSchemaAsync(default);

// Configure the HTTP request pipeline.
app.MapGrpcServiceFromDomain(libplanetBuilder.Scopes);
app.MapGet("/", () => handlerMessage);
app.MapGet("/schema", () => schema);

if (builder.Environment.IsDevelopment())
{
    app.MapGrpcReflectionService().AllowAnonymous();
}

await app.RunAsync();
