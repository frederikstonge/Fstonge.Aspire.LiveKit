# LiveKit agents

LiveKit agents connect to the LiveKit server as workers and join rooms when
they receive jobs. The Aspire integration can run Python and Node.js agents as
local processes, run npm scripts, or build an agent container from a Dockerfile.
Agent resources appear under their LiveKit server in the Aspire dashboard.

For the LiveKit server and shared Redis setup, start with the
[getting-started guide](getting-started.md).

## Configure an agent in the AppHost

Add the hosting package namespace and register your LiveKit server as usual.
Create provider API keys as secret Aspire parameters, then pass the required
key to each agent:

```csharp
var redis = builder.AddRedis("redis");
var livekit = builder.AddLiveKit("livekit")
    .WithRedis(redis);

var openAiKey = builder.AddParameter("openai-api-key", secret: true);

var pythonAgent = livekit.AddPythonAgent(
        "python-voice-agent",
        "../Fstonge.Aspire.LiveKit.Example.PythonAgent",
        scriptPath: "main.py")
    .WithOpenAiApiKey(openAiKey);

var nodeAgent = livekit.AddNodeAgent(
        "node-voice-agent",
        "../Fstonge.Aspire.LiveKit.Example.NodeAgent",
        scriptPath: "agent.js")
    .WithOpenAiApiKey(openAiKey);
```

Add only the agent resources you intend to run. The API key parameter is
required only if one or more agents need it. Keep provider keys in Aspire
secret parameters or another secret store; do not put real keys in source
control or pass them to browser clients.

For local development, set the secret in the AppHost project's user secrets
from the repository root:

```powershell
dotnet user-secrets set "Parameters:openai-api-key" "<your-openai-api-key>" `
  --project example/Fstonge.Aspire.LiveKit.Example.AppHost
```

Replace the parameter name and value for other providers. The hosting package
also has `WithDeepgramApiKey`, `WithCartesiaApiKey`, `WithElevenLabsApiKey`,
and `WithAnthropicApiKey` helpers. They inject the corresponding provider
environment variable into the agent.

## Choose how the agent runs

### Local Python process

Create a virtual environment and install the agent's dependencies in its
project directory before starting Aspire:

```powershell
cd example/Fstonge.Aspire.LiveKit.Example.PythonAgent
python -m venv .venv
.\.venv\Scripts\python.exe -m pip install -r requirements.txt
cd ..\..
```

`AddPythonAgent` uses that `.venv` automatically when present; otherwise it
looks for `python` on `PATH`. On macOS or Linux, the virtual environment
executable is `.venv/bin/python`.

### Local Node.js process

Install npm dependencies before starting Aspire:

```powershell
cd example/Fstonge.Aspire.LiveKit.Example.NodeAgent
npm install
cd ..\..
```

`AddNodeAgent` runs the configured JavaScript entry point with Node.js, which
must be on `PATH`. `AddNpmAgent` is an alternative when the project's `package.json`
defines the desired scripts; it runs `dev` in development by default and
`start` otherwise.

### Dockerfile container

Build an agent image from its Dockerfile and apply the provider key just as for
a local process:

```csharp
var pythonAgent = livekit.AddAgentDockerfile(
        "python-docker-agent",
        "../Fstonge.Aspire.LiveKit.Example.PythonAgent",
        dockerfilePath: "Dockerfile")
    .WithOpenAiApiKey(openAiKey);
```

The context path must contain the Docker build context and the Dockerfile. For
a Dockerfile-based agent, its `CMD` or `ENTRYPOINT` determines how the worker
starts; Aspire does not replace it with the local process's `dev` or `start`
arguments. Install all runtime dependencies in the image. Docker must be
available to build and run the container.

## What Aspire configures

`AddPythonAgent`, `AddNodeAgent`, and `AddNpmAgent` automatically configure
each worker with:

- `LIVEKIT_URL`, `LIVEKIT_API_KEY`, and `LIVEKIT_API_SECRET` for the
  LiveKit server.
- `LIVEKIT_AGENT_NAME` using the Aspire resource name.
- `LIVEKIT_AGENT_DEV=true` in Development.
- A dependency on the LiveKit server.

The Python, Node.js, and npm helpers launch `dev` in Development and `start`
in other environments (unless you override the script or provide additional
arguments). With `AddAgentDockerfile`, Aspire provides the LiveKit environment
and dependency, but the Dockerfile controls the worker command.

Aspire does not install local Python or npm dependencies for process-based
agents. It also does not infer provider keys from the agent code: pass each
required key explicitly using its `With...ApiKey` helper or configure the
environment variable yourself.

## Use the repository example

The example AppHost has opt-in switches in
`example/Fstonge.Aspire.LiveKit.Example.AppHost/appsettings.json`:

```json
{
  "LiveKit": {
    "UseDockerImage": true,
    "EnablePythonAgent": false,
    "EnableNodeAgent": false
  }
}
```

Set either `EnablePythonAgent` or `EnableNodeAgent` to `true` to run that
agent. Set `UseDockerImage` to `true` to build from its Dockerfile; set it to
`false` to run the process directly on the host. The example's
`appsettings.Development.json` enables both agents in Docker by default. Supply
the `openai-api-key` secret before starting the AppHost.

Run the AppHost from the repository root:

```powershell
dotnet run --project example/Fstonge.Aspire.LiveKit.Example.AppHost
```

Use the Aspire dashboard to inspect agent resource state and logs. A running
agent worker still needs to receive a job in a room; verify that your client
connects to the same LiveKit server and requests or dispatches the agent as
appropriate for your LiveKit setup.
