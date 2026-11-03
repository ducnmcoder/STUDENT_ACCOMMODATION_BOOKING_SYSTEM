using System.ComponentModel.DataAnnotations;

namespace STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Models;

public sealed class LoginInput
{
    [Required(ErrorMessage = "Vui lòng nhập tài khoản.")]
    [EmailAddress(ErrorMessage = "Tài khoản phải là email hợp lệ.")]
    [Display(Name = "Tài khoản")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = "";
}

public sealed record AccountRecord(int Id, string FullName, string Email,
    string PasswordHash, string Status, string Role);

public sealed class ProfileItemInput
{
    public int Id { get; set; }

    [StringLength(80, ErrorMessage = "Tên mục tối đa 80 ký tự.")]
    public string Label { get; set; } = "";

    [StringLength(500, ErrorMessage = "Nội dung tối đa 500 ký tự.")]
    public string Value { get; set; } = "";

    public bool Delete { get; set; }
}

public sealed class ProfileViewModel
{
    public string Role { get; set; } = "";
    public string RoleTitle => Role switch
    {
        "STUDENT" => "STUDENT DASHBOARD",
        "ADMIN" => "ADMIN DASHBOARD",
        "FACILITIES_STAFF" => "FACILITIES STAFF DASHBOARD",
        "SECURITY_GUARD" => "SECURITY GUARD DASHBOARD",
        _ => "DASHBOARD"
    };

    [Required(ErrorMessage = "Vui lòng nhập họ và tên.")]
    [StringLength(150, ErrorMessage = "Họ và tên tối đa 150 ký tự.")]
    [Display(Name = "Họ và tên")]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [StringLength(255, ErrorMessage = "Email tối đa 255 ký tự.")]
    public string Email { get; set; } = "";

    [StringLength(30)] public string? StudentCode { get; set; }
    [StringLength(20)] public string? Cohort { get; set; }
    public string? EnrollmentStatus { get; set; }
    [StringLength(30)] public string? EmployeeCode { get; set; }
    [StringLength(100)] public string? Department { get; set; }
    [StringLength(100)] public string? AssignedArea { get; set; }
    [StringLength(50)] public string? ShiftLabel { get; set; }
    [StringLength(100)] public string? AdminScope { get; set; }
    public List<ProfileItemInput> Items { get; set; } = [];
}
