using AkwariumApi;
using AkwariumShared.Auth;
using AkwariumShared.Dashboard;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Net;
using System.Security.Cryptography;
using System.Text;



var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

var webBaseUrl = builder.Configuration["AppSettings:WebBaseUrl"];

var espBaseUrl = builder.Configuration["Esp:BaseUrl"] ?? "http://10.71.91.17";


builder.Services.AddHttpClient("EspClient", client =>
{
    client.BaseAddress = new Uri(espBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(3);
});





var app = builder.Build();



var adminToken = builder.Configuration["Admin:Token"];

// helper
bool IsAdmin(HttpRequest req)
{
    if (string.IsNullOrWhiteSpace(adminToken)) return true; 
    if (!req.Headers.TryGetValue("X-Admin-Token", out var token)) return false;
    return token == adminToken;
}


//reset hasla



// UZYTKOWNIK login!!!!!!!!!!!!!!!!!!!!

app.MapGet("/", () => "Akwarium API działa");

app.MapPost("/api/auth/login", async (LoginRequest request) =>
{
    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();

    await using var command = connection.CreateCommand();
    command.CommandText = @"
         SELECT TOP 1 UserID, Password, Role, Nick, Email
        FROM Users
        WHERE Nick = @Login OR Email = @Login";

    var login = (request.Login ?? "").Trim();
    command.Parameters.AddWithValue("@Login", login);

    using var reader = await command.ExecuteReaderAsync();

    if (!await reader.ReadAsync())
        return Results.Unauthorized(); 

    int userId = reader.GetInt32(0);
    string storedPassword = reader.GetString(1);
    string role = reader.GetString(2);
    string nick = reader.GetString(3);
    string email = reader.GetString(4);

    bool ok = PasswordHasher.VerifyPassword(request.Password, storedPassword);
    if (!ok)
        return Results.Unauthorized();

    if (string.Equals(role, "Disabled", StringComparison.OrdinalIgnoreCase))
        return Results.StatusCode(StatusCodes.Status403Forbidden);

    return Results.Ok(new LoginResponse
    {
        Success = true,
        UserID = userId,
        Role = role,
        Token = "dummy-token",
        Error = null,
        Nick = nick,
        Email = email
    });
});

// AKWARIA!!!!!!!!!!!!!!!!!!!!!!!!!!

app.MapPost("/api/users/{userId:int}/aquariums", async (int userId, CreateAquariumRequest req) =>
{
    if (req.UserId != userId || string.IsNullOrWhiteSpace(req.AquariumName))
        return Results.BadRequest("Błędne dane.");

    var name = req.AquariumName.Trim();

    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();

    
    await using (var check = connection.CreateCommand())
    {
        check.CommandText = @"
            SELECT COUNT(1)
            FROM Aquariums
            WHERE UserID = @UserId
              AND LOWER(LTRIM(RTRIM(AquariumName))) = LOWER(@Name);";
        check.Parameters.AddWithValue("@UserId", userId);
        check.Parameters.AddWithValue("@Name", name);

        var exists = (int)(await check.ExecuteScalarAsync() ?? 0) > 0;
        if (exists)
            return Results.Conflict("Masz już akwarium o takiej nazwie.");
    }

    // 2) Insert
    await using var cmd = connection.CreateCommand();
    cmd.CommandText = @"
        INSERT INTO Aquariums (AquariumName, UserID)
        OUTPUT INSERTED.AquariumID
        VALUES (@Name, @UserId);";
    cmd.Parameters.AddWithValue("@Name", name);
    cmd.Parameters.AddWithValue("@UserId", userId);

    var newId = (int)(await cmd.ExecuteScalarAsync() ?? 0);

    return Results.Ok(new AquariumDto(newId, name));
});


app.MapGet("/api/aquariums/{userId:int}", async (int userId) =>
{
    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();

    await using var command = connection.CreateCommand();
    command.CommandText = @"
        SELECT AquariumID, AquariumName
        FROM Aquariums     
        WHERE UserID = @UserId";

    command.Parameters.AddWithValue("@UserId", userId);

    var list = new List<AquariumDto>();

    using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        list.Add(new AquariumDto(
            reader.GetInt32(0),
            reader.GetString(1)
        ));
    }

    return Results.Ok(list);
});


// POMIARY!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!


app.MapPost("/api/sensordata", async (SensorDataInput input) =>
{
    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();

    await using var command = connection.CreateCommand();
    command.CommandText = @"
        INSERT INTO SensorData (SensorID, Value, TimeAdded)
        VALUES (@SensorID, @Value, SYSDATETIME());";

    command.Parameters.Add("@SensorID", SqlDbType.Int).Value = input.SensorId;
    command.Parameters.Add("@Value", SqlDbType.Float).Value = input.Value;

    await command.ExecuteNonQueryAsync();
    return Results.Ok();
});

app.MapPut("/api/sensors/{sensorId:int}/meta", async (int sensorId, UpdateSensorMetaRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.SensorName) || string.IsNullOrWhiteSpace(req.SensorType))
        return Results.BadRequest("Nazwa i typ są wymagane.");

    var name = req.SensorName.Trim();
    var type = req.SensorType.Trim();
    var desc = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim();

    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();

   
    int aquariumId;
    await using (var cmdAq = connection.CreateCommand())
    {
        cmdAq.CommandText = "SELECT AquariumID FROM Sensors WHERE SensorID = @Id;";
        cmdAq.Parameters.AddWithValue("@Id", sensorId);

        var obj = await cmdAq.ExecuteScalarAsync();
        if (obj == null) return Results.NotFound("Nie znaleziono czujnika.");
        aquariumId = (int)obj;
    }

    
    await using (var check = connection.CreateCommand())
    {
        check.CommandText = @"
            SELECT COUNT(1)
            FROM Sensors
            WHERE AquariumID = @AqId
              AND SensorID <> @Id
              AND LOWER(LTRIM(RTRIM(SensorName))) = LOWER(@Name);";

        check.Parameters.AddWithValue("@AqId", aquariumId);
        check.Parameters.AddWithValue("@Id", sensorId);
        check.Parameters.AddWithValue("@Name", name);

        var exists = (int)(await check.ExecuteScalarAsync() ?? 0) > 0;
        if (exists)
            return Results.Conflict("Czujnik o takiej nazwie już istnieje w tym akwarium.");
    }

   
    await using (var upd = connection.CreateCommand())
    {
        upd.CommandText = @"
            UPDATE Sensors
            SET SensorName = @Name,
                SensorType = @Type,
                Description = @Desc
            WHERE SensorID = @Id;";
        upd.Parameters.AddWithValue("@Name", name);
        upd.Parameters.AddWithValue("@Type", type);
        upd.Parameters.AddWithValue("@Desc", (object?)desc ?? DBNull.Value);
        upd.Parameters.AddWithValue("@Id", sensorId);

        var rows = await upd.ExecuteNonQueryAsync();
        if (rows == 0) return Results.NotFound("Nie znaleziono czujnika.");
    }

    return Results.Ok();
});



