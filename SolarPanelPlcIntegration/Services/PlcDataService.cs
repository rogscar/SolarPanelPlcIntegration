using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Text.Json;
using Opc.Ua;
using Opc.Ua.Client;
using System.Security.Cryptography.X509Certificates;
using System.Net.Sockets;
using System.Collections.Concurrent;
using NModbus.Device;
using NModbus;
using PlcIntegration.Manufacturing;

namespace PlcIntegration.Manufacturing
{
    public class PlcDataService
    {
        private readonly string _connectionString;
        private readonly string _opcUaEndpoint;
        private readonly string _certificatePath;
        private readonly string _modbusIpAddress;
        private readonly int _modbusPort;
        private readonly ConcurrentQueue<string> _dataQueue;

        public PlcDataService(string connectionString, string opcUaEndpoint, string certificatePath, string modbusIpAddress, int modbusPort)
        {
            _connectionString = connectionString;
            _opcUaEndpoint = opcUaEndpoint;
            _certificatePath = certificatePath;
            _modbusIpAddress = modbusIpAddress;
            _modbusPort = modbusPort;
            _dataQueue = new ConcurrentQueue<string>();
            Task.Run(() => ProcessQueueAsync());
        }

        public async Task<PlcData> ReadPlcDataOpcUaAsync(int deviceId)
        {
            try
            {
                var config = new ApplicationConfiguration
                {
                    ApplicationName = "PlcIntegrationClient",
                    ApplicationType = ApplicationType.Client,
                    SecurityConfiguration = new SecurityConfiguration
                    {
                        ApplicationCertificate = new CertificateIdentifier
                        {
                            StoreType = "X509Store",
                            StorePath = "CurrentUser\\My",
                            SubjectName = "PlcIntegrationClient"
                        },
                        TrustedPeerCertificates = new CertificateTrustList
                        {
                            StoreType = "X509Store",
                            StorePath = "CurrentUser\\Root"
                        },
                        TrustedIssuerCertificates = new CertificateTrustList
                        {
                            StoreType = "X509Store",
                            StorePath = "CurrentUser\\Root"
                        },
                        AutoAcceptUntrustedCertificates = true
                    },
                    TransportConfigurations = new TransportConfigurationCollection(),
                    ClientConfiguration = new ClientConfiguration
                    {
                        DefaultSessionTimeout = 60000,
                        MinSubscriptionLifetime = 10000
                    }
                };

                await config.Validate(ApplicationType.Client);

                X509Certificate2 certificate = null;
                if (!string.IsNullOrEmpty(_certificatePath))
                {
                    certificate = new X509Certificate2(_certificatePath);
                }

                // Discover endpoints using DiscoveryClient
                using (var discoveryClient = DiscoveryClient.Create(new Uri(_opcUaEndpoint)))
                {
                    EndpointDescriptionCollection endpointDescriptionCollection = discoveryClient.GetEndpoints(null);
                    foreach (var desc in endpointDescriptionCollection)
                    {
                        Console.WriteLine($"Endpoint: {desc.EndpointUrl}, SecurityMode: {desc.SecurityMode}, SupportsAnonymous: {desc.UserIdentityTokens.Any(t => t.TokenType == UserTokenType.Anonymous)}");
                    }
                    var endpointDescription = endpointDescriptionCollection.FirstOrDefault(e => e.SecurityMode == MessageSecurityMode.None && e.UserIdentityTokens.Any(t => t.TokenType == UserTokenType.Anonymous));
                    if (endpointDescription == null)
                    {
                        Console.WriteLine("No endpoint found with SecurityMode.None and anonymous support.");
                        await LogErrorAsync("No endpoint found with SecurityMode.None and anonymous support.", new PlcData { DeviceId = deviceId });
                        return null;
                    }
                    var endpointConfiguration = EndpointConfiguration.Create(config);
                    var endpoint = new ConfiguredEndpoint(null, endpointDescription, endpointConfiguration);
                    Console.WriteLine($"Creating OPC UA session to {endpointDescription.EndpointUrl}...");
                    using (var session = await Session.Create(config, endpoint, false, false, "FirstSolarSession", 60000, new UserIdentity(new AnonymousIdentityToken()), null))
                    {
                        var nodeId = new NodeId("ns=0;i=2258"); // Server/ServerStatus/CurrentTime
                        var nodesToRead = new ReadValueIdCollection
                        {
                            new ReadValueId { NodeId = nodeId, AttributeId = Attributes.Value }
                        };
                        session.Read(null, 0, TimestampsToReturn.Both, nodesToRead, out DataValueCollection results, out DiagnosticInfoCollection diagnostics);
                        if (StatusCode.IsBad(results[0].StatusCode))
                        {
                            await LogErrorAsync("Failed to read default node", new PlcData { DeviceId = deviceId });
                            return null;
                        }
                        var data = new PlcData
                        {
                            DeviceId = deviceId,
                            Temperature = 25.5, // Hardcoded for testing
                            Pressure = 1.2,     // Hardcoded for testing
                            Timestamp = DateTime.UtcNow
                        };

                        await ProcessPlcDataAsync(data);
                        return data;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OPC UA error: {ex.Message}");
                await LogErrorAsync($"OPC UA communication error: {ex.Message}", new PlcData { DeviceId = deviceId });
                return null;
            }
        }

        public async Task<PlcData> ReadPlcDataModbusAsync(int deviceId)
        {
            try
            {
                using (var client = new TcpClient(_modbusIpAddress, _modbusPort))
                {
                    var factory = new ModbusFactory();
                    IModbusMaster modbusClient = factory.CreateMaster(client);
                    ushort[] registers = await modbusClient.ReadHoldingRegistersAsync(1, 40001, 4);

                    float temperature = BitConverter.ToSingle(BitConverter.GetBytes(registers[0]).Concat(BitConverter.GetBytes(registers[1])).ToArray(), 0);
                    float pressure = BitConverter.ToSingle(BitConverter.GetBytes(registers[2]).Concat(BitConverter.GetBytes(registers[3])).ToArray(), 0);

                    var data = new PlcData
                    {
                        DeviceId = deviceId,
                        Temperature = temperature,
                        Pressure = pressure,
                        Timestamp = DateTime.UtcNow
                    };

                    if (data.Temperature < -50 || data.Temperature > 200)
                    {
                        await LogErrorAsync("Invalid temperature reading from Modbus", data);
                        return null;
                    }

                    await ProcessPlcDataAsync(data);
                    return data;
                }
            }
            catch (Exception ex)
            {
                await LogErrorAsync($"Modbus communication error: {ex.Message}", new PlcData { DeviceId = deviceId });
                return null;
            }
        }

        public async Task<bool> ProcessPlcDataAsync(PlcData data)
        {
            try
            {
                if (data.Temperature < -50 || data.Temperature > 200)
                {
                    await LogErrorAsync("Invalid temperature reading", data);
                    return false;
                }

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new SqlCommand(
                        "INSERT INTO PlcReadings (DeviceId, Temperature, Pressure, Timestamp) " +
                        "VALUES (@DeviceId, @Temperature, @Pressure, @Timestamp)", connection))
                    {
                        command.Parameters.AddWithValue("@DeviceId", data.DeviceId);
                        command.Parameters.AddWithValue("@Temperature", data.Temperature);
                        command.Parameters.AddWithValue("@Pressure", data.Pressure);
                        command.Parameters.AddWithValue("@Timestamp", data.Timestamp);
                        await command.ExecuteNonQueryAsync();
                    }
                }

                await QueueForAnalysisAsync(data);
                return true;
            }
            catch (Exception ex)
            {
                await LogErrorAsync(ex.Message, data);
                return false;
            }
        }

        private async Task QueueForAnalysisAsync(PlcData data)
        {
            var message = JsonSerializer.Serialize(data);
            _dataQueue.Enqueue(message);
            await Task.CompletedTask;
        }

        private async Task ProcessQueueAsync()
        {
            while (true)
            {
                if (_dataQueue.TryDequeue(out var message))
                {
                    Console.WriteLine($"Processing queued data: {message}");
                    await Task.Delay(100);
                }
                else
                {
                    await Task.Delay(500);
                }
            }
        }

        private async Task LogErrorAsync(string message, PlcData data)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand(
                    "INSERT INTO ErrorLog (ErrorMessage, DeviceId, Timestamp) " +
                    "VALUES (@ErrorMessage, @DeviceId, @Timestamp)", connection))
                {
                    command.Parameters.AddWithValue("@ErrorMessage", message);
                    command.Parameters.AddWithValue("@DeviceId", data.DeviceId);
                    command.Parameters.AddWithValue("@Timestamp", DateTime.UtcNow);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }
    }
}