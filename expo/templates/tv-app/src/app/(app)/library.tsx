import { useRouter } from 'expo-router';

import { LibraryScreen } from '../../features/library/LibraryScreen';

export default function Library() {
  const router = useRouter();
  return <LibraryScreen onBackPress={() => router.back()} />;
}
