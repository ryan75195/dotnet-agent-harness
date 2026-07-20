@{
    Type        = 'expo-tv'
    Description = 'Expo TV app for Android TV and Apple TV'
    StampName   = 'expo-tv-app'
    ExtraArgs   = @(
        @{ Name = 'BundleId'; Required = $false; Help = 'reverse-DNS id, e.g. com.acme.tvapp' }
    )
    PreInstall  = $null
    Scaffold    = {
        param($ctx)
        $extra = $ctx.Extra
        & "$($ctx.Repo)/expo/new-app.ps1" -Template tv-app -Name $ctx.Name -Destination $ctx.Dest @extra
    }
    Verify      = {
        param($ctx)
        npm run verify
        if ($LASTEXITCODE) { throw "npm run verify failed (exit $LASTEXITCODE)" }
    }
}
