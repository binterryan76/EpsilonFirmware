
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace GenericHelpers;

/// <summary>
/// Represents a result which may either be something and contain a value, 
/// an error and contain an exception,
/// or none and contain neither.
/// </summary>
/// <typeparam name="T"></typeparam>
public readonly record struct OptionalResult<T>
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
    /// Returns true if result was successful and has a value.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Value))]
    public bool IsSome { get; }

    /// <summary>
    /// Returns true if result was successful but has no value.
    /// </summary>
    public bool IsNone { get; }

    /// <summary>
    /// Returns true if result was unsuccessful.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Exception))]
    public readonly bool IsError { get; }

    /// <summary>
    /// Returns true if result was unsuccessful or if result was successful but has no value.
    /// </summary>
    [MemberNotNullWhen(false, nameof(Value))]
    public readonly bool IsNoneOrError => IsError || IsNone;


    /// <summary>
    /// Constructs an unsuccessful result.
    /// </summary>
    /// <param name="exception"></param>
    public OptionalResult(Exception exception)
    {
        this.exception = exception;
        IsSome = false;
        IsNone = false;
        IsError = true;
    }

    /// <summary>
    /// Constructs a successful or none result.
    /// </summary>
    /// <param name="value"></param>
    public OptionalResult(T? value)
    {
        this.value = value;
        IsSome = value is not null;
        IsNone = value is null;
        IsError = false;
    }

    /// <summary>
    /// Constructs a none result.
    /// </summary>
    public OptionalResult()
    {
        IsSome = false;
        IsNone = true;
        IsError = false;
    }

    /// <summary>
    /// Automatically converts <see cref="{T}"/> to <see cref="OptionalResult{T}"/> for easy return.
    /// </summary>
    /// <param name="value"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Pure]
    public static implicit operator OptionalResult<T>(T value)
    {
        return new OptionalResult<T>(value);
    }

    /// <summary>
    /// Automatically converts <see cref="Exception"/> to an error <see cref="OptionalResult{T}"/> for easy return.
    /// </summary>
    /// <param name="value"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Pure]
    public static implicit operator OptionalResult<T>(Exception ex)
    {
        return new OptionalResult<T>(ex);
    }

    /// <inheritdoc/>
    [Pure]
    public override readonly string ToString()
    {
        if (IsSome)
            return Value.ToString() ?? "(null)";
        else if (IsError)
            return Exception.ToString();
        else
            return "None";
    }

    /// <inheritdoc/>
    [Pure]
    public override int GetHashCode()
    {
        if (IsSome)
            return Value.GetHashCode();
        else if (IsError)
            return Exception.GetHashCode();
        else
            return 0;
    }

    /// <summary>
    /// Returns <paramref name="Some"/>(<see cref="Value"/>) if successful,
    /// <paramref name="Error"/>(<see cref="Exception"/>) if unsuccessful,
    /// otherwise <paramref name="None"/>().
    /// </summary>
    /// <typeparam name="R"></typeparam>
    /// <param name="Some"></param>
    /// <param name="None"></param>
    /// <param name="Error"></param>
    /// <returns></returns>
    [Pure]
    public R Match<R>(Func<T, R> Some, Func<R> None, Func<Exception, R> Error)
    {
        if (IsSome)
            return Some(Value);
        else if (IsError)
            return Error(Exception);
        else
            return None();
    }

    /// <summary>
    /// Converts a result of one type to a result of another.
    /// </summary>
    /// <typeparam name="B"></typeparam>
    /// <param name="f"></param>
    /// <returns></returns>
    [Pure]
    public OptionalResult<B> Map<B>(Func<T, B> f)
    {
        if (IsSome)
            return new OptionalResult<B>(f(Value));
        else if (IsError)
            return new OptionalResult<B>(Exception);
        else
            return new OptionalResult<B>();
    }

    /// <summary>
    /// Converts a result of one type to a result of another asychronously.
    /// </summary>
    /// <typeparam name="B"></typeparam>
    /// <param name="f"></param>
    /// <returns></returns>
    [Pure]
    public async Task<OptionalResult<B>> MapAsync<B>(Func<T, Task<B>> f)
    {
        if (IsSome)
            return new OptionalResult<B>(await f(Value));
        else if (IsError)
            return new OptionalResult<B>(Exception);
        else
            return new OptionalResult<B>();
    }
}
