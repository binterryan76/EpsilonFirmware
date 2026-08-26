using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EpsilonCore.Commands;
using EpsilonCore.Commands.MotionCommands;
using EpsilonCore.Commands.SetupCommands;
using EpsilonCore.Display;
using EpsilonCore.Engine;
using EpsilonCore.Machines;
using EpsilonCore.Motion.Axis;
using GenericHelpers;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using UnitsNet;

namespace EpsilonDesktop.ViewModels;

public partial class JogAxesViewModel : ObservableObject
{
    private EpsilonEngine Engine { get; init; }
    private uint MachineId { get; init; }
    private IResultMessageLogger ResultMessageLogger { get; init; }

    [ObservableProperty]
    public partial bool Axis1Enabled { get; private set; } = false;

    [ObservableProperty]
    public partial bool Axis2Enabled { get; private set; } = false;

    [ObservableProperty]
    public partial bool Axis3Enabled { get; private set; } = false;

    [ObservableProperty]
    public partial bool HomeAllEnabled { get; private set; } = false;

    [ObservableProperty]
    public partial AxisLinear? Axis1 { get; private set; } = null;

    [ObservableProperty]
    public partial AxisLinear? Axis2 { get; private set; } = null;

    [ObservableProperty]
    public partial AxisLinear? Axis3 { get; private set; } = null;

    [ObservableProperty]
    public partial Visibility Axis1Visibility { get; private set; } = Visibility.Collapsed;

    [ObservableProperty]
    public partial Visibility Axis2Visibility { get; private set; } = Visibility.Collapsed;

    [ObservableProperty]
    public partial Visibility Axis3Visibility { get; private set; } = Visibility.Collapsed;

    [ObservableProperty]
    public partial Visibility HomeAllVisibility { get; private set; } = Visibility.Collapsed;

    [ObservableProperty]
    public partial Visibility RotationalDistancesVisibility { get; private set; } = Visibility.Collapsed;

    public ObservableCollection<AxisLinearButtonsViewModel> OtherAxesLinear { get; set; } = [];
    public ObservableCollection<AxisRotationalButtonsViewModel> AllAxesRotational { get; set; } = [];

    /// <summary>
    /// These are the distances that can be used to move the linear axes. 
    /// </summary>
    public ObservableCollection<Length> DistancesLinear { get; set; }

    /// <summary>
    /// These are the distances that can be used to move the rotational axes. 
    /// </summary>
    public ObservableCollection<Angle> DistancesRotational { get; set; }

    [ObservableProperty]
    public partial Length SelectedDistanceLinear { get; set; }

    [ObservableProperty]
    public partial Angle SelectedDistanceRotational { get; set; }

    private JogAxesViewModel(
        EpsilonEngine engine,
        uint machineId,
        IResultMessageLogger resultMessageLogger,
        IEnumerable<Length> distancesLinear,
        IEnumerable<Angle> distancesRotational)
    {
        Engine = engine;
        MachineId = machineId;
        DistancesLinear = [with(distancesLinear)];
        DistancesRotational = [with(distancesRotational)];
        SelectedDistanceLinear = DistancesLinear.First();
        SelectedDistanceRotational = DistancesRotational.First();
        ResultMessageLogger = resultMessageLogger;
        ReadAllAxes();
        engine.CommandResolved += (object? sender, QueuedCommand queuedCommand) =>
        {
            // TODO: Add rotational axis command here.
            if (queuedCommand.Command is AddLinearAxisCommand)
                ReadAllAxes(queuedCommand.ResultantMachine);
        };
    }

    public static Result<JogAxesViewModel> New(
        EpsilonEngine engine,
        uint machineId,
        IResultMessageLogger resultMessageLogger,
        IEnumerable<Length>? distancesLinear = null,
        IEnumerable<Angle>? distancesRotational = null)
    {
        // Use default distances if none are provided.
        distancesLinear ??=
        [
            Length.FromMillimeters(0.01),
            Length.FromMillimeters(0.1),
            Length.FromMillimeters(1),
            Length.FromMillimeters(10),
            Length.FromMillimeters(100)
        ];

        distancesRotational ??=
        [
            Angle.FromDegrees(0.01),
            Angle.FromDegrees(0.1),
            Angle.FromDegrees(1),
            Angle.FromDegrees(10),
            Angle.FromDegrees(90)
        ];

        if (!distancesLinear.Any())
            return new ArgumentException($"{nameof(distancesLinear)} cannot be empty.");

        if (!distancesRotational.Any())
            return new ArgumentException($"{nameof(distancesRotational)} cannot be empty.");

        if (distancesLinear.Any(q => q.Value <= 0.0))
            return new ArgumentException($"{nameof(distancesLinear)} must contain only positive values.");

        if (distancesRotational.Any(q => q.Value <= 0.0))
            return new ArgumentException($"{nameof(distancesRotational)} must contain only positive values.");

        return new JogAxesViewModel(engine, machineId, resultMessageLogger, distancesLinear, distancesRotational);
    }

