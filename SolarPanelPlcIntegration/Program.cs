using PlcIntegration.Manufacturing;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<PlcDataService>(sp =>
    new PlcDataService(
        connectionString: "Server=localhost\\SQLEXPRESS;Database=Manufacturing;Trusted_Connection=True;",
        opcUaEndpoint: "opc.tcp://localhost:53530/OPCUA/SimulationServer",
        certificatePath: null,
        modbusIpAddress: "127.0.0.1",
        modbusPort: 502
    ));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