app.MapPut("/api/sensors/{sensorId:int}", async (int sensorId, UpdateSensorRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.SensorName) || string.IsNullOrWhiteSpace(req.SensorType))
        return Results.BadRequest("Nazwa i typ są wymagane.");

    var name = req.SensorName.Trim();
    var type = req.SensorType.Trim();

    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();

    
    int aquariumId;
    await using (var getAq = connection.CreateCommand())
    {
        getAq.CommandText = "SELECT AquariumID FROM Sensors WHERE SensorID=@Id;";
        getAq.Parameters.AddWithValue("@Id", sensorId);

        var obj = await getAq.ExecuteScalarAsync();
        if (obj == null) return Results.NotFound("Nie znaleziono czujnika.");
        aquariumId = (int)obj;
    }

   
    await using (var check = connection.CreateCommand())
    {
        check.CommandText = @"
            SELECT COUNT(1)
            FROM Sensors
            WHERE AquariumID = @AqId
              AND SensorID <> @Id
              AND LOWER(LTRIM(RTRIM(SensorName))) = LOWER(@Name);";
        check.Parameters.AddWithValue("@AqId", aquariumId);
        check.Parameters.AddWithValue("@Id", sensorId);
        check.Parameters.AddWithValue("@Name", name);

        var exists = (int)(await check.ExecuteScalarAsync() ?? 0) > 0;
        if (exists) return Results.Conflict("W tym akwarium istnieje już czujnik o takiej nazwie.");
    }

   
    await using (var upd = connection.CreateCommand())
    {
        upd.CommandText = @"
            UPDATE Sensors
            SET SensorName = @Name,
                SensorType = @Type,
                Description = @Desc
            WHERE SensorID = @Id;";
        upd.Parameters.AddWithValue("@Name", name);
        upd.Parameters.AddWithValue("@Type", type);
        upd.Parameters.AddWithValue("@Desc", (object?)req.Description?.Trim() ?? DBNull.Value);
        upd.Parameters.AddWithValue("@Id", sensorId);

        await upd.ExecuteNonQueryAsync();
    }

    return Results.Ok();
});

app.MapGet("/api/aquariums/{aquariumId:int}/sensors", async (int aquariumId) =>
{
    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();

    var list = new List<SensorEditDto>();

    await using var cmd = connection.CreateCommand();
    cmd.CommandText = @"
        SELECT SensorID, AquariumID, SensorName, SensorType, Description
        FROM Sensors
        WHERE AquariumID = @AqId
        ORDER BY SensorName;";
    cmd.Parameters.AddWithValue("@AqId", aquariumId);

    using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        list.Add(new SensorEditDto(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4)
        ));
    }

    return Results.Ok(list);
});


app.MapPost("/api/aquariums/{aquariumId:int}/sensors", async (int aquariumId, CreateSensorRequest req) =>
{
    if (req.AquariumId != aquariumId || string.IsNullOrWhiteSpace(req.SensorName))
        return Results.BadRequest("Błędne dane.");

    var name = req.SensorName.Trim();
    var type = (req.SensorType ?? "").Trim();

    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();

    
    await using (var check = connection.CreateCommand())
    {
        check.CommandText = @"
            SELECT COUNT(1)
            FROM Sensors
            WHERE AquariumID = @AqId
              AND LOWER(LTRIM(RTRIM(SensorName))) = LOWER(@Name);";
        check.Parameters.AddWithValue("@AqId", aquariumId);
        check.Parameters.AddWithValue("@Name", name);

        var exists = (int)(await check.ExecuteScalarAsync() ?? 0) > 0;
        if (exists)
            return Results.Conflict("Czujnik o takiej nazwie już istnieje w tym akwarium.");
    }

   
    await using var cmd = connection.CreateCommand();
    cmd.CommandText = @"
        INSERT INTO Sensors (AquariumID, SensorName, SensorType, Description)
        OUTPUT INSERTED.SensorID
        VALUES (@AqId, @Name, @Type, @Desc);";

    cmd.Parameters.AddWithValue("@AqId", aquariumId);
    cmd.Parameters.AddWithValue("@Name", name);
    cmd.Parameters.AddWithValue("@Type", type);
    cmd.Parameters.AddWithValue("@Desc", (object?)req.Description ?? DBNull.Value);

    var newId = (int)(await cmd.ExecuteScalarAsync() ?? 0);

    return Results.Ok(new { SensorId = newId });
});


// wysylanie do bazy !!!!
// lista czujników danego akwarium + ostatni pomiar

app.MapGet("/api/aquariums/{aquariumId:int}/sensors/latest", async (int aquariumId) =>
{
    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();

    await using var command = connection.CreateCommand();
    command.CommandText = @"
        SELECT 
            s.SensorID,
            s.SensorName,
            s.SensorType,
            s.Description,
            s.MinValue,
            s.MaxValue,
            sd.Value,
            sd.TimeAdded
        FROM Sensors s
        OUTER APPLY (
            SELECT TOP 1 Value, TimeAdded
            FROM SensorData sd
            WHERE sd.SensorID = s.SensorID
            ORDER BY TimeAdded DESC
        ) sd
        WHERE s.AquariumID = @AquariumID
        ORDER BY s.SensorID;";

    command.Parameters.AddWithValue("@AquariumID", aquariumId);

    using var reader = await command.ExecuteReaderAsync();

    var list = new List<SensorLatestResponse>();

    while (await reader.ReadAsync())
    {
        int sensorId = reader.GetInt32(0);
        string sensorName = reader.GetString(1);
        string sensorType = reader.GetString(2);
        string description = reader.IsDBNull(3) ? "" : reader.GetString(3);

        double? minValue = reader.IsDBNull(4)
            ? (double?)null
            : Convert.ToDouble(reader.GetValue(4));

        double? maxValue = reader.IsDBNull(5)
            ? (double?)null
            : Convert.ToDouble(reader.GetValue(5));

        double? value = reader.IsDBNull(6)
            ? (double?)null
            : Convert.ToDouble(reader.GetValue(6));

        DateTime? timeAdded = reader.IsDBNull(7)
            ? (DateTime?)null
            : reader.GetDateTime(7);

        list.Add(new SensorLatestResponse(
            sensorId,
            sensorName,
            sensorType,
            description,
            minValue,
            maxValue,
            value,
            timeAdded));
    }

    return Results.Ok(list);
});