    [RelayCommand]
    private void MoveAxisForwardLinear(AxisLinear? axis) =>
        MoveAxisLinear(axis, SelectedDistanceLinear);

    [RelayCommand]
    private void MoveAxisBackwardLinear(AxisLinear? axis) =>
        MoveAxisLinear(axis, -SelectedDistanceLinear);

    [RelayCommand]
    private void MoveAxisForwardRotational(AxisRotational? axis) =>
        MoveAxisRotational(axis, SelectedDistanceRotational);

    [RelayCommand]
    private void MoveAxisBackwardRotational(AxisRotational? axis) =>
        MoveAxisRotational(axis, -SelectedDistanceRotational);

    private void MoveAxisLinear(AxisLinear? axis, Length distance)
    {
        if (axis is null)
            return;

        ImmutableDictionary<uint, Length> distances =
            ImmutableDictionary.CreateRange(new Dictionary<uint, Length>
        {
            { axis.Id, distance }
        });

        Result<MoveCommand> move = MoveCommand.New(
            linearPositions: distances,
            absoluteMove: false);

        if (move.IsError)
        {
            ResultMessageLogger.Log($"Cannot move: {move.Exception.Message}");
            return;
        }

        Engine.EnqueueCommand(MachineId, move.Value);
    }

    private void MoveAxisRotational(AxisRotational? axis, Angle distance)
    {
        if (axis is null)
            return;

        ImmutableDictionary<uint, Angle> distances =
            ImmutableDictionary.CreateRange(new Dictionary<uint, Angle>
        {
            { axis.Id, distance }
        });

        Result<MoveCommand> move = MoveCommand.New(
            rotationalPositions: distances,
            absoluteMove: false);

        if (move.IsError)
        {
            ResultMessageLogger.Log($"Cannot move: {move.Exception.Message}");
            return;
        }

        Engine.EnqueueCommand(MachineId, move.Value);
    }

    [RelayCommand]
    public void HomeAxis1()
    {
        // TODO: Add homing command/macro.
    }

    [RelayCommand]
    public void HomeAxis2()
    {
        // TODO: Add homing command/macro.
    }

    [RelayCommand]
    public void HomeAxis3()
    {
        // TODO: Add homing command/macro.
    }

    [RelayCommand]
    public void HomeAllCommand()
    {
        // TODO: Add homing command/macro.
    }

    [RelayCommand]
    public void Axis1Backwards()
    {
        MoveAxisBackwardLinear(Axis1);
    }

    [RelayCommand]
    public void Axis1Forwards()
    {
        MoveAxisForwardLinear(Axis1);
    }

    [RelayCommand]
    public void Axis2Backwards()
    {
        MoveAxisBackwardLinear(Axis2);
    }

    [RelayCommand]
    public void Axis2Forwards()
    {
        MoveAxisForwardLinear(Axis2);
    }

    [RelayCommand]
    public void Axis3Backwards()
    {
        MoveAxisBackwardLinear(Axis3);
    }

    [RelayCommand]
    public void Axis3Forwards()
    {
        MoveAxisForwardLinear(Axis3);
    }

    public void CommandResolved(object? sender, QueuedCommand queuedCommand)
    {
        switch (queuedCommand.Command)
        {
            // TODO: Add other commands that should trigger a refresh of the axes here (AddRotationalAxisCommand).
            case AddLinearAxisCommand:
                Debug.Assert(queuedCommand.ResultantMachine is not null);
                ReadAllAxes(queuedCommand.ResultantMachine);
                break;
            default:
                // Do nothing for other commands
                break;
        }
        ;
    }

