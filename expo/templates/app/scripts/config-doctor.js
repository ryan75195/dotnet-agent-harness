const OPTIONAL_FEATURES = [
  { key: 'payments', label: 'Payments (RevenueCat)', requiredEnv: ['EXPO_PUBLIC_REVENUECAT_IOS_API_KEY'] },
  { key: 'auth', label: 'Auth (Auth0)', requiredEnv: ['EXPO_PUBLIC_AUTH0_DOMAIN', 'EXPO_PUBLIC_AUTH0_CLIENT_ID'] },
  { key: 'api', label: 'API base URL', requiredEnv: ['EXPO_PUBLIC_API_BASE_URL'] }
];

function evaluateCritical(inputs) {
  const results = [];
  const whoami = inputs.easWhoami || { status: 'unknown', user: null };
  if (whoami.status === 'logged-in') {
    results.push({ key: 'eas-login', label: 'EAS login', ok: true, detail: whoami.user ? `logged in as ${whoami.user}` : 'logged in' });
  } else if (whoami.status === 'logged-out') {
    results.push({ key: 'eas-login', label: 'EAS login', ok: false, detail: 'not logged in — run `eas login`' });
  } else {
    results.push({ key: 'eas-login', label: 'EAS login', ok: false, detail: 'could not verify (offline/timeout) — first-run-setup will re-check' });
  }

  results.push({
    key: 'eas-project-id',
    label: 'EAS project linked',
    ok: Boolean(inputs.projectId),
    detail: inputs.projectId ? `projectId ${inputs.projectId}` : 'extra.eas.projectId not set — run `eas init`'
  });

  const profilesOk = Boolean(inputs.hasProductionBuildProfile) && Boolean(inputs.hasProductionSubmitProfile);
  results.push({
    key: 'eas-production-profiles',
    label: 'EAS production profiles',
    ok: profilesOk,
    detail: profilesOk ? 'build + submit profiles present' : 'eas.json missing build.production or submit.production'
  });

  const bundleId = inputs.bundleId || '';
  const bundleOk = bundleId.length > 0 && !bundleId.includes('com.example.');
  results.push({
    key: 'bundle-id',
    label: 'Production bundle identifier',
    ok: bundleOk,
    detail: bundleOk ? bundleId : `still a placeholder (${bundleId || 'unset'})`
  });

  const credentialsOk = Boolean(inputs.setupState && inputs.setupState.iosCredentials === 'provisioned');
  results.push({
    key: 'ios-credentials',
    label: 'iOS credentials',
    ok: credentialsOk,
    detail: credentialsOk ? 'provisioned' : 'not yet provisioned — first-run-setup runs `eas credentials`'
  });

  return results;
}

function isConfigured(feature, env) {
  return feature.requiredEnv.every((name) => Boolean(env && env[name]));
}

function evaluateOptional(inputs) {
  const env = inputs.env || {};
  const features = (inputs.setupState && inputs.setupState.features) || {};
  return OPTIONAL_FEATURES.map((feature) => {
    const configured = isConfigured(feature, env);
    const decision = configured ? 'enabled' : (features[feature.key] || null);
    return { key: feature.key, label: feature.label, configured, decision, needsDecision: !configured && !decision };
  });
}

function evaluateConfig(inputs) {
  const critical = evaluateCritical(inputs);
  const optional = evaluateOptional(inputs);
  const allCriticalPass = critical.every((r) => r.ok);
  const allOptionalDecided = optional.every((o) => !o.needsDecision);
  return { critical, optional, allCriticalPass, allOptionalDecided, shouldNudge: !allCriticalPass || !allOptionalDecided };
}

module.exports = { evaluateConfig, OPTIONAL_FEATURES };
