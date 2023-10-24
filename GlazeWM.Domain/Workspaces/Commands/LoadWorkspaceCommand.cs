using GlazeWM.Infrastructure.Bussing;

namespace GlazeWM.Domain.Workspaces.Commands
{
  internal sealed class LoadWorkspaceCommand : Command
  {
    // TODO: Add argument for workspace to move instead of assuming the focused workspace.
    public string WorkspaceName { get; }

    public LoadWorkspaceCommand(string workspaceName)
    {
      WorkspaceName = workspaceName;
    }
  }
}
