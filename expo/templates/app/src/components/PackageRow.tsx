import { useState } from 'react';
import { Pressable, StyleSheet, Text } from 'react-native';
import Purchases, { PurchasesPackage } from 'react-native-purchases';

type PackageRowProps = {
  pkg: PurchasesPackage;
  onPurchased: () => void;
};

export function PackageRow({ pkg, onPurchased }: PackageRowProps) {
  const [isPurchasing, setIsPurchasing] = useState(false);

  const handlePress = async () => {
    setIsPurchasing(true);
    try {
      await Purchases.purchasePackage(pkg);
      onPurchased();
    } catch {
      setIsPurchasing(false);
    }
  };

  return (
    <Pressable onPress={handlePress} disabled={isPurchasing} style={styles.row} accessibilityRole="button">
      <Text style={styles.title}>{pkg.product.title}</Text>
      <Text style={styles.price}>{pkg.product.priceString}</Text>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  row: { padding: 16, borderRadius: 12, backgroundColor: '#1f2430', marginVertical: 6 },
  title: { color: '#ffffff', fontSize: 16, fontWeight: '600' },
  price: { color: '#9be29b', fontSize: 14, marginTop: 4 }
});
