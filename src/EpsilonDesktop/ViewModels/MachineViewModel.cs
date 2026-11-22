using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EpsilonCore.Display;
using EpsilonCore.Engine;
using EpsilonCore.Machines;
using GenericHelpers;
using System.Collections.ObjectModel;
using UnitsNet;

namespace EpsilonDesktop.ViewModels;

public partial class MachineViewModel : ObservableObject
{
    private EpsilonEngine Engine { get; init; }
    private IResultMessageLogger ResultMessageLogger { get; init; }

    [ObservableProperty]
    public partial Machine CurrentMachine { get; set; }

    [ObservableProperty]
    public partial JogAxesViewModel JogAxesViewModel { get; set; }

    [ObservableProperty]
    public partial CurrentPositionViewModel CurrentPositionViewModel { get; set; }

    // Don't mark as static or partial.
    public ObservableCollection<AxisLinearButtonsViewModel> ExampleList { get; set; } = [];

    private MachineViewModel(
        EpsilonEngine engine,
        Machine currentMachine,
        IResultMessageLogger resultMessageLogger,
        JogAxesViewModel jogAxisViewModel,
        CurrentPositionViewModel currentPositionViewModel)
    {
        Engine = engine;
        CurrentMachine = currentMachine;
        ResultMessageLogger = resultMessageLogger;
        JogAxesViewModel = jogAxisViewModel;
        CurrentPositionViewModel = currentPositionViewModel;
    }

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

        return new MachineViewModel(engine, machine, resultMessageLogger, jogAxisViewModel.Value, currentPositionViewModel);
    }



    [RelayCommand]
    public void Example2()
    {

    }
}