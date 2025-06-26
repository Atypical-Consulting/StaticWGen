using Nuke.Common;
using Nuke.Common.Tools.Docker;
using static Nuke.Common.Tools.Docker.DockerTasks;
using static Serilog.Log;

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
            DockerBuild(x => x
                .SetPath(RootDirectory)
                .SetTag($"{ImageName}:{VersionTag}")
            );
            
            Information("Docker image {ImageName}:{VersionTag} built successfully!", ImageName, VersionTag);
        });
    
    Target DeployDockerImage => _ => _
        .DependsOn(BuildDockerImage)
        .Executes(() =>
        {
            // Stop and remove any running container with the same name
            if (IsContainerRunning(ContainerName))
            {
                Information("Stopping and removing existing container {ContainerName}...", ContainerName);
                DockerStop(x => x.SetContainers(ContainerName));
                DockerRm(x => x.SetForce(true).SetContainers(ContainerName));
                Information("Existing container {ContainerName} stopped and removed successfully!", ContainerName);
            }

            // Run the new container
            DockerRun(x => x
                .SetDetach(true)
                .SetPublish($"{HostPort}:{ContainerPort}")
                .SetName(ContainerName)
                .SetImage($"{ImageName}:{VersionTag}")
            );
        
            Information("Docker container {ContainerName} running at http://localhost:{HostPort}", ContainerName, HostPort);
        });
    
    bool IsContainerRunning(string containerName)
    {
        var result = DockerPs(x => x
            .SetFormat("{{.Names}}")
            .SetFilter($"name={containerName}")
        );
        
        return result.Count != 0;
    }
}