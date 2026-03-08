using Biogarden.WebApi.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAppInfrastructure(builder.Configuration);
builder.Services.AddApplicationServices();
builder.Services.AddSwaggerDocumentation();

var app = builder.Build();

app.UseAppPipeline();

app.Run();