using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Newtonsoft.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Cấu hình Firebase Admin SDK
FirebaseApp.Create(new AppOptions()
{
    Credential = GoogleCredential.FromFile("D:/NAM_4/HK1_Final/Final/WebAPI/WebAPI/Config/hondamaintenance-f06a8-firebase-adminsdk-5qbhs-10b85627e2.json")
});

// Thêm dịch vụ MVC và Newtonsoft.Json
builder.Services.AddControllers()
    .AddNewtonsoftJson(options =>
    {
        options.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
        options.SerializerSettings.Formatting = Newtonsoft.Json.Formatting.Indented;
    });

// Thêm dịch vụ CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins",
        builder => builder
            .WithOrigins("http://localhost:3000", "http://192.168.1.7:3000")
            .AllowAnyMethod()
            .AllowAnyHeader());
});

// Cấu hình Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Cấu hình pipeline HTTP request
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowAllOrigins");
app.UseAuthorization();
app.MapControllers();

app.Run();
