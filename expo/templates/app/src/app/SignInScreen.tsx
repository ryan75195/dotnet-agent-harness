import * as AppleAuthentication from 'expo-apple-authentication';
import { ActivityIndicator, Platform, Pressable, StyleSheet, Text, View } from 'react-native';

import type { AuthState } from '../lib/auth/useAuth';

type SignInScreenProps = {
  auth: AuthState;
};

export function SignInScreen({ auth }: SignInScreenProps) {
  if (auth.isLoading) {
    return (
      <View style={styles.container}>
        <ActivityIndicator />
      </View>
    );
  }
  return (
    <View style={styles.container}>
      <Text style={styles.title}>Sign in</Text>
      <Pressable onPress={auth.signIn} accessibilityRole="button" style={styles.primary}>
        <Text style={styles.primaryText}>Continue</Text>
      </Pressable>
      {Platform.OS === 'ios' && (
        <AppleAuthentication.AppleAuthenticationButton
          buttonType={AppleAuthentication.AppleAuthenticationButtonType.SIGN_IN}
          buttonStyle={AppleAuthentication.AppleAuthenticationButtonStyle.BLACK}
          cornerRadius={12}
          style={styles.apple}
          onPress={auth.signInWithApple}
        />
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, alignItems: 'center', justifyContent: 'center', padding: 24, backgroundColor: '#10131a' },
  title: { color: '#ffffff', fontSize: 28, fontWeight: '700', marginBottom: 24 },
  primary: { paddingHorizontal: 32, paddingVertical: 14, borderRadius: 12, backgroundColor: '#4c6ef5' },
  primaryText: { color: '#ffffff', fontSize: 16, fontWeight: '600' },
  apple: { width: 240, height: 48, marginTop: 16 }
});
