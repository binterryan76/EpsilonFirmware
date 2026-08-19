using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EpsilonCore.Display;
using EpsilonCore.Engine;
using EpsilonCore.Helpers;
using EpsilonCore.Machines;
using System.Collections.ObjectModel;
using System.Windows.Documents;
using UnitsNet;

namespace EpsilonDesktop.ViewModels;

public partial class MachineViewModel : ObservableObject
{
    private EpsilonEngine Engine { get; init; }
    private uint MachineId { get; init; }
    private IResultMessageLogger ResultMessageLogger { get; init; }

    [ObservableProperty]
    public partial Machine Machine { get; set; } = machine;

    [ObservableProperty]
    private partial JogAxesViewModel JogAxesViewModel { get; set; }

    // Don't mark as static or partial.
    public ObservableCollection<AxisLinearButtonsViewModel> ExampleList { get; set; } = [];

    private MachineViewModel(
        EpsilonEngine engine,
        uint machineId,
        IResultMessageLogger resultMessageLogger,
        JogAxesViewModel jogAxisViewModel)
    {
        Engine = engine;
        MachineId = machineId;
        ResultMessageLogger = resultMessageLogger;
        JogAxesViewModel = jogAxisViewModel;
        engine.
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
            return new ArgumentException($"Machine {machineId} doesn't exist in engine.");

        Result<JogAxesViewModel> jogAxisViewModel = JogAxesViewModel.New(
            engine,
            machineId,
            resultMessageLogger,
            jogDistancesLinear,
            jogDistancesRotational);

        if (jogAxisViewModel.IsError)
            return jogAxisViewModel.Exception;

        return new MachineViewModel(engine, machineId, resultMessageLogger, jogAxisViewModel.Value);
    }



    [RelayCommand]
    public void Example2()
    {

    }
}