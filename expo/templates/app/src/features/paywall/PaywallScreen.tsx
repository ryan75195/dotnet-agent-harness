import { useEffect, useState } from 'react';
import { ActivityIndicator, Pressable, StyleSheet, Text, View } from 'react-native';
import Purchases, { PurchasesPackage } from 'react-native-purchases';

import { PackageRow } from '../../components/PackageRow';

type PaywallScreenProps = {
  onClose: () => void;
};

export function PaywallScreen({ onClose }: PaywallScreenProps) {
  const [packages, setPackages] = useState<PurchasesPackage[]>([]);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    Purchases.getOfferings()
      .then((offerings) => {
        setPackages(offerings.current?.availablePackages ?? []);
        setIsLoading(false);
      })
      .catch(() => setIsLoading(false));
  }, []);

  if (isLoading) {
    return (
      <View style={styles.container}>
        <ActivityIndicator />
      </View>
    );
  }

  return (
    <View style={styles.container}>
      <Text style={styles.title}>Go Premium</Text>
      {packages.map((pkg) => (
        <PackageRow key={pkg.identifier} pkg={pkg} onPurchased={onClose} />
      ))}
      <Pressable onPress={onClose} accessibilityRole="button">
        <Text style={styles.close}>Not now</Text>
      </Pressable>
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, justifyContent: 'center', padding: 24, backgroundColor: '#10131a' },
  title: { color: '#ffffff', fontSize: 28, fontWeight: '700', marginBottom: 16 },
  close: { color: '#8a93a6', fontSize: 16, textAlign: 'center', marginTop: 24 }
});
