using ManagedBackgroundServices.Abstractions.Infrastructure;
using WebDemo;
using WebDemo.BackgroundJobs;
using WebDemo.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register durable queue and queue consumer for SampleMessage
builder.Services
    .AddDurableQueue(sp => new SqLiteDurableQueue("queue.db"))
    .AddQueueConsumer<SampleMessage, SampleMessageHandler>();

// Register scheduled jobs
builder.Services
    .AddScheduledJob<DbCleanupJob>("*1d t[2:00am]")
    .AddScheduledJob<ReindexJob>("d[mon..fri] t[9:30am, 3:30pm]")
    .AddScheduledJob<WeeklyReportsJob>("d[sat]")
    .AddScheduledJob<FrequentJob>("*5s");

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
