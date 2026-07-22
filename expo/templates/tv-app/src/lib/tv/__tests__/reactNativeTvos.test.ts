describe('installed react-native build', () => {
  test('is the TV fork, so the native focus path is not inert', () => {
    const pkg = jest.requireActual<{ name: string }>('react-native/package.json');
    expect(pkg.name).toBe('react-native-tvos');
  });

  test('exposes a requestTVFocus view command for the native focus path', () => {
    const nativeComponent = jest.requireActual<{ Commands: Record<string, unknown> }>(
      'react-native/Libraries/Components/View/ViewNativeComponent'
    );
    expect(Object.keys(nativeComponent.Commands)).toContain('requestTVFocus');
  });

  test('ships the TV component surface', () => {
    const tvFocusGuideView = jest.requireActual<{ default: unknown }>(
      'react-native/Libraries/Components/TV/TVFocusGuideView'
    );
    expect(tvFocusGuideView.default).toBeDefined();
  });
});
