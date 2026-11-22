using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EpsilonCore.Display;
using EpsilonCore.Engine;
using EpsilonCore.Machines;
using GenericHelpers;
using System.Collections.ObjectModel;
using System.Windows;
using UnitsNet;

namespace EpsilonDesktop.ViewModels;

public partial class MachineViewModel : ObservableObject
{
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
    /// The machine queue's status, such as Running, Paused or EmergencyStopped.
    /// </summary>
    [ObservableProperty]
    public partial MachineQueueStatus Status { get; set; }

    [ObservableProperty]
    public partial Machine CurrentMachine { get; set; }

    [ObservableProperty]
    public partial JogAxesViewModel JogAxesViewModel { get; set; }

    [ObservableProperty]
    public partial CurrentPositionViewModel CurrentPositionViewModel { get; set; }

    // Don't mark as static or partial.
    public ObservableCollection<AxisLinearButtonsViewModel> ExampleList { get; set; } = [];

    /// <summary>
    /// Creates the view model for a machine queue and the view models of its controls.
    /// </summary>
    /// <param name="engine">The engine running the machine.</param>
    /// <param name="machineId">Id of the machine queue.</param>
    /// <param name="resultMessageLogger">Logger for messages to the user.</param>
    /// <param name="jogDistancesLinear">Distances the linear jog buttons can move, or null for the defaults.</param>
    /// <param name="jogDistancesRotational">Distances the rotational jog buttons can move, or null for the defaults.</param>
    /// <returns>The view model, or an error if the machine queue doesn't exist.</returns>
    public static Result<MachineViewModel> New(
        EpsilonEngine engine,
        uint machineId,
        IResultMessageLogger resultMessageLogger,
        IEnumerable<Length>? jogDistancesLinear = null,
        IEnumerable<Angle>? jogDistancesRotational = null)
    {
        // Validate that machine exists.
        Machine? machine = engine.GetCurrentMachine(machineId);

        if (machine is null)
            return new ArgumentException($"CurrentMachine {machineId} doesn't exist in engine.");

        Result<JogAxesViewModel> jogAxisViewModel = JogAxesViewModel.New(
            engine,
            machineId,
            resultMessageLogger,
            jogDistancesLinear,
            jogDistancesRotational);

        if (jogAxisViewModel.IsError)
            return jogAxisViewModel.Exception;

        CurrentPositionViewModel currentPositionViewModel = new(engine, machineId, resultMessageLogger);

        return new MachineViewModel(engine, machineId, machine, resultMessageLogger, jogAxisViewModel.Value, currentPositionViewModel);
    }

    /// <summary>
    /// Creates the view model. Use <see cref="New"/>.
    /// </summary>
    /// <param name="engine">The engine running the machine.</param>
    /// <param name="machineId">Id of the machine queue.</param>
    /// <param name="currentMachine">The machine after the last resolved command.</param>
    /// <param name="resultMessageLogger">Logger for messages to the user.</param>
    /// <param name="jogAxisViewModel">View model of the jog axes control.</param>
    /// <param name="currentPositionViewModel">View model of the current position control.</param>
    private MachineViewModel(
        EpsilonEngine engine,
        uint machineId,
        Machine currentMachine,
        IResultMessageLogger resultMessageLogger,
        JogAxesViewModel jogAxisViewModel,
        CurrentPositionViewModel currentPositionViewModel)
    {
        Engine = engine;
        MachineId = machineId;
        CurrentMachine = currentMachine;
        ResultMessageLogger = resultMessageLogger;
        JogAxesViewModel = jogAxisViewModel;
        CurrentPositionViewModel = currentPositionViewModel;
        Status = engine.GetMachineQueueStatus(machineId) ?? MachineQueueStatus.Stopped;

        // The engine raises this on its own threads, so update the bound property on the UI thread.
        // BeginInvoke so the engine never waits on the UI thread.
        engine.MachineQueueStatusChanged += (sender, change) =>
        {
            if (change.MachineId != MachineId)
                return;

            if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
                dispatcher.BeginInvoke(() => Status = change.Status);
            else
                Status = change.Status;
        };
    }

    /// <summary>
    /// Sends an emergency stop to the machine.
    /// </summary>
    [RelayCommand]
    public void EmergencyStop() => LogIfError(Engine.EmergencyStop(MachineId));

    /// <summary>
    /// Logs an error returned by the engine, if there is one.
    /// </summary>
    /// <param name="error">The error returned by the engine, or null.</param>
    private void LogIfError(Exception? error)
    {
        if (error is not null)
            ResultMessageLogger.Log($"Error: {error.Message}");
    }

    /// <summary>
    /// Pauses the machine.
    /// </summary>
    [RelayCommand]
    public void Pause() => LogIfError(Engine.Pause(MachineId));

    /// <summary>
    /// Resumes a paused machine.
    /// </summary>
    [RelayCommand]
    public void Resume() => LogIfError(Engine.Resume(MachineId));

    /// <summary>
    /// Restarts a machine stopped by an error or an emergency stop.
    /// </summary>
    [RelayCommand]
    public void ClearFaults() => LogIfError(Engine.ClearFaults(MachineId));

    [RelayCommand]
    public void Example2()
    {

    }
}