using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace GenericHelpers;

/// <summary>
/// Represents a result which may either be successful and contain a value or an error and contain an exception.
/// </summary>
/// <typeparam name="T"></typeparam>
public readonly record struct Result<T>
{
    private readonly Exception? exception;
    private readonly T? value;

#pragma warning disable CS8603 // Possible null reference return.
    /// <summary>
    /// Returns the exception of the result if unsuccessful.
    /// </summary>
    /// <exception cref="NullReferenceException"/>
    public readonly Exception? Exception => exception ?? throw new NullReferenceException("Result is success and has no exception");

    /// <summary>
    /// Returns the success value of the result if successful.
    /// </summary>
    /// <exception cref="NullReferenceException"/>
    public readonly T? Value => value ?? throw new NullReferenceException("Result is an error and has no value");

#pragma warning restore CS8603 // Possible null reference return.

    /// <summary>
    /// Returns true if result was successful.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Exception))]
    public bool IsSuccess { get; }

    /// <summary>
    /// Returns true if result was unsuccessful.
    /// </summary>
    [MemberNotNullWhen(false, nameof(Value))]
    [MemberNotNullWhen(true, nameof(Exception))]
    public readonly bool IsError => !IsSuccess;

    /// <summary>
    /// Constructs an unsuccessful result.
    /// </summary>
    /// <param name="exception"></param>
    public Result(Exception exception)
    {
        this.exception = exception;
        IsSuccess = false;
    }

    /// <summary>
    /// Constructs a successful result.
    /// </summary>
    /// <param name="value"></param>
    public Result(T value)
    {
        this.value = value;
        IsSuccess = true;
    }

    /// <summary>
    /// Automatically converts <see cref="{T}"/> to <see cref="Result{T}"/> for easy return.
    /// </summary>
    /// <param name="value"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Pure]
    public static implicit operator Result<T>(T value)
    {
        return new Result<T>(value);
    }

    /// <summary>
    /// Automatically converts <see cref="System.Exception"/> to <see cref="Result{T}"/> for easy return.
    /// </summary>
    /// <param name="ex"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Pure]
    public static implicit operator Result<T>(Exception ex)
    {
        return new Result<T>(ex);
    }

    /// <inheritdoc/>
    [Pure]
    public override readonly string ToString()
    {
        if (IsSuccess)
            return Value.ToString() ?? "(null)";
        else
            return Exception.ToString();
    }

    /// <inheritdoc/>
    [Pure]
    public override int GetHashCode()
    {
        return IsSuccess ? Value.GetHashCode() : Exception.GetHashCode();
    }

    /// <summary>
    /// Returns <paramref name="Succ"/>(<see cref="Value"/>) if successful
    /// otherwise returns <paramref name="Fail"/>(<see cref="Value"/>).
    /// </summary>
    /// <typeparam name="R"></typeparam>
    /// <param name="Succ"></param>
    /// <param name="Fail"></param>
    /// <returns></returns>
    [Pure]
    public R Match<R>(Func<T, R> Succ, Func<Exception, R> Fail)
    {
        return IsSuccess ? Succ(Value) : Fail(Exception);
    }

    [Pure]
    internal OptionalResult<T> ToOptional()
    {
        return IsSuccess ? new OptionalResult<T>(Value) : new OptionalResult<T>(Exception);
    }

    /// <summary>
    /// Converts a result of one type to a result of another.
    /// </summary>
    /// <typeparam name="B"></typeparam>
    /// <param name="f"></param>
    /// <returns></returns>
    [Pure]
    public Result<B> Map<B>(Func<T, B> f)
    {
        return IsSuccess ? new Result<B>(f(Value)) : new Result<B>(Exception);
    }

    /// <summary>
    /// Converts a result of one type to a result of another asychronously.
    /// </summary>
    /// <typeparam name="B"></typeparam>
    /// <param name="f"></param>
    /// <returns></returns>
    [Pure]
    public async Task<Result<B>> MapAsync<B>(Func<T, Task<B>> f)
    {
        return IsSuccess ? new Result<B>(await f(Value)) : new Result<B>(Exception);
    }
}
