using DockerDualControl.Core;

namespace DockerDualControl.Tests;

public class EngineControlTests
{
    private const string DesktopServiceQcOutput = """
        [SC] QueryServiceConfig SUCCESS

        SERVICE_NAME: docker
                TYPE               : 10  WIN32_OWN_PROCESS
                START_TYPE         : 3   DEMAND_START
                ERROR_CONTROL      : 1   NORMAL
                BINARY_PATH_NAME   : "C:\Program Files\Docker\Docker\resources\dockerd.exe" --run-service --service-name docker -G docker-users --config-file C:\ProgramData\Docker\config\daemon.json
                LOAD_ORDER_GROUP   :
                TAG                : 0
                DISPLAY_NAME       : Docker Engine
                DEPENDENCIES       :
                SERVICE_START_NAME : LocalSystem
        """;

    [Fact]
    public void ParseServiceBinaryPath_ExtractsBinaryPathName()
    {
        var path = EngineControl.ParseServiceBinaryPath(DesktopServiceQcOutput);

        Assert.Equal(
            "\"C:\\Program Files\\Docker\\Docker\\resources\\dockerd.exe\" --run-service --service-name docker -G docker-users --config-file C:\\ProgramData\\Docker\\config\\daemon.json",
            path);
    }

    [Fact]
    public void ParseServiceBinaryPath_ReturnsNull_WhenLineMissing()
    {
        Assert.Null(EngineControl.ParseServiceBinaryPath("[SC] OpenService FAILED 1060:\r\n\r\nThe specified service does not exist.\r\n"));
    }

    [Theory]
    // Docker Desktop's bundled windows-containers daemon: listens on its own pipe,
    // so starting the service does not bring up //./pipe/docker_engine.
    [InlineData("\"C:\\Program Files\\Docker\\Docker\\resources\\dockerd.exe\" --run-service --service-name docker", true)]
    [InlineData("\"c:\\program files\\docker\\docker\\RESOURCES\\dockerd.exe\" --run-service", true)]
    // Standalone dockerd installs (Mirantis / manual dockerd --register-service).
    [InlineData("\"C:\\Program Files\\Docker\\dockerd.exe\" --run-service", false)]
    [InlineData("C:\\docker\\dockerd.exe --run-service", false)]
    public void IsDockerDesktopDaemonService_DetectsDesktopInstallLayout(string binaryPath, bool expected)
    {
        Assert.Equal(expected, EngineControl.IsDockerDesktopDaemonService(binaryPath));
    }
}
