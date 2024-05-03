# <img src="logo.svg" width="32"> NuGet proxy

This is a simple, cross platform (Windows / Linux / MacOS), proxy for NuGet servers that insist on requiring authentication ([looking at you, GitLab](https://gitlab.com/gitlab-org/gitlab/-/issues/293684)) but where you want to allow public access to the packages. This project uses [YARP](https://microsoft.github.io/reverse-proxy/index.html) to (reverse) proxy requests to the NuGet server and adds an accesstoken to each request. Any responses to `*.json` URLs that return an `application/json` response will then be rewritten to contain the URL of the proxy instead of the actual NuGet server behind the proxy so everything keeps working as expected.

Since this project doesn't have a clue on what response structures exist, replacements of the URLs is done in a simple "find and replace" fashion on all string nodes. This _could_, potentially, be dangerous or could replace URLs that shouldn't be replaced (like URLs in metadata). It _should_ be safe enough though since we only replace the NuGet server url specifically. Since I can't be bothered to implement all responses and, as a consequence, keep up with future changes in NuGet, I opted to go for this _"quick'n'dirty"_ route. Now you know, just be aware. And [let me know](https://github.com/RobThree/NuGetProxy/issues/new) if you run into any issues.

## Usage

To configure the proxy, change the appsettings:

```json
{
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://my.nuget.org:5000"
      }
  },
  "PathBase": "/NuGet",
  "ReverseProxy": {
    "Routes": {
      "CatchAllRoute": {
        "ClusterId": "NuGetCluster",
        "Match": {
          "Path": "{**catch-all}"
        },
        "Transforms": [
          {
            "RequestHeader": "X-NuGet-ApiKey",
            "Set": "glpat-ExaMplEKeyExAmplEKey"
          }
        ]
      }
    },
    "Clusters": {
      "NuGetCluster": {
        "Destinations": {
          "Nuget": {
            "Address": "https://my.gitlab.org/api/v4/projects/69/packages/nuget"
          }
        }
      }
    }
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Yarp": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  }
}
```

The values you need to set / change are the following:

* `Url`: The URL(s)/port(s) at which the proxy will be listening.
* `PathBase`: (OPTIONAL) The path base that the proxy will be using. This is useful if you want to host multiple proxies on the same server.
* `ApiKey`: The API key that will be used to authenticate with the NuGet server.
* `Address`: The URL of the NuGet server feed.

Next, start the proxy and configure your NuGet client to use the proxy: set the `source` to the URL of the proxy + `PathBase` and add `index.json` to the end of the URL. For example: if you configured the `Url` as `http://localhost:5000/` and `PathBase` as `MyNuGet` then set the NuGet source for your client to `http://localhost:5000/MyNuGet/index.json`.

## What accesstoken?

For GitLab, you can create an access token by going to the NuGet repository -> Settings -> Access Tokens -> Add new Token. Give the token a name (like `NuGetProxy`), expiration date and give `read_api` permissions, make sure role is at least `reporter`.

Note that when the token expires, you _will_ need to update the token in the appsettings and restart the proxy.

## Can I push via the proxy?

No, the proxy is read-only. If you want to push packages, you will need to push them directly to the NuGet server.

## HTTPS & TLS

You can use HTTPS & TLS; documentation on how to configure YARP can be found [here](https://microsoft.github.io/reverse-proxy/articles/https-tls.html). Basically, you'd add something like this:

```json
"Https": {
    "Url": "https://my.nuget.org:5001",
    "Certificate": {
        "Path": "my.nuget.org.pfx",
        "Password": "ExAmplePa$$w0rd*&"
    }
}
```

## Running as service

Below are instructions on how to run the proxy as a service on Linux and Windows. The instructions are for systemd on Linux and for the Windows Service Control Manager on Windows.

### Debian

1. Make a directory for the service (example we'll use here is `/usr/local/bin/nugetproxy`)
2. Copy the binaries to `/usr/local/bin/nugetproxy`
3. Add/edit the `appsettings.json` in the app directory
4. Create `/etc/systemd/system/nugetproxy.service` with the following contents:
    ```bash
	[Unit]
	Description=NuGetProxy

	[Service]
	Type=notify
	ExecStart=/usr/local/bin/nugetproxy/NuGetProxy
	WorkingDirectory=/usr/local/bin/nugetproxy/
	User=root

	# ensure the service restarts after crashing
	Restart=always
	# amount of time to wait before restarting the service
	RestartSec=5

	[Install]
	WantedBy=multi-user.target
    ```
5. Make the executable exacutable: `chmod +x /usr/local/bin/nugetproxy/NuGetProxy`
6. Enable the service: `systemctl enable nugetproxy`
7. Run `systemctl daemon-reload` for systemd to pick up the new service
8. You can now start/stop service like any other service: `service nugetproxy start`

### Windows

1. Make a directory for the service (example we'll use here is `C:\Program Files\NuGetProxy`)
2. Copy the binaries to `C:\Program Files\NuGetProxy`
3. Add/edit the `appsettings.json` in the app directory
4. Open a command prompt as administrator and run the following command:
    ```cmd
    sc create NuGetProxy binPath= "C:\Program Files\NuGetProxy\NuGetProxy.exe" start= auto
    ```
    ⚠️ [Note the space(s)](https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/sc-create#remarks) after `binPath= ` and `start= `
5. You can now start/stop service like any other service: `sc start NuGetProxy`