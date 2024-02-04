using System;
using System.Linq;
using GlazeWM.Domain.Common.Enums;
using GlazeWM.Domain.Monitors;
using GlazeWM.Domain.UserConfigs;
using GlazeWM.Domain.Workspaces.Commands;
using GlazeWM.Infrastructure.Bussing;

namespace GlazeWM.Domain.Workspaces.CommandHandlers
{
  internal sealed class FocusWorkspaceSequenceHandler : ICommandHandler<FocusWorkspaceSequenceCommand>
  {
    private readonly Bus _bus;
    private readonly UserConfigService _userConfigService;
    private readonly WorkspaceService _workspaceService;

    public FocusWorkspaceSequenceHandler(
      Bus bus,
      UserConfigService userConfigService,
      WorkspaceService workspaceService)
    {
      _bus = bus;
      _userConfigService = userConfigService;
      _workspaceService = workspaceService;
    }

    public CommandResponse Handle(FocusWorkspaceSequenceCommand command)
    {
      var isMonitor = false;
      var direction = command.Direction;
      var workspacesConfigs = _userConfigService.WorkspaceConfigs;
      if (direction == Sequence.MonitorPrevious) {
        isMonitor = true;
        direction = Sequence.Previous;
      } else if (direction == Sequence.MonitorNext) {
        isMonitor = true;
        direction = Sequence.Next;
      }

      // Get focused workspace + monitor.
      var focusedWorkspace = _workspaceService.GetFocusedWorkspace();
      var focusedMonitor = MonitorService.GetMonitorFromChildContainer(focusedWorkspace);

      // Get active workspaces in order of their config index.
      var activeWorkspaces = _workspaceService.GetActiveWorkspaces();
      var sortedWorkspacesList = activeWorkspaces
          .OrderBy(workspace => workspacesConfigs.FindIndex(config => config.Name == workspace.Name));

      var sortedWorkspaces = isMonitor ? sortedWorkspacesList
            .Where(workspace => MonitorService.GetMonitorFromChildContainer(workspace) == focusedMonitor)
            .ToList() : sortedWorkspacesList.ToList();

      // Get config index of the currently focused workspace.
      var configIndex = sortedWorkspaces.IndexOf(focusedWorkspace);

      // Get index in `sortedWorkspaces` of target workspace to focus. Wrap around to start if
      // there is no previous/next workspace.
      var indexToFocus = direction switch
      {
        Sequence.Previous => configIndex == 0 ? sortedWorkspaces.Count - 1 : configIndex - 1,
        Sequence.Next => configIndex == sortedWorkspaces.Count - 1 ? 0 : configIndex + 1,
        _ => throw new ArgumentException(nameof(direction)),
      };

      var workspaceToFocus = sortedWorkspaces.ElementAtOrDefault(indexToFocus);

      // Set focus to the previous/next workspace if found.
      if (workspaceToFocus is not null && workspaceToFocus != focusedWorkspace)
        _bus.Invoke(new FocusWorkspaceCommand(workspaceToFocus.Name));

      return CommandResponse.Ok;
    }
  }
}