app.MapGet("/api/sensors/{sensorId:int}/history", async (int sensorId, DateTime? from, DateTime? to, int? take) =>
{
    DateTime fromDt = from ?? DateTime.Now.AddDays(-1);
    DateTime toDt = to ?? DateTime.Now;
    if (fromDt > toDt) (fromDt, toDt) = (toDt, fromDt);

    int limit = take is > 0 and <= 10000 ? take.Value : 10000;

    var list = new List<SensorHistoryResponse>();

    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();

    await using var command = connection.CreateCommand();
    command.CommandText = @"
        SELECT TOP (@Take)
            Value,
            TimeAdded
        FROM SensorData
        WHERE SensorID = @SensorId
          AND TimeAdded BETWEEN @From AND @To
        ORDER BY TimeAdded ASC;";

    command.Parameters.AddWithValue("@SensorId", sensorId);
    command.Parameters.AddWithValue("@From", fromDt);
    command.Parameters.AddWithValue("@To", toDt);
    command.Parameters.AddWithValue("@Take", limit);

    using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        list.Add(new SensorHistoryResponse(
            reader.GetDouble(0),
            reader.GetDateTime(1)
        ));
    }

    return Results.Ok(list);
});

app.MapPut("/api/sensors/{sensorId:int}/thresholds", async (
    int sensorId,
    SensorThresholdsUpdateRequest req) =>
{
    if (req.MinValue.HasValue && req.MaxValue.HasValue && req.MinValue > req.MaxValue)
        return Results.BadRequest("MinValue cannot be greater than MaxValue.");

    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();

    await using var command = connection.CreateCommand();
    command.CommandText = @"
        UPDATE Sensors
        SET MinValue = @MinValue,
            MaxValue = @MaxValue
        WHERE SensorId = @SensorId;

        SELECT @@ROWCOUNT;";
    command.Parameters.AddWithValue("@SensorId", sensorId);

    
    command.Parameters.AddWithValue("@MinValue", (object?)req.MinValue ?? DBNull.Value);
    command.Parameters.AddWithValue("@MaxValue", (object?)req.MaxValue ?? DBNull.Value);

    var rows = (int)(await command.ExecuteScalarAsync() ?? 0);
    if (rows == 0) return Results.NotFound();

    return Results.NoContent(); // 204
});


app.MapPut("/api/sensors/{sensorId:int}", async (int sensorId, SensorUpdateRequest request) =>
{
    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();

    var sets = new List<string>();

    if (!string.IsNullOrWhiteSpace(request.SensorName))
        sets.Add("SensorName = @Name");

    if (!string.IsNullOrWhiteSpace(request.Description))
        sets.Add("Description = @Desc");

    if (request.MinValue.HasValue)
        sets.Add("MinValue = @Min");

    if (request.MaxValue.HasValue)
        sets.Add("MaxValue = @Max");

    if (sets.Count == 0)
        return Results.BadRequest("Brak pól do aktualizacji.");

    string sql = $@"
        UPDATE Sensors
        SET {string.Join(", ", sets)}
        WHERE SensorID = @SensorId;";

    await using var command = connection.CreateCommand();
    command.CommandText = sql;

    command.Parameters.Add("@SensorId", SqlDbType.Int).Value = sensorId;

    if (!string.IsNullOrWhiteSpace(request.SensorName))
        command.Parameters.Add("@Name", SqlDbType.NVarChar, 100).Value = request.SensorName!;

    if (!string.IsNullOrWhiteSpace(request.Description))
        command.Parameters.Add("@Desc", SqlDbType.NVarChar, 255).Value = request.Description!;

    if (request.MinValue.HasValue)
        command.Parameters.Add("@Min", SqlDbType.Float).Value = request.MinValue.Value;

    if (request.MaxValue.HasValue)
        command.Parameters.Add("@Max", SqlDbType.Float).Value = request.MaxValue.Value;

    int rows = await command.ExecuteNonQueryAsync();
    return rows > 0 ? Results.Ok() : Results.NotFound();
});

// REJESTRACJA!!!!!!!!!!

app.MapPost("/api/auth/register", async (RegisterRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Email) ||
        string.IsNullOrWhiteSpace(request.Login) ||
        string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.BadRequest(new { message = "Wszystkie pola są wymagane." });
    }

    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();

   
    await using (var checkCmd = connection.CreateCommand())
    {
        checkCmd.CommandText = @"
            SELECT COUNT(1)
            FROM Users
            WHERE Nick = @Login OR Email = @Email";

        checkCmd.Parameters.AddWithValue("@Login", request.Login);
        checkCmd.Parameters.AddWithValue("@Email", request.Email);

        var exists = (int)(await checkCmd.ExecuteScalarAsync() ?? 0);
        if (exists > 0)
            return Results.BadRequest(new { message = "Użytkownik o takim loginie lub e-mailu już istnieje." });
    }

    var hashed = PasswordHasher.HashPassword(request.Password);

    int newUserId;
    await using (var insertCmd = connection.CreateCommand())
    {
        insertCmd.CommandText = @"
            INSERT INTO Users (Nick, Email, Password, Role)
            OUTPUT INSERTED.UserID
            VALUES (@Login, @Email, @Password, @Role);";

        insertCmd.Parameters.AddWithValue("@Login", request.Login);
        insertCmd.Parameters.AddWithValue("@Email", request.Email);
        insertCmd.Parameters.AddWithValue("@Password", hashed);
        insertCmd.Parameters.AddWithValue("@Role", "User");

        newUserId = (int)(await insertCmd.ExecuteScalarAsync() ?? 0);
    }

    
    return Results.Ok(new LoginResponse
    {
        Success = true,
        UserID = newUserId,
        Role = "User",
        Token = "dummy-token",
        Error = null,
        Nick = request.Login,
        Email = request.Email
    });
});

