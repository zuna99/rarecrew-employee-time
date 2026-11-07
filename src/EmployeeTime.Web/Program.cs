using EmployeeTime.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddMemoryCache();
builder.Services.Configure<TimeEntriesOptions>(
    builder.Configuration.GetSection(TimeEntriesOptions.SectionName));
builder.Services.AddHttpClient<ITimeEntriesClientService, TimeEntriesClientService>(client =>
{
    client.BaseAddress = new Uri("https://rc-vault-fap-live-1.azurewebsites.net/api/");
    client.Timeout = TimeSpan.FromSeconds(20);
}).RemoveAllLoggers();
builder.Services.AddSingleton<EmployeeTimeCalculatorService>();
builder.Services.AddSingleton<PieChartRendererService>();

var app = builder.Build();

app.UseExceptionHandler("/error");
app.UseStaticFiles();
app.UseRouting();
app.MapControllers();
app.MapControllerRoute("default", "{controller=Dashboard}/{action=Index}");

app.Run();

public partial class Program;
