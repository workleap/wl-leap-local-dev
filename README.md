# Leap local dev

[![nuget](https://feeds.dev.azure.com/gsoft/_apis/public/Packaging/Feeds/gsoft/Packages/7e26c0cd-3179-49c3-a3a7-e92df061eee5/Badge)](https://dev.azure.com/gsoft/Shared-Assets/_artifacts/feed/gsoft/NuGet/Workleap.Leap)
[![build](https://dev.azure.com/gsoft/Shared-Assets/_apis/build/status%2FLeap%2FLeap%20Publish?branchName=main)](https://dev.azure.com/gsoft/Shared-Assets/_build/latest?definitionId=219&branchName=main)

LEAP, which stands for Local Environment Application Proxy, is command-line application developed by Workleap. It helps developers by addressing their pain points with local development.

Currently, Leap helps with configuring popular third-party dependencies such as MongoDB or Redis locally in a uniform way so that developers don't end up creating their own `docker-compose.yml` files every single time. Instead, they can reuse pre-configured third-party dependencies provided by Leap.

User documentation is [available on Confluence](https://gsoftdev.atlassian.net/wiki/x/dIDh4Q)

## Requirements

Install the .NET SDK specified in the `global.json` file.

## Build and test

- Run `Build.ps1` to build the project and generate a NuGet package in the `.output` folder. [GitVersion](https://gitversion.net/) determines the NuGet package version based on commit history and tags.

- Run `BuildAndInstall.ps1` to build and install the package locally from your current directory. It will replace any existing version of the tool.

- Preview packages are published on the main branch ([gsoft feed](https://dev.azure.com/gsoft/Shared-Assets/_artifacts/feed/gsoft/NuGet/Workleap.Leap/)) and pull requests ([gsoftdev feed](https://dev.azure.com/gsoft/Shared-Assets/_artifacts/feed/gsoftdev/NuGet/Workleap.Leap/)). Create a tag in the format `x.y.z` (e.g., `1.0.0`) to publish a stable build.

# Usage

```ps1
leap run
leap run --file leap1.yaml leap2.yaml ...
leap run --file (Get-ChildItem -Recurse -Filter leap.yaml)
```

## Leap configuration file

The configuration file `leap.yaml` is where you define your services and dependencies. Leap reads this file and starts the services and dependencies automagically 🙂

Example `leap.yaml` file:

````yaml
services:
  sample_worker:
    ingress:
      host: worker.workleap.localhost
    healthcheck: /alive
    env:
      AZURE_EXPERIMENTAL_ENABLE_ACTIVITY_SOURCE: "true"
    runners:
      - type: dotnet
        project: "./worker/src/Workleap.Sample.Worker/Workleap.Sample.Worker.csproj"
      - type: docker
        image: "alpine:latest"
  sample_api:
    ingress:
      host: api.workleap.localhost
    runners:
      - type: executable
        command: "npm run start"
        args: ["--silent"]
````

## Ingress definition

The ingress definition of a service allows you to customize where your service will listen. Here’s the properties you can define:

| **Property** | **Description** |
| --- | --- |
| `host` | The host property enables you to define a custom localhost domain for your service. This localhost domain will be used by Leap’s proxy to properly redirect requests to your service. It also enables your service to run in HTTPS via our dev certificate. We can see in the example above that the host value is set to `organizationworker.workleap.localhost` for the `organizationworker` service. This way you can then reach your service at this address: `https://organizationworker.workleap.localhost:1347` |

## Healthcheck definition

You can specify the URL path used by health checks. Defaults to `/health` when omitted or `null`. Use empty string to disable a health check for a particular service. Healthchecks expect HTTP 200.

## Environment variables definition

You can specify arbitrary environment variables. It is best to double quote the values to avoid any YAML parsing errors.

| **Property** | **Description** |
| --- | --- |
| `env` | A YAML object where the key is the environment variable name, with the value as the corresponding environment variable value. |

## Runners definitions

### Dotnet runner

This runner is specialized in starting services that are dotnet projects. It is meant to point at a `csproj` on the local file system. When used, Leap will execute the following command:

`dotnet run --project <path_to_your_csproj> --no-launch-profile`

| **Property** | **Status** | **Description** |
| --- | --- | --- |
| `project` | Mandatory | Path to the `.csproj` file that you want to run, this path can be absolute or relative to where your `leap.yaml` is located. We recommend using _relative path_. |
| `port` | Optional | Localhost port override if you want the service to run on a constant port. If not provided, Leap will assign a _random available port_. |
| `protocol` | Optional | Protocol used by the app on start, options are `http` and `https`. If none is provided, `http` is used. |

### Docker runner

This runner is specialized in starting services in containers. It is meant to point towards a service’s production image stored in a Container Registry.
When used, Leap will defer the handling of the image and container to .NET Aspire.

| **Property** | **Status** | **Description** |
| --- | --- | --- |
| `image` | Mandatory | Docker image to use. Example: `workleap/eventgridemulator:0.1.0` |
| `containerPort` | Mandatory | Port within the container to expose. |
| `hostPort` | Optional | Host port to map to the `containerPort`. If none is provided, Leap will assign a random available port. |
| `protocol` | Optional | Protocol to use for the service, options are `http` and `https`. If none is provided, `http` is used. |
| `volumes` | Optional | A Docker Compose-like list of bind mounts in order to make directories and files from the host available in the container. Relative source paths must be relative to the directory containing the `leap.yaml` file. Absolute paths are not recommended. |

### Executable runner

This runner is meant as a more general runner for services. It should be used in situations where you want to run a service which wouldn’t fit with any of the specialized runners already defined.

| **Property** | **Status** | **Description** |
| --- | --- | --- |
| `command` | Mandatory | The command to execute. |
| `args` | Optional | String array of arguments passed to the executed command. |
| `workingDirectory` | Mandatory | Set the location where the command will be executed. |
| `port` | Optional | Localhost port override if you want the service to run on a constant port. If not provided, Leap will assign a random available port. |
| `protocol` | Optional | Protocol to use for the service, options are `http` and `https`. If none is provided, `http` is used. |

### OpenAPI runner

This runner is specialized in starting mock services based on their OpenAPI specification. It is meant to point at an OpenAPI specification file on the local file system.
When used, Leap will leverage `https://stoplight.io/open-source/prism` in order to start a mock server based on the specification file.

| **Property** | **Status** | **Description** |
| --- | --- | --- |
| `spec` | Mandatory | Location of the spec file to use to generate the mock server. |
| `port` | Optional | Localhost port override if you want the service to run on a constant port. If not provided, Leap will assign a random available port. |

## Service discovery

In order for services to communicate with each other, Leap will push all the relevant connection strings and service URLs to services via environment variables. These can then be used in your service’s configuration.

For each service, Leap adds an environment variable with the URL of the service in `SERVICES__<service_name>__BASEURL`. For dependencies, it can set multiple environment variables such as the connection strings and multiple URLs.

Here are a few example environment variables:

|                        | Environment Variable(s)                                                                                                    |
|------------------------|----------------------------------------------------------------------------------------------------------------------------|
| Services and applications | `SERVICES__<service_name>__BASEURL`                                                                                     |
| Mongo                  | `ConnectionStrings__Mongo`                                                                                                |
| Redis                  | `ConnectionStrings__Redis`                                                                                                |
| PostgreSQL             | `ConnectionStrings__Postgres`                                                                                             |
| Azure Storage (Azurite)| Connection strings:<br>`Azure__Storage__ConnectionString`<br>`Azure__Storage__Blob__ConnectionString`<br>`Azure__Storage__Queue__ConnectionString`<br>`Azure__Storage__Table__ConnectionString`<br>URIs with shared access signatures (preferred):<br>`Azure__Storage__Blob__ServiceUri`<br>`Azure__Storage__Queue__ServiceUri`<br>`Azure__Storage__Table__ServiceUri` |
| EventGrid (emulator)   | `ConnectionStrings__EventGrid`<br>`EventPropagation__Publisher__TopicEndpoint`                                             |
| SQL Server             | `ConnectionStrings__SqlServer`                                                                                            |

## Dependencies definitions

The dependencies section of this file is used to describe the third-party dependencies required to run your service. Here's an example including all supported dependencies.

````yaml
dependencies:
  - type: redis
  - type: fusionauth
  - type: mongo
  - type: postgres
  - type: sqlserver
  - type: eventgrid
  - type: azurite
    containers: ["mycontainer"]
    tables: ["mytable"]
    queues: ["myqueue"]
````
