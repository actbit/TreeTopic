namespace TreeTopic.Common;

/// <summary>
/// 操作結果を表現する汎用結果型
/// Success/Failure状態、Data、Error、StatusCodeを保持
/// </summary>
public class Result<T>
{
    /// <summary>
    /// 操作が成功したかどうかを示す
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// 操作が失敗したかどうかを示す（IsSuccessの逆）
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// 成功時のデータペイロード
    /// </summary>
    public T? Data { get; }

    /// <summary>
    /// 失敗時のエラー情報
    /// </summary>
    public Error? Error { get; }

    /// <summary>
    /// HTTPステータスコード（200, 201, 400, 404, 409, 500等）
    /// </summary>
    public int StatusCode { get; private set; }

    /// <summary>
    /// プライベートコンストラクタ（ファクトリーメソッドから呼ばれる）
    /// </summary>
    private Result(bool isSuccess, T? data, Error? error, int statusCode = 200)
    {
        IsSuccess = isSuccess;
        Data = data;
        Error = error;
        StatusCode = statusCode;
    }

    #region ファクトリーメソッド

    /// <summary>
    /// 成功結果を作成（デフォルトステータス: 200 OK）
    /// </summary>
    public static Result<T> Success(T data, int statusCode = 200)
        => new(true, data, null, statusCode);

    /// <summary>
    /// 成功結果を作成（ステータス: 201 Created）
    /// </summary>
    public static Result<T> Created(T data)
        => new(true, data, null, 201);

    /// <summary>
    /// 失敗結果を作成（ステータス: 204 No Content）
    /// </summary>
    public static Result<T> NoContent()
        => new(false, default, null, 204);

    /// <summary>
    /// 失敗結果を作成（カスタムError）
    /// </summary>
    public static Result<T> Failure(Error error, int statusCode = 400)
        => new(false, default, error, statusCode);

    /// <summary>
    /// 失敗結果を作成（ステータス: 404 Not Found）
    /// </summary>
    public static Result<T> NotFound(string message = "Resource not found")
        => new(false, default, new Error(ErrorType.NotFound, message), 404);

    /// <summary>
    /// 失敗結果を作成（ステータス: 400 Bad Request）
    /// </summary>
    public static Result<T> BadRequest(string message)
        => new(false, default, new Error(ErrorType.Validation, message), 400);

    /// <summary>
    /// 失敗結果を作成（ステータス: 409 Conflict）
    /// </summary>
    public static Result<T> Conflict(string message)
        => new(false, default, new Error(ErrorType.Conflict, message), 409);

    /// <summary>
    /// 失敗結果を作成（ステータス: 500 Internal Server Error）
    /// </summary>
    public static Result<T> InternalError(string message = "An error occurred")
        => new(false, default, new Error(ErrorType.Internal, message), 500);

    /// <summary>
    /// 失敗結果を作成（ステータス: 401 Unauthorized）
    /// </summary>
    public static Result<T> Unauthorized(string message = "Unauthorized")
        => new(false, default, new Error(ErrorType.Unauthorized, message), 401);

    /// <summary>
    /// 失敗結果を作成（ステータス: 403 Forbidden）
    /// </summary>
    public static Result<T> Forbidden(string message = "Forbidden")
        => new(false, default, new Error(ErrorType.Forbidden, message), 403);

    #endregion

    #region 変換メソッド

    /// <summary>
    /// 成功時のデータを変換
    /// </summary>
    public Result<TNew> Map<TNew>(Func<T, TNew> mapper)
    {
        if (IsFailure)
            return Result<TNew>.Failure(Error!, StatusCode);

        return Result<TNew>.Success(mapper(Data!), StatusCode);
    }

    /// <summary>
    /// 成功時のデータを非同期で変換
    /// </summary>
    public async Task<Result<TNew>> MapAsync<TNew>(Func<T, Task<TNew>> mapper)
    {
        if (IsFailure)
            return Result<TNew>.Failure(Error!, StatusCode);

        var mapped = await mapper(Data!);
        return Result<TNew>.Success(mapped, StatusCode);
    }

    #endregion
}

/// <summary>
/// データなしの操作結果を表現する結果型
/// </summary>
public class Result
{
    /// <summary>
    /// 操作が成功したかどうかを示す
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// 操作が失敗したかどうかを示す（IsSuccessの逆）
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// 失敗時のエラー情報
    /// </summary>
    public Error? Error { get; }

    /// <summary>
    /// HTTPステータスコード（200, 201, 204, 400, 404, 409, 500等）
    /// </summary>
    public int StatusCode { get; private set; }

    /// <summary>
    /// プライベートコンストラクタ（ファクトリーメソッドから呼ばれる）
    /// </summary>
    private Result(bool isSuccess, Error? error, int statusCode)
    {
        IsSuccess = isSuccess;
        Error = error;
        StatusCode = statusCode;
    }

    #region ファクトリーメソッド

    /// <summary>
    /// 成功結果を作成（デフォルトステータス: 200 OK）
    /// </summary>
    public static Result Success(int statusCode = 200)
        => new(true, null, statusCode);

    /// <summary>
    /// 成功結果を作成（ステータス: 204 No Content）
    /// </summary>
    public static Result NoContent()
        => new(true, null, 204);

    /// <summary>
    /// 失敗結果を作成（カスタムError）
    /// </summary>
    public static Result Failure(Error error, int statusCode = 400)
        => new(false, error, statusCode);

    /// <summary>
    /// 失敗結果を作成（ステータス: 404 Not Found）
    /// </summary>
    public static Result NotFound(string message = "Resource not found")
        => new(false, new Error(ErrorType.NotFound, message), 404);

    /// <summary>
    /// 失敗結果を作成（ステータス: 400 Bad Request）
    /// </summary>
    public static Result BadRequest(string message)
        => new(false, new Error(ErrorType.Validation, message), 400);

    /// <summary>
    /// 失敗結果を作成（ステータス: 409 Conflict）
    /// </summary>
    public static Result Conflict(string message)
        => new(false, new Error(ErrorType.Conflict, message), 409);

    /// <summary>
    /// 失敗結果を作成（ステータス: 500 Internal Server Error）
    /// </summary>
    public static Result InternalError(string message = "An error occurred")
        => new(false, new Error(ErrorType.Internal, message), 500);

    /// <summary>
    /// 失敗結果を作成（ステータス: 401 Unauthorized）
    /// </summary>
    public static Result Unauthorized(string message = "Unauthorized")
        => new(false, new Error(ErrorType.Unauthorized, message), 401);

    /// <summary>
    /// 失敗結果を作成（ステータス: 403 Forbidden）
    /// </summary>
    public static Result Forbidden(string message = "Forbidden")
        => new(false, new Error(ErrorType.Forbidden, message), 403);

    #endregion
}

/// <summary>
/// エラー情報を表現するレコード型
/// </summary>
/// <param name="Type">エラーの種類</param>
/// <param name="Message">エラーメッセージ</param>
/// <param name="ValidationErrors">検証エラー（オプション）</param>
public record Error(
    ErrorType Type,
    string Message,
    Dictionary<string, string[]>? ValidationErrors = null);

/// <summary>
/// エラーの種類を表す列挙型
/// </summary>
public enum ErrorType
{
    /// <summary>検証エラー</summary>
    Validation,

    /// <summary>リソースが見つからない</summary>
    NotFound,

    /// <summary>リソースが既に存在する等の競合</summary>
    Conflict,

    /// <summary>認証エラー</summary>
    Unauthorized,

    /// <summary>認可エラー</summary>
    Forbidden,

    /// <summary>予期しないエラー</summary>
    Internal
}
