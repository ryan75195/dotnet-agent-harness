@{
    Type        = 'expo'
    Description = 'Expo / React Native mobile app (payments, auth, EAS build/deploy)'
    StampName   = 'expo-app'
    ExtraArgs   = @(
        @{ Name = 'BundleId'; Required = $false; Help = 'reverse-DNS id, e.g. com.acme.myapp' }
    )
    PreInstall  = $null
    Scaffold    = {
        param($ctx)
        $extra = $ctx.Extra
        & "$($ctx.Repo)/expo/new-app.ps1" -Name $ctx.Name -Destination $ctx.Dest @extra
    }
    Verify      = {
        param($ctx)
        npm run verify
        if ($LASTEXITCODE) { throw "npm run verify failed (exit $LASTEXITCODE)" }
    }
}
