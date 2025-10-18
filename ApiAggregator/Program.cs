var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();  
builder.Services.AddEndpointsApiExplorer();

// Configure Swagger with JWT support
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { Title = "API Aggregator",
        Version = "v1" ,
        Description = "An API Aggregator that combines data from multiple external APIs including News, Weather, and GitHub.",
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Enter 'Bearer' followed by your JWT token. Example: `Bearer eyJhbGciOiJI...`",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type=ReferenceType.SecurityScheme,
                    Id="Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});
 

// Register HTTP clients with Polly retry policies
builder.Services.AddHttpClient<IWeatherApiService, WeatherApiService>(client =>
{
    client.BaseAddress = new Uri("http://api.openweathermap.org/");

})
.AddPolicyHandler(RetryPolicies.GetRetryPolicy());

builder.Services.AddHttpClient<INewsApiService, NewsApiService>(client =>
{
    client.BaseAddress = new Uri("https://newsapi.org/");
    client.DefaultRequestHeaders.Add("User-Agent", "ApiAggregatorApp/1.0");
})
.AddPolicyHandler(RetryPolicies.GetRetryPolicy());

builder.Services.AddHttpClient<IGitHubApiService, GitHubApiService>(client =>
{
    client.BaseAddress = new Uri("https://api.github.com/");
    client.DefaultRequestHeaders.Add("User-Agent", "APIAggregator");
    client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
})
.AddPolicyHandler(RetryPolicies.GetRetryPolicy());

builder.Services.AddSingleton<IStatisticsService, StatisticsService>();

builder.Services.AddScoped<IAggregationService, AggregationService>();


// Register background service for performance monitoring
builder.Services.AddHostedService<PerformanceMonitoringService>();

// Register in-memory caching
builder.Services.AddMemoryCache();

var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] ?? "YourSuperSecretKeyThatIsAtLeast32CharactersLong!";
var key = Encoding.ASCII.GetBytes(secretKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ClockSkew = TimeSpan.FromSeconds(30)
    };
});

builder.Services.AddAuthorization();


var app = builder.Build();


if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseMiddleware<RequestTimingMiddleware>(); // Custom middleware usually goes after HTTPS
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();