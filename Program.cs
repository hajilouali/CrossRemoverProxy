using CrossRemoverProxy.Controllers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register HttpClient for making external requests
builder.Services.AddHttpClient<ProxyController>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30); // Set a reasonable timeout
    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
{
    AllowAutoRedirect = true,
    MaxAutomaticRedirections = 5,
    UseCookies = false // Disable cookies to prevent issues
});
// Add CORS policy
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()      // یا یک دامنه مشخص
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseCors();  // حتما قبل از MapControllers

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
