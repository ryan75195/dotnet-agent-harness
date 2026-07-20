import { useState } from 'react';
import { Pressable, StyleSheet, Text } from 'react-native';

type FocusButtonProps = {
  label: string;
  onPress: () => void;
  preferredFocus?: boolean;
};

export function FocusButton({ label, onPress, preferredFocus = false }: FocusButtonProps) {
  const [isFocused, setIsFocused] = useState(false);
  return (
    <Pressable
      accessibilityRole="button"
      hasTVPreferredFocus={preferredFocus}
      onBlur={() => setIsFocused(false)}
      onFocus={() => setIsFocused(true)}
      onPress={onPress}
      style={[styles.button, isFocused && styles.focused]}
    >
      <Text style={[styles.label, isFocused && styles.focusedLabel]}>{label}</Text>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  button: { alignItems: 'center', backgroundColor: '#273049', borderColor: '#273049', borderRadius: 12, borderWidth: 3, minWidth: 224, paddingHorizontal: 28, paddingVertical: 16 },
  focused: { backgroundColor: '#ffffff', borderColor: '#7cb9ff', transform: [{ scale: 1.06 }] },
  label: { color: '#ffffff', fontSize: 20, fontWeight: '700' },
  focusedLabel: { color: '#10131a' }
});
