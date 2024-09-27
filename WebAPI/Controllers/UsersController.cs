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
                if (string.IsNullOrEmpty(id) || user == null)
                {
                    return BadRequest("Invalid user data.");
                }

                var auth = FirebaseAdmin.Auth.FirebaseAuth.DefaultInstance;
                var userRecordArgs = new UserRecordArgs
                {
                    Uid = id,
                    DisplayName = user.Username,
                    Email = user.Email,
                    PhoneNumber = string.IsNullOrEmpty(user.Phone) ? null : user.Phone
                };

                try
                {
                    await auth.UpdateUserAsync(userRecordArgs);
                }
                catch (FirebaseAuthException authEx)
                {
                    return StatusCode(500, $"Error updating user in Firebase Authentication: {authEx.Message}");
                }

                DocumentReference docRef = _firestoreDb.Collection("users").Document(id);

                var updateData = new Dictionary<string, object>
        {
            { "username", user.Username },
            { "fullname", user.Fullname },
            { "email", user.Email },
            { "phone", string.IsNullOrEmpty(user.Phone) ? null : user.Phone },
            { "address", string.IsNullOrEmpty(user.Address) ? null : user.Address },
            { "isActive", user.IsActive }
        };

                try
                {
                    await docRef.UpdateAsync(updateData);
                }
                catch (Exception firestoreEx)
                {
                    return StatusCode(500, $"Error updating user in Firestore: {firestoreEx.Message}");
                }

                return Ok("User updated successfully");
            }
            catch (Exception ex)
            {
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
                var userArgs = new FirebaseAdmin.Auth.UserRecordArgs();

                if (!string.IsNullOrEmpty(email))
                {
                    userArgs.Email = email;
                    userArgs.Password = password;
                }

                if (!string.IsNullOrEmpty(phoneNumber))
                {
                    userArgs.PhoneNumber = phoneNumber;
                }

                // Lấy đối tượng FirebaseAuth từ Firebase Admin SDK
                var auth = FirebaseAdmin.Auth.FirebaseAuth.DefaultInstance;
                var userRecord = await auth.CreateUserAsync(userArgs);

                return userRecord;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating user in Firebase Authentication: {ex.Message}");
                return null;
            }
        }



        // Hàm lưu người dùng vào Firestore
        private async Task<bool> AddUserToDatabase(User user)
        {
            try
            {
                // Sử dụng UID của người dùng làm ID tài liệu
                DocumentReference docRef = _firestoreDb.Collection("users").Document(user.Uid);

                // Thêm hoặc cập nhật thông tin người dùng
                await docRef.SetAsync(new
                {
                    uid = user.Uid,
                    username = user.Username,
                    fullname = user.Fullname,
                    isActive = user.IsActive,
                    email = user.Email,
                    phone = user.Phone,
                    address = user.Address,
                    password = user.Password,
                    createdAt = Timestamp.FromDateTime(DateTime.UtcNow) // Lưu thời gian đăng ký
                });

                // Ghi log khi thêm thành công
                Console.WriteLine($"User {user.Uid} added to database successfully.");
                return true;
            }
            catch (Exception ex)
            {
                // Ghi log lỗi nếu xảy ra lỗi
                Console.WriteLine($"Error adding user to database: {ex.Message}");
                return false;
            }
        }

        private bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return false; // Email không được để trống
            }

            try
            {
                // Sử dụng lớp MailAddress để kiểm tra định dạng email
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
