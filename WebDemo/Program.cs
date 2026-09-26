using ManagedBackgroundServices.Abstractions;
using WebDemo;
using WebDemo.BackgroundJobs;
using WebDemo.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register durable queue and queue consumer for SampleMessage
builder.Services
    .AddQueue<SqLiteDurableQueue>(handlers =>
    {
        handlers.Add<SampleMessage, SampleMessageHandler>();
    });

// Register scheduled jobs
builder.Services.AddScheduledJobs(schedule =>
{
    schedule.Add<DbCleanupJob>("*1d t[2:00am]");
    schedule.Add<ReindexJob>("d[mon..fri] t[9:30am, 3:30pm]");
    schedule.Add<WeeklyReportsJob>("d[sat] tz:America/New_York");
    schedule.Add<FrequentJob>("*5s");
});

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