//zmiana hasla 


app.MapPost("/api/auth/change-password", async (ChangePasswordRequest request) =>
{
    if (request.UserId <= 0 ||
        string.IsNullOrWhiteSpace(request.CurrentPassword) ||
        string.IsNullOrWhiteSpace(request.NewPassword))
    {
        return Results.BadRequest("Wszystkie pola są wymagane.");
    }

    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();

    
    string? storedPassword = null;

    await using (var selectCmd = connection.CreateCommand())
    {
        selectCmd.CommandText = "SELECT Password FROM Users WHERE UserID = @UserId";
        selectCmd.Parameters.AddWithValue("@UserId", request.UserId);

        var result = await selectCmd.ExecuteScalarAsync();
        if (result == null)
        {
            return Results.NotFound("Nie znaleziono użytkownika.");
        }

        storedPassword = (string)result;
    }

    
    if (!PasswordHasher.VerifyPassword(request.CurrentPassword, storedPassword!))
    {
        return Results.BadRequest("Obecne hasło jest nieprawidłowe.");
    }

    
    var newHashed = PasswordHasher.HashPassword(request.NewPassword);

    await using (var updateCmd = connection.CreateCommand())
    {
        updateCmd.CommandText = "UPDATE Users SET Password = @Password WHERE UserID = @UserId";
        updateCmd.Parameters.AddWithValue("@Password", newHashed);
        updateCmd.Parameters.AddWithValue("@UserId", request.UserId);

        await updateCmd.ExecuteNonQueryAsync();
    }

    return Results.Ok("Hasło zostało zmienione.");
});

//threshols!!!!!!!!!!!!!!!!!!!!!!!

app.MapGet("/api/users/{userId:int}/thresholds", async (int userId) =>
{
    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();

    var list = new List<ThresholdDto>();

    await using var cmd = connection.CreateCommand();
    cmd.CommandText = @"
        SELECT SensorID, MinValue, MaxValue
        FROM Sensors"; 

    using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        list.Add(new ThresholdDto
        {
            SensorId = reader.GetInt32(0),
            Min = reader.IsDBNull(1) ? null : reader.GetDouble(1),
            Max = reader.IsDBNull(2) ? null : reader.GetDouble(2)
        });
    }

    return Results.Ok(list);
});


app.MapPost("/api/users/{userId:int}/thresholds",
    async (int userId, ThresholdUpdateRequest request) =>
    {
        if (request.UserId != userId)
            return Results.BadRequest("Niespójny UserId.");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        foreach (var t in request.Thresholds)
        {
            await using var cmd = connection.CreateCommand();

            if (t.Min is null && t.Max is null)
            {
               
                cmd.CommandText = @"
                UPDATE Sensors
                SET MinValue = NULL, MaxValue = NULL
                WHERE SensorID = @SensorId;";
            }
            else
            {
                
                cmd.CommandText = @"
                UPDATE Sensors
                SET MinValue = @Min, MaxValue = @Max
                WHERE SensorID = @SensorId;";
            }

            cmd.Parameters.AddWithValue("@SensorId", t.SensorId);
            cmd.Parameters.AddWithValue("@Min", (object?)t.Min ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Max", (object?)t.Max ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync();
        }

        return Results.Ok();
    });

//laczenie z webowka

app.MapPost("/api/mobile/generate-login-link",
    async (MagicLoginLinkRequest request) =>
    {
        if (request.UserId <= 0)
            return Results.BadRequest("Nieprawidłowy UserId.");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

       
        await using (var checkCmd = connection.CreateCommand())
        {
            checkCmd.CommandText = "SELECT COUNT(1) FROM Users WHERE UserID = @UserId";
            checkCmd.Parameters.AddWithValue("@UserId", request.UserId);

            var exists = (int)(await checkCmd.ExecuteScalarAsync() ?? 0);
            if (exists == 0)
                return Results.BadRequest("Użytkownik nie istnieje.");
        }

        var token = Guid.NewGuid().ToString("N");
        var expiresAt = DateTime.UtcNow.AddMinutes(5);

       
        await using (var insertCmd = connection.CreateCommand())
        {
            insertCmd.CommandText = @"
                INSERT INTO MagicLoginTokens (Token, UserId, ExpiresAt, Used)
                VALUES (@Token, @UserId, @ExpiresAt, 0);";

            insertCmd.Parameters.AddWithValue("@Token", token);
            insertCmd.Parameters.AddWithValue("@UserId", request.UserId);
            insertCmd.Parameters.AddWithValue("@ExpiresAt", expiresAt);

            await insertCmd.ExecuteNonQueryAsync();
        }

        
        var baseUrl = string.IsNullOrWhiteSpace(webBaseUrl)
            ? "http://10.71.78.241:7132"
            : webBaseUrl;

        var link = $"{baseUrl}/Mobile/MagicLogin?token={token}";

        return Results.Ok(new { link });
    });


//zmiana nazwy akwa

app.MapPut("/api/aquariums/{aquariumId:int}/name",
    async (int aquariumId, UpdateAquariumNameRequest req) =>
    {
        if (string.IsNullOrWhiteSpace(req.AquariumName))
            return Results.BadRequest("Nazwa nie może być pusta.");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            UPDATE Aquariums
            SET AquariumName = @Name
            WHERE AquariumID = @Id";

        cmd.Parameters.AddWithValue("@Id", aquariumId);
        cmd.Parameters.AddWithValue("@Name", req.AquariumName);

        var rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0 ? Results.Ok() : Results.NotFound();
    });

//usun akwa

app.MapDelete("/api/users/{userId:int}/aquariums/{aquariumId:int}",
    async (int userId, int aquariumId) =>
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        
        await using (var checkCmd = connection.CreateCommand())
        {
            checkCmd.CommandText = @"
                SELECT COUNT(1)
                FROM Aquariums
                WHERE AquariumID = @AqId AND UserID = @UserId";

            checkCmd.Parameters.AddWithValue("@AqId", aquariumId);
            checkCmd.Parameters.AddWithValue("@UserId", userId);

            var exists = (int)(await checkCmd.ExecuteScalarAsync() ?? 0);
            if (exists == 0)
                return Results.NotFound("Nie znaleziono akwarium użytkownika.");
        }

        
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                -- usuń progi dla czujników tego akwarium
                DELETE FROM SensorThresholds
                WHERE UserID = @UserId
                  AND SensorID IN (
                      SELECT SensorID FROM Sensors WHERE AquariumID = @AqId
                  );

                -- usuń dane pomiarowe
                DELETE FROM SensorData
                WHERE SensorID IN (
                    SELECT SensorID FROM Sensors WHERE AquariumID = @AqId
                );

                -- usuń czujniki
                DELETE FROM Sensors
                WHERE AquariumID = @AqId;

                -- na końcu usuń samo akwarium
                DELETE FROM Aquariums
                WHERE AquariumID = @AqId AND UserID = @UserId;
            ";

            cmd.Parameters.AddWithValue("@AqId", aquariumId);
            cmd.Parameters.AddWithValue("@UserId", userId);

            await cmd.ExecuteNonQueryAsync();
        }

        return Results.Ok();
    });

