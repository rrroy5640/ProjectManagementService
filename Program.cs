using System.Text;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using ProjectManagementService.Models;
using ProjectManagementService.Services;
using Hellang.Middleware.ProblemDetails;
using Amazon.SQS;

var builder = WebApplication.CreateBuilder(args);

var JwtSettings = builder.Configuration.GetSection("JwtSettings");
var AWSParameterStore = builder.Configuration.GetSection("AWS:ParameterStore");

builder.Services.AddAWSService<IAmazonSimpleSystemsManagement>();
builder.Services.AddAWSService<IAmazonSQS>();
var awsOptions = builder.Configuration.GetAWSOptions();
var ssmClient = awsOptions.CreateServiceClient<IAmazonSimpleSystemsManagement>();
var sqsClient = awsOptions.CreateServiceClient<IAmazonSQS>();

var JwtSecretKeyPath = AWSParameterStore["JwtSecretKeyPath"];
var DbConnectionStringPath = AWSParameterStore["DbConnectionStringPath"];
var DbNamePath = AWSParameterStore["DbNamePath"];
var SQSUrlPath = AWSParameterStore["SQSUrlPath"];

var Issuer = JwtSettings["Issuer"];
var Audience = JwtSettings["Audience"];

if (string.IsNullOrEmpty(JwtSecretKeyPath))
{
    throw new ArgumentNullException(nameof(JwtSecretKeyPath), "JwtSecretKeyPath cannot be null or empty.");
}

if (string.IsNullOrEmpty(DbConnectionStringPath))
{
    throw new ArgumentNullException(nameof(DbConnectionStringPath), "DbConnectionStringPath cannot be null or empty.");
}

if (string.IsNullOrEmpty(DbNamePath))
{
    throw new ArgumentNullException(nameof(DbNamePath), "DbNamePath cannot be null or empty.");
}

if (string.IsNullOrEmpty(Issuer))
{
    throw new ArgumentNullException(nameof(Issuer), "Issuer cannot be null or empty.");
}

if (string.IsNullOrEmpty(Audience))
{
    throw new ArgumentNullException(nameof(Audience), "Audience cannot be null or empty.");
}
if (string.IsNullOrEmpty(SQSUrlPath))
{
    throw new ArgumentNullException(nameof(SQSUrlPath), "SQSUrlPath cannot be null or empty.");
}

await ConfigureJwtAuthentication(builder, ssmClient, JwtSecretKeyPath, Issuer, Audience);

await ConfigureDatabase(builder, ssmClient, DbConnectionStringPath, DbNamePath);
await ConfigureSQS(builder, sqsClient, ssmClient, SQSUrlPath);

builder.Services.AddAuthorization();

builder.Services.AddSingleton<ProjectService>();
builder.Services.AddSingleton<SqsService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();

builder.Services.AddProblemDetails(options =>
{
    // Customize ProblemDetails based on the exception type
    options.MapToStatusCode<NotImplementedException>(StatusCodes.Status501NotImplemented);
    options.MapToStatusCode<HttpRequestException>(StatusCodes.Status503ServiceUnavailable);

    // Map custom exceptions to specific status codes
    options.MapToStatusCode<ArgumentException>(StatusCodes.Status400BadRequest);

    // Configure default problem details for unhandled exceptions
    options.MapToStatusCode<Exception>(StatusCodes.Status500InternalServerError);

    // Include exception details in development environment
    options.IncludeExceptionDetails = (ctx, ex) =>
        builder.Environment.IsDevelopment() || builder.Environment.IsStaging();
});

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

app.UseProblemDetails();

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

async Task ConfigureSQS(WebApplicationBuilder builder, IAmazonSQS sqsClient, IAmazonSimpleSystemsManagement ssmClient, string sqsUrlPath)
{
    try
    {
        var parameterResponse = await ssmClient.GetParameterAsync(new GetParameterRequest
        {
            Name = sqsUrlPath,
            WithDecryption = true
        });

        var sqsUrl = parameterResponse.Parameter.Value;
        builder.Services.Configure<SQSSettings>(option =>
        {
            option.Url = sqsUrl;
        });

        builder.Services.AddSingleton<ISQSSettings>(sp =>
         sp.GetRequiredService<IOptions<SQSSettings>>().Value
        );

        
    }
    catch (Exception e)
    {
        Console.WriteLine($"Error retrieving SQS URL: {e.Message}");
        throw;
    }
}