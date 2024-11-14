using Google.Cloud.Firestore;
using Google.Cloud.Storage.V1;
using Microsoft.AspNetCore.Mvc;
using WebAPI.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

[Route("api/[controller]")]
[ApiController]
public class ProductController : ControllerBase
{
    private const long MaxFileSize = 10 * 1024 * 1024; // Giới hạn kích thước file 10 MB
    private readonly FirestoreDb _firestoreDb;
    private readonly StorageClient _storageClient;

    public ProductController()
    {
        _firestoreDb = FirestoreDb.Create("hondamaintenance-f06a8"); // Khởi tạo Firestore Client
        _storageClient = StorageClient.Create(); // Khởi tạo Firebase Storage Client
    }

    // API to add a product
    [HttpPost("add")]
    public async Task<IActionResult> AddProduct([FromForm] ProductDto productDto)
    {
        // Validation
        if (productDto == null || string.IsNullOrEmpty(productDto.NameProduct) || productDto.Price <= 0 ||
            string.IsNullOrEmpty(productDto.Description) || string.IsNullOrEmpty(productDto.Category))
        {
            return BadRequest(new { message = "All fields must be provided and valid." });
        }

        string imageUrl = null;

        // Upload image to Firebase Storage if present
        if (productDto.Image != null)
        {
            imageUrl = await UploadImageToStorage(productDto.Image);
            if (imageUrl == null)
            {
                return BadRequest(new { message = "Failed to upload image to Firebase Storage." });
            }
        }

        // Create Product object
        var product = new Product
        {
            NameProduct = productDto.NameProduct,
            Price = (double)productDto.Price,  // Đảm bảo chuyển đổi về kiểu double
            Description = productDto.Description,
            Category = productDto.Category,
            AddedDate = DateTime.UtcNow,
            ImageUrl = imageUrl // Lưu URL ảnh từ Firebase Storage (bao gồm token)
        };

        // Save product to Firestore
        try
        {
            await SaveProductToFirestore(product);
            return Ok(new { message = "Product added successfully", product });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while adding the product.", error = ex.Message });
        }
    }

