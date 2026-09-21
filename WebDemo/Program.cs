using ManagedBackgroundServices.Abstractions.Infrastructure;
using WebDemo;
using WebDemo.BackgroundJobs;
using WebDemo.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDurableQueue(new SqLiteDurableQueue("queue.db"));

// Register scheduled jobs service - handlers are registered in MyScheduledJobs.RegisterHandlers()
builder.Services.AddManagedBackgroundService<MyScheduledJobs>();

builder.Services.AddManagedBackgroundService<SampleQueueConsumer>();

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
