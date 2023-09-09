namespace SessionDemo.Models
{
    public enum Sessionkind
    {
        SSH,
        CustomImage,
    }

    public class Session
    {
        public string Name { get; set; }

        public int? RequestedDurationInSeconds { get; set; }

        public int TimeElapsedInSeconds { get; set; }

        public DateTime ExpiryTime { get; set; }

        public DateTime GeneratedTime { get; set; }

        public string ConnectionString { get; set; }

        public string Endpoint { get; set; }

        public Sessionkind? SessionKind { get; set; }

        public string Status { get; set; }

        public SSHConfiguration SSHConfiguration { get; set; }

        public CustomContainerConfiguration CustomContainerConfiguration { get; set; }

        public CodeExecutionConfigration CodeExecutionConfigration { get; set; }

        public SessionIngress SessionIngress { get; set; }
    }

    public class SSHConfiguration
    {
        public string UserName { get; set; }

        public string Password { get; set; }

        public string AuthorizedPublicKey { get; set; }
    }

    public class SessionIngress
    {
        public int TargetPort { get; set; }
    }

    public class CodeExecutionConfigration
    {
        public string Token { get; set; }

        public string Environment { get; set; }
    }

    public class CustomContainerConfiguration
    {
        public SessionRegistryCredentials SessionRegistryCredentials { get; set; }

        public SessionContainers SessionContainers { get; set; }
    }

    /// <summary>
    /// Session Private Registry
    /// </summary>
    public class SessionRegistryCredentials
    {
        /// <summary>
        /// Container Registry Server
        /// </summary>
        public string Server { get; set; }

        /// <summary>
        /// Container Registry Username
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// The name of the Secret that contains the registry login password
        /// </summary>
        public string PasswordSecretRef { get; set; }
    }

    /// <summary>
    /// Collection of Session containers.
    /// </summary>
    public class SessionContainers : List<SessionContainer>
    {
        public SessionContainers()
        {
        }

        public SessionContainers(List<SessionContainer> containers)
            : base(containers)
        {
        }
    }

    /// <summary>
    /// Collection of Session container env var.
    /// </summary>
    public class SessionContainerEnvironmentVars : List<SessionContainerEnvironmentVar>
    {
        public SessionContainerEnvironmentVars()
        {
        }

        public SessionContainerEnvironmentVars(List<SessionContainerEnvironmentVar> sessionContainerEnvironmentVar)
            : base(sessionContainerEnvironmentVar)
        {
        }
    }

    /// <summary>
    /// Session container definition.
    /// </summary>
    public class SessionContainer
    {
        /// <summary>
        /// Container image tag.
        /// </summary>
        public string Image { get; set; }

        /// <summary>
        /// Custom container name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Container start command.
        /// </summary>
        public string[] Command { get; set; }

        /// <summary>
        /// Container start command arguments.
        /// </summary>
        public string[] Args { get; set; }

        /// <summary>
        /// Container environment variables.
        /// </summary>
        public SessionContainerEnvironmentVars Env { get; set; }

        /// <summary>
        /// Container resource requirements.
        /// </summary>
        public SessionContainerResources Resources { get; set; }
    }

    public class SessionContainerEnvironmentVar
    {
        /// <summary>
        /// Environment variable name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Non-secret environment variable value.
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// Name of the Container App secret from which to pull the environment variable value.
        /// </summary>
        public string SecretRef { get; set; }
    }

    public class SessionContainerResources
    {
        /// <summary>
        /// Required CPU in cores, e.g. 0.5
        /// </summary>
        public decimal Cpu { get; set; }

        /// <summary>
        /// Required memory, e.g. "250Mb"
        /// </summary>
        public string Memory { get; set; }
    }

    public class SessionResponse
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string Location { get; set; }
        public Session Properties { get; set; }
    }

    public class ApiResponse
    {
        public List<SessionResponse> Value { get; set; }
    }
}