//sterowanie urzadzeniami

app.MapPost("/api/devices/control",
    async (DeviceCommandRequest req,
           IHttpClientFactory httpClientFactory) =>
    {
        var client = httpClientFactory.CreateClient("EspClient");

        var state = req.On ? "on" : "off";

        var url =
            $"/device?deviceId={Uri.EscapeDataString(req.DeviceId)}" +
            $"&state={Uri.EscapeDataString(state)}";

        try
        {
            var resp = await client.GetAsync(url);
            var text = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
                return Results.Problem($"ESP zwrócił {resp.StatusCode}: {text}");

            return Results.Ok();
        }
        catch (Exception ex)
        {
            return Results.Problem($"Błąd przy wywołaniu ESP (device control): {ex.Message}");
        }
    });

app.MapPost("/api/devices/schedule",
    async (DeviceScheduleRequest req,
           IHttpClientFactory httpClientFactory) =>
    {
        var client = httpClientFactory.CreateClient("EspClient");

        var daysStr = string.Join(",", req.Days);

        var url =
            $"/device/schedule" +
            $"?deviceId={Uri.EscapeDataString(req.DeviceId)}" +
            $"&days={Uri.EscapeDataString(daysStr)}" +
            $"&start={Uri.EscapeDataString(req.Start)}" +
            $"&end={Uri.EscapeDataString(req.End)}";

        try
        {
            var resp = await client.GetAsync(url);
            var text = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
                return Results.Problem($"ESP zwrócił {resp.StatusCode}: {text}");

            return Results.Ok();
        }
        catch (Exception ex)
        {
            return Results.Problem($"Błąd przy wywołaniu ESP (device schedule): {ex.Message}");
        }
    });

app.MapPost("/api/devices/schedule/disable",
    async (DeviceScheduleRequest req,
           IHttpClientFactory httpClientFactory) =>
    {
        var client = httpClientFactory.CreateClient("EspClient");

        var url =
            $"/device/schedule/disable" +
            $"?deviceId={Uri.EscapeDataString(req.DeviceId)}";

        try
        {
            var resp = await client.GetAsync(url);
            var text = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
                return Results.Problem($"ESP zwrócił {resp.StatusCode}: {text}");

            return Results.Ok();
        }
        catch (Exception ex)
        {
            return Results.Problem($"Błąd przy wywołaniu ESP (disable schedule): {ex.Message}");
        }

    });


//usun hamronogram 

// USUWANIE CZUJNIKA (wraz z danymi pomiarowymi)

app.MapDelete("/api/aquariums/{aquariumId:int}/sensors/{sensorId:int}",
    async (int aquariumId, int sensorId) =>
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        
        await using (var check = connection.CreateCommand())
        {
            check.CommandText = @"
                SELECT COUNT(1)
                FROM Sensors
                WHERE SensorID = @SensorId AND AquariumID = @AqId;";
            check.Parameters.AddWithValue("@SensorId", sensorId);
            check.Parameters.AddWithValue("@AqId", aquariumId);

            var exists = (int)(await check.ExecuteScalarAsync() ?? 0);
            if (exists == 0)
                return Results.NotFound("Czujnik nie należy do tego akwarium.");
        }

        // 2) usuń dane zależne i sam czujnik
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                DELETE FROM SensorThresholds WHERE SensorID = @SensorId;
                DELETE FROM SensorData       WHERE SensorID = @SensorId;
                DELETE FROM Sensors          WHERE SensorID = @SensorId AND AquariumID = @AqId;
            ";

            cmd.Parameters.AddWithValue("@SensorId", sensorId);
            cmd.Parameters.AddWithValue("@AqId", aquariumId);

            await cmd.ExecuteNonQueryAsync();
        }





        return Results.Ok();
    });

app.MapGet("/api/admin/stats", async (HttpRequest req) =>
{
    if (!IsAdmin(req)) return Results.Unauthorized();

    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();

    int users = 0, aquariums = 0, sensors = 0, data24h = 0;

    // Users
    await using (var cmd = connection.CreateCommand())
    {
        cmd.CommandText = "SELECT COUNT(1) FROM Users;";
        users = (int)(await cmd.ExecuteScalarAsync() ?? 0);
    }

    // Aquariums
    await using (var cmd = connection.CreateCommand())
    {
        cmd.CommandText = "SELECT COUNT(1) FROM Aquariums;";
        aquariums = (int)(await cmd.ExecuteScalarAsync() ?? 0);
    }

    // Sensors
    await using (var cmd = connection.CreateCommand())
    {
        cmd.CommandText = "SELECT COUNT(1) FROM Sensors;";
        sensors = (int)(await cmd.ExecuteScalarAsync() ?? 0);
    }

    // SensorData last 24h
    await using (var cmd = connection.CreateCommand())
    {
        cmd.CommandText = @"
            SELECT COUNT(1)
            FROM SensorData
            WHERE TimeAdded >= DATEADD(hour, -24, SYSDATETIME());";
        data24h = (int)(await cmd.ExecuteScalarAsync() ?? 0);
    }

    return Results.Ok(new
    {
        usersCount = users,
        aquariumsCount = aquariums,
        sensorsCount = sensors,
        sensorDataLast24h = data24h
    });
});


