using EpsilonCore.Commands;
using EpsilonCore.Machines;
using System.Collections.Immutable;
using UnitsNet;
using UnitsNetHelpers;

namespace EpsilonCore.Helpers;

public static class Helper
{
    /// <summary>
    /// Sets a message on an <see cref="Exception"/> that a user can understand.
    /// </summary>
    /// <param name="ex"></param>
    /// <returns></returns>
    public static string? GetDisplayMessage(this Exception ex)
    {
        if (ex.Data.Contains("DisplayMessage"))
            return (string?)ex.Data["DisplayMessage"];
        return null;
    }

    /// <summary>
    /// Returns the display message assigned to the <see cref="Exception"/> if it exists.
    /// </summary>
    /// <param name="ex"></param>
    /// <param name="displayMessage"></param>
    public static void SetDisplayMessage(this Exception ex, string displayMessage)
    {
        ex.Data["DisplayMessage"] = displayMessage;
    }

    /// <summary>
    /// Returns an <see cref="Exception"/> with a display message.
    /// The display message is also the exception message.
    /// Use this if a developer needs the same info to debug as a user would need to understand the error. 
    /// </summary>
    /// <param name="displayMessage"></param>
    /// <returns></returns>
    public static Exception ExceptionWithMessage(string displayMessage)
    {
        Exception ex = new(displayMessage);
        ex.SetDisplayMessage(displayMessage);
        return ex;
    }

    /// <summary>
    /// Returns an <see cref="Exception"/> with a display message and a different exception message.
    /// Use this if a developer needs different info to debug than a user would need to understand the error. 
    /// </summary>
    /// <param name="displayMessage"></param>
    /// <param name="exceptionMessage"></param>
    /// <returns></returns>
    public static Exception ExceptionWithMessage(string displayMessage, string exceptionMessage)
    {
        Exception ex = new(exceptionMessage);
        ex.SetDisplayMessage(displayMessage);
        return ex;
    }

    /// <summary>
    /// Used to format <see cref="QueuedCommand.DisplayMessage"/> for <see cref="Display.IResultMessageLogger"/>.
    /// Returns a message formatted like:
    /// Time, ErrorLevel, LineNumber - DisplayMessage: CommandDescription.
    /// 03:35:01 PM, Success, Line 5 - Command queued succesfully: Add kinematic system 'SCARA' to a motion system.
    /// </summary>
    /// <param name="queuedCommand"></param>
    /// <returns></returns>
    public static string GetFormattedDisplayMessage(QueuedCommand queuedCommand)
    {
        return GetFormattedDisplayMessage(
            queuedCommand.Command,
            queuedCommand.ErrorLevel,
            queuedCommand.DisplayMessage);
    }

    /// <summary>
    /// Used to format a log message for <see cref="Display.IResultMessageLogger"/>.
    /// Returns a message formatted like:
    /// Time, ErrorLevel, LineNumber - DisplayMessage: CommandDescription.
    /// 03:35:01 PM, Success, Line 5 - Command queued succesfully: Add kinematic system 'SCARA' to a motion system.
    /// </summary>
    /// <param name="command"></param>
    /// <param name="errorLevel"></param>
    /// <param name="displayMessage"></param>
    /// <returns></returns>
    public static string GetFormattedDisplayMessage(ICommand command, ErrorLevel errorLevel, string displayMessage)
    {
        string date = DateTime.Now.ToString("hh:mm:ss tt");
        string errorLevelStr = ToDisplayStr(errorLevel);

        if (command.LineNumber.HasValue)
            return $"{date}, {errorLevelStr}, Line {command.LineNumber} - {displayMessage}: {command.Description}.";
        else
            return $"{date}, {errorLevelStr} - {displayMessage}: {command.Description}.";
    }

    public static bool Equals(this double left, double right, double epsilon)
        => Math.Abs(left - right) <= epsilon;

    public static bool IsZero(this double val, double epsilon)
        => Math.Abs(val) <= epsilon;

    /*
    public static void ApplyLineNumbers(this IEnumerable<ICommandDescription> commands)
    {
        int lineNumber = 1;
        foreach (ICommandDescription command in commands)
        {
            command.LineNumber = lineNumber;
            lineNumber++;
        }
    }
    */

    public static string ToDisplayStr(this ErrorLevel errorLevel)
    {
        return errorLevel switch
        {
            ErrorLevel.Success => "Success",
            ErrorLevel.Unnecessary => "Unnecessary",
            ErrorLevel.Warning => "Warning",
            ErrorLevel.Error => "Error",
            ErrorLevel.EmergencyShutdown => "Emergency Shutdown",
            _ => "Unknown Error Level"
        };
    }

    public static ImmutableList<T> ReplaceFirst<T>(this ImmutableList<T> list, Predicate<T> matchFunc, T newValue)
    {
        return list.SetItem(list.FindIndex(matchFunc), newValue);
    }

    public static IImmutableList<T> ReplaceFirst<T>(this IImmutableList<T> list, Predicate<T> matchFunc, T newValue)
    {
        int index = 0;
        foreach (T item in list)
        {
            if (matchFunc(item))
                break;
            ++index;
        }

        return list.SetItem(index, newValue);
    }

    public static bool ContainsIndex<T>(this IList<T> list, int index) => list.Count > index;
    public static bool ContainsIndex<T>(this IReadOnlyList<T> list, int index) => list.Count > index;

