using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReviewController : ControllerBase
    {
        private readonly FirestoreDb _firestoreDb;

        public ReviewController()
        {
            _firestoreDb = FirestoreDb.Create("hondamaintenance-f06a8");
        }

        // GET: api/review/getReviews
        [HttpGet("getReviews")]
        public async Task<IActionResult> GetReviews([FromQuery] string staffName = null)
        {
            try
            {
                // If staffName is provided, fetch the staffId first
                string staffId = null;
                if (!string.IsNullOrEmpty(staffName))
                {
                    staffId = await GetStaffIdByName(staffName);
                    if (staffId == null)
                    {
                        return NotFound(new { message = "Staff member not found." });
                    }
                }

                // Get all reviews from the "reviews" collection, optionally filtering by staffId
                Query reviewsQuery = _firestoreDb.Collection("reviews");
                if (!string.IsNullOrEmpty(staffId))
                {
                    reviewsQuery = reviewsQuery.WhereEqualTo("staffId", staffId);
                }

                // Sort reviews by createdAt in descending order (newest first)
                reviewsQuery = reviewsQuery.OrderByDescending("createdAt");

                QuerySnapshot snapshot = await reviewsQuery.GetSnapshotAsync();

                // List to store reviews
                var reviewList = new List<Reviews>();

                foreach (DocumentSnapshot document in snapshot.Documents)
                {
                    if (document.Exists)
                    {
                        var reviewData = document.ToDictionary();
                        var review = new Reviews
                        {
                            Id = document.Id,
                            Comment = reviewData["comment"].ToString(),
                            Rating = Convert.ToInt32(reviewData["rating"]),
                            ScheduleId = reviewData["scheduleId"].ToString(),
                            StaffId = reviewData["staffId"].ToString(),
                            CreatedAt = reviewData["createdAt"] is Timestamp timestamp
                                ? timestamp.ToDateTime()
                                : DateTime.MinValue
                        };

                        // Fetch staff details based on staffId
                        var staffDetails = await GetStaffDetailsById(review.StaffId);
                        review.StaffName = staffDetails.Fullname;

                        reviewList.Add(review);
                    }
                }

                return Ok(reviewList);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Internal server error: {ex.Message}" });
            }
        }

        // Helper method to fetch staffId based on staffName
        private async Task<string> GetStaffIdByName(string staffName)
        {
            try
            {
                // Query Firestore to get user document based on staffName
                Query staffQuery = _firestoreDb.Collection("users").WhereEqualTo("fullname", staffName);
                QuerySnapshot staffSnapshot = await staffQuery.GetSnapshotAsync();

                if (staffSnapshot.Documents.Count > 0)
                {
                    return staffSnapshot.Documents.FirstOrDefault()?.Id; // Return the staffId (document ID)
                }
                return null;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error fetching staffId: {ex.Message}");
            }
        }

        // Helper method to fetch staff details based on staffId
        private async Task<StaffDetails> GetStaffDetailsById(string staffId)
        {
            try
            {
                // Query Firestore to get user document based on staffId
                DocumentReference staffDocRef = _firestoreDb.Collection("users").Document(staffId);
                DocumentSnapshot staffSnapshot = await staffDocRef.GetSnapshotAsync();

                if (staffSnapshot.Exists)
                {
                    var staffData = staffSnapshot.ToDictionary();
                    return new StaffDetails
                    {
                        Fullname = staffData.ContainsKey("fullname") ? staffData["fullname"].ToString() : "Unknown",
                    };
                }
                else
                {
                    return new StaffDetails
                    {
                        Fullname = "Unknown",
                    };
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error fetching staff details: {ex.Message}");
            }
        }
    }

    // Review class
    public class Reviews
    {
        public string Id { get; set; }
        public string Comment { get; set; }
        public int Rating { get; set; }
        public string ScheduleId { get; set; }
        public string StaffId { get; set; }
        public DateTime CreatedAt { get; set; }
        public string StaffName { get; set; }
    }

    // StaffDetails class
    public class StaffDetails
    {
        public string Fullname { get; set; }
    }
}
