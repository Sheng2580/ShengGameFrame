namespace Sheng.GameFramework.Json
{
    /// <summary>
    /// JSON 操作失败类型
    /// </summary>
    public enum JsonErrorCode
    {
        None,
        InvalidPath,
        NotFound,
        SerializeFailed,
        DeserializeFailed,
        ReadFailed,
        WriteFailed,
        DeleteFailed,
        Cancelled
    }

    /// <summary>
    /// JSON 读取位置
    /// </summary>
    public enum JsonDataLocation
    {
        PersistentData,
        StreamingAssets
    }

    /// <summary>
    /// JSON 写入结果
    /// </summary>
    public sealed class JsonWriteResult
    {
        private JsonWriteResult(
            bool success,
            string path,
            JsonErrorCode errorCode,
            string errorMessage)
        {
            Success = success;
            Path = path;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
        }

        public bool Success { get; }
        public string Path { get; }
        public JsonErrorCode ErrorCode { get; }
        public string ErrorMessage { get; }

        internal static JsonWriteResult Succeeded(string path)
        {
            return new JsonWriteResult(true, path, JsonErrorCode.None, string.Empty);
        }

        internal static JsonWriteResult Failed(
            string path,
            JsonErrorCode errorCode,
            string errorMessage)
        {
            return new JsonWriteResult(false, path, errorCode, errorMessage);
        }
    }

    /// <summary>
    /// JSON 读取结果
    /// </summary>
    public sealed class JsonReadResult<T>
    {
        private JsonReadResult(
            bool success,
            T value,
            string path,
            JsonErrorCode errorCode,
            string errorMessage)
        {
            Success = success;
            Value = value;
            Path = path;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
        }

        public bool Success { get; }
        public T Value { get; }
        public string Path { get; }
        public JsonErrorCode ErrorCode { get; }
        public string ErrorMessage { get; }

        internal static JsonReadResult<T> Succeeded(T value, string path)
        {
            return new JsonReadResult<T>(
                true,
                value,
                path,
                JsonErrorCode.None,
                string.Empty);
        }

        internal static JsonReadResult<T> Failed(
            string path,
            JsonErrorCode errorCode,
            string errorMessage)
        {
            return new JsonReadResult<T>(
                false,
                default,
                path,
                errorCode,
                errorMessage);
        }
    }
}