    public static uint NextId<T>(this IEnumerable<KeyValuePair<uint, T>> enumerable) =>
        enumerable.Any() ? enumerable.Max(i => i.Key) + 1 : 0;

    public static bool ContainsEntityWithName<TEntity>(this IImmutableDictionary<uint, TEntity> dict, string nameToFind)
        where TEntity : IEntity
    {
        return dict.Where(kvp => kvp.Value.Name == nameToFind).Any();
    }

    /// <summary>
    /// Returns a <see cref="Result{T}"/> containing the first element of <paramref name="enumerable"/> 
    /// which meets condition <paramref name="predicate"/> or an error <see cref="Result{T}"/> if none exist.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="enumerable"></param>
    /// <param name="predicate"></param>
    /// <returns></returns>
    public static Result<T> TryFirst<T>(this IEnumerable<T> enumerable, Func<T, bool> predicate)
    {
        try
        {
            return enumerable.First(predicate);
        }
        catch
        {
            return new Exception($"No element in {nameof(enumerable)} meets condition {nameof(predicate)}.");
        }
    }

    public static Result<Duration> TimeOfConstAccelerationMove(Speed vi, Acceleration a, Length l)
    {
        if (l.Equals(Length.Zero, Length.FromMillimeters(0.0001)))
            return Duration.Zero;

        if (a.Equals(Acceleration.Zero, Acceleration.FromMillimetersPerSecondSquared(0.01)))
        {
            if (vi.Equals(Speed.Zero, Speed.FromMillimetersPerSecond(0.0001)))
                return new ArgumentException("Cannot reach destination wtih acceleration and velocity of zero.");

            return l / vi;
        }

        double viSquared = Math.Pow(vi.MillimetersPerSecond, 2);
        double aMmPerSec2 = a.MillimetersPerSecondSquared;
        double squareRoot = Math.Sqrt(viSquared + 2 * aMmPerSec2 * l.Millimeters);

        if (!double.IsNormal(squareRoot))
            return new ArgumentException("No solution to constant velocity equation");

        double s1 = ((-viSquared + squareRoot) / aMmPerSec2);
        double s2 = ((-viSquared - squareRoot) / aMmPerSec2);
        if (s1 > 0)
        {
            if (s2 > 0)
                return Duration.FromSeconds(Math.Min(s1, s2));
            else
                return Duration.FromSeconds(s1);
        }
        else
        {
            if (s2 > 0)
                return Duration.FromSeconds(s2);
            else
                return new ArgumentException("No positive solution to constant velocity equation");
        }
    }

    public static Result<Duration> TimeOfConstAccelerationMove(RotationalSpeed vi, RotationalAcceleration a, Angle l)
    {
        if (l.Equals(Length.Zero, Length.FromMillimeters(0.0001)))
            return Duration.Zero;

        if (a.Equals(RotationalAcceleration.Zero, RotationalAcceleration.FromDegreesPerSecondSquared(0.01)))
        {
            if (vi.Equals(Speed.Zero, Speed.FromMillimetersPerSecond(0.0001)))
                return new ArgumentException("Cannot reach destination wtih acceleration and velocity of zero.");

            return l / vi;
        }

        double viSquared = Math.Pow(vi.RadiansPerSecond, 2);
        double aMmPerSec2 = a.RadiansPerSecondSquared;
        double squareRoot = Math.Sqrt(viSquared + 2 * aMmPerSec2 * l.Radians);

        if (!double.IsNormal(squareRoot))
            return new ArgumentException("No solution to constant velocity equation");

        double s1 = ((-vi.RadiansPerSecond + squareRoot) / aMmPerSec2);
        double s2 = ((-vi.RadiansPerSecond - squareRoot) / aMmPerSec2);
        if (s1 > 0)
        {
            if (s2 > 0)
                return Duration.FromSeconds(Math.Min(s1, s2));
            else
                return Duration.FromSeconds(s1);
        }
        else
        {
            if (s2 > 0)
                return Duration.FromSeconds(s2);
            else
                return new ArgumentException("No positive solution to constant velocity equation");
        }
    }

    /// <summary>
    /// Returns the absolute value or null if the <paramref name="value"/> is null.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static decimal? Abs(decimal? value)
    {
        return value.HasValue ? Math.Abs(value.Value) : null;
    }

    /// <summary>
    /// Returns the absolute value or null if the <paramref name="value"/> is null.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static double? Abs(double? value)
    {
        return value.HasValue ? Math.Abs(value.Value) : null;
    }

    /// <summary>
    /// Returns true if the <paramref name="superset"/> contains all elements in <paramref name="subset"/>.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="superset"></param>
    /// <param name="subset"></param>
    /// <returns></returns>
    public static bool ContainsAll<T>(this IEnumerable<T> superset, IEnumerable<T> subset)
    {
        return !subset.Except(superset).Any();
    }

    /// <summary>
    /// Returns a new <see cref="IEnumerable{T}"/> without null values.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="values"></param>
    /// <returns></returns>
    public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> values)
        where T : class
    {
        return values.Where(element => element is not null)!;
    }

    /// <summary>
    /// Returns a new <see cref="IEnumerable{T}"/> without null values.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="values"></param>
    /// <returns></returns>
    public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> values)
        where T : struct
    {
        return values
            .Where(element => element.HasValue)
            .Select(element => element!.Value);
    }
}