// 2) Lista użytkowników + filtrowanie (q po nick/email, role)
app.MapGet("/api/admin/users", async (HttpRequest req, string? q, string? role) =>
{
    if (!IsAdmin(req)) return Results.Unauthorized();

    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();

    var list = new List<AdminUserDto>();

    await using var cmd = connection.CreateCommand();

    // budowa WHERE
    var where = new List<string>();
    if (!string.IsNullOrWhiteSpace(q))
    {
        where.Add("(Nick LIKE @Q OR Email LIKE @Q)");
        cmd.Parameters.AddWithValue("@Q", "%" + q.Trim() + "%");
    }
    if (!string.IsNullOrWhiteSpace(role))
    {
        where.Add("Role = @Role");
        cmd.Parameters.AddWithValue("@Role", role.Trim());
    }

    cmd.CommandText = $@"
        SELECT UserID, Nick, Email, Role
        FROM Users
        {(where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "")}
        ORDER BY UserID;";

    using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        list.Add(new AdminUserDto(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3)
        ));
    }

    return Results.Ok(list);
});

app.MapGet("/api/admin/users/{userId:int}/aquariums", async (HttpRequest req, int userId) =>
{
    if (!IsAdmin(req)) return Results.Unauthorized();

    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();

    var list = new List<AdminAquariumDto>();

    await using var cmd = connection.CreateCommand();
    cmd.CommandText = @"
        SELECT
            a.AquariumID,
            a.AquariumName,
            a.UserID,
            u.Nick,
            u.Email,
            (SELECT COUNT(1) FROM Sensors s WHERE s.AquariumID = a.AquariumID) AS SensorsCount
        FROM Aquariums a
        INNER JOIN Users u ON u.UserID = a.UserID
        WHERE a.UserID = @UserId
        ORDER BY a.AquariumID DESC;";
    cmd.Parameters.AddWithValue("@UserId", userId);

    using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        list.Add(new AdminAquariumDto(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetInt32(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetInt32(5)
        ));
    }

    return Results.Ok(list);
});

// 3) Zmiana roli użytkownika
app.MapPut("/api/admin/users/{userId:int}/role", async (HttpRequest req, int userId, AdminSetRoleRequest body) =>
{
    if (!IsAdmin(req)) return Results.Unauthorized();

    var newRole = (body.Role ?? "").Trim();
    if (newRole != "User" && newRole != "Admin" && newRole != "Disabled")
        return Results.BadRequest("Rola musi być: User / Admin / Disabled.");

    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();

    await using var cmd = connection.CreateCommand();
    cmd.CommandText = @"
        UPDATE Users
        SET Role = @Role
        WHERE UserID = @UserId;";
    cmd.Parameters.AddWithValue("@Role", newRole);
    cmd.Parameters.AddWithValue("@UserId", userId);

    var rows = await cmd.ExecuteNonQueryAsync();
    if (rows <= 0) return Results.NotFound("Nie znaleziono użytkownika.");

    await WriteAdminLog(connection, req,
        action: "USER_ROLE",
        targetType: "User",
        targetId: userId,
        details: $"Role={newRole}");

    return Results.Ok();
});

app.MapGet("/api/admin/aquariums", async (HttpRequest req, string? q, int? minSensors, int? maxSensors, bool? onlyEmpty) =>
{
    if (!IsAdmin(req)) return Results.Unauthorized();

    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();

    var list = new List<AdminAquariumDto>();
    await using var cmd = connection.CreateCommand();

    var whereParts = new List<string>();

    if (!string.IsNullOrWhiteSpace(q))
    {
        whereParts.Add("(a.AquariumName LIKE @Q OR u.Nick LIKE @Q OR u.Email LIKE @Q)");
        cmd.Parameters.AddWithValue("@Q", "%" + q.Trim() + "%");
    }

    if (onlyEmpty == true)
    {
        whereParts.Add("(SELECT COUNT(1) FROM Sensors s WHERE s.AquariumID = a.AquariumID) = 0");
    }
    if (minSensors.HasValue)
    {
        whereParts.Add("(SELECT COUNT(1) FROM Sensors s WHERE s.AquariumID = a.AquariumID) >= @MinSensors");
        cmd.Parameters.AddWithValue("@MinSensors", minSensors.Value);
    }
    if (maxSensors.HasValue)
    {
        whereParts.Add("(SELECT COUNT(1) FROM Sensors s WHERE s.AquariumID = a.AquariumID) <= @MaxSensors");
        cmd.Parameters.AddWithValue("@MaxSensors", maxSensors.Value);
    }

    var whereSql = whereParts.Count > 0 ? "WHERE " + string.Join(" AND ", whereParts) : "";

    cmd.CommandText = $@"
        SELECT
            a.AquariumID,
            a.AquariumName,
            a.UserID,
            u.Nick,
            u.Email,
            (SELECT COUNT(1) FROM Sensors s WHERE s.AquariumID = a.AquariumID) AS SensorsCount
        FROM Aquariums a
        INNER JOIN Users u ON u.UserID = a.UserID
        {whereSql}
        ORDER BY a.AquariumID DESC;";

    using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        list.Add(new AdminAquariumDto(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetInt32(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetInt32(5)
        ));
    }

    return Results.Ok(list);
});



