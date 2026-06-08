import { Pressable, StyleSheet, Text, View } from 'react-native';

type HomeScreenProps = {
  isSubscribed: boolean;
  onUpgradePress: () => void;
  onSettingsPress?: () => void;
};

export function HomeScreen({ isSubscribed, onUpgradePress, onSettingsPress }: HomeScreenProps) {
  return (
    <View style={styles.container}>
      <Text style={styles.title}>AppTemplate</Text>
      <Text style={styles.subtitle}>{isSubscribed ? 'Premium active' : 'Free plan'}</Text>
      {!isSubscribed && (
        <Pressable onPress={onUpgradePress} accessibilityRole="button" style={styles.upgrade}>
          <Text style={styles.upgradeText}>Upgrade</Text>
        </Pressable>
      )}
      {onSettingsPress && (
        <Pressable onPress={onSettingsPress} accessibilityRole="button" style={styles.settings}>
          <Text style={styles.settingsText}>Settings</Text>
        </Pressable>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: '#10131a' },
  title: { color: '#ffffff', fontSize: 32, fontWeight: '700' },
  subtitle: { color: '#8a93a6', fontSize: 16, marginTop: 8 },
  upgrade: { marginTop: 24, paddingHorizontal: 24, paddingVertical: 12, borderRadius: 12, backgroundColor: '#4c6ef5' },
  upgradeText: { color: '#ffffff', fontSize: 16, fontWeight: '600' },
  settings: { marginTop: 16, paddingHorizontal: 24, paddingVertical: 12 },
  settingsText: { color: '#8a93a6', fontSize: 16 }
});
