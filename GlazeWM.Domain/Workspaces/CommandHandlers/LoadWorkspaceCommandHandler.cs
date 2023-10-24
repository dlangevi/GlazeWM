using System.Linq;
using GlazeWM.Domain.Containers.Commands;
using GlazeWM.Domain.Monitors;
using GlazeWM.Domain.Windows;
using GlazeWM.Domain.Workspaces.Commands;
using GlazeWM.Domain.Workspaces.Events;
using GlazeWM.Infrastructure.Bussing;
using GlazeWM.Infrastructure.Exceptions;

namespace GlazeWM.Domain.Workspaces.CommandHandlers
{
  internal sealed class LoadWorkspaceHandler :
    ICommandHandler<LoadWorkspaceCommand>
  {
    private readonly Bus _bus;
    private readonly MonitorService _monitorService;
    private readonly WorkspaceService _workspaceService;

    public LoadWorkspaceHandler(
      Bus bus,
      MonitorService monitorService,
      WorkspaceService workspaceService)
    {
      _bus = bus;
      _monitorService = monitorService;
      _workspaceService = workspaceService;
    }

    public CommandResponse Handle(LoadWorkspaceCommand command)
    {
      var workspaceName = command.WorkspaceName;

      // Get focused workspace + monitor.
      var focusedWorkspace = _workspaceService.GetFocusedWorkspace();
      var focusedMonitor = MonitorService.GetMonitorFromChildContainer(focusedWorkspace);

      // Workspace we want to move to (if exists)
      var sourceWorkspace = _workspaceService.GetActiveWorkspaceByName(workspaceName);

      // Are on the same workspace already, so no worries
      if (focusedWorkspace == sourceWorkspace)
      {
        _bus.Invoke(
          new FocusWorkspaceCommand(command.WorkspaceName)
        );
        return CommandResponse.Ok;
      }

      if (sourceWorkspace is null)
      {
        _bus.Invoke(
          new FocusWorkspaceCommand(command.WorkspaceName)
        );
        return CommandResponse.Ok;
      }

      // Get the source monitor pre move
      var sourceMonitor = MonitorService.GetMonitorFromChildContainer(sourceWorkspace);

      // Move workspace to focused monitor.
      _bus.Invoke(
        new MoveContainerWithinTreeCommand(sourceWorkspace, focusedMonitor, false)
      );

      var dpiDifference = MonitorService.HasDpiDifference(
          focusedWorkspace, targetWorkspace);
      // Update floating placement since the windows have to cross monitors.
      foreach (var window in sourceWorkspace.Descendants.OfType<Window>())
      {
        window.FloatingPlacement = window.FloatingPlacement.TranslateToCenter(
          focusedMonitor.DisplayedWorkspace.ToRect()
        );
      }
      // TODO: There is still a bug with windows being drawn wrong
      // the first time they are moved across the space.

      // Focus workspace .
      _bus.Invoke(
        new FocusWorkspaceCommand(sourceWorkspace.Name)
      );
      // Prevent original monitor from having no workspaces.
      if (sourceMonitor.Children.Count == 0)
      {
        ActivateWorkspaceOnMonitor(sourceMonitor);
      }
      return CommandResponse.Ok;
    }

    private void ActivateWorkspaceOnMonitor(Monitor monitor)
    {
      // Get name of first workspace that is not active for that specified monitor or any.
      var inactiveWorkspaceConfig =
        _workspaceService.GetWorkspaceConfigToActivate(monitor);

      if (inactiveWorkspaceConfig is null)
        throw new FatalUserException("At least 1 workspace is required per monitor.");

      // Assign the workspace to the empty monitor.
      _bus.Invoke(new ActivateWorkspaceCommand(inactiveWorkspaceConfig.Name, monitor));
    }
  }
}
