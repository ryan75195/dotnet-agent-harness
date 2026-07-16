@{
    Type        = 'dotnet-durable'
    Description = '.NET 10 Azure Durable Functions app (isolated worker, orchestrator determinism analyzers + architecture tests)'
    StampName   = 'durable'
    ExtraArgs   = @()
    PreInstall  = {
        param($ctx)
        dotnet new install "$($ctx.Repo)/dotnet" --force
        if ($LASTEXITCODE) { throw "dotnet new install failed (exit $LASTEXITCODE)" }
    }
    Scaffold    = {
        param($ctx)
        dotnet new durable -n $ctx.Name -o $ctx.Dest
        if ($LASTEXITCODE) { throw "dotnet new durable failed (exit $LASTEXITCODE)" }
    }
    Verify      = {
        param($ctx)
        dotnet build --no-incremental
        if ($LASTEXITCODE) { throw "dotnet build failed (exit $LASTEXITCODE)" }
        # Scoped to Unit/Architecture/Analyzers - a superset of the pre-commit hook, which
        # matches only *Unit*/*Arch*. Analyzers is included because it needs nothing external
        # and covers the durable determinism rules. Integration is excluded: it needs Azurite
        # + Core Tools on PATH, and scaffolding must never require them.
        # `dotnet test` only accepts one project per invocation, so loop instead of
        # space-joining paths. Project folders are renamed to $ctx.Name by the template
        # engine, not the template repo's literal "SampleDurable".
        $testProjects = @(
            "tests/$($ctx.Name).Tests.Unit",
            "tests/$($ctx.Name).Tests.Architecture",
            "tests/$($ctx.Name).Tests.Analyzers"
        )
        foreach ($proj in $testProjects) {
            dotnet test $proj --no-build --verbosity minimal
            if ($LASTEXITCODE) { throw "dotnet test failed for $proj (exit $LASTEXITCODE)" }
        }
    }
}
