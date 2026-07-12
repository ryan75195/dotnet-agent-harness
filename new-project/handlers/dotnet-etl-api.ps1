@{
    Type        = 'dotnet-etl-api'
    Description = '.NET 10 ETL/API service (Roslyn analyzers + architecture tests, agent configs)'
    StampName   = 'etl-api'
    ExtraArgs   = @()
    PreInstall  = {
        param($ctx)
        dotnet new install "$($ctx.Repo)/dotnet" --force
        if ($LASTEXITCODE) { throw "dotnet new install failed (exit $LASTEXITCODE)" }
    }
    Scaffold    = {
        param($ctx)
        dotnet new etl-api -n $ctx.Name -o $ctx.Dest
        if ($LASTEXITCODE) { throw "dotnet new etl-api failed (exit $LASTEXITCODE)" }
    }
    Verify      = {
        param($ctx)
        dotnet build --no-incremental
        if ($LASTEXITCODE) { throw "dotnet build failed (exit $LASTEXITCODE)" }
        dotnet test --no-build --verbosity minimal
        if ($LASTEXITCODE) { throw "dotnet test failed (exit $LASTEXITCODE)" }
    }
}
