using CommunityToolkit.Mvvm.ComponentModel;
using EpsilonCore.Commands;
using EpsilonCore.Commands.SetupCommands;
using EpsilonCore.Display;
using EpsilonCore.Engine;
using EpsilonCore.Machines;
using EpsilonCore.Motion.Axis;
using System.Collections.ObjectModel;
using System.Windows;

namespace EpsilonDesktop.ViewModels;

/// <summary>
/// View model for <see cref="Views.CurrentPositionControl"/>: lists each axis and its position.
/// </summary>
public partial class CurrentPositionViewModel : ObservableObject
{
    /// <summary>
    /// One row of the position list.
    /// </summary>
    public struct AxisPositionData(string axisName = "", string currentPosition = "", string distanceToGo = "")
    {
        /// <summary>
        /// Name of the axis.
        /// </summary>
        public string AxisName { get; set; } = axisName;
        /// <summary>
        /// The axis position in display units, or a message if it isn't homed.
        /// </summary>
        public string CurrentPosition { get; set; } = currentPosition;
        /// <summary>
        /// Distance left to the latest queued position. Not filled in yet.
        /// </summary>
        public string DistanceToGo { get; set; } = distanceToGo;
    }

    private const string NOT_HOMED_TEXT = "Not homed";

    /// <summary>
    /// The engine running the machine.
    /// </summary>
    private EpsilonEngine Engine { get; init; }
    /// <summary>
    /// Id of the machine queue shown.
    /// </summary>
    private uint MachineId { get; init; }
    /// <summary>
    /// Logger for messages to the user.
    /// </summary>
    private IResultMessageLogger ResultMessageLogger { get; init; }

    /// <summary>
    /// One row per non-extrusion axis.
    /// </summary>
    public ObservableCollection<AxisPositionData> PositionData { get; } = [];

    /// <summary>
    /// Creates the view model and refreshes it whenever an axis is added to the machine.
    /// </summary>
    /// <param name="engine">The engine running the machine.</param>
    /// <param name="machineId">Id of the machine queue.</param>
    /// <param name="resultMessageLogger">Logger for messages to the user.</param>
    public CurrentPositionViewModel(
        EpsilonEngine engine,
        uint machineId,
        IResultMessageLogger resultMessageLogger)
    {
        Engine = engine;
        MachineId = machineId;
        ResultMessageLogger = resultMessageLogger;
        ReadAllAxes();
        engine.CommandResolved += (object? sender, QueuedCommand queuedCommand) =>
        {
            // TODO: Add rotational axis command here.
            if (queuedCommand.Command is AddLinearAxisCommand)
                ReadAllAxes(queuedCommand.ResultantMachine);
        };
    }

    /// <summary>
    /// Rebuilds <see cref="PositionData"/> from the axes of the given machine,
    /// or from the engine's current machine if none is given.
    /// </summary>
    /// <param name="machine">Machine whose boards are searched.</param>
    private void ReadAllAxes(Machine? machine = null)
    {
        machine ??= Engine.GetCurrentMachine(MachineId);

        if (machine is null)
            return;

        List<AxisPositionData> rows = [];

        foreach (AxisLinear axis in machine.Entities.AxesLinear.Values.Where(axis => !axis.IsExtrusionAxis))
            rows.Add(new AxisPositionData(
                axis.Name,
                axis.Pos?.ToUnit(machine.DisplayUnits.LengthUnitDisplay).ToString() ?? NOT_HOMED_TEXT));

        foreach (AxisRotational axis in machine.Entities.AxesRotational.Values.Where(axis => !axis.IsExtrusionAxis))
            rows.Add(new AxisPositionData(
                axis.Name,
                axis.Pos?.ToUnit(machine.DisplayUnits.AngleUnitDisplay).ToString() ?? NOT_HOMED_TEXT));

        // CommandResolved is raised on the engine thread, so update the bound collection on the UI thread.
        // BeginInvoke (not Invoke) so the engine never waits on the UI thread, which may itself be waiting on the engine
        // (for example a startup script calling CompleteAllCommands).
        void UpdateRows()
        {
            PositionData.Clear();
            foreach (AxisPositionData row in rows)
                PositionData.Add(row);
        }

        if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
            dispatcher.BeginInvoke(UpdateRows);
        else
            UpdateRows();
    }
}
