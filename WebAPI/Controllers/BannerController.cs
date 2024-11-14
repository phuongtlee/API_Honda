using Google.Cloud.Firestore;
using Google.Cloud.Storage.V1;
using Microsoft.AspNetCore.Mvc;
using WebAPI.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BannerController : ControllerBase
    {
        private const long MaxFileSize = 10 * 1024 * 1024; // Giới hạn kích thước file 10 MB
        private readonly FirestoreDb _firestoreDb;
        private readonly StorageClient _storageClient;

        public BannerController()
        {
            _firestoreDb = FirestoreDb.Create("hondamaintenance-f06a8"); // Khởi tạo Firestore Client
            _storageClient = StorageClient.Create(); // Khởi tạo Firebase Storage Client
        }

        // API to add a banner
        [HttpPost("add")]
        public async Task<IActionResult> AddBanner([FromForm] BannerDTO bannerDto)
        {
            // Validation
            if (bannerDto == null || string.IsNullOrEmpty(bannerDto.Title) || string.IsNullOrEmpty(bannerDto.NewsContent))
            {
                return BadRequest(new { message = "All fields must be provided and valid." });
            }

            string imageUrl = null;

            // Upload banner image to Firebase Storage if present
            if (bannerDto.Image != null)
            {
                imageUrl = await UploadImageToStorage(bannerDto.Image);
                if (imageUrl == null)
                {
                    Console.WriteLine("Error: Failed to upload banner image to Firebase Storage."); 
                    return BadRequest(new { message = "Failed to upload banner image to Firebase Storage." });
                }
            }

            // Create Banner object
            var banner = new Banner
            {
                Title = bannerDto.Title,
                NewsContent = bannerDto.NewsContent,
                CreatedAt = DateTime.UtcNow,
                ImageUrl = imageUrl // Lưu URL ảnh từ Firebase Storage
            };

            // Save banner to Firestore
            try
            {
                await SaveBannerToFirestore(banner);
                return Ok(new { message = "Banner added successfully", banner });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while adding banner: {ex.Message}"); // Log lỗi chi tiết
                return StatusCode(500, new { message = "An error occurred while adding the banner.", error = ex.Message });
            }
        }

        private async Task<string> UploadImageToStorage(IFormFile image)
        {
            if (image.Length > MaxFileSize)
            {
                Console.WriteLine("Error: File size exceeds maximum limit."); // Log lỗi
                return null;
            }

            var bucketName = "hondamaintenance-f06a8.appspot.com";
            var objectName = $"Banners/{Guid.NewGuid()}_{image.FileName}";

            try
            {
                using (var stream = image.OpenReadStream())
                {
                    // Upload object to Firebase Storage
                    var storageObject = await _storageClient.UploadObjectAsync(bucketName, objectName, image.ContentType, stream);

                    // Get metadata to retrieve the token
                    var storageObjectMetadata = await _storageClient.GetObjectAsync(bucketName, objectName);

                    if (storageObjectMetadata.Metadata != null && storageObjectMetadata.Metadata.ContainsKey("firebaseStorageDownloadTokens"))
                    {
                        var downloadToken = storageObjectMetadata.Metadata["firebaseStorageDownloadTokens"];
                        var imageUrl = $"https://firebasestorage.googleapis.com/v0/b/{bucketName}/o/{Uri.EscapeDataString(storageObject.Name)}?alt=media&token={downloadToken}";

                        return imageUrl;
                    }
                    else
                    {
                        // Handle case with no token
                        var newToken = Guid.NewGuid().ToString();
                        storageObjectMetadata.Metadata = storageObjectMetadata.Metadata ?? new Dictionary<string, string>();
                        storageObjectMetadata.Metadata["firebaseStorageDownloadTokens"] = newToken;
                        await _storageClient.UpdateObjectAsync(storageObjectMetadata);

                        var imageUrl = $"https://firebasestorage.googleapis.com/v0/b/{bucketName}/o/{Uri.EscapeDataString(storageObject.Name)}?alt=media&token={newToken}";

                        return imageUrl;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error uploading image: {ex.Message}"); // Log lỗi
                return null;
            }
        }

        // Helper method to save banner to Firestore
        private async Task SaveBannerToFirestore(Banner banner)
        {
            var docRef = _firestoreDb.Collection("banners").Document();
            await docRef.SetAsync(banner);  // Lưu banner vào Firestore
        }

        [HttpGet("all")]
        public async Task<IActionResult> GetAllProduct([FromQuery] string search = "")
        {
            try
            {
                CollectionReference productRef = _firestoreDb.Collection("banners");
                QuerySnapshot snapshot = await productRef.GetSnapshotAsync();

                List<Banner> bannerList = new List<Banner>();

                // Loop through each document in the snapshot
                foreach (DocumentSnapshot document in snapshot.Documents)
                {
                    if (document.Exists)
                    {
                        // Get product data from Firestore document
                        var banner = document.ToDictionary();
                        var convertedBanner = new Banner
                        {
                            Id = document.Id,
                            Title = banner["Title"].ToString(),
                            NewsContent = banner["NewsContent"].ToString(),
                            CreatedAt = banner.ContainsKey("CreatedAt") && banner["CreatedAt"] is Timestamp timestamp
                          ? timestamp.ToDateTime()  // Convert the Timestamp to DateTime
                          : DateTime.MinValue,
                            ImageUrl = banner["ImageUrl"].ToString()
                        };

                        // If a search query is provided, filter products based on the search criteria
                        if (!string.IsNullOrEmpty(search))
                        {
                            if (convertedBanner.Title.Contains(search, StringComparison.OrdinalIgnoreCase))
                            {
                                bannerList.Add(convertedBanner);
                            }
                        }
                        else
                        {
                            bannerList.Add(convertedBanner);
                        }
                    }
                }

                return Ok(bannerList);  // Return the list of products as a JSON response
            }
            catch (Exception ex)
            {
                // Handle any errors that occur during the data fetching process
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> DeleteBanner(string id)
        {
            try
            {
                DocumentReference docRef = _firestoreDb.Collection("banners").Document(id);

                // Kiểm tra nếu tài liệu tồn tại
                DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();
                if (!snapshot.Exists)
                {
                    return NotFound(new { message = "Banner not found." });
                }

                await docRef.DeleteAsync(); // Xóa banner
                return Ok(new { message = "Banner deleted successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while deleting the banner.", error = ex.Message });
            }
        }

        [HttpPut("update/{id}")]
        public async Task<IActionResult> UpdateProduct(string id, [FromForm] BannerDTO bannerDTO)
        {
            // Validation
            if (bannerDTO == null || string.IsNullOrEmpty(bannerDTO.Title) || 
                string.IsNullOrEmpty(bannerDTO.NewsContent))
            {
                return BadRequest(new { message = "All fields must be provided and valid." });
            }

            // Fetch the product from Firestore
            var bannerRef = _firestoreDb.Collection("banners").Document(id);
            var bannerDoc = await bannerRef.GetSnapshotAsync();

            if (!bannerDoc.Exists)
            {
                return NotFound(new { message = "Banner not found." });
            }

            // Get the existing product data
            var banner = bannerDoc.ToDictionary();

            // Update the product fields
            banner["Title"] = bannerDTO.Title;
            banner["NewsContent"] = bannerDTO.NewsContent;
            banner["CreatedAt"] = DateTime.UtcNow;  

            string imageUrl = null;

            if (bannerDTO.Image != null)
            {
                imageUrl = await UploadImageToStorage(bannerDTO.Image);
                if (imageUrl == null)
                {
                    return BadRequest(new { message = "Failed to upload image to Firebase Storage." });
                }
                banner["ImageUrl"] = imageUrl;
            }

            try
            {
                await bannerRef.SetAsync(banner, SetOptions.MergeAll); 
                return Ok(new { message = "Banner updated successfully", banner });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while updating the product.", error = ex.Message });
            }
        }


    }
}
