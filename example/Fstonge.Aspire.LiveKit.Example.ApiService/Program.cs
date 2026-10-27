using Fstonge.Aspire.LiveKit.Example.ApiService;
using Fstonge.Aspire.Sdk.LiveKit;
using Fstonge.Aspire.Rtc.LiveKit;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddLiveKitSdk(builder.Configuration);
builder.Services.AddLiveKitRtcSdk(builder.Configuration);
builder.Services.AddHostedService<LiveKitRoomLifecycleService>();
builder.Services.AddProblemDetails();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapDefaultEndpoints();

app.Run();