    // Helper method to upload an image to Firebase Storage
    private async Task<string> UploadImageToStorage(IFormFile image)
    {
        if (image.Length > MaxFileSize)
        {
            return null;
        }

        var bucketName = "hondamaintenance-f06a8.appspot.com";
        var objectName = $"Product/{Guid.NewGuid()}_{image.FileName}";

        try
        {
            using (var stream = image.OpenReadStream())
            {
                // Upload object to Firebase Storage
                var storageObject = await _storageClient.UploadObjectAsync(bucketName, objectName, image.ContentType, stream);

                // Get metadata to retrieve the token
                var storageObjectMetadata = await _storageClient.GetObjectAsync(bucketName, objectName);

                // Kiểm tra nếu metadata có chứa token, nếu không thì tạo mới
                if (storageObjectMetadata.Metadata != null && storageObjectMetadata.Metadata.ContainsKey("firebaseStorageDownloadTokens"))
                {
                    var downloadToken = storageObjectMetadata.Metadata["firebaseStorageDownloadTokens"];
                    var imageUrl = $"https://firebasestorage.googleapis.com/v0/b/{bucketName}/o/{Uri.EscapeDataString(storageObject.Name)}?alt=media&token={downloadToken}";

                    return imageUrl;
                }
                else
                {
                    // Trường hợp không có token, có thể tạo mới một token hoặc xử lý lỗi
                    var newToken = Guid.NewGuid().ToString(); // Tạo một token mới
                    // Cập nhật metadata với token mới
                    storageObjectMetadata.Metadata = storageObjectMetadata.Metadata ?? new Dictionary<string, string>();
                    storageObjectMetadata.Metadata["firebaseStorageDownloadTokens"] = newToken;
                    await _storageClient.UpdateObjectAsync(storageObjectMetadata); // Cập nhật metadata mới

                    var imageUrl = $"https://firebasestorage.googleapis.com/v0/b/{bucketName}/o/{Uri.EscapeDataString(storageObject.Name)}?alt=media&token={newToken}";

                    return imageUrl;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error uploading image: {ex.Message}");
            return null;
        }
    }

    private async Task SaveProductToFirestore(Product product)
    {
        var docRef = _firestoreDb.Collection("products").Document();
        await docRef.SetAsync(product);  // Lưu sản phẩm vào Firestore
    }

    [HttpGet("all")]
    public async Task<IActionResult> GetAllProduct([FromQuery] string search = "")
    {
        try
        {
            // Access the "products" collection in Firestore
            CollectionReference productRef = _firestoreDb.Collection("products");
            QuerySnapshot snapshot = await productRef.GetSnapshotAsync();

            List<Product> productList = new List<Product>();

            // Loop through each document in the snapshot
            foreach (DocumentSnapshot document in snapshot.Documents)
            {
                if (document.Exists)
                {
                    // Get product data from Firestore document
                    var product = document.ToDictionary();
                    var convertedProduct = new Product
                    {
                        Id = document.Id,
                        NameProduct = product["NameProduct"].ToString(),
                        Price = Convert.ToDouble(product["Price"]),
                        Description = product["Description"].ToString(),
                        Category = product["Category"].ToString(),
                        AddedDate = product.ContainsKey("AddedDate") && product["AddedDate"] is Timestamp timestamp
                      ? timestamp.ToDateTime()  // Convert the Timestamp to DateTime
                      : DateTime.MinValue,
                        ImageUrl = product["ImageUrl"].ToString()
                    };

                    // If a search query is provided, filter products based on the search criteria
                    if (!string.IsNullOrEmpty(search))
                    {
                        if (convertedProduct.NameProduct.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                            convertedProduct.Description.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                            convertedProduct.Category.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                            convertedProduct.Price.ToString().Contains(search, StringComparison.OrdinalIgnoreCase))
                        {
                            productList.Add(convertedProduct);
                        }
                    }
                    else
                    {
                        productList.Add(convertedProduct);
                    }
                }
            }

            return Ok(productList);  // Return the list of products as a JSON response
        }
        catch (Exception ex)
        {
            // Handle any errors that occur during the data fetching process
            return BadRequest(new { message = ex.Message });
        }
    }

    // API to update a product
    [HttpPut("update/{id}")]
    public async Task<IActionResult> UpdateProduct(string id, [FromForm] ProductDto productDto)
    {
        // Validation
        if (productDto == null || string.IsNullOrEmpty(productDto.NameProduct) || productDto.Price <= 0 ||
            string.IsNullOrEmpty(productDto.Description) || string.IsNullOrEmpty(productDto.Category))
        {
            return BadRequest(new { message = "All fields must be provided and valid." });
        }

        // Fetch the product from Firestore
        var productRef = _firestoreDb.Collection("products").Document(id);
        var productDoc = await productRef.GetSnapshotAsync();

        if (!productDoc.Exists)
        {
            return NotFound(new { message = "Product not found." });
        }

        // Get the existing product data
        var product = productDoc.ToDictionary();

        // Update the product fields
        product["NameProduct"] = productDto.NameProduct;
        product["Price"] = productDto.Price;
        product["Description"] = productDto.Description;
        product["Category"] = productDto.Category;
        product["AddedDate"] = DateTime.UtcNow;  // Optional: update the added date to now, or keep it unchanged

        string imageUrl = null;

        // If new image is uploaded, upload to Firebase Storage and update imageUrl
        if (productDto.Image != null)
        {
            imageUrl = await UploadImageToStorage(productDto.Image);
            if (imageUrl == null)
            {
                return BadRequest(new { message = "Failed to upload image to Firebase Storage." });
            }
            product["ImageUrl"] = imageUrl; 
        }

        // Save the updated product to Firestore
        try
        {
            await productRef.SetAsync(product, SetOptions.MergeAll); // Merge with existing document
            return Ok(new { message = "Product updated successfully", product });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while updating the product.", error = ex.Message });
        }
    }

    // API to delete a product
    [HttpDelete("delete/{id}")]
    public async Task<IActionResult> DeleteProduct(string id)
    {
        var productRef = _firestoreDb.Collection("products").Document(id);
        var productDoc = await productRef.GetSnapshotAsync();

        if (!productDoc.Exists)
        {
            return NotFound(new { message = "Product not found." });
        }

        try
        {
            await productRef.DeleteAsync();
            return Ok();  // Chỉ trả về status code 200 khi xóa thành công
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while deleting the product.", error = ex.Message });
        }
    }



}
