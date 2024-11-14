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
    public class BillingController : Controller
    {
        private readonly FirestoreDb _firestoreDb;

        public BillingController()
        {
            _firestoreDb = FirestoreDb.Create("hondamaintenance-f06a8");
        }
        [HttpGet("allBillings")]
        public async Task<IActionResult> GetAllBillings([FromQuery] string search = "", [FromQuery] DateTime? filterDate = null)
        {
            try
            {
                CollectionReference billingRef = _firestoreDb.Collection("billing");
                QuerySnapshot snapshot = await billingRef.GetSnapshotAsync();

                List<BillDetail> billingList = new List<BillDetail>();

                Console.WriteLine($"Số lượng document: {snapshot.Documents.Count}");

                foreach (DocumentSnapshot document in snapshot.Documents)
                {
                    Console.WriteLine($"Document ID: {document.Id}");
                    Console.WriteLine($"Data: {document.ToDictionary()}");

                    if (document.Exists)
                    {
                        var billingData = document.ToDictionary();

                        // Check for the existence of required fields
                        string[] requiredFields = {
                    "customerUid", "customerName", "carName", "date", "services", "totalCost"
                };

                        // Check if all required fields exist
                        bool allFieldsExist = true;
                        foreach (var field in requiredFields)
                        {
                            if (!billingData.ContainsKey(field))
                            {
                                Console.WriteLine($"Field '{field}' does not exist in document ID {document.Id}.");
                                allFieldsExist = false;
                                break;
                            }
                        }

                        if (!allFieldsExist)
                        {
                            continue;
                        }

                        var servicesData = billingData["services"] as List<object>;
                        List<ServiceBill> services = new List<ServiceBill>();

                        if (servicesData != null && servicesData.Count > 0)
                        {
                            foreach (var serviceObj in servicesData)
                            {
                                var serviceDict = serviceObj as Dictionary<string, object>;

                                if (serviceDict != null && serviceDict.ContainsKey("id") && serviceDict.ContainsKey("name") && serviceDict.ContainsKey("price"))
                                {
                                    var service = new ServiceBill
                                    {
                                        Id = serviceDict["id"].ToString(),
                                        Name = serviceDict["name"].ToString(),
                                        Price = Convert.ToInt32(serviceDict["price"])
                                    };
                                    services.Add(service);
                                }
                                else
                                {
                                    Console.WriteLine("Một trong các trường dịch vụ không tồn tại.");
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine("Dữ liệu dịch vụ không có hoặc trống.");
                        }




                        var convertedBilling = new BillDetail
                        {
                            Id = document.Id,
                            CustomerUid = billingData["customerUid"].ToString(),
                            CustomerName = billingData["customerName"].ToString(),
                            CarName = billingData["carName"].ToString(),
                            Date = billingData["date"].ToString(), 
                            CreatedAt = billingData.ContainsKey("createdAt") && billingData["createdAt"] is Timestamp timestamp
                                ? timestamp.ToDateTime()  
                                : DateTime.MinValue,
                            Services = services, 
                            TotalCost = Convert.ToDecimal(billingData["totalCost"]),
                            StaffName = billingData["staffName"].ToString(),
                        };
                        if (!string.IsNullOrEmpty(search))
                        {
                            if (convertedBilling.CustomerName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                                convertedBilling.StaffName.Contains(search, StringComparison.OrdinalIgnoreCase))
                            {
                                billingList.Add(convertedBilling);
                            }
                        }
                        else
                        {
                            billingList.Add(convertedBilling);
                        }
                        

                    }
                }

                if (billingList.Count == 0)
                {
                    Console.WriteLine("No billing data was added to the list.");
                }

                return Ok(billingList);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }


    }
}
