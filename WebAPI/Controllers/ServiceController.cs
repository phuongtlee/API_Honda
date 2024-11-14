using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using WebAPI.Data;
using System.Threading.Tasks;
using Google.Cloud.Firestore;
using WebAPI.HTML;
using FirebaseAdmin.Auth;
using Humanizer;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ServiceController : ControllerBase
    {
        private readonly FirestoreDb _firestoreDb;

        public ServiceController()
        {
            _firestoreDb = FirestoreDb.Create("hondamaintenance-f06a8");
        }

        [HttpPost("add")]
        public async Task<IActionResult> AddUser([FromBody] Service service)
        {
            if (service == null)
            {
                return BadRequest(new { message = "Service object is null" });
            }

            try
            {
                DocumentReference docRef = _firestoreDb.Collection("services").Document();
                await docRef.SetAsync(new
                {
                    name_service = service.NameService,
                    type = service.Type,
                    price = service.Price,
  
                });
                return Ok("Add successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("all")]
        public async Task<IActionResult> GetAllUser([FromQuery] string search = "")
        {
            try
            {
                CollectionReference useRef = _firestoreDb.Collection("services");
                QuerySnapshot snapshot = await useRef.GetSnapshotAsync();

                List<Service> userList = new List<Service>();

                foreach (DocumentSnapshot document in snapshot.Documents)
                {
                    if (document.Exists)
                    {
                        var service = document.ToDictionary();
                        var convertedService = new Service
                        {
                            IdService = document.Id,
                            NameService = service["name_service"].ToString(),
                            Type = service["type"].ToString(),
                            Price = Convert.ToInt32(service["price"]),
                        };

                        if (!string.IsNullOrEmpty(search))
                        {
                            if (convertedService.NameService.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                                convertedService.Type.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                                convertedService.Price.ToString().Contains(search, StringComparison.OrdinalIgnoreCase))



                            {
                                userList.Add(convertedService);
                            }
                        }
                        else
                        {
                            userList.Add(convertedService);
                        }
                    }
                }
                return Ok(userList);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("update/{id}")]
        public async Task<IActionResult> UpdateService(string id, [FromBody] Service updatedService)
        {
            if (string.IsNullOrEmpty(id) || updatedService == null)
            {
                return BadRequest(new { message = "Invalid service ID or data." });
            }

            try
            {
                DocumentReference serviceRef = _firestoreDb.Collection("services").Document(id);
                DocumentSnapshot snapshot = await serviceRef.GetSnapshotAsync();

                if (!snapshot.Exists)
                {
                    return NotFound(new { message = "Service not found." });
                }

                Dictionary<string, object> updates = new Dictionary<string, object>
        {
            { "name_service", updatedService.NameService },
            { "type", updatedService.Type },
            { "price", updatedService.Price }
        };

                await serviceRef.UpdateAsync(updates);
                return Ok(new { message = "Service updated successfully!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return StatusCode(500, new { message = ex.Message });
            }
        }


        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> DeleteService(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return BadRequest(new { message = "Invalid service ID." });
            }

            try
            {
                DocumentReference vehicleRef = _firestoreDb.Collection("services").Document(id);
                DocumentSnapshot snapshot = await vehicleRef.GetSnapshotAsync();

                if (!snapshot.Exists)
                {
                    return NotFound(new { message = "Service not found." });
                }

                await vehicleRef.DeleteAsync();
                return Ok(new { message = "Service deleted successfully!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
