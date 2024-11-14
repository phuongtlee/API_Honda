using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using WebAPI.Data;
using System.Threading.Tasks;
using Google.Cloud.Firestore;
using WebAPI.HTML;
using FirebaseAdmin.Auth;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly FirestoreDb _firestoreDb;

        public UsersController()
        {
            _firestoreDb = FirestoreDb.Create("hondamaintenance-f06a8");
        }

        [HttpPost("add")]
        public async Task<IActionResult> AddUser([FromBody] User user)
        {
            if (user == null)
            {
                return BadRequest(new { message = "User object is null" });
            }

            try
            {
                DocumentReference docRef = _firestoreDb.Collection("users").Document();
                await docRef.SetAsync(new
                {
                    username = user.Username,
                    fullname = user.Fullname,
                    isActive = user.IsActive,
                    email = user.Email,
                    phone = user.Phone,
                    address = user.Address,
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
                CollectionReference useRef = _firestoreDb.Collection("users");
                QuerySnapshot snapshot = await useRef.GetSnapshotAsync();

                List<User> userList = new List<User>();

                foreach (DocumentSnapshot document in snapshot.Documents)
                {
                    if (document.Exists)
                    {
                        var user = document.ToDictionary();

                        // Đảm bảo các khóa tồn tại trước khi truy cập
                        if (user.TryGetValue("username", out var username) &&
                            user.TryGetValue("fullname", out var fullname) &&
                            user.TryGetValue("email", out var email) &&
                            user.TryGetValue("phone", out var phone) &&
                            user.TryGetValue("address", out var address) &&
                            user.TryGetValue("isActive", out var isActive) &&
                            user.TryGetValue("uid", out var uid))
                        {
                            var convertedUser = new User
                            {
                                Id = document.Id,
                                Username = username?.ToString() ?? string.Empty,
                                Fullname = fullname?.ToString() ?? string.Empty,
                                Email = email?.ToString() ?? string.Empty,
                                Phone = phone?.ToString() ?? string.Empty,
                                Address = address?.ToString() ?? string.Empty,
                                IsActive = bool.TryParse(isActive?.ToString(), out var isActiveBool) ? isActiveBool : false,
                                Uid = uid?.ToString() ?? string.Empty
                            };

                            if (string.IsNullOrEmpty(search) ||
                                convertedUser.Username.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                                convertedUser.Fullname.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                                convertedUser.Email.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                                convertedUser.Address.Contains(search, StringComparison.OrdinalIgnoreCase))
                            {
                                userList.Add(convertedUser);
                            }
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
        public async Task<IActionResult> UpdateUser(string id, [FromBody] User user)
        {
            try
            {
                // Validate the ID and user object
                if (string.IsNullOrEmpty(id))
                {
                    Console.WriteLine("Invalid user ID: The ID cannot be null or empty.");
                    return BadRequest("Invalid user ID: The ID cannot be null or empty.");
                }

                if (user == null)
                {
                    Console.WriteLine("Invalid user data: The user object cannot be null.");
                    return BadRequest("Invalid user data: The user object cannot be null.");
                }

                var auth = FirebaseAdmin.Auth.FirebaseAuth.DefaultInstance;

                // Get the current user information from Firebase
                UserRecord currentUserRecord = await auth.GetUserAsync(id);
                string currentPhoneNumber = currentUserRecord.PhoneNumber;

                // Process the phone number
                string updatedPhoneNumber = user.Phone ?? null; // Initialize variable to store updated phone number
                if (!string.IsNullOrEmpty(updatedPhoneNumber))
                {
                    // Remove leading zero if present
                    if (updatedPhoneNumber.StartsWith("0"))
                    {
                        updatedPhoneNumber = updatedPhoneNumber.Substring(1);
                    }

                    // Ensure it does not already contain the +84 prefix
                    if (!updatedPhoneNumber.StartsWith("+84"))
                    {
                        updatedPhoneNumber = $"+84{updatedPhoneNumber}"; // Add country code
                    }

                    // Validate the format
                    if (!System.Text.RegularExpressions.Regex.IsMatch(updatedPhoneNumber, @"^\+84\d{9,10}$"))
                    {
                        Console.WriteLine($"Invalid phone number format: {updatedPhoneNumber}. It must match the pattern +84 followed by 9 to 10 digits.");
                        return BadRequest($"Invalid phone number format: {updatedPhoneNumber}. It must match the pattern +84 followed by 9 to 10 digits.");
                    }
                }
                else
                {
                    // If no new phone number, use current
                    updatedPhoneNumber = currentPhoneNumber;
                }

                // Update user information in Firebase Authentication
                var userRecordArgs = new UserRecordArgs
                {
                    Uid = id,
                    DisplayName = user.Username,
                    Email = user.Email,
                    PhoneNumber = updatedPhoneNumber // Use processed or current phone number
                };

                try
                {
                    Console.WriteLine($"Updating user in Firebase Auth with UID: {id}");
                    await auth.UpdateUserAsync(userRecordArgs);
                    Console.WriteLine($"User {id} updated successfully in Firebase Authentication.");
                }
                catch (FirebaseAuthException authEx)
                {
                    Console.WriteLine($"Error updating user in Firebase Authentication: {authEx.Message}");
                    return StatusCode(500, $"Error updating user in Firebase Authentication: {authEx.Message} ({authEx.AuthErrorCode})");
                }

                // Update user information in Firestore
                DocumentReference docRef = _firestoreDb.Collection("users").Document(id);

                var updateData = new Dictionary<string, object>
        {
            { "username", user.Username },
            { "fullname", user.Fullname },
            { "email", user.Email },
            { "phone", updatedPhoneNumber }, // Use processed or current phone number
            { "address", string.IsNullOrEmpty(user.Address) ? null : user.Address },
            { "isActive", user.IsActive }
        };

                try
                {
                    Console.WriteLine($"Updating Firestore document for user {id}.");
                    await docRef.UpdateAsync(updateData);
                    Console.WriteLine($"User {id} updated successfully in Firestore.");
                }
                catch (Exception firestoreEx)
                {
                    Console.WriteLine($"Error updating user in Firestore: {firestoreEx.Message}");
                    return StatusCode(500, $"Error updating user in Firestore: {firestoreEx.Message}");
                }

                return Ok("User updated successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating user: {ex.Message}");
                return StatusCode(500, $"Error updating user: {ex.Message}");
            }
        }







        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> DeleteUser(string id)
        {
            try
            {
                var auth = FirebaseAdmin.Auth.FirebaseAuth.DefaultInstance;

                await auth.DeleteUserAsync(id);

                DocumentReference docRef = _firestoreDb.Collection("users").Document(id);
                await docRef.DeleteAsync();

                return Ok("User deleted successfully.");
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, $"Error deleting user: {ex.Message}");
            }
        }


        [HttpPost("register")]
        public async Task<IActionResult> RegisterUser([FromBody] UserRegistrationDto dto)
        {
            try
            {
                // Kiểm tra email hợp lệ
                if (!IsValidEmail(dto.Email))
                {
                    return BadRequest("Invalid email format.");
                }

                // Đăng ký tài khoản trong Firebase Authentication
                var authResult = await RegisterUserInFirebase(dto.Email, dto.Password);
                if (authResult == null)
                {
                    return StatusCode(500, "Failed to register user in Firebase Authentication.");
                }

                // Tạo đối tượng người dùng để lưu vào Firestore
                var user = new User
                {
                    Uid = authResult.Uid,
                    Username = dto.Username,
                    Fullname = dto.Fullname,
                    Email = dto.Email,
                    Address = dto.Address,
                    Phone = dto.Phone,
                    IsActive = true
                };

                // Thêm người dùng vào cơ sở dữ liệu Firestore
                var addUserResult = await AddUserToDatabase(user);
                if (!addUserResult)
                {
                    return StatusCode(500, "User registration successful, but failed to add user info to the database.");
                }

                return Ok($"User {user.Uid} registered successfully and user info added to the database.");
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, $"Error registering user: {ex.Message}");
            }
        }

        // Hàm đăng ký tài khoản trong Firebase Authentication
        private async Task<FirebaseAdmin.Auth.UserRecord> RegisterUserInFirebase(string email, string password, string phoneNumber = null)
        {
            try
            {
                var userArgs = new FirebaseAdmin.Auth.UserRecordArgs
                {
                    Email = email,
                    Password = password
                };

                if (!string.IsNullOrEmpty(phoneNumber))
                {
                    userArgs.PhoneNumber = phoneNumber;
                }

                // Lấy đối tượng FirebaseAuth từ Firebase Admin SDK
                var auth = FirebaseAdmin.Auth.FirebaseAuth.DefaultInstance;
                var userRecord = await auth.CreateUserAsync(userArgs);

                return userRecord;
            }
            catch (FirebaseAdmin.Auth.FirebaseAuthException ex)
            {
                // Trả về chi tiết lỗi khi đăng ký
                Console.WriteLine($"Firebase Authentication error: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"General error creating user: {ex.Message}");
                return null;
            }
        }

        // Hàm lưu người dùng vào Firestore
        private async Task<bool> AddUserToDatabase(User user)
        {
            try
            {
                DocumentReference docRef = _firestoreDb.Collection("users").Document(user.Uid);

                await docRef.SetAsync(new
                {
                    uid = user.Uid,
                    username = user.Username,
                    fullname = user.Fullname,
                    isActive = user.IsActive,
                    email = user.Email,
                    phone = user.Phone,
                    address = user.Address,
                    createdAt = Timestamp.FromDateTime(DateTime.UtcNow)
                });

                Console.WriteLine($"User {user.Uid} added to database successfully.");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding user to database: {ex.Message}");
                return false;
            }
        }

        private bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return false;
            }

            try
            {
                var emailAddress = new System.Net.Mail.MailAddress(email);
                return emailAddress.Address == email;
            }
            catch
            {
                return false;
            }
        }

    }
}