app.MapDelete("/api/admin/aquariums/{aquariumId:int}", async (HttpRequest req, int aquariumId) =>
{
    if (!IsAdmin(req)) return Results.Unauthorized();

    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();

    
    int userId;
    await using (var checkCmd = connection.CreateCommand())
    {
        checkCmd.CommandText = @"
            SELECT UserID
            FROM Aquariums
            WHERE AquariumID = @AqId;";
        checkCmd.Parameters.AddWithValue("@AqId", aquariumId);

        var result = await checkCmd.ExecuteScalarAsync();
        if (result == null) return Results.NotFound("Nie znaleziono akwarium.");

        userId = (int)result;
    }

  
    await using (var cmd = connection.CreateCommand())
    {
        cmd.CommandText = @"
            DELETE FROM SensorThresholds
            WHERE UserID = @UserId
              AND SensorID IN (SELECT SensorID FROM Sensors WHERE AquariumID = @AqId);

            DELETE FROM SensorData
            WHERE SensorID IN (SELECT SensorID FROM Sensors WHERE AquariumID = @AqId);

            DELETE FROM Sensors
            WHERE AquariumID = @AqId;

            DELETE FROM Aquariums
            WHERE AquariumID = @AqId;
        ";

        cmd.Parameters.AddWithValue("@AqId", aquariumId);
        cmd.Parameters.AddWithValue("@UserId", userId);

        await cmd.ExecuteNonQueryAsync();
    }

    await WriteAdminLog(connection, req,
    action: "AQUARIUM_DELETE",
    targetType: "Aquarium",
    targetId: aquariumId,
    details: $"OwnerUserId={userId}");

    return Results.Ok();
});

async Task WriteAdminLog(SqlConnection connection, HttpRequest req,
    string action, string targetType, int? targetId = null, string? details = null)
{
    var actor = req.Headers.TryGetValue("X-Admin-Actor", out var a) ? a.ToString() : null;
    var ip = req.HttpContext.Connection.RemoteIpAddress?.ToString();

    await using var cmd = connection.CreateCommand();
    cmd.CommandText = @"
        INSERT INTO AdminLogs (AdminActor, Action, TargetType, TargetId, Details, Ip)
        VALUES (@Actor, @Action, @TargetType, @TargetId, @Details, @Ip);";

    cmd.Parameters.AddWithValue("@Actor", (object?)actor ?? DBNull.Value);
    cmd.Parameters.AddWithValue("@Action", action);
    cmd.Parameters.AddWithValue("@TargetType", targetType);
    cmd.Parameters.AddWithValue("@TargetId", (object?)targetId ?? DBNull.Value);
    cmd.Parameters.AddWithValue("@Details", (object?)details ?? DBNull.Value);
    cmd.Parameters.AddWithValue("@Ip", (object?)ip ?? DBNull.Value);

    await cmd.ExecuteNonQueryAsync();
}

app.MapGet("/api/admin/sensors", async (
    HttpRequest req,
    string? q,
    int? aquariumId,
    int? userId,
    int? inactiveHours) =>
{
    if (!IsAdmin(req)) return Results.Unauthorized();

    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();

    var list = new List<AdminSensorDto>();
    await using var cmd = connection.CreateCommand();

    var where = new List<string>();

    if (!string.IsNullOrWhiteSpace(q))
    {
        where.Add("(s.SensorName LIKE @Q OR s.SensorType LIKE @Q OR a.AquariumName LIKE @Q OR u.Nick LIKE @Q OR u.Email LIKE @Q)");
        cmd.Parameters.AddWithValue("@Q", "%" + q.Trim() + "%");
    }

    if (aquariumId.HasValue)
    {
        where.Add("a.AquariumID = @AqId");
        cmd.Parameters.AddWithValue("@AqId", aquariumId.Value);
    }

    if (userId.HasValue)
    {
        where.Add("a.UserID = @UserId");
        cmd.Parameters.AddWithValue("@UserId", userId.Value);
    }

    if (inactiveHours.HasValue && inactiveHours.Value > 0)
    {
        
        where.Add("(sd.TimeAdded IS NULL OR sd.TimeAdded < DATEADD(hour, -@H, SYSDATETIME()))");
        cmd.Parameters.AddWithValue("@H", inactiveHours.Value);
    }

    cmd.CommandText = $@"
        SELECT
            s.SensorID,
            s.AquariumID,
            a.AquariumName,
            a.UserID,
            u.Nick,
            u.Email,
            s.SensorName,
            s.SensorType,
            s.MinValue,
            s.MaxValue,
            sd.Value,
            sd.TimeAdded
        FROM Sensors s
        INNER JOIN Aquariums a ON a.AquariumID = s.AquariumID
        INNER JOIN Users u ON u.UserID = a.UserID
        OUTER APPLY (
            SELECT TOP 1 Value, TimeAdded
            FROM SensorData
            WHERE SensorID = s.SensorID
            ORDER BY TimeAdded DESC
        ) sd
        {(where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "")}
        ORDER BY s.SensorID DESC;";

    using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        list.Add(new AdminSensorDto(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetString(2),
            reader.GetInt32(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetString(6),
            reader.GetString(7),
            reader.IsDBNull(8) ? null : Convert.ToDouble(reader.GetValue(8)),
            reader.IsDBNull(9) ? null : Convert.ToDouble(reader.GetValue(9)),
            reader.IsDBNull(10) ? null : Convert.ToDouble(reader.GetValue(10)),
            reader.IsDBNull(11) ? null : reader.GetDateTime(11)
        ));
    }

    return Results.Ok(list);
});

app.MapPut("/api/admin/sensors/{sensorId:int}/minmax", async (HttpRequest req, int sensorId, AdminSensorMinMaxRequest body) =>
{
    if (!IsAdmin(req)) return Results.Unauthorized();

    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();

    await using var cmd = connection.CreateCommand();
    cmd.CommandText = @"
        UPDATE Sensors
        SET MinValue = @Min, MaxValue = @Max
        WHERE SensorID = @Id;";
    cmd.Parameters.AddWithValue("@Id", sensorId);
    cmd.Parameters.AddWithValue("@Min", (object?)body.MinValue ?? DBNull.Value);
    cmd.Parameters.AddWithValue("@Max", (object?)body.MaxValue ?? DBNull.Value);

    var rows = await cmd.ExecuteNonQueryAsync();
    if (rows <= 0) return Results.NotFound();

    await WriteAdminLog(connection, req, "SENSOR_MINMAX", "Sensor", sensorId,
        $"Min={body.MinValue?.ToString() ?? "null"}, Max={body.MaxValue?.ToString() ?? "null"}");

    return Results.Ok();
});

