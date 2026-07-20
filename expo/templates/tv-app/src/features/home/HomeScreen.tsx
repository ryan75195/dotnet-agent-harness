import { StyleSheet, Text, View } from 'react-native';

import { FocusButton } from '../../components/FocusButton';

type HomeScreenProps = {
  isSubscribed: boolean;
  onBrowsePress: () => void;
  onUpgradePress: () => void;
  onSettingsPress?: () => void;
};

export function HomeScreen({ isSubscribed, onBrowsePress, onUpgradePress, onSettingsPress }: HomeScreenProps) {
  return (
    <View style={styles.container}>
      <View>
        <Text style={styles.eyebrow}>YOUR LIVING-ROOM LIBRARY</Text>
        <Text style={styles.title}>AppTemplate</Text>
        <Text style={styles.subtitle}>{isSubscribed ? 'Premium active' : 'Free plan'}</Text>
      </View>
      <View style={styles.actions}>
        <FocusButton label="Browse library" onPress={onBrowsePress} preferredFocus={true} />
        {!isSubscribed && <FocusButton label="Upgrade" onPress={onUpgradePress} />}
        {onSettingsPress && <FocusButton label="Settings" onPress={onSettingsPress} />}
      </View>
      <Text style={styles.hint}>Use the remote to move focus, select, and return.</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  container: { backgroundColor: '#10131a', flex: 1, justifyContent: 'center', paddingHorizontal: '10%' },
  eyebrow: { color: '#7cb9ff', fontSize: 16, fontWeight: '700', letterSpacing: 1.5 },
  title: { color: '#ffffff', fontSize: 56, fontWeight: '800', marginTop: 12 },
  subtitle: { color: '#b6c0d4', fontSize: 24, marginTop: 12 },
  actions: { flexDirection: 'row', flexWrap: 'wrap', gap: 20, marginTop: 44 },
  hint: { color: '#7f8aa3', fontSize: 18, marginTop: 36 }
});
