using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Threading.Tasks;
using Google.Cloud.Firestore;
using WebAPI.Data;
using FirebaseAdmin.Auth;
using FirebaseAdmin.Messaging;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class VehicleController : ControllerBase
    {
        private readonly FirestoreDb _firestoreDb;

        public VehicleController()
        {
            _firestoreDb = FirestoreDb.Create("hondamaintenance-f06a8");
        }

        [HttpPost("add")]
        public async Task<IActionResult> AddVehicle([FromBody] Vehicle vehicle)
        {
            if (vehicle == null)
            {
                return BadRequest(new { message = "Vehicle object cannot be null" });
            }

            try
            {
                DocumentReference docRef = _firestoreDb.Collection("vehicles").Document();
                await docRef.SetAsync(new
                {
                    id = vehicle.Id,
                    vehicle_name = vehicle.VehicleName,
                    brand = vehicle.Brand,
                    model = vehicle.Model,
                    id_user = vehicle.IdUser,
                    vin_num = vehicle.VIN,
                    color = vehicle.Color,
                    km = vehicle.KM,
                    purchase_date = vehicle.PurchaseDate,
                    license_plate = vehicle.LicensePlate,
                });
                return Ok(new { message = "Added successfully!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("all")]
        public async Task<IActionResult> GetAllVehicles([FromQuery] string search = "")
        {
            try
            {
                CollectionReference vehicleRef = _firestoreDb.Collection("vehicles");
                QuerySnapshot snapshots = await vehicleRef.GetSnapshotAsync();

                List<Vehicle> vehiclesList = new List<Vehicle>();
                foreach (DocumentSnapshot document in snapshots.Documents)
                {
                    if (document.Exists)
                    {
                        var vehicle = document.ToDictionary();
                        var convertVehicle = new Vehicle
                        {
                            Id = document.Id,
                            VehicleName = vehicle.ContainsKey("vehicle_name") ? vehicle["vehicle_name"].ToString() : string.Empty,
                            IdUser = vehicle.ContainsKey("id_user") ? vehicle["id_user"].ToString() : string.Empty,
                            Brand = vehicle.ContainsKey("brand") ? vehicle["brand"].ToString() : string.Empty,
                            Model = vehicle.ContainsKey("model") ? vehicle["model"].ToString() : string.Empty,
                            VIN = vehicle.ContainsKey("vin_num") ? vehicle["vin_num"].ToString() : string.Empty,
                            Color = vehicle.ContainsKey("color") ? vehicle["color"].ToString() : string.Empty,
                            PurchaseDate = vehicle.ContainsKey("purchase_date") ? vehicle["purchase_date"].ToString() : string.Empty,
                            KM = vehicle.ContainsKey("km") ? Convert.ToInt32(vehicle["km"]) : 0,
                            LicensePlate = vehicle.ContainsKey("license_plate") ? vehicle["license_plate"].ToString() : string.Empty,
                        };

                        if (string.IsNullOrEmpty(search) ||
                            convertVehicle.Model.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                            convertVehicle.Brand.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                            convertVehicle.VIN.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                            convertVehicle.LicensePlate.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                            convertVehicle.VehicleName.Contains(search, StringComparison.OrdinalIgnoreCase))
                        {
                            vehiclesList.Add(convertVehicle);
                        }
                    }
                }
                return Ok(vehiclesList);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("update/{id}")]
        public async Task<IActionResult> UpdateVehicle(string id, [FromBody] Vehicle updatedVehicle)
        {
            if (string.IsNullOrEmpty(id) || updatedVehicle == null)
            {
                return BadRequest(new { message = "Invalid vehicle ID or data." });
            }

            try
            {
                DocumentReference vehicleRef = _firestoreDb.Collection("vehicles").Document(id);
                DocumentSnapshot snapshot = await vehicleRef.GetSnapshotAsync();

                if (!snapshot.Exists)
                {
                    return NotFound(new { message = "Vehicle not found." });
                }

                Dictionary<string, object> updates = new Dictionary<string, object>
        {
            { "vehicle_name", updatedVehicle.VehicleName },
            { "brand", updatedVehicle.Brand },
            { "model", updatedVehicle.Model },
            { "id_user", updatedVehicle.IdUser },
            { "vin_num", updatedVehicle.VIN },
            { "color", updatedVehicle.Color },
            { "license_plate", updatedVehicle.LicensePlate }
        };

                await vehicleRef.UpdateAsync(updates);
                return Ok(new { message = "Vehicle updated successfully!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> DeleteVehicle(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return BadRequest(new { message = "Invalid vehicle ID." });
            }

            try
            {
                DocumentReference vehicleRef = _firestoreDb.Collection("vehicles").Document(id);
                DocumentSnapshot snapshot = await vehicleRef.GetSnapshotAsync();

                if (!snapshot.Exists)
                {
                    return NotFound(new { message = "Vehicle not found." });
                }

                await vehicleRef.DeleteAsync();
                return Ok(new { message = "Vehicle deleted successfully!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return BadRequest(new { message = ex.Message });
            }
        }


    }
}
