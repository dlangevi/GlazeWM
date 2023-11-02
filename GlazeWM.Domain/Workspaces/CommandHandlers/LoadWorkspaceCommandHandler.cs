using System.Linq;
using GlazeWM.Domain.Containers;
using GlazeWM.Domain.Containers.Commands;
using GlazeWM.Domain.Containers.Events;
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
    private readonly ContainerService _containerService;
    private readonly MonitorService _monitorService;
    private readonly WorkspaceService _workspaceService;

    public LoadWorkspaceHandler(
      Bus bus,
      ContainerService containerService,
      MonitorService monitorService,
      WorkspaceService workspaceService)
    {
      _bus = bus;
      _containerService = containerService;
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

      var dpiDifference = MonitorService.HasDpiDifference(
          focusedWorkspace, sourceWorkspace);
      // Update floating placement since the windows have to cross monitors.
      // TODO: There is still a bug with windows being drawn wrong
      // the first time they are moved across the space.

      foreach (var window in sourceWorkspace.Descendants.OfType<Window>())
      {
        if (dpiDifference)
          window.HasPendingDpiAdjustment = true;

        window.FloatingPlacement = window.FloatingPlacement.TranslateToCenter(
          focusedMonitor.DisplayedWorkspace.ToRect()
        );
        _containerService.ContainersToRedraw.Add(window);
      }
      foreach (var window in focusedWorkspace.Descendants.OfType<Window>())
      {
        if (dpiDifference)
          window.HasPendingDpiAdjustment = true;

        window.FloatingPlacement = window.FloatingPlacement.TranslateToCenter(
          focusedMonitor.DisplayedWorkspace.ToRect()
        );
        _containerService.ContainersToRedraw.Add(window);
      }

      // Move workspace to focused monitor.
      _bus.Invoke(
        new MoveContainerWithinTreeCommand(sourceWorkspace, focusedMonitor, true)
      );

      // Focus workspace .
      _bus.Invoke(
        new FocusWorkspaceCommand(sourceWorkspace.Name)
      );

      _bus.Invoke(
          new RedrawContainersCommand()
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
