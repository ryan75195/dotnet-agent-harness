@{
    Type        = 'dotnet-mcp'
    Description = '.NET 10 MCP server (HTTP/streamable, ModelContextProtocol SDK, analyzers + architecture tests)'
    StampName   = 'mcp'
    ExtraArgs   = @()
    PreInstall  = {
        param($ctx)
        dotnet new install "$($ctx.Repo)/dotnet" --force
        if ($LASTEXITCODE) { throw "dotnet new install failed (exit $LASTEXITCODE)" }
    }
    Scaffold    = {
        param($ctx)
        dotnet new mcp -n $ctx.Name -o $ctx.Dest
        if ($LASTEXITCODE) { throw "dotnet new mcp failed (exit $LASTEXITCODE)" }
    }
    Verify      = {
        param($ctx)
        dotnet build --no-incremental
        if ($LASTEXITCODE) { throw "dotnet build failed (exit $LASTEXITCODE)" }
        dotnet test --no-build --verbosity minimal
        if ($LASTEXITCODE) { throw "dotnet test failed (exit $LASTEXITCODE)" }
    }
}
