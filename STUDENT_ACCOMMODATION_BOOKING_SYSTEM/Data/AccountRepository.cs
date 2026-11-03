using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Models;

namespace STUDENT_ACCOMMODATION_BOOKING_SYSTEM.Data;

public sealed class AccountRepository(IConfiguration configuration)
{
    private readonly string _connectionString = configuration.GetConnectionString("AccommodationDb")
        ?? throw new InvalidOperationException("Thiếu ConnectionStrings:AccommodationDb.");
    private readonly PasswordHasher<AccountRecord> _passwordHasher = new();

    private SqlConnection Connection() => new(_connectionString);

    private const string RoleSql = """
        CASE WHEN EXISTS (SELECT 1 FROM dbo.[ADMIN] WHERE account_id=a.account_id) THEN 'ADMIN'
             WHEN EXISTS (SELECT 1 FROM dbo.FACILITIES_STAFF WHERE account_id=a.account_id) THEN 'FACILITIES_STAFF'
             WHEN EXISTS (SELECT 1 FROM dbo.SECURITY_GUARD WHERE account_id=a.account_id) THEN 'SECURITY_GUARD'
             WHEN EXISTS (SELECT 1 FROM dbo.STUDENT WHERE account_id=a.account_id) THEN 'STUDENT'
             ELSE '' END
        """;

    public async Task<AccountRecord?> FindByEmailAsync(string email)
    {
        await using var connection = Connection();
        await connection.OpenAsync();
        await using var command = new SqlCommand($"""
            SELECT a.account_id,a.full_name,a.email,a.password_hash,a.account_status,{RoleSql} AS role
            FROM dbo.ACCOUNT a WHERE a.email=@email
            """, connection);
        command.Parameters.AddWithValue("@email", email.Trim());
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;
        return new AccountRecord(reader.GetInt32(0), reader.GetString(1), reader.GetString(2),
            reader.GetString(3), reader.GetString(4), reader.GetString(5));
    }

    public bool VerifyPassword(AccountRecord account, string password) =>
        account.PasswordHash != "DEMO_HASH_NOT_FOR_LOGIN" &&
        _passwordHasher.VerifyHashedPassword(account, account.PasswordHash, password)
            != PasswordVerificationResult.Failed;

    public async Task<int> SetDemoPasswordsAsync(string password, bool overwriteExisting = false)
    {
        await using var connection = Connection();
        await connection.OpenAsync();
        await EnsureProfileTableAsync(connection);
        var whereClause = overwriteExisting
            ? "account_id BETWEEN 1 AND 8"
            : "password_hash='DEMO_HASH_NOT_FOR_LOGIN'";
        await using var read = new SqlCommand($"SELECT account_id FROM dbo.ACCOUNT WHERE {whereClause}", connection);
        var ids = new List<int>();
        await using (var reader = await read.ExecuteReaderAsync())
            while (await reader.ReadAsync()) ids.Add(reader.GetInt32(0));
        foreach (var id in ids)
        {
            var account = new AccountRecord(id, "", "", "", "ACTIVE", "");
            var hash = _passwordHasher.HashPassword(account, password);
            var updateSql = overwriteExisting
                ? "UPDATE dbo.ACCOUNT SET password_hash=@hash WHERE account_id=@id"
                : "UPDATE dbo.ACCOUNT SET password_hash=@hash WHERE account_id=@id AND password_hash='DEMO_HASH_NOT_FOR_LOGIN'";
            await using var update = new SqlCommand(updateSql, connection);
            update.Parameters.AddWithValue("@hash", hash);
            update.Parameters.AddWithValue("@id", id);
            await update.ExecuteNonQueryAsync();
        }
        return ids.Count;
    }

    public async Task<ProfileViewModel?> GetProfileAsync(int accountId)
    {
        await using var connection = Connection();
        await connection.OpenAsync();
        await EnsureProfileTableAsync(connection);
        await using var command = new SqlCommand($"""
            SELECT a.full_name,a.email,{RoleSql} AS role,
                   s.student_code,s.cohort,s.enrollment_status,
                   COALESCE(f.employee_code,g.employee_code),f.department,f.assigned_area,
                   g.shift_label,ad.admin_scope
            FROM dbo.ACCOUNT a
            LEFT JOIN dbo.STUDENT s ON s.account_id=a.account_id
            LEFT JOIN dbo.FACILITIES_STAFF f ON f.account_id=a.account_id
            LEFT JOIN dbo.SECURITY_GUARD g ON g.account_id=a.account_id
            LEFT JOIN dbo.[ADMIN] ad ON ad.account_id=a.account_id
            WHERE a.account_id=@id AND a.account_status='ACTIVE'
            """, connection);
        command.Parameters.AddWithValue("@id", accountId);
        ProfileViewModel? profile;
        await using (var reader = await command.ExecuteReaderAsync())
        {
            if (!await reader.ReadAsync()) return null;
            string? Field(int i) => reader.IsDBNull(i) ? null : reader.GetString(i);
            profile = new ProfileViewModel
            {
                FullName = reader.GetString(0), Email = reader.GetString(1), Role = reader.GetString(2),
                StudentCode = Field(3), Cohort = Field(4), EnrollmentStatus = Field(5),
                EmployeeCode = Field(6), Department = Field(7), AssignedArea = Field(8),
                ShiftLabel = Field(9), AdminScope = Field(10)
            };
        }
        await using var items = new SqlCommand("SELECT item_id,label,field_value FROM dbo.ACCOUNT_PROFILE_ITEM WHERE account_id=@id ORDER BY item_id", connection);
        items.Parameters.AddWithValue("@id", accountId);
        await using var itemReader = await items.ExecuteReaderAsync();
        while (await itemReader.ReadAsync())
            profile.Items.Add(new ProfileItemInput { Id = itemReader.GetInt32(0), Label = itemReader.GetString(1), Value = itemReader.GetString(2) });
        return profile;
    }

