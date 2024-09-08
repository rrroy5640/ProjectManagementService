using System.Text;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using ProjectManagementService.Models;
using ProjectManagementService.Services;

var builder = WebApplication.CreateBuilder(args);

var JwtSettings = builder.Configuration.GetSection("JwtSettings");
var AWSParameterStore = builder.Configuration.GetSection("AWS:ParameterStore");

builder.Services.AddAWSService<IAmazonSimpleSystemsManagement>();
var awsOptions = builder.Configuration.GetAWSOptions();
var ssmClient = awsOptions.CreateServiceClient<IAmazonSimpleSystemsManagement>();

var JwtSecretKeyPath = AWSParameterStore["JwtSecretKeyPath"];
var DbConnectionStringPath = AWSParameterStore["DbConnectionStringPath"];
var DbNamePath = AWSParameterStore["DbNamePath"];

var Issuer = JwtSettings["Issuer"];
var Audience = JwtSettings["Audience"];

await ConfigureJwtAuthentication(builder, ssmClient, JwtSecretKeyPath, Issuer, Audience);
await ConfigureDatabase(builder, ssmClient, DbConnectionStringPath, DbNamePath);

builder.Services.AddAuthorization();

builder.Services.AddSingleton<ProjectService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

async Task ConfigureJwtAuthentication(WebApplicationBuilder builder, IAmazonSimpleSystemsManagement ssmClient, string jwtSecretKeyPath, string jwtIssuer, string jwtAudience)
{
    try
    {
        var parameterResponse = await ssmClient.GetParameterAsync(new GetParameterRequest
        {
            Name = jwtSecretKeyPath,
            WithDecryption = true
        });

        var secretKey = parameterResponse.Parameter.Value;
        builder.Services.AddAuthentication(option =>
            {
                option.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                option.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtIssuer,
                    ValidAudience = jwtAudience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
                };
            });
    }
    catch (Exception e)
    {
        Console.WriteLine($"Error retrieving JWT secret key: {e.Message}");
        throw;
    }

}

async Task ConfigureDatabase(WebApplicationBuilder builder, IAmazonSimpleSystemsManagement ssmClient, string dbConnectionStringPath, string dbNamePath)
{
    try
    {
        var connectionStringParameterResponse = await ssmClient.GetParameterAsync(new GetParameterRequest
        {
            Name = dbConnectionStringPath,
            WithDecryption = true
        });

        var databaseStringParameterResponse = await ssmClient.GetParameterAsync(new GetParameterRequest
        {
            Name = dbNamePath,
            WithDecryption = true
        });

        var connectionString = connectionStringParameterResponse.Parameter.Value;
        var databaseName = databaseStringParameterResponse.Parameter.Value;
        builder.Services.Configure<MongoDBSettings>(option =>
        {
            option.ConnectionString = connectionString;
            option.DatabaseName = databaseName;
        });

        builder.Services.AddSingleton<IMongoDBSettings>(sp =>
         sp.GetRequiredService<IOptions<MongoDBSettings>>().Value
        );

        builder.Services.AddSingleton<IMongoClient>(sp =>
        {
            var settings = sp.GetRequiredService<IMongoDBSettings>();
            return new MongoClient(settings.ConnectionString);
        });

        builder.Services.AddScoped(sp =>
        {
            var client = sp.GetRequiredService<IMongoClient>();
            var settings = sp.GetRequiredService<IMongoDBSettings>();
            return client.GetDatabase(settings.DatabaseName);
        });
    }
    catch (Exception e)
    {
        Console.WriteLine($"Error retrieving database connection string: {e.Message}");
        throw;
    }
}