import { useState } from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';

type SettingsScreenProps = {
  isAuthenticated: boolean;
  canDeleteAccount: boolean;
  onSignOut: () => void;
  onDeleteAccount: () => void;
};

export function SettingsScreen({ isAuthenticated, canDeleteAccount, onSignOut, onDeleteAccount }: SettingsScreenProps) {
  const [confirming, setConfirming] = useState(false);

  return (
    <View style={styles.container}>
      <Text style={styles.title}>Settings</Text>
      {isAuthenticated && (
        <Pressable onPress={onSignOut} accessibilityRole="button" style={styles.row}>
          <Text style={styles.rowText}>Sign out</Text>
        </Pressable>
      )}
      {canDeleteAccount && !confirming && (
        <Pressable onPress={() => setConfirming(true)} accessibilityRole="button" style={styles.row}>
          <Text style={styles.danger}>Delete account</Text>
        </Pressable>
      )}
      {canDeleteAccount && confirming && (
        <View style={styles.confirm}>
          <Text style={styles.confirmText}>This permanently deletes your account.</Text>
          <Pressable onPress={onDeleteAccount} accessibilityRole="button" style={styles.row}>
            <Text style={styles.danger}>Confirm delete</Text>
          </Pressable>
          <Pressable onPress={() => setConfirming(false)} accessibilityRole="button" style={styles.row}>
            <Text style={styles.rowText}>Cancel</Text>
          </Pressable>
        </View>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, padding: 24, backgroundColor: '#10131a' },
  title: { color: '#ffffff', fontSize: 28, fontWeight: '700', marginBottom: 24 },
  row: { paddingVertical: 14 },
  rowText: { color: '#ffffff', fontSize: 16 },
  danger: { color: '#ff6b6b', fontSize: 16, fontWeight: '600' },
  confirm: { marginTop: 8 },
  confirmText: { color: '#8a93a6', fontSize: 14, marginBottom: 8 }
});