    /// <summary>
    /// This method reads all the non-extrusion axes from the machine and populates 
    /// the axes and axis button view models lists.
    /// </summary>
    /// <param name="machine"></param>
    private void ReadAllAxes(Machine? machine = null)
    {
        Axis1 = null;
        Axis2 = null;
        Axis3 = null;
        OtherAxesLinear.Clear();
        AllAxesRotational.Clear();

        machine ??= Engine.GetCurrentMachine(MachineId);

        if (machine is null)
            return;

        List<AxisLinear> nonExtrusionAxesLinear =
            [.. machine.Entities.AxesLinear.Values.Where(axis => !axis.IsExtrusionAxis)];

        for (int i = 0; i < nonExtrusionAxesLinear.Count; i++)
        {
            AxisLinear axis = nonExtrusionAxesLinear[i];
            switch (i)
            {
                case 0:
                    Axis1 = axis;
                    break;
                case 1:
                    Axis2 = axis;
                    break;
                case 2:
                    Axis3 = axis;
                    break;
                default:
                    OtherAxesLinear.Add(new AxisLinearButtonsViewModel(axis, !DisableAllButtonsFlag));
                    break;
            }
        }

        List<AxisRotational> nonExtrusionAxesRotational =
            [.. machine.Entities.AxesRotational.Values.Where(axis => !axis.IsExtrusionAxis)];

        for (int i = 0; i < nonExtrusionAxesRotational.Count; i++)
            AllAxesRotational.Add(new AxisRotationalButtonsViewModel(nonExtrusionAxesRotational[i], !DisableAllButtonsFlag));

        SetButtonEnabledAndVisibility();
    }


    /// <summary>
    /// Flag is set to true during printing so that the user can't jog axes while the machine is moving.
    /// </summary>
    private bool DisableAllButtonsFlag { get; set; } = false;
    public void DisableAllButtons()
    {
        DisableAllButtonsFlag = true;
        SetButtonEnabledAndVisibility();
    }

    public void CancelDisableAllButtons()
    {
        DisableAllButtonsFlag = false;
        SetButtonEnabledAndVisibility();
    }

    /// <summary>
    /// This sets the visibility and enabled state of the buttons based on the presence of axes. 
    /// If an axis is present, its corresponding buttons are enabled and visible.
    /// Otherwise, they are disabled and hidden. 
    /// </summary>
    private void SetButtonEnabledAndVisibility()
    {
        // First set the visibility of the axes based on whether they are null or not.
        Axis1Visibility = Axis1 is not null ? Visibility.Visible : Visibility.Collapsed;
        Axis2Visibility = Axis2 is not null ? Visibility.Visible : Visibility.Collapsed;
        Axis3Visibility = Axis3 is not null ? Visibility.Visible : Visibility.Collapsed;

        // Axis1 is the first linear axis to be set so that is all we need to check to determine if a linear axis is present.
        bool atLeastOneLinearAxisPresent = Axis1 is not null;
        bool atLeastOneRotationalAxisPresent = AllAxesRotational.Count > 0;
        bool atLeastOneAxisPresent = atLeastOneLinearAxisPresent || atLeastOneRotationalAxisPresent;

        // One axis must exist to home all.
        HomeAllVisibility = atLeastOneAxisPresent ? Visibility.Visible : Visibility.Collapsed;

        // If the DisableAllButtonsFlag is set, disable all buttons and return early.
        if (DisableAllButtonsFlag)
        {
            Axis1Enabled = false;
            Axis2Enabled = false;
            Axis3Enabled = false;
            HomeAllEnabled = false;

            foreach (AxisLinearButtonsViewModel linearAxisButtons in OtherAxesLinear)
                linearAxisButtons.ButtonsEnabled = false;

            foreach (AxisRotationalButtonsViewModel rotationalAxisButtons in AllAxesRotational)
                rotationalAxisButtons.ButtonsEnabled = false;

            return;
        }

        Axis1Enabled = Axis1 is not null;
        Axis2Enabled = Axis2 is not null;
        Axis3Enabled = Axis3 is not null;

        // Axes will never be null so enable all buttons for the other axes.
        foreach (AxisLinearButtonsViewModel linearAxisButtons in OtherAxesLinear)
            linearAxisButtons.ButtonsEnabled = true;

        foreach (AxisRotationalButtonsViewModel rotationalAxisButtons in AllAxesRotational)
            rotationalAxisButtons.ButtonsEnabled = true;

        bool enableHomeAll = ShouldEnableHomeAll();

        bool ShouldEnableHomeAll()
        {
            if (Axis1 is not null && !Axis1Enabled)
                return false;

            if (Axis2 is not null && !Axis2Enabled)
                return false;

            if (Axis3 is not null && !Axis3Enabled)
                return false;

            if (OtherAxesLinear.Any(axisButtons => !axisButtons.ButtonsEnabled))
                return false;

            if (AllAxesRotational.Any(axisButtons => !axisButtons.ButtonsEnabled))
                return false;

            return true;
        }
    }
}