using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EpsilonCore.Commands;
using EpsilonCore.Display;
using EpsilonCore.Engine;
using EpsilonCore.Units;
using System.Collections.ObjectModel;

namespace EpsilonDesktop.ViewModels;

public partial class CurrentPositionViewModel : ObservableObject
{
    internal struct AxisPositionData(string currentPosition = "", string distanceToGo = "")
    {
        public string CurrentPosition { get; set; } = currentPosition;
        public string DistanceToGo { get; set; } = distanceToGo;
    }

    private EpsilonEngine Engine { get; init; }
    private uint MachineId { get; init; }
    private IResultMessageLogger ResultMessageLogger { get; init; }

    [ObservableProperty]
    public partial DisplayUnits DisplayUnits { get; set; }

    public ObservableCollection<AxisPositionData> PositionData { get; set; } = [];

    public CurrentPositionViewModel(EpsilonEngine engine)
    {
        Engine = engine;
        engine.CommandResolved += (object? sender, QueuedCommand queuedCommand) =>
        {
            // TODO: Add rotational axis command here.
            if (queuedCommand.Command is AddLinearAxisCommand)
                ReadAllAxes(queuedCommand.ResultantMachine);
        };
    }

    [RelayCommand]
    public void Example2()
    {

    }
}