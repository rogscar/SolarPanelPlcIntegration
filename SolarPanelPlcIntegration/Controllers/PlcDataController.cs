using PlcIntegration.Manufacturing;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace PlcIntegration.Manufacturing
{
    [Route("api/[controller]")]
    [ApiController]
    public class PlcDataController : ControllerBase
    {
        private readonly PlcDataService _service;

        public PlcDataController(PlcDataService service)
        {
            _service = service;
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] PlcData data)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _service.ProcessPlcDataAsync(data);
            return result ? Ok() : StatusCode(500, "Error processing data");
        }

        [HttpGet("opcua/{deviceId}")]
        public async Task<IActionResult> GetOpcUa(int deviceId)
        {
            var data = await _service.ReadPlcDataOpcUaAsync(deviceId);
            if (data == null)
            {
                return StatusCode(500, "Error reading PLC data via OPC UA");
            }
            return Ok(data);
        }

        [HttpGet("modbus/{deviceId}")]
        public async Task<IActionResult> GetModbus(int deviceId)
        {
            var data = await _service.ReadPlcDataModbusAsync(deviceId);
            if (data == null)
            {
                return StatusCode(500, "Error reading PLC data via Modbus");
            }
            return Ok(data);
        }
    }
}