import { StyleSheet, Text, View } from 'react-native';

import { FocusButton } from '../../components/FocusButton';

type LibraryScreenProps = {
  onBackPress: () => void;
};

export function LibraryScreen({ onBackPress }: LibraryScreenProps) {
  return (
    <View style={styles.container}>
      <Text style={styles.title}>Your library starts here</Text>
      <Text style={styles.body}>Add shelves, searchable catalogues, and video detail screens in this feature.</Text>
      <FocusButton label="Back" onPress={onBackPress} preferredFocus={true} />
    </View>
  );
}

const styles = StyleSheet.create({
  container: { backgroundColor: '#10131a', flex: 1, gap: 28, justifyContent: 'center', paddingHorizontal: '10%' },
  title: { color: '#ffffff', fontSize: 48, fontWeight: '800' },
  body: { color: '#b6c0d4', fontSize: 22, maxWidth: 760 }
});
