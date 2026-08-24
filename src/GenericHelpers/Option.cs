using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace GenericHelpers;

internal readonly record struct Option<T>
{
    private readonly T? value;

#pragma warning disable CS8603 // Possible null reference return.

    /// <summary>
    /// Returns the value of the option if something.
    /// </summary>
    /// <exception cref="NullReferenceException"/>
    public readonly T? Value => value ?? throw new NullReferenceException("Option is nothing and has no value");

#pragma warning restore CS8603 // Possible null reference return.

    /// <summary>
    /// Returns true if option is something.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Value))]
    public bool IsSome { get; }

    /// <summary>
    /// Returns true if option is nothing.
    /// </summary>
    [MemberNotNullWhen(false, nameof(Value))]
    public readonly bool IsNone => !IsSome;

    /// <summary>
    /// Constructs an option from a possible value.
    /// </summary>
    /// <param name="value"></param>
    public Option(T? value)
    {
        this.value = value;
        IsSome = value is not null;
    }

    /// <summary>
    /// Constructs an option from a.
    /// </summary>
    public Option()
    {
        IsSome = false;
    }

    /// <summary>
    /// Automatically converts <see cref="{T}"/> to <see cref="Option{T}"/> for easy return.
    /// </summary>
    /// <param name="value"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Pure]
    public static implicit operator Option<T>(T? value)
    {
        return new Option<T>(value);
    }

    /// <inheritdoc/>
    [Pure]
    public override readonly string ToString()
    {
        if (IsSome)
            return Value.ToString() ?? "(null)";
        else
            return "None";
    }

    /// <inheritdoc/>
    [Pure]
    public override int GetHashCode()
    {
        return IsSome ? Value.GetHashCode() : 0;
    }

    /// <summary>
    /// Returns <paramref name="Some"/>(<see cref="Value"/>) if this is something
    /// otherwise returns <paramref name="None"/>().
    /// </summary>
    /// <typeparam name="R">Return type</typeparam>
    /// <param name="Some"></param>
    /// <param name="None"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Pure]
    public R Match<R>(Func<T, R> Some, Func<R> None)
    {
        return IsSome ? Some(Value) : None();
    }

    /// <summary>
    /// Returns <paramref name="Some"/>(<see cref="Value"/>) if this is something
    /// otherwise returns <paramref name="None"/>.
    /// </summary>
    /// <typeparam name="R">Return type</typeparam>
    /// <param name="Some"></param>
    /// <param name="None"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Pure]
    public R Match<R>(Func<T, R> Some, R None)
    {
        return IsSome ? Some(Value) : None;
    }

    /// <summary>
    /// Converts an option of one type to an option of another.
    /// </summary>
    /// <typeparam name="B">Output type</typeparam>
    /// <param name="f"></param>
    /// <returns></returns>
    [Pure]
    public Option<B> Map<B>(Func<T, B> f)
    {
        return IsSome ? new Option<B>(f(Value)) : new Option<B>();
    }

    /// <summary>
    /// Converts an option of one type to an option of another asychronously.
    /// </summary>
    /// <typeparam name="B">Output type</typeparam>
    /// <param name="f"></param>
    /// <returns></returns>
    [Pure]
    public async Task<Option<B>> MapAsync<B>(Func<T, Task<B>> f)
    {
        return IsSome ? new Option<B>(await f(Value)) : new Option<B>();
    }
}
