namespace Paper.Core.Hooks
{
    /// <summary>
    /// Represents the state of an async operation started by <see cref="Hooks.UseAsync{T}"/>.
    /// </summary>
    public sealed class AsyncState<T>
    {
        /// <summary>True while the async operation has not yet completed.</summary>
        public bool IsLoading { get; }

        /// <summary>The result value when the operation completed successfully. Default when loading or errored.</summary>
        public T? Value { get; }

        /// <summary>The exception if the operation failed; null on success or while loading.</summary>
        public Exception? Error { get; }

        /// <summary>True when the operation completed without throwing.</summary>
        public bool IsSuccess => !IsLoading && Error == null;

        /// <summary>True when the operation threw an exception.</summary>
        public bool IsError => !IsLoading && Error != null;

        private AsyncState(bool loading, T? value, Exception? error)
        {
            IsLoading = loading;
            Value     = value;
            Error     = error;
        }

        public static AsyncState<T> Loading()              => new(true,  default, null);
        public static AsyncState<T> Success(T value)       => new(false, value,   null);
        public static AsyncState<T> Failure(Exception ex)  => new(false, default, ex);
    }
}
