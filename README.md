SolarPanelPlcIntegration
Overview
SolarPanelPlcIntegration is a .NET Core Web API project developed as a proof-of-concept for integrating PLC (Programmable Logic Controller) data in a manufacturing context, specifically for solar panel production. This project demonstrates the ability to read data from PLCs using OPC UA and Modbus protocols, validate and store the data in a SQL Server database, and expose it via RESTful API endpoints. 

Features

OPC UA Integration: Reads data from the Prosys OPC UA Simulation Server (opc.tcp://localhost:53530/OPCUA/SimulationServer) using anonymous authentication. Currently reads the CurrentTime node (ns=0;i=2258) and hardcodes temperature and pressure for demo purposes.
Modbus Integration: Implements Modbus TCP to read simulated temperature and pressure values from a local Modbus server (127.0.0.1:502), with validation for temperature readings.
Data Storage: Stores PLC data (DeviceId, Temperature, Pressure, Timestamp) in a SQL Server database (Manufacturing database, PlcReadings table) and logs errors to an ErrorLog table.
REST API: Exposes endpoints via Swagger:
POST /api/PlcData: Stores PLC data in the database.
GET /api/PlcData/opcua/{deviceId}: Reads data via OPC UA.
GET /api/PlcData/modbus/{deviceId}: Reads data via Modbus.


Error Handling: Validates temperature readings (must be between -50°C and 200°C) and logs errors to the database.

Technologies Used

.NET Core 8.0: Web API framework.
OPC UA: OPCFoundation.NetStandard.Opc.Ua library for PLC communication.
Modbus: NModbus library for Modbus TCP communication.
SQL Server: Data storage and error logging.
Swagger: API testing and documentation.
Git/GitHub: Version control.

Prerequisites

.NET 8.0 SDK: Install from Microsoft.
SQL Server: Install SQL Server Express and SSMS. Create a database named Manufacturing with two tables:

CREATE TABLE PlcReadings (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    DeviceId INT,
    Temperature FLOAT,
    Pressure FLOAT,
    Timestamp DATETIME
);

CREATE TABLE ErrorLog (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    ErrorMessage NVARCHAR(MAX),
    DeviceId INT,
    Timestamp DATETIME
);


Prosys OPC UA Simulation Server: Download and run the server, configured to listen on opc.tcp://localhost:53530/OPCUA/SimulationServer.
Modbus Server (Optional): For Modbus testing, use Modbus Poll or a similar tool to simulate a server on 127.0.0.1:502.

Setup Instructions

Clone the Repository:git clone https://github.com/rogscar/SolarPanelPlcIntegration.git
cd SolarPanelPlcIntegration/SolarPanelPlcIntegration


Restore Dependencies:dotnet restore


Run the Prosys OPC UA Simulation Server:
Launch the Prosys server and ensure it’s running on opc.tcp://localhost:53530/OPCUA/SimulationServer.


Run the Application:
In Visual Studio 2022, open the solution and run the project, or use the terminal:dotnet run


The API will be hosted at https://localhost:7075 (or http://localhost:5062 for HTTP).


Access Swagger:
Open https://localhost:7075/swagger in your browser to test the API endpoints.



Usage

GET /api/PlcData/opcua/{deviceId}: Reads data from the OPC UA server.
Example: GET /api/PlcData/opcua/1
Response:{
    "deviceId": 1,
    "temperature": 25.5,
    "pressure": 1.2,
    "timestamp": "2025-04-25T00:25:42.2056553Z"
}




POST /api/PlcData: Stores PLC data in the database.
Example Payload:{
    "DeviceId": 1,
    "Temperature": 25.5,
    "Pressure": 1.2,
    "Timestamp": "2025-04-23T21:25:00Z"
}


Response: HTTP 200 OK if successful.


GET /api/PlcData/modbus/{deviceId}: Reads data from a Modbus server (requires a running Modbus server).

Challenges Overcome

OPC UA Authentication: Switched from ConsoleReferenceServer.exe to Prosys OPC UA Simulation Server, debugged with UAExpert to confirm anonymous authentication and correct node ID.
Build Errors: Fixed issues with DiscoveryClient usage in .NET Core, ensuring proper endpoint selection.

Future Improvements

Replace hardcoded temperature and pressure values with actual PLC data.
Implement authentication for API endpoints.
Add more robust data validation and error handling.
Deploy the API to a cloud service for remote access.

License
This project is licensed under the MIT License. See the LICENSE file for details.

## Project Flowchart

```mermaid
flowchart TD
    A[OPC UA Server\nProsys Simulation Server] -->|Read CurrentTime Node| B[GET /api/PlcData/opcua/deviceId]
    C[Modbus Server\n127.0.0.1 502] -->|Read Registers 40001-40004| D[GET /api/PlcData/modbus/deviceId]
    E[Client Request] -->|POST Data| F[POST /api/PlcData]

    B --> G[PlcDataService.cs\nReadPlcDataOpcUaAsync]
    D --> H[PlcDataService.cs\nReadPlcDataModbusAsync]
    F --> I[PlcDataService.cs\nProcessPlcDataAsync]

    G --> J[Validate Data\nTemperature -50C to 200C]
    H --> J
    I --> J

    J -->|Invalid| K[LogErrorAsync\nStore in ErrorLog Table]
    K --> L[Return Error Response\nHTTP 500]

    J -->|Valid| M[ProcessPlcDataAsync\nStore in PlcReadings Table]

    M --> N[QueueForAnalysisAsync\nAdd to ConcurrentQueue]

    G -->|Success| O[Return PlcData\nHTTP 200 OK]
    H -->|Success| O
    M -->|Success| P[Return HTTP 200 OK]

    M --> Q[SQL Server\nManufacturing Database]
    K --> Q

    N --> R[ProcessQueueAsync\nBackground Processing]