    public async Task SaveProfileAsync(int accountId, string role, ProfileViewModel profile)
    {
        await using var connection = Connection();
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        try
        {
            await using (var account = new SqlCommand("UPDATE dbo.ACCOUNT SET full_name=@name,email=@email WHERE account_id=@id AND account_status='ACTIVE'", connection, transaction))
            {
                account.Parameters.AddWithValue("@name", profile.FullName.Trim());
                account.Parameters.AddWithValue("@email", profile.Email.Trim());
                account.Parameters.AddWithValue("@id", accountId);
                if (await account.ExecuteNonQueryAsync() != 1) throw new InvalidOperationException("Tài khoản không còn hoạt động.");
            }

            var sql = role switch
            {
                "STUDENT" => "UPDATE dbo.STUDENT SET student_code=@code,cohort=@cohort WHERE account_id=@id",
                "FACILITIES_STAFF" => "UPDATE dbo.FACILITIES_STAFF SET employee_code=@code,department=@department,assigned_area=@area WHERE account_id=@id",
                "SECURITY_GUARD" => "UPDATE dbo.SECURITY_GUARD SET employee_code=@code,shift_label=@shift WHERE account_id=@id",
                "ADMIN" => "UPDATE dbo.[ADMIN] SET admin_scope=@scope WHERE account_id=@id",
                _ => throw new InvalidOperationException("Vai trò không hợp lệ.")
            };
            await using (var details = new SqlCommand(sql, connection, transaction))
            {
                details.Parameters.AddWithValue("@id", accountId);
                if (role == "STUDENT")
                {
                    details.Parameters.AddWithValue("@code", profile.StudentCode!.Trim());
                    details.Parameters.AddWithValue("@cohort", DbValue(profile.Cohort));
                }
                if (role is "FACILITIES_STAFF" or "SECURITY_GUARD")
                    details.Parameters.AddWithValue("@code", profile.EmployeeCode!.Trim());
                if (role == "FACILITIES_STAFF")
                {
                    details.Parameters.AddWithValue("@department", DbValue(profile.Department));
                    details.Parameters.AddWithValue("@area", DbValue(profile.AssignedArea));
                }
                if (role == "SECURITY_GUARD") details.Parameters.AddWithValue("@shift", DbValue(profile.ShiftLabel));
                if (role == "ADMIN") details.Parameters.AddWithValue("@scope", DbValue(profile.AdminScope));
                if (await details.ExecuteNonQueryAsync() != 1) throw new InvalidOperationException("Không tìm thấy hồ sơ vai trò.");
            }

            foreach (var item in profile.Items)
            {
                if (item.Id > 0)
                {
                    var itemSql = item.Delete
                        ? "DELETE FROM dbo.ACCOUNT_PROFILE_ITEM WHERE item_id=@item AND account_id=@id"
                        : "UPDATE dbo.ACCOUNT_PROFILE_ITEM SET label=@label,field_value=@value WHERE item_id=@item AND account_id=@id";
                    await using var edit = new SqlCommand(itemSql, connection, transaction);
                    edit.Parameters.AddWithValue("@item", item.Id);
                    edit.Parameters.AddWithValue("@id", accountId);
                    if (!item.Delete) AddItemParameters(edit, item);
                    if (await edit.ExecuteNonQueryAsync() != 1) throw new InvalidOperationException("Mục thông tin không thuộc tài khoản này.");
                }
                else if (!item.Delete)
                {
                    await using var add = new SqlCommand("INSERT dbo.ACCOUNT_PROFILE_ITEM(account_id,label,field_value) VALUES(@id,@label,@value)", connection, transaction);
                    add.Parameters.AddWithValue("@id", accountId);
                    AddItemParameters(add, item);
                    await add.ExecuteNonQueryAsync();
                }
            }
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static object DbValue(string? value) => string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();

    private static void AddItemParameters(SqlCommand command, ProfileItemInput item)
    {
        command.Parameters.AddWithValue("@label", item.Label.Trim());
        command.Parameters.AddWithValue("@value", item.Value.Trim());
    }

    private static async Task EnsureProfileTableAsync(SqlConnection connection)
    {
        await using var command = new SqlCommand("""
            IF OBJECT_ID(N'dbo.ACCOUNT_PROFILE_ITEM', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.ACCOUNT_PROFILE_ITEM (
                    item_id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ACCOUNT_PROFILE_ITEM PRIMARY KEY,
                    account_id INT NOT NULL CONSTRAINT FK_ACCOUNT_PROFILE_ITEM_ACCOUNT REFERENCES dbo.ACCOUNT(account_id),
                    label NVARCHAR(80) NOT NULL,
                    field_value NVARCHAR(500) NOT NULL
                );
                CREATE INDEX IX_ACCOUNT_PROFILE_ITEM_account ON dbo.ACCOUNT_PROFILE_ITEM(account_id);
            END
            """, connection);
        await command.ExecuteNonQueryAsync();
    }
}
