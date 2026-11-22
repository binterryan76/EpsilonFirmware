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

public partial class CurrentPositionViewModel : ObservableObject
{
    public struct AxisPositionData(string axisName = "", string currentPosition = "", string distanceToGo = "")
    {
        public string AxisName { get; set; } = axisName;
        public string CurrentPosition { get; set; } = currentPosition;
        public string DistanceToGo { get; set; } = distanceToGo;
    }

    private const string NOT_HOMED_TEXT = "Not homed";

    private EpsilonEngine Engine { get; init; }
    private uint MachineId { get; init; }
    private IResultMessageLogger ResultMessageLogger { get; init; }

    public ObservableCollection<AxisPositionData> PositionData { get; } = [];

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
    /// <param name="machine"></param>
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
        void UpdateRows()
        {
            PositionData.Clear();
            foreach (AxisPositionData row in rows)
                PositionData.Add(row);
        }

        if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
            dispatcher.Invoke(UpdateRows);
        else
            UpdateRows();
    }
}
