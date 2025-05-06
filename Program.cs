using YShorts.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<TranscriptionService>();
builder.Services.AddSingleton<GeminiService>();

// Register API keys in configuration
builder.Configuration["AssemblyAI:ApiKey"] = "af7c531f7d254d27afb91b1b25f400de";
builder.Configuration["Google:ApiKey"] = "AIzaSyCGMnW4ZC7CCVL1K4HfVlv0kxHbxg0AB-Y";

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
