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
    private const long MaxFileSize = 10 * 1024 * 1024; 
    private readonly FirestoreDb _firestoreDb;
    private readonly StorageClient _storageClient;

    public ProductController()
    {
        _firestoreDb = FirestoreDb.Create("hondamaintenance-f06a8"); 
        _storageClient = StorageClient.Create(); 
    }

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

        if (productDto.Image != null)
        {
            imageUrl = await UploadImageToStorage(productDto.Image);
            if (imageUrl == null)
            {
                return BadRequest(new { message = "Failed to upload image to Firebase Storage." });
            }
        }

        var product = new Product
        {
            NameProduct = productDto.NameProduct,
            Price = (double)productDto.Price, 
            Description = productDto.Description,
            Category = productDto.Category,
            AddedDate = DateTime.UtcNow,
            ImageUrl = imageUrl 
        };

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
        await docRef.SetAsync(product);  
    }

    [HttpGet("all")]
    public async Task<IActionResult> GetAllProduct([FromQuery] string search = "")
    {
        try
        {
            CollectionReference productRef = _firestoreDb.Collection("products");
            QuerySnapshot snapshot = await productRef.GetSnapshotAsync();

            List<Product> productList = new List<Product>();

            foreach (DocumentSnapshot document in snapshot.Documents)
            {
                if (document.Exists)
                {
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

            return Ok(productList);  
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("update/{id}")]
    public async Task<IActionResult> UpdateProduct(string id, [FromForm] ProductDto productDto)
    {
        if (productDto == null || string.IsNullOrEmpty(productDto.NameProduct) || productDto.Price <= 0 ||
            string.IsNullOrEmpty(productDto.Description) || string.IsNullOrEmpty(productDto.Category))
        {
            return BadRequest(new { message = "All fields must be provided and valid." });
        }

        var productRef = _firestoreDb.Collection("products").Document(id);
        var productDoc = await productRef.GetSnapshotAsync();

        if (!productDoc.Exists)
        {
            return NotFound(new { message = "Product not found." });
        }

        var product = productDoc.ToDictionary();

        product["NameProduct"] = productDto.NameProduct;
        product["Price"] = productDto.Price;
        product["Description"] = productDto.Description;
        product["Category"] = productDto.Category;
        product["AddedDate"] = DateTime.UtcNow;  

        string imageUrl = null;

        if (productDto.Image != null)
        {
            imageUrl = await UploadImageToStorage(productDto.Image);
            if (imageUrl == null)
            {
                return BadRequest(new { message = "Failed to upload image to Firebase Storage." });
            }
            product["ImageUrl"] = imageUrl; 
        }

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
