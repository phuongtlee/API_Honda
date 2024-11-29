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
    public class TestDriveSchedulesController : ControllerBase
    {

        private readonly FirestoreDb _firestoreDb;

        public TestDriveSchedulesController()
        {
            _firestoreDb = FirestoreDb.Create("hondamaintenance-f06a8");
        }

        [HttpGet("allTestDrives")]
        public async Task<IActionResult> GetAllTestDrives([FromQuery] string search = "", [FromQuery] DateTime? filterDate = null)
        {
            try
            {
                CollectionReference testDriveRef = _firestoreDb.Collection("testDriveSchedules");
                QuerySnapshot snapshot = await testDriveRef.GetSnapshotAsync();

                List<TestDriveSchedules> testDriveList = new List<TestDriveSchedules>();

                Console.WriteLine($"Số lượng document: {snapshot.Documents.Count}");

                foreach (DocumentSnapshot document in snapshot.Documents)
                {
                    Console.WriteLine($"Document ID: {document.Id}");
                    Console.WriteLine($"Data: {document.ToDictionary()}");

                    if (document.Exists)
                    {
                        var booking = document.ToDictionary();

                        string[] requiredFields = {
                    "productId", "productName", "status", "date", "uid", "userName"
                };

                        bool allFieldsExist = true;
                        foreach (var field in requiredFields)
                        {
                            if (!booking.ContainsKey(field))
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

                        DateTime bookingDate;
                        object dateValue = booking["date"];

                        if (dateValue is Timestamp timestamp)
                        {
                            bookingDate = timestamp.ToDateTime();
                        }
                        else if (dateValue is string dateString)
                        {
                            if (!DateTime.TryParseExact(dateString, "ddd MMM dd yyyy HH:mm:ss 'GMT'K", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out bookingDate))
                            {
                                Console.WriteLine($"Không thể chuyển đổi ngày: {dateValue}");
                                continue;
                            }
                        }
                        else if (!DateTime.TryParse(dateValue.ToString(), out bookingDate))
                        {
                            Console.WriteLine($"Không thể chuyển đổi ngày: {dateValue}");
                            continue; 
                        }

                        var convertedBooking = new TestDriveSchedules
                        {
                            Id=document.Id,
                            ProductId = booking["productId"].ToString(),
                            ProductName = booking["productName"].ToString(),
                            Status = booking["status"].ToString(),
                            Uid = booking["uid"].ToString(),
                            UserName = booking["userName"].ToString(),
                            Date = bookingDate
                        };

                        testDriveList.Add(convertedBooking);
                    }
                }

                if (testDriveList.Count == 0)
                {
                    Console.WriteLine("Không có dữ liệu testDrive nào được thêm vào danh sách.");
                }

                return Ok(testDriveList);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("updateTestDrive/{id}")]
        public async Task<IActionResult> UpdateTestDrive(string id, [FromBody] UpdateTestDriveRequest updatedTestDrive)
        {
            if (updatedTestDrive == null)
            {
                return BadRequest(new { message = "Dữ liệu không hợp lệ." });
            }

            try
            {
                DocumentReference testDriveRef = _firestoreDb.Collection("testDriveSchedules").Document(id);
                DocumentSnapshot snapshot = await testDriveRef.GetSnapshotAsync();

                if (!snapshot.Exists)
                {
                    return NotFound(new { message = "Lịch lái thử không tồn tại." });
                }

                var updatedData = new Dictionary<string, object>
        {
            { "date", updatedTestDrive.Date },
            { "status", updatedTestDrive.Status }
        };

                await testDriveRef.UpdateAsync(updatedData);
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("deleteTestDrive/{id}")]
        public async Task<IActionResult> DeleteTestDrive(string id)
        {
            try
            {
                DocumentReference testDriveRef = _firestoreDb.Collection("testDriveSchedules").Document(id);
                DocumentSnapshot snapshot = await testDriveRef.GetSnapshotAsync();

                if (!snapshot.Exists)
                {
                    return NotFound(new { message = "Lịch lái thử không tồn tại." });
                }

                await testDriveRef.DeleteAsync();
                return Ok(new { message = "Xóa lịch lái thử thành công." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }


    }
}
