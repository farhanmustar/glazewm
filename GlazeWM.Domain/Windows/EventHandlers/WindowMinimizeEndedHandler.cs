using System.Diagnostics;
using System.Linq;
using GlazeWM.Domain.Containers;
using GlazeWM.Domain.Containers.Commands;
using GlazeWM.Domain.Workspaces;
using GlazeWM.Infrastructure.Bussing;
using GlazeWM.Infrastructure.WindowsApi.Events;

namespace GlazeWM.Domain.Windows.EventHandlers
{
  class WindowMinimizeEndedHandler : IEventHandler<WindowMinimizeEndedEvent>
  {
    private Bus _bus;
    private WindowService _windowService;
    private ContainerService _containerService;
    private WorkspaceService _workspaceService;

    public WindowMinimizeEndedHandler(Bus bus, WindowService windowService, ContainerService containerService, WorkspaceService workspaceService)
    {
      _bus = bus;
      _windowService = windowService;
      _containerService = containerService;
      _workspaceService = workspaceService;
    }

    public void Handle(WindowMinimizeEndedEvent @event)
    {
      var window = _windowService.GetWindows()
        .FirstOrDefault(window => window.Hwnd == @event.WindowHandle);

      if (window == null)
        return;

      var tilingWindow = new TilingWindow(window.Hwnd, window.OriginalWidth, window.OriginalHeight);

      // Keep reference to the window's ancestor workspace and focus order index prior to detaching.
      var workspace = _workspaceService.GetWorkspaceFromChildContainer(window);

      _bus.Invoke(new DetachContainerCommand(window));
      AttachChildWindow(window);

      _containerService.ContainersToRedraw.Add(workspace);
      _bus.Invoke(new RedrawContainersCommand());
    }

    private void AttachChildWindow(Window window)
    {
      var focusedContainer = _containerService.FocusedContainer;

      // If the focused container is a workspace, attach the window as a child of the workspace.
      if (focusedContainer is Workspace)
      {
        _bus.Invoke(new AttachContainerCommand(focusedContainer as Workspace, window));
        return;
      }

      // Attach the window as a sibling next to the focused window.
      _bus.Invoke(new AttachContainerCommand(
        focusedContainer.Parent as SplitContainer, window, focusedContainer.Index + 1
      ));
    }
  }
}
