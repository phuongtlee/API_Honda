using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using WebAPI.Data;
using System.Threading.Tasks;
using Google.Cloud.Firestore;
using Newtonsoft.Json;
using Google.Api;
using System.Linq;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RepairSchedulesController : ControllerBase
    {
        private readonly FirestoreDb _firestoreDb;

        public RepairSchedulesController()
        {
            _firestoreDb = FirestoreDb.Create("hondamaintenance-f06a8");
        }

        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> DeleteRepairSchedule(string id)
        {
            try
            {
                DocumentReference repairScheduleRef = _firestoreDb.Collection("repairSchedules").Document(id);
                await repairScheduleRef.DeleteAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("update/{id}")]
        public async Task<IActionResult> UpdateRepairSchedule(string id, [FromBody] RepairSchedules updatedRepairSchedule)
        {
            if (updatedRepairSchedule == null)
            {
                return BadRequest(new { message = "Dữ liệu không hợp lệ." });
            }

            try
            {
                DocumentReference repairScheduleRef = _firestoreDb.Collection("repairSchedules").Document(id);
                DocumentSnapshot snapshot = await repairScheduleRef.GetSnapshotAsync();

                if (!snapshot.Exists)
                {
                    return NotFound(new { message = "Lịch sửa chữa không tồn tại." });
                }

                var updatedData = new Dictionary<string, object>
                {
                    { "carName", updatedRepairSchedule.Carname },
                    { "carType", updatedRepairSchedule.Cartype },
                    { "date", updatedRepairSchedule.Date },
                    { "service", updatedRepairSchedule.Service },
                    { "staff", updatedRepairSchedule.Staff },
                    { "uid", updatedRepairSchedule.Uid },
                    { "userName", updatedRepairSchedule.Username },
                    { "status", updatedRepairSchedule.Status },
                    { "statusCheck", updatedRepairSchedule.StatusCheck },
                };

                await repairScheduleRef.UpdateAsync(updatedData);
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("all")]
        public async Task<IActionResult> GetAllRepair([FromQuery] string search = "", [FromQuery] DateTime? filterDate = null)
        {
            try
            {
                CollectionReference repairRef = _firestoreDb.Collection("repairSchedules");
                QuerySnapshot snapshot = await repairRef.GetSnapshotAsync();

                List<RepairSchedules> repairSchedulesList = new List<RepairSchedules>();

                Console.WriteLine($"Số lượng document: {snapshot.Documents.Count}");

                foreach (DocumentSnapshot document in snapshot.Documents)
                {
                    Console.WriteLine($"Document ID: {document.Id}");
                    Console.WriteLine($"Data: {document.ToDictionary()}");

                    if (document.Exists)
                    {
                        var repair = document.ToDictionary();

                        string[] requiredFields = {
                    "carName", "carType", "damageDescription", "date",
                    "service", "uid", "userName", "status", "imageUrls"
                };

                        bool allFieldsExist = true;
                        foreach (var field in requiredFields)
                        {
                            if (!repair.ContainsKey(field))
                            {
                                Console.WriteLine($"Trường '{field}' không tồn tại trong document ID {document.Id}.");
                                allFieldsExist = false;
                                break;
                            }
                        }

                        if (!allFieldsExist)
                        {
                            continue;
                        }

                        DateTime repairDate;
                        object dateValue = repair["date"];

                        if (dateValue is Timestamp timestamp)
                        {
                            repairDate = timestamp.ToDateTime();
                        }
                        else if (dateValue is string dateString)
                        {
                            if (!DateTime.TryParseExact(dateString, "ddd MMM dd yyyy HH:mm:ss 'GMT'K", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out repairDate))
                            {
                                Console.WriteLine($"Không thể chuyển đổi ngày: {dateValue}");
                                continue; 
                            }
                        }
                        else if (!DateTime.TryParse(dateValue.ToString(), out repairDate))
                        {
                            Console.WriteLine($"Không thể chuyển đổi ngày: {dateValue}");
                            continue; 
                        }

                        DateTime repairDateLocal = repairDate.AddHours(7);

                        string staffId = repair.ContainsKey("staff") ? repair["staff"].ToString() : string.Empty;
                        string staffName = "Unknown Staff"; // Giá trị mặc định

                        if (!string.IsNullOrWhiteSpace(staffId))
                        {
                            DocumentReference staffRef = _firestoreDb.Collection("users").Document(staffId);
                            DocumentSnapshot staffSnapshot = await staffRef.GetSnapshotAsync();
                            staffName = staffSnapshot.Exists ? staffSnapshot.GetValue<string>("fullname") : staffName;
                        }
                        else
                        {
                            Console.WriteLine($"Staff ID trống trong document ID {document.Id}. Sử dụng 'Unknown Staff' làm tên nhân viên.");
                        }

                        var convertedRepair = new RepairSchedules
                        {
                            Id = document.Id,
                            Username = repair["userName"]?.ToString() ?? string.Empty,
                            Carname = repair["carName"]?.ToString() ?? string.Empty,
                            Date = repairDateLocal, // Sử dụng thời gian đã chuyển đổi
                            Service = repair["service"]?.ToString() ?? string.Empty,
                            Staff = staffName,
                            Uid = repair["uid"]?.ToString() ?? string.Empty,
                            Cartype = repair["carType"]?.ToString() ?? string.Empty,
                            Status = repair["status"]?.ToString() ?? string.Empty,
                            StatusCheck = repair.ContainsKey("statusCheck") ? repair["statusCheck"]?.ToString() ?? string.Empty : string.Empty,
                            DamageDescription = repair["damageDescription"]?.ToString() ?? string.Empty,
                            ImageUrls = repair.ContainsKey("imageUrls")
                                ? ((IEnumerable<object>)repair["imageUrls"]).Cast<string>().ToList()
                                : new List<string>()
                        };

                        repairSchedulesList.Add(convertedRepair);
                    }
                }

                if (repairSchedulesList.Count == 0)
                {
                    Console.WriteLine("Không có dữ liệu repairSchedules nào được thêm vào danh sách.");
                }

                return Ok(repairSchedulesList);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }




    }
}
