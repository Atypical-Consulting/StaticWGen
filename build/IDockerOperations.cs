using Nuke.Common;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Docker;
using Serilog;

public interface IDockerOperations : INukeBuild
{
    [Parameter("Docker image name to build")][Required]
    string ImageName => TryGetValue(() => ImageName);

    [Parameter("Docker image version tag")][Required]
    string VersionTag => TryGetValue(() => VersionTag);

    [Parameter("Docker container name")][Required]
    string ContainerName => TryGetValue(() => ContainerName);

    [Parameter("Host port to publish the container")][Required]
    int HostPort => TryGetValue<int>(() => HostPort);

    [Parameter("Container port to expose the application")][Required]
    int ContainerPort => TryGetValue<int>(() => ContainerPort);
    
    Target BuildDockerImage => _ => _
        .DependsOn<IGenerateWebsite>(x => x.BuildWebsite)
        .Executes(() =>
        {
            DockerTasks.DockerLogger = (type, text) => Log.Debug(text);
            
            DockerTasks.DockerBuild(x => x
                .SetPath(RootDirectory)
                .SetTag($"{ImageName}:{VersionTag}")
            );
            
            Log.Information($"Docker image {ImageName}:{VersionTag} built successfully!");
        });
    
    Target DeployDockerImage => _ => _
        .DependsOn(BuildDockerImage)
        .Executes(() =>
        {
            // Stop and remove any running container with the same name
            if (IsContainerRunning(ContainerName))
            {
                Log.Information($"Stopping and removing existing container {ContainerName}...");
                DockerTasks.DockerStop(x => x.SetContainers(ContainerName));
                DockerTasks.DockerRm(x => x.SetForce(true).SetContainers(ContainerName));
                Log.Information($"Existing container {ContainerName} stopped and removed successfully!");
            }

            // Run the new container
            DockerTasks.DockerRun(x => x
                .SetDetach(true)
                .SetPublish($"{HostPort}:{ContainerPort}")
                .SetName(ContainerName)
                .SetImage($"{ImageName}:{VersionTag}")
                .SetProcessLogOutput(true)
                .SetProcessLogInvocation(true)
            );
        
            Log.Information($"Docker container {ContainerName} running at http://localhost:{HostPort}");
        });
    
    bool IsContainerRunning(string containerName)
    {
        var result = DockerTasks.DockerPs(x => x
            .SetFormat("{{.Names}}")
            .SetFilter($"name={containerName}")
        );
        
        return result.Count != 0;
    }
}