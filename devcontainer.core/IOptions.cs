namespace devcontainer.core
{
    public interface IInitOptions
    {
        string TemplateName { get;}
        string Context { get; }
        string Name { get; }
        string ShutdownAction { get; }
        string Shell { get; }
        string Id { get; }
        string Dockerfile { get; }
        string DevDockerfile { get; }
        bool Overwrite { get; }
        string WorkspaceRoot { get; }
        bool DisableHooks { get; }
    }

    public interface IActivateOptions
    {
        string Name { get; }
        bool Overwrite { get; }

        bool DisableHooks { get; }
    }

    public interface IDeactivateOptions
    {
        bool DisableHooks { get; }
    }

    public interface ILSOptions
    {
        string Context { get; }
    }

    public interface IRunOptions
    {
        string Context { get; }
        string Command { get; }
        string WorkDir { get; }
    }
    
    public interface IStartOptions
    {
        string Context { get; }
        bool Build { get; }
    }
    
    public interface IStopOptions
    {
        string Context { get; }
        int TimeoutSec { get; }
    }

    public interface IStatusOptions
    {
        string Context { get; }
    }

    public interface IExecOptions
    {
        string Context { get; set; }
        string Command { get; set; }
        string WorkDir { get; set; }
    }
}