app.MapDelete("/api/admin/sensors/{sensorId:int}", async (HttpRequest req, int sensorId) =>
{
    if (!IsAdmin(req)) return Results.Unauthorized();

    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();

   
    int aquariumId;
    int userId;

    await using (var check = connection.CreateCommand())
    {
        check.CommandText = @"
            SELECT a.AquariumID, a.UserID
            FROM Sensors s
            INNER JOIN Aquariums a ON a.AquariumID = s.AquariumID
            WHERE s.SensorID = @Id;";
        check.Parameters.AddWithValue("@Id", sensorId);

        using var r = await check.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return Results.NotFound("Nie znaleziono czujnika.");

        aquariumId = r.GetInt32(0);
        userId = r.GetInt32(1);
    }

    await using (var cmd = connection.CreateCommand())
    {
        cmd.CommandText = @"
            DELETE FROM SensorThresholds WHERE UserID = @UserId AND SensorID = @SensorId;
            DELETE FROM SensorData       WHERE SensorID = @SensorId;
            DELETE FROM Sensors          WHERE SensorID = @SensorId;";
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@SensorId", sensorId);
        await cmd.ExecuteNonQueryAsync();
    }

    await WriteAdminLog(connection, req, "SENSOR_DELETE", "Sensor", sensorId, $"AquariumID={aquariumId}, UserID={userId}");

    return Results.Ok();
});

app.MapGet("/api/admin/logs", async (HttpRequest req, int? take, string? q, string? action, string? targetType, int? targetId) =>
{
    if (!IsAdmin(req)) return Results.Unauthorized();

    int limit = take is > 0 and <= 500 ? take.Value : 100;

    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();

    var list = new List<AdminLogDto>();
    await using var cmd = connection.CreateCommand();

    var where = new List<string>();

    if (!string.IsNullOrWhiteSpace(targetType))
    {
        where.Add("TargetType = @TargetType");
        cmd.Parameters.AddWithValue("@TargetType", targetType.Trim());
    }
    if (targetId.HasValue)
    {
        where.Add("TargetId = @TargetId");
        cmd.Parameters.AddWithValue("@TargetId", targetId.Value);
    }

    if (!string.IsNullOrWhiteSpace(q))
    {
        where.Add("(AdminActor LIKE @Q OR Details LIKE @Q OR Ip LIKE @Q)");
        cmd.Parameters.AddWithValue("@Q", "%" + q.Trim() + "%");
    }
    if (!string.IsNullOrWhiteSpace(action))
    {
        where.Add("Action = @Action");
        cmd.Parameters.AddWithValue("@Action", action.Trim());
    }

    cmd.CommandText = $@"
        SELECT TOP (@Take)
            LogID, TimeAdded, AdminActor, Action, TargetType, TargetId, Details, Ip
        FROM AdminLogs
        {(where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "")}
        ORDER BY LogID DESC;";

    cmd.Parameters.AddWithValue("@Take", limit);

    using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        list.Add(new AdminLogDto(
            reader.GetInt32(0),
            reader.GetDateTime(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetInt32(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetString(7)
        ));
    }

    return Results.Ok(list);
});

app.MapGet("/api/admin/notifications", async (HttpRequest req, int? inactiveHours) =>
{
    if (!IsAdmin(req)) return Results.Unauthorized();

    int hours = inactiveHours is > 0 and <= 168 ? inactiveHours.Value : 6;

    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();

    int inactive = 0;

    await using (var cmd = connection.CreateCommand())
    {
        cmd.CommandText = @"
            SELECT COUNT(1)
            FROM Sensors s
            OUTER APPLY (
                SELECT TOP 1 TimeAdded
                FROM SensorData
                WHERE SensorID = s.SensorID
                ORDER BY TimeAdded DESC
            ) sd
            WHERE sd.TimeAdded IS NULL OR sd.TimeAdded < DATEADD(hour, -@H, SYSDATETIME());";
        cmd.Parameters.AddWithValue("@H", hours);
        inactive = (int)(await cmd.ExecuteScalarAsync() ?? 0);
    }

    // ostatnie 10 logów
    var logs = new List<AdminLogDto>();
    await using (var cmd = connection.CreateCommand())
    {
        cmd.CommandText = @"
            SELECT TOP 10 LogID, TimeAdded, AdminActor, Action, TargetType, TargetId, Details, Ip
            FROM AdminLogs
            ORDER BY LogID DESC;";
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            logs.Add(new AdminLogDto(
                r.GetInt32(0),
                r.GetDateTime(1),
                r.IsDBNull(2) ? null : r.GetString(2),
                r.GetString(3),
                r.GetString(4),
                r.IsDBNull(5) ? null : r.GetInt32(5),
                r.IsDBNull(6) ? null : r.GetString(6),
                r.IsDBNull(7) ? null : r.GetString(7)
            ));
        }
    }

    return Results.Ok(new AdminNotificationsDto(inactive, logs));
});

app.Run();

// ================== DTO / RECORDY ==================

public record LoginRequest(string Login, string Password);

public record RegisterRequest(string Email, string Login, string Password);

public record AquariumDto(int AquariumId, string AquariumName);
public record SensorDataInput(int SensorId, double Value);

public record MagicLoginLinkRequest(int UserId);

public record UpdateAquariumNameRequest(string AquariumName);
public record DeviceCommandRequest(int AquariumId, string DeviceId, bool On, string? Endpoint);
public record DeviceScheduleRequest(int AquariumId, string DeviceId, List<int> Days, string Start, string End, string? Endpoint);

public record AdminUserDto(int UserId, string Nick, string Email, string Role);
public record AdminSetRoleRequest(string Role);
public record AdminAquariumDto(int AquariumId, string AquariumName, int UserId, string OwnerNick, string OwnerEmail, int SensorsCount);

public record AdminSensorDto(
    int SensorId,
    int AquariumId,
    string AquariumName,
    int UserId,
    string OwnerNick,
    string OwnerEmail,
    string SensorName,
    string SensorType,
    double? MinValue,
    double? MaxValue,
    double? LatestValue,
    DateTime? LatestTime
);

public record AdminSensorMinMaxRequest(double? MinValue, double? MaxValue);
public record AdminLogDto(int LogId, DateTime TimeAdded, string? AdminActor, string Action, string TargetType, int? TargetId, string? Details, string? Ip);
public record AdminNotificationsDto(int inactiveSensors, List<AdminLogDto> recentLogs);

public record UpdateSensorMetaRequest(string SensorName, string SensorType, string? Description);

public record SensorThresholdsUpdateRequest(double? MinValue, double? MaxValue);

public record SensorMetaUpdateRequest(string SensorName, string SensorType, string? Description);
