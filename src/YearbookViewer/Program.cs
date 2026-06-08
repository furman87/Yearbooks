using YearbookViewer.Services;
using SixLabors.ImageSharp.Web.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Don't hardcode URLs - let Docker environment variables control this
// builder.WebHost.UseUrls("http://localhost:5010", "https://localhost:5011");

// Add services to the container.
builder.Services.AddRazorPages()
    .AddNewtonsoftJson();

// Add API controllers
builder.Services.AddControllers();

// Configure ImageSharp for image processing
builder.Services.AddImageSharp();

// Add custom services
builder.Services.AddSingleton<YearbookService>();

// Configure CORS for API access
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Disable HTTPS redirection for Docker deployment
// app.UseHttpsRedirection();

// Add ImageSharp middleware before static files
app.UseImageSharp();
app.UseStaticFiles();

app.UseRouting();
app.UseCors();
app.UseAuthorization();

// Map both API controllers and Razor Pages
app.MapControllers();
app.MapRazorPages();

app.Run();